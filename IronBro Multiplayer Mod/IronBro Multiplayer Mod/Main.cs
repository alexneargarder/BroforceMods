/**
 * TODO
 * 
 * Fix weird crashes which seem to be more frequent in the Hell levels
 * 
 * Sync lives correctly in iron bro multiplayer
 * 
 * drop out works but is sort of weird if player 1 leaves and rejoins
 * 
 * Fix other player dying when one player finishes level via zip line in hell
 * 
 * Helicopter stays and level doesn't end on special challenge levels
 * 
**/
/**
 * FIXED
 * 
 * dropout doesn't appear
 * 
 * Doesn't work well in Normal mode multiplayer (if both players die, the level finishes and you win)
 * 
 * If in singleplayer, dying resets the level
 * 
 * helicopter doesn't leave on levels where helicopter drops you off (not a huge deal)
 * 
 * 
 * 
**/

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace IronBro_Multiplayer_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        public static int playersFinished = 0;
        public static GameModeController control;
        public static Map map;
        public static Helicopter heli;

        static bool Load( UnityModManager.ModEntry modEntry )
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = Settings.Load<Settings>( modEntry );


            var harmony = new Harmony( modEntry.Info.Id );
            var assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll( assembly );
            mod = modEntry;

            return true;
        }

        static void OnGUI( UnityModManager.ModEntry modEntry )
        {
            settings.helicopterWait = GUILayout.Toggle( settings.helicopterWait, "Helicopter waits for all players", GUILayout.Width( 300f ) );
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

        public static void Log( String str )
        {
            if ( mod != null )
            {
                mod.Logger.Log( str );
            }
        }
    }
}
