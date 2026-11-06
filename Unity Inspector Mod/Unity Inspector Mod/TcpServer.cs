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
        private const int MaxAwaitingHello = 16;

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

        internal static bool IsUnix => Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX;

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
                string tokenDirectory = Path.GetDirectoryName( tokenPath );
                Directory.CreateDirectory( tokenDirectory );

                // Mono binds with SO_REUSEADDR, so two games can both bind before either listens
                using ( StartLock.Acquire( Path.Combine( tokenDirectory, $"unity-inspector-{port}.lock" ) ) )
                {
                    socket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
                    socket.Bind( new IPEndPoint( bindAddress, port ) );
                    WriteTokenFile();
                    socket.Listen( (int)SocketOptionName.MaxConnections );
                }

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
            bool wasRunning = isRunning;
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

            if ( wasRunning )
            {
                Main.Log( $"Server stopped; clients can no longer connect with the token in {tokenPath}" );
            }
        }

        private static string GenerateToken()
        {
            var bytes = new byte[32];
            new RNGCryptoServiceProvider().GetBytes( bytes );
            return BitConverter.ToString( bytes ).Replace( "-", "" ).ToLowerInvariant();
        }

        private void WriteTokenFile()
        {
            if ( IsUnix )
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
            else if ( WineFiles.UnderWine() )
            {
                File.Delete( tokenPath );
                tokenFileWritten = true;
                try
                {
                    WineFiles.WriteOwnerOnly( tokenPath, token );
                }
                catch ( Exception ex )
                {
                    Main.Log( $"Could not make the token file readable by its owner only ({ex.Message}); other local users may be able to read it" );
                    File.WriteAllText( tokenPath, token, Encoding.ASCII );
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

                    if ( !currentListener.Poll( 100000, SelectMode.SelectRead ) )
                    {
                        continue;
                    }

                    var socket = currentListener.Accept();
                    ClientHandler evicted = null;

                    try
                    {
                        lock ( clientsLock )
                        {
                            if ( isRunning )
                            {
                                evicted = OldestAwaitingHelloAtCap();
                                clients.Add( new ClientHandler( socket, this, token ) );
                                socket = null;
                            }
                        }
                    }
                    finally
                    {
                        socket?.Close();
                    }

                    if ( evicted != null )
                    {
                        Main.Log( $"[ClientHandler] Closed {evicted.Remote}: {MaxAwaitingHello} connections were already waiting to send a hello" );
                        evicted.Disconnect();
                    }
                }
                catch ( Exception ex )
                {
                    if ( isRunning )
                    {
                        Main.Log( $"Error accepting client: {ex.Message}" );
                        Main.Log( $"Exception details: {ex}" );
                        Thread.Sleep( 100 );
                    }
                }
            }
        }

        private ClientHandler OldestAwaitingHelloAtCap()
        {
            ClientHandler oldest = null;
            int awaiting = 0;
            foreach ( var client in clients )
            {
                if ( !client.AwaitingHello )
                {
                    continue;
                }
                if ( oldest == null )
                {
                    oldest = client;
                }
                awaiting++;
            }
            return awaiting >= MaxAwaitingHello ? oldest : null;
        }

        internal void RemoveClient( ClientHandler client )
        {
            lock ( clientsLock )
            {
                clients.Remove( client );
            }

            SpawnTrace.ReleaseOwner( client );
            InputSimulator.ReleaseOwner( client );
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
        private const int HelloMaxChars = 4096;
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
        private volatile bool isConnected;
        private int helloState = HelloWaiting;

        public string Remote => remote;
        public bool AwaitingHello => Thread.VolatileRead( ref helloState ) == HelloWaiting;

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

                        try
                        {
                            writer.WriteLine( AsciiOnly( response ) );
                        }
                        catch ( Exception ex ) when ( ex is IOException || ex is ObjectDisposedException )
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                catch ( IOException ex ) when ( ( ex.InnerException as SocketException )?.SocketErrorCode == SocketError.ConnectionReset )
                {
                    break;
                }
                catch ( Exception ex )
                {
                    if ( isConnected )
                    {
                        Main.Log( $"[ClientHandler] Error receiving message: {ex.Message}" );
                    }
                    break;
                }
            }

            Disconnect();
        }

        private bool Handshake()
        {
            // ReceiveTimeout does not bound a blocking recv on this game's Mono; shutting the socket down makes it return 0.
            string line = null;
            bool tooLong = false;
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
                line = ReadHelloLine( out tooLong );
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

            if ( tooLong )
            {
                Main.Log( $"[ClientHandler] Refused {remote}: the first line ran past {HelloMaxChars} characters" );
                return false;
            }

            if ( line == null )
            {
                if ( isConnected )
                {
                    Main.Log( $"[ClientHandler] Closed {remote}: the connection ended before a hello" );
                }
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

        private string ReadHelloLine( out bool tooLong )
        {
            tooLong = false;
            var line = new StringBuilder();
            for ( ;; )
            {
                int next = reader.Read();
                if ( next == -1 )
                {
                    return line.Length == 0 ? null : line.ToString();
                }
                if ( next == '\n' )
                {
                    break;
                }
                if ( line.Length >= HelloMaxChars )
                {
                    tooLong = true;
                    return null;
                }
                line.Append( (char)next );
            }
            if ( line.Length > 0 && line[line.Length - 1] == '\r' )
            {
                line.Length--;
            }
            return line.ToString();
        }

        // Mono's StreamWriter encodes each 1024-character block on its own and would split a surrogate pair that straddles two
        private static string AsciiOnly( string json )
        {
            StringBuilder escaped = null;
            for ( int i = 0; i < json.Length; i++ )
            {
                char c = json[i];
                if ( c < 0x80 )
                {
                    escaped?.Append( c );
                    continue;
                }
                if ( escaped == null )
                {
                    escaped = new StringBuilder( json.Length + 16 );
                    escaped.Append( json, 0, i );
                }
                escaped.Append( "\\u" ).Append( ( (int)c ).ToString( "x4" ) );
            }
            return escaped == null ? json : escaped.ToString();
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
                writer.WriteLine( AsciiOnly( reply.ToString( Formatting.None ) ) );
                return true;
            }
            catch ( Exception ex )
            {
                if ( isConnected )
                {
                    Main.Log( $"[ClientHandler] Failed to reply to {remote}: {ex.Message}" );
                }
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

    internal sealed class StartLock : IDisposable
    {
        private const int WaitMs = 2000;
        private const int O_RDWR = 2;
        private const int LOCK_EX = 2;
        private const int LOCK_NB = 4;
        private const int LOCK_UN = 8;

        [DllImport( "libc", SetLastError = true )]
        private static extern int open( string path, int flags );

        [DllImport( "libc", SetLastError = true )]
        private static extern int flock( int fd, int operation );

        [DllImport( "libc" )]
        private static extern int close( int fd );

        private int fd = -1;
        private FileStream stream;

        // wine enforces a FileShare.None open across the processes of one prefix
        public static StartLock Acquire( string path )
        {
            var held = new StartLock();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds( WaitMs );

            if ( TcpServer.IsUnix )
            {
                File.Open( path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite ).Close();
                held.fd = open( path, O_RDWR );
                if ( held.fd < 0 )
                {
                    Main.Log( $"Could not open {path} to start the server alone (errno {Marshal.GetLastWin32Error()})" );
                    return held;
                }
                while ( flock( held.fd, LOCK_EX | LOCK_NB ) != 0 )
                {
                    if ( DateTime.UtcNow >= deadline )
                    {
                        Main.Log( $"Another game held {path} for {WaitMs / 1000}s; starting the server without it" );
                        break;
                    }
                    Thread.Sleep( 20 );
                }
                return held;
            }

            for ( ;; )
            {
                try
                {
                    held.stream = new FileStream( path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None );
                    return held;
                }
                catch ( IOException )
                {
                    if ( DateTime.UtcNow >= deadline )
                    {
                        Main.Log( $"Another game held {path} for {WaitMs / 1000}s; starting the server without it" );
                        return held;
                    }
                    Thread.Sleep( 20 );
                }
            }
        }

        public void Dispose()
        {
            if ( fd >= 0 )
            {
                flock( fd, LOCK_UN );
                close( fd );
                fd = -1;
            }
            stream?.Close();
            stream = null;
        }
    }

    internal static class WineFiles
    {
        private const uint GenericWrite = 0x40000000;
        private const uint CreateNew = 1;

        private const uint FileAttributeNormal = 0x80;

        // wine maps a descriptor whose owner is a SID outside its token, granted read and write, to Unix mode 0600
        private const string OwnerOnly = "O:OWD:P(A;;FRFW;;;OW)";

        [StructLayout( LayoutKind.Sequential )]
        private struct SecurityAttributes
        {
            public int length;
            public IntPtr descriptor;
            public int inherit;
        }

        [DllImport( "kernel32", CharSet = CharSet.Unicode )]
        private static extern IntPtr GetModuleHandleW( string name );

        [DllImport( "kernel32", CharSet = CharSet.Ansi )]
        private static extern IntPtr GetProcAddress( IntPtr module, string name );

        [DllImport( "advapi32", CharSet = CharSet.Unicode, SetLastError = true )]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW( string sddl, uint revision, out IntPtr descriptor, IntPtr size );

        [DllImport( "kernel32" )]
        private static extern IntPtr LocalFree( IntPtr memory );

        [DllImport( "kernel32", CharSet = CharSet.Unicode, SetLastError = true )]
        private static extern IntPtr CreateFileW( string name, uint access, uint share, ref SecurityAttributes attributes, uint disposition, uint flags, IntPtr template );

        [DllImport( "kernel32", SetLastError = true )]
        private static extern bool WriteFile( IntPtr file, byte[] buffer, uint count, out uint written, IntPtr overlapped );

        [DllImport( "kernel32" )]
        private static extern bool CloseHandle( IntPtr handle );

        public static bool UnderWine()
        {
            try
            {
                IntPtr ntdll = GetModuleHandleW( "ntdll.dll" );
                return ntdll != IntPtr.Zero && GetProcAddress( ntdll, "wine_get_version" ) != IntPtr.Zero;
            }
            catch ( Exception )
            {
                return false;
            }
        }

        public static void WriteOwnerOnly( string path, string text )
        {
            IntPtr descriptor;
            if ( !ConvertStringSecurityDescriptorToSecurityDescriptorW( OwnerOnly, 1, out descriptor, IntPtr.Zero ) )
            {
                throw new IOException( $"the security descriptor could not be built (error {Marshal.GetLastWin32Error()})" );
            }

            try
            {
                var attributes = new SecurityAttributes { length = Marshal.SizeOf( typeof( SecurityAttributes ) ), descriptor = descriptor };
                IntPtr file = CreateFileW( path, GenericWrite, 0, ref attributes, CreateNew, FileAttributeNormal, IntPtr.Zero );
                if ( file == new IntPtr( -1 ) )
                {
                    throw new IOException( $"{path} could not be created (error {Marshal.GetLastWin32Error()})" );
                }

                try
                {
                    byte[] bytes = Encoding.ASCII.GetBytes( text );
                    uint written;
                    if ( !WriteFile( file, bytes, (uint)bytes.Length, out written, IntPtr.Zero ) || written != bytes.Length )
                    {
                        throw new IOException( $"{path} could not be written (error {Marshal.GetLastWin32Error()})" );
                    }
                }
                finally
                {
                    CloseHandle( file );
                }
            }
            finally
            {
                LocalFree( descriptor );
            }
        }
    }
}
