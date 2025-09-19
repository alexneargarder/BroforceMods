using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace Unity_Inspector_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        public static TcpServer server;

        static bool Load( UnityModManager.ModEntry modEntry )
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
            
            // Initialize main thread dispatcher
            MainThreadDispatcher.Initialize();
            
            // Initialize input simulator
            InputSimulator.Initialize();

            // Apply Harmony patches
            try
            {
                var harmony = new Harmony(modEntry.Info.Id);
                var assembly = Assembly.GetExecutingAssembly();
                harmony.PatchAll(assembly);
                Log("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Log($"Failed to apply Harmony patches: {ex.Message}");
            }

            return true;
        }

        static void OnGUI( UnityModManager.ModEntry modEntry )
        {
            GUILayout.Label( $"TCP Server Status: {( server != null && server.IsRunning ? "Running" : "Stopped" )}" );

            if ( server != null && server.IsRunning )
            {
                GUILayout.Label( $"Port: {settings.serverPort}" );
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
        }

        static void OnSaveGUI( UnityModManager.ModEntry modEntry )
        {
            settings.Save( modEntry );
        }

        static bool OnToggle( UnityModManager.ModEntry modEntry, bool value )
        {
            enabled = value;
            return true;
        }

        static bool OnUnload( UnityModManager.ModEntry modEntry )
        {
            StopServer();
            return true;
        }

        static void StartServer()
        {
            try
            {
                server = new TcpServer( settings.serverPort );
                server.Start();
            }
            catch ( Exception ex )
            {
                Log( $"Failed to start TCP server: {ex.Message}" );
            }
        }

        static void StopServer()
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

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save( this, modEntry );
        }
    }

}