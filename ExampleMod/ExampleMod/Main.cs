// If you are interested in modding this game, I would highly recommend reading
// the Unity Mod Manager Wiki: https://wiki.nexusmods.com/index.php/Category:Unity_Mod_Manager
// There is lots of information there about getting started with modding

// This project can serve as a starting off point although you'll like have to do some
// configuration, such as making sure you have the right references set and making sure
// you're using .NET Framework 3.5

// I've included more comments than you'll find in my other projects throughout this example
// to help anyone who is new to modding this game
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;

// The namespace is important when it comes to loading the mod
// If you change this make sure you update the EntryMethod field in the Info.json file,
// or else your mod will not load.
namespace EXAMPLE_MOD
{
    // If you change the name of your class you'll need to update the Info.json file
    static class Main
    {
        // ModEntry is mainly used to print to the log
        public static UnityModManager.ModEntry mod;
        
        // Enabled indicates whether the mod is enabled, this controls
        // whether harmony patches should be modifying behavior or not
        public static bool enabled;

        
        public static Settings settings;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            // These lines are telling Unity Mod Manager the functions it needs to call
            // to display the GUI, save the Mod Settings, and Enable/Disable the mod
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;

            // This line loads the settings from the Settings.xml file
            settings = Settings.Load<Settings>(modEntry);

            // These lines are for executing the harmony patches
            // Harmony patches are what actually change the behavior of the game
            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);

            // We save a reference to modEntry for displaying messages to the log
            mod = modEntry;

            return true;
        }

        // This method handles displaying the mod settings
        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (GUILayout.Button("Button 1", GUILayout.Width(100)))
            {
                Main.Log("Button 1 Pressed");
                settings.count++;
            }

            GUILayout.Label("Button 1 Pressed: " + settings.count + " times");
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        // This method is called whenever the mod is enabled / disabled
        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            return true;
        }

        // This function makes printing stuff to the log easier
        // Normally you would do
        // Main.mod.Logger.Log("Hello World");
        // With this function you can do
        // Main.Log("Hello World");
        // If you want to use this function in classes in other namespaces, include this line:
        // using EXAMPLE_MOD;
        public static void Log(String str)
        {
            mod.Logger.Log(str);
        }

    }

    // If you have any mod settings that you want to have persist between game sessions you can store them here
    // All you need to do is add a new variable to the Settings class below and it will automatically be saved and loaded
    // The settings.xml file will automatically be updated as well
    public class Settings : UnityModManager.ModSettings
    {
        public int count = 0;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }


    // Here is an example of a Harmony Patch
    // Player is the class that contains the method you're patching
    // GetInput is the method you're patchhing
    [HarmonyPatch(typeof(Player), "GetInput")]
    // Player_GetInput_Patch is simply a name for the class that contains the patch
    // You can use any name here but I would recommend using a consistent naming scheme
    static class Player_GetInput_Patch
    {
        // Prefix patches run before the method, Postfix patches run after

        // Player __instance provides a reference to the current object 
        // that is running the method

        // You can access the original arguments of the method by including
        // arguments with the same name and type with the ref keyword added
        public static void Prefix(Player __instance, ref bool buttonGesture)
        {
            // I would recommend including this in all of your patches
            // If the mod is toggled off, enabled will be set to false,
            // so we can simply return to avoid modifying the game behavior at all
            if (!Main.enabled)
            {
                return;
            }
            if (buttonGesture)
            {
                // This message will display multiple times because the GetInput method is called many times per second
                Main.Log("Player taunted");
            }
        }
    }

    // For more details about how Harmony Patches work, see the documentation:
    // https://harmony.pardeike.net/articles/patching-prefix.html

}