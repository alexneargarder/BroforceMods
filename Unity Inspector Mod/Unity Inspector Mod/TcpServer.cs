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
        private bool isRunning;
        private readonly int port;
        private readonly List<ClientHandler> clients = new List<ClientHandler>();
        private readonly MessageHandler messageHandler;

        public bool IsRunning => isRunning;
        public int Port => port;
        public int ConnectedClients => clients.Count;

        public TcpServer( int port )
        {
            this.port = port;
            this.messageHandler = new MessageHandler();
        }

        public void Start()
        {
            if ( isRunning ) return;

            try
            {
                listener = new TcpListener( IPAddress.Any, port );
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

            foreach ( var client in clients.ToArray() )
            {
                client.Disconnect();
            }
            clients.Clear();

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

                    if ( listener.Pending() )
                    {
                        var tcpClient = listener.AcceptTcpClient();
                        var client = new ClientHandler( tcpClient, this );
                        clients.Add( client );
                    }
                    Thread.Sleep( 100 );
                }
                catch ( Exception ex )
                {
                    if ( isRunning )
                    {
                        Main.Log( $"Error accepting client: {ex.Message}" );
                        Main.Log( $"Exception details: {ex}" );
                    }
                }
            }
        }

        internal void RemoveClient( ClientHandler client )
        {
            clients.Remove( client );
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