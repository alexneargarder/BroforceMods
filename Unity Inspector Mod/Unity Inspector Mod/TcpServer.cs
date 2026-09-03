using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Unity_Inspector_Mod
{
    public class TcpServer
    {
        private TcpListener listener;
        private Thread listenerThread;
        private volatile bool isRunning;
        private readonly int port;
        private readonly bool allowRemoteConnections;
        private readonly List<ClientHandler> clients = new List<ClientHandler>();
        private readonly object clientsLock = new object();
        private readonly MessageHandler messageHandler;

        public bool IsRunning => isRunning;
        public int Port => port;
        public bool AllowRemoteConnections => allowRemoteConnections;

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

        public TcpServer( int port, bool allowRemoteConnections )
        {
            this.port = port;
            this.allowRemoteConnections = allowRemoteConnections;
            this.messageHandler = new MessageHandler();
        }

        public void Start()
        {
            if ( isRunning ) return;

            try
            {
                IPAddress bindAddress = allowRemoteConnections ? IPAddress.Any : IPAddress.Loopback;

                if ( allowRemoteConnections )
                {
                    Main.Log( "==============================================================" );
                    Main.Log( $"WARNING: Unity Inspector is listening on ALL network interfaces (0.0.0.0:{port})." );
                    Main.Log( "WARNING: There is NO authentication. Anyone who can reach this port" );
                    Main.Log( "WARNING: can execute arbitrary code on this machine." );
                    Main.Log( "WARNING: Disable 'Allow remote connections' unless you need it." );
                    Main.Log( "==============================================================" );
                }

                listener = new TcpListener( bindAddress, port );
                listener.Start();
                isRunning = true;

                listenerThread = new Thread( ListenForClients );
                listenerThread.IsBackground = true;
                listenerThread.Start();
            }
            catch ( Exception ex )
            {
                Main.Log( $"Failed to start TcpListener: {ex}" );
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
                listener.Stop();
                listener = null;
            }

            if ( listenerThread != null )
            {
                listenerThread.Join( 1000 );
                listenerThread = null;
            }
        }

        private void ListenForClients()
        {
            while ( isRunning )
            {
                try
                {
                    var currentListener = listener;
                    if ( currentListener == null ) break;

                    if ( currentListener.Pending() )
                    {
                        var tcpClient = currentListener.AcceptTcpClient();

                        try
                        {
                            lock ( clientsLock )
                            {
                                if ( isRunning )
                                {
                                    clients.Add( new ClientHandler( tcpClient, this ) );
                                    tcpClient = null;
                                }
                            }
                        }
                        finally
                        {
                            tcpClient?.Close();
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
        }

        internal string ProcessMessage( string message )
        {
            string result = messageHandler.HandleMessage( message );
            return result;
        }
    }

    internal class ClientHandler
    {
        private readonly TcpClient client;
        private readonly TcpServer server;
        private readonly Thread receiveThread;
        private readonly NetworkStream stream;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;
        private bool isConnected;

        public ClientHandler( TcpClient client, TcpServer server )
        {
            this.client = client;
            this.server = server;
            this.stream = client.GetStream();
            this.reader = new StreamReader( stream, new UTF8Encoding( false ) ); // false = no BOM
            this.writer = new StreamWriter( stream, new UTF8Encoding( false ) ) { AutoFlush = true };
            this.isConnected = true;

            receiveThread = new Thread( ReceiveMessages );
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        private void ReceiveMessages()
        {
            while ( isConnected && client.Connected )
            {
                try
                {
                    string message = reader.ReadLine();
                    if ( message != null )
                    {
                        // Process message from client
                        string response = server.ProcessMessage( message );
                        
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

        public void Disconnect()
        {
            if ( !isConnected ) return;
            isConnected = false;

            try
            {
                reader?.Close();
                writer?.Close();
                stream?.Close();
                client?.Close();
            }
            catch { }

            server.RemoveClient( this );
        }
    }
}