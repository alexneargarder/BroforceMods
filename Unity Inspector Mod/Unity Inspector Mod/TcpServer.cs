using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity_Inspector_Mod
{
    public class TcpServer
    {
        internal const int Protocol = 1;

        private Socket listener;
        private Thread listenerThread;
        private volatile bool isRunning;
        private readonly int port;
        private readonly bool allowRemoteConnections;
        private readonly string tokenPath;
        private string token;
        private bool tokenFileWritten;
        private readonly List<ClientHandler> clients = new List<ClientHandler>();
        private readonly object clientsLock = new object();
        private readonly MessageHandler messageHandler;

        public bool IsRunning => isRunning;
        public int Port => port;
        public bool AllowRemoteConnections => allowRemoteConnections;

        public static string TokenFileName( int port ) => $"unity-inspector-{port}.token";

        public int ConnectedClients
        {
            get
            {
                lock ( clientsLock )
                {
                    return clients.Count;
                }
            }
        }

        [DllImport( "libc" )]
        private static extern uint umask( uint mask );

        public TcpServer( int port, bool allowRemoteConnections, string tokenDirectory )
        {
            this.port = port;
            this.allowRemoteConnections = allowRemoteConnections;
            // Application.persistentDataPath, which only the main thread may read, arrives as tokenDirectory.
            tokenPath = Path.Combine( tokenDirectory, TokenFileName( port ) );
            messageHandler = new MessageHandler();
        }

        public void Start()
        {
            if ( isRunning )
            {
                return;
            }

            Socket socket = null;
            try
            {
                IPAddress bindAddress = allowRemoteConnections ? IPAddress.Any : IPAddress.Loopback;

                if ( allowRemoteConnections )
                {
                    Main.Log( $"Remote connections are on: the server listens on all network interfaces (0.0.0.0:{port})." );
                    Main.Log( $"Clients on other machines need the token from {tokenPath}" );
                }

                token = GenerateToken();

                socket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
                socket.Bind( new IPEndPoint( bindAddress, port ) );
                WriteTokenFile();
                socket.Listen( (int)SocketOptionName.MaxConnections );

                listener = socket;
                socket = null;
                isRunning = true;

                listenerThread = new Thread( ListenForClients );
                listenerThread.IsBackground = true;
                listenerThread.Start();

                Main.Log( $"Clients connect with the token in {tokenPath}" );
            }
            catch ( Exception ex )
            {
                Main.Log( $"Failed to start the TCP server: {ex}" );
                socket?.Close();
                DeleteTokenFile();
                throw;
            }
        }

        public void Stop()
        {
            isRunning = false;

            ClientHandler[] toDisconnect;
            lock ( clientsLock )
            {
                toDisconnect = clients.ToArray();
                clients.Clear();
            }

            foreach ( var client in toDisconnect )
            {
                client.Disconnect();
            }

            if ( listener != null )
            {
                listener.Close();
                listener = null;
            }

            if ( listenerThread != null )
            {
                listenerThread.Join( 1000 );
                listenerThread = null;
            }

            DeleteTokenFile();
        }

        private static string GenerateToken()
        {
            var bytes = new byte[32];
            new RNGCryptoServiceProvider().GetBytes( bytes );
            return BitConverter.ToString( bytes ).Replace( "-", "" ).ToLowerInvariant();
        }

        private void WriteTokenFile()
        {
            Directory.CreateDirectory( Path.GetDirectoryName( tokenPath ) );

            var platform = Environment.OSVersion.Platform;
            if ( platform == PlatformID.Unix || platform == PlatformID.MacOSX )
            {
                // A truncated file keeps its mode, so it is replaced, and the umask makes the new one 0600 from creation.
                File.Delete( tokenPath );
                uint oldMask = umask( 0x3F );
                try
                {
                    tokenFileWritten = true;
                    File.WriteAllText( tokenPath, token, Encoding.ASCII );
                }
                finally
                {
                    umask( oldMask );
                }
            }
            else
            {
                tokenFileWritten = true;
                File.WriteAllText( tokenPath, token, Encoding.ASCII );
            }
        }

        private void DeleteTokenFile()
        {
            if ( !tokenFileWritten )
            {
                return;
            }
            tokenFileWritten = false;

            try
            {
                File.Delete( tokenPath );
            }
            catch ( DirectoryNotFoundException ) { }
            catch ( Exception ex )
            {
                Main.Log( $"Failed to delete the token file {tokenPath}: {ex.Message}" );
            }
        }

        private void ListenForClients()
        {
            while ( isRunning )
            {
                try
                {
                    var currentListener = listener;
                    if ( currentListener == null )
                    {
                        break;
                    }

                    if ( currentListener.Poll( 0, SelectMode.SelectRead ) )
                    {
                        var socket = currentListener.Accept();

                        try
                        {
                            lock ( clientsLock )
                            {
                                if ( isRunning )
                                {
                                    clients.Add( new ClientHandler( socket, this, token ) );
                                    socket = null;
                                }
                            }
                        }
                        finally
                        {
                            socket?.Close();
                        }
                    }
                }
                catch ( Exception ex )
                {
                    if ( isRunning )
                    {
                        Main.Log( $"Error accepting client: {ex.Message}" );
                        Main.Log( $"Exception details: {ex}" );
                    }
                }
                finally
                {
                    Thread.Sleep( 100 );
                }
            }
        }

        internal void RemoveClient( ClientHandler client )
        {
            lock ( clientsLock )
            {
                clients.Remove( client );
            }

            SpawnTrace.ReleaseOwner( client );
        }

        internal string ProcessMessage( string message, ClientHandler client )
        {
            string result = messageHandler.HandleMessage( message, client );
            return result;
        }
    }

    internal class ClientHandler
    {
        private const int HelloTimeoutMs = 5000;
        private const int HelloWaiting = 0;
        private const int HelloDone = 1;
        private const int HelloTimedOut = 2;
        private const string UpdateClient = "Update the client (the Unity Inspector MCP server). An MCP session whose server files are already updated loads them with the restart_server tool.";

        private readonly Socket socket;
        private readonly TcpServer server;
        private readonly string token;
        private readonly string remote;
        private readonly Thread receiveThread;
        private readonly NetworkStream stream;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;
        private bool isConnected;
        private int helloState = HelloWaiting;

        public ClientHandler( Socket socket, TcpServer server, string token )
        {
            this.socket = socket;
            this.server = server;
            this.token = token;
            remote = socket.RemoteEndPoint?.ToString() ?? "unknown peer";
            stream = new NetworkStream( socket, true );
            reader = new StreamReader( stream, new UTF8Encoding( false ) );
            writer = new StreamWriter( stream, new UTF8Encoding( false ) ) { AutoFlush = true };
            isConnected = true;

            receiveThread = new Thread( ReceiveMessages );
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        private void ReceiveMessages()
        {
            bool accepted;
            try
            {
                accepted = Handshake();
            }
            catch ( Exception ex )
            {
                Main.Log( $"[ClientHandler] Closed {remote}: the hello failed ({ex.Message})" );
                accepted = false;
            }

            if ( !accepted )
            {
                Disconnect();
                return;
            }

            while ( isConnected && socket.Connected )
            {
                try
                {
                    string message = reader.ReadLine();
                    if ( message != null )
                    {
                        string response = server.ProcessMessage( message, this );

                        writer.WriteLine( response );

                    }
                    else
                    {
                        break;
                    }
                }
                catch ( Exception ex )
                {
                    Main.Log( $"[ClientHandler] Error receiving message: {ex.Message}" );
                    break;
                }
            }

            Disconnect();
        }

        private bool Handshake()
        {
            // ReceiveTimeout does not bound a blocking recv on this game's Mono; shutting the socket down makes it return 0.
            string line = null;
            Exception readError = null;
            var timer = new Timer( _ =>
            {
                if ( Interlocked.CompareExchange( ref helloState, HelloTimedOut, HelloWaiting ) != HelloWaiting )
                {
                    return;
                }
                try
                {
                    socket.Shutdown( SocketShutdown.Both );
                }
                catch ( Exception ) { }
            }, null, HelloTimeoutMs, Timeout.Infinite );

            try
            {
                line = reader.ReadLine();
            }
            catch ( Exception ex )
            {
                readError = ex;
            }
            finally
            {
                Interlocked.CompareExchange( ref helloState, HelloDone, HelloWaiting );
                timer.Dispose();
            }

            if ( helloState == HelloTimedOut )
            {
                if ( isConnected )
                {
                    Main.Log( $"[ClientHandler] Closed {remote}: no hello within {HelloTimeoutMs / 1000}s" );
                }
                return false;
            }

            if ( readError != null )
            {
                if ( isConnected )
                {
                    Main.Log( $"[ClientHandler] Closed {remote}: no hello read ({readError.Message})" );
                }
                return false;
            }

            if ( line == null )
            {
                Main.Log( $"[ClientHandler] Closed {remote}: the connection ended before a hello" );
                return false;
            }

            JObject request;
            try
            {
                request = MessageHandler.ParseRequest( line );
            }
            catch ( Exception )
            {
                Main.Log( $"[ClientHandler] Refused {remote}: the first line is not a JSON object" );
                return false;
            }

            string id = request["id"]?.ToString();
            string method = request["method"]?.ToString();
            var parameters = request["params"] as JObject;

            if ( method != "hello" )
            {
                Main.Log( $"[ClientHandler] Refused {remote}: sent a command before a hello" );
                Reply( Refusal( id, "The Unity Inspector Mod needs every connection to open with a hello carrying its token, and this client sent a command first. " + UpdateClient ) );
                return false;
            }

            JToken protocolToken = parameters?["protocol"];
            if ( protocolToken == null || protocolToken.Type != JTokenType.Integer || protocolToken.Value<long>() != TcpServer.Protocol )
            {
                bool theirsHigher = protocolToken != null && protocolToken.Type == JTokenType.Integer && protocolToken.Value<long>() > TcpServer.Protocol;
                string theirs = protocolToken == null || protocolToken.Type == JTokenType.Null ? "none" : protocolToken.ToString( Formatting.None );
                string update = theirsHigher ? "Update the Unity Inspector Mod." : UpdateClient;
                Main.Log( $"[ClientHandler] Refused {remote}: handshake protocol {theirs}, this mod speaks {TcpServer.Protocol}" );
                JObject refusal = Refusal( id, $"Handshake protocol {theirs} does not match this Unity Inspector Mod's protocol {TcpServer.Protocol}. {update}" );
                refusal["protocol"] = TcpServer.Protocol;
                Reply( refusal );
                return false;
            }

            JToken tokenToken = parameters["token"];
            string theirToken = tokenToken != null && tokenToken.Type == JTokenType.String ? tokenToken.Value<string>() : null;
            if ( !string.Equals( theirToken, token, StringComparison.Ordinal ) )
            {
                Main.Log( $"[ClientHandler] Refused {remote}: {( theirToken == null ? "no token" : "wrong token" )}" );
                Reply( Refusal( id, "The token does not match this game's token file, which the mod rewrites every time its server starts." ) );
                return false;
            }

            bool replied = Reply( new JObject
            {
                ["id"] = id,
                ["success"] = true,
                ["result"] = new JObject { ["protocol"] = TcpServer.Protocol }
            } );
            return replied;
        }

        private static JObject Refusal( string id, string error )
        {
            return new JObject
            {
                ["id"] = id,
                ["success"] = false,
                ["error"] = error
            };
        }

        private bool Reply( JObject reply )
        {
            try
            {
                writer.WriteLine( reply.ToString( Formatting.None ) );
                return true;
            }
            catch ( Exception ex )
            {
                Main.Log( $"[ClientHandler] Failed to reply to {remote}: {ex.Message}" );
                return false;
            }
        }

        public void Disconnect()
        {
            if ( !isConnected )
            {
                return;
            }
            isConnected = false;

            try
            {
                reader?.Close();
                writer?.Close();
                stream?.Close();
                socket?.Close();
            }
            catch { }

            server.RemoveClient( this );
        }
    }
}
