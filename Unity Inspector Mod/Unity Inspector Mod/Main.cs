using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace Unity_Inspector_Mod
{
    internal static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static Settings settings;
        public static TcpServer server;
        private static int mainThreadId;
        private static readonly Queue<string> pendingLog = new Queue<string>();

        private static bool Load( UnityModManager.ModEntry modEntry )
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;
            settings = Settings.Load<Settings>( modEntry );
            mod = modEntry;

            MainThreadDispatcher.Initialize();

            InputSimulator.Initialize();

            SwapBrosIntegration.Initialize( Log );

            try
            {
                var harmony = new Harmony( modEntry.Info.Id );
                var assembly = Assembly.GetExecutingAssembly();
                harmony.PatchAll( assembly );
                Log( "Harmony patches applied successfully" );
                InputSimulator.CheckPatches( harmony, null );
            }
            catch ( Exception ex )
            {
                Log( $"Failed to apply Harmony patches: {ex.Message}" );
                InputSimulator.CheckPatches( new Harmony( modEntry.Info.Id ), ex );
            }

            FrozenClock.Install( new Harmony( modEntry.Info.Id ) );

            InputSimulator.InstallBindingPatch( new Harmony( modEntry.Info.Id ) );

            return true;
        }

        private static void OnGUI( UnityModManager.ModEntry modEntry )
        {
            GUILayout.Label( $"TCP Server Status: {( server != null && server.IsRunning ? "Running" : "Stopped" )}" );

            if ( server != null && server.IsRunning )
            {
                GUILayout.Label( $"Port: {settings.serverPort}" );
                GUILayout.Label( $"Bound To: {( server.AllowRemoteConnections ? "0.0.0.0 (all interfaces)" : "127.0.0.1 (localhost only)" )}" );
                GUILayout.Label( $"Connected Clients: {server.ConnectedClients}" );

                if ( GUILayout.Button( "Stop Server", GUILayout.Width( 200 ) ) )
                {
                    StopServer();
                }
            }
            else
            {
                if ( GUILayout.Button( "Start Server", GUILayout.Width( 200 ) ) )
                {
                    StartServer();
                }
            }

            GUILayout.Space( 10 );
            settings.autoStartServer = GUILayout.Toggle( settings.autoStartServer, "Auto-start server on load" );

            string tokenFile = TcpServer.TokenFileName( settings.serverPort );

            // Drawn before the toggle so the Layout and event passes see the same value
            if ( settings.allowRemoteConnections )
            {
                GUILayout.Label( $"Remote connections are on. A client on the network needs the token from {tokenFile}, which changes every time the server starts. The mod's log names its full path." );
            }

            if ( settings.allowRemoteConnections != ( settings.allowRemoteConnections = GUILayout.Toggle( settings.allowRemoteConnections,
                    new GUIContent( $"Allow remote connections (LAN) - clients need the token from {tokenFile}",
                        $"Off: the server only listens on 127.0.0.1. On: it listens on all interfaces, which a client on another machine needs, and so does one in WSL2 with its default NAT networking. A client on the network needs the token from {tokenFile}, which changes every time the server starts." ) ) ) )
            {
                settings.Save( mod );

                if ( server != null && server.IsRunning )
                {
                    Log( $"Allow remote connections changed to {settings.allowRemoteConnections} - restarting TCP server" );
                    StopServer();
                    StartServer();
                }
            }
        }

        private static void OnSaveGUI( UnityModManager.ModEntry modEntry )
        {
            settings.Save( modEntry );
        }

        // UMM activates a mod it has just loaded through OnToggle(true)
        private static bool OnToggle( UnityModManager.ModEntry modEntry, bool value )
        {
            if ( !value )
            {
                StopServer();
                InputSimulator.StopAll( "the Unity Inspector Mod was disabled" );
            }
            else if ( settings.autoStartServer )
            {
                StartServer();
            }
            return true;
        }

        private static bool OnUnload( UnityModManager.ModEntry modEntry )
        {
            StopServer();
            InputSimulator.StopAll( "the Unity Inspector Mod was unloaded" );
            FlushPendingLog();
            return true;
        }

        private static void StartServer()
        {
            if ( server != null && server.IsRunning )
            {
                return;
            }
            try
            {
                server = new TcpServer( settings.serverPort, settings.allowRemoteConnections, Application.persistentDataPath );
                server.Start();
            }
            catch ( Exception ex )
            {
                Log( $"Failed to start TCP server: {ex.Message}" );
            }
        }

        private static void StopServer()
        {
            if ( server != null )
            {
                server.Stop();
                server = null;
            }
        }

        // UMM's logger is unsynchronized and its main thread writes the buffer out every half second
        public static void Log( String str )
        {
            if ( Thread.CurrentThread.ManagedThreadId != mainThreadId )
            {
                lock ( pendingLog )
                {
                    pendingLog.Enqueue( str );
                }
                return;
            }
            FlushPendingLog();
            mod.Logger.Log( str );
        }

        internal static void FlushPendingLog()
        {
            while ( true )
            {
                string line;
                lock ( pendingLog )
                {
                    if ( pendingLog.Count == 0 )
                    {
                        return;
                    }
                    line = pendingLog.Dequeue();
                }
                mod.Logger.Log( line );
            }
        }
    }

    public class Settings : UnityModManager.ModSettings
    {
        public int serverPort = 9999;
        public bool autoStartServer = true;
        public bool allowRemoteConnections = false;

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save( this, modEntry );
        }
    }
}
