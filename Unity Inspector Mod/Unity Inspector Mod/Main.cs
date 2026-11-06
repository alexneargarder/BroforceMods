using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace Unity_Inspector_Mod
{
    internal static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        public static TcpServer server;

        private static bool Load( UnityModManager.ModEntry modEntry )
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;
            settings = Settings.Load<Settings>( modEntry );
            mod = modEntry;

            if ( settings.autoStartServer )
            {
                StartServer();
            }

            MainThreadDispatcher.Initialize();

            InputSimulator.Initialize();

            SwapBrosIntegration.Initialize( Log );

            try
            {
                var harmony = new Harmony( modEntry.Info.Id );
                var assembly = Assembly.GetExecutingAssembly();
                harmony.PatchAll( assembly );
                Log( "Harmony patches applied successfully" );
            }
            catch ( Exception ex )
            {
                Log( $"Failed to apply Harmony patches: {ex.Message}" );
            }

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
                        $"Off: the server only listens on 127.0.0.1. On: it listens on all interfaces, which is required when the MCP client runs in a VM such as WSL. A client on the network needs the token from {tokenFile}, which changes every time the server starts." ) ) ) )
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

        private static bool OnToggle( UnityModManager.ModEntry modEntry, bool value )
        {
            enabled = value;
            return true;
        }

        private static bool OnUnload( UnityModManager.ModEntry modEntry )
        {
            StopServer();
            return true;
        }

        private static void StartServer()
        {
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

        public static void Log( String str )
        {
            mod.Logger.Log( str );
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
