using System;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace Custom_Triggers_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;

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

            CustomTriggerManager.RegisterCustomTrigger( typeof( MyCustomTriggerAction ), typeof( MyCustomTriggerActionInfo ), "my custom trigger" );

            return true;
        }

        static void OnGUI( UnityModManager.ModEntry modEntry )
        {
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
            mod.Logger.Log( str );
        }

    }

    public class Settings : UnityModManager.ModSettings
    {

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save( this, modEntry );
        }

    }
}