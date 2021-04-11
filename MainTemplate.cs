/**
 * TODO
 * 
 * 
**/
/**
 * IDEAS
 * 
 * 
**/
/**
 * DONE
 * 
 * 
**/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;

namespace NAME_SPACE_HERE
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = Settings.Load<Settings>(modEntry);
            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);
            mod = modEntry;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {

        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            return true;
        }

    }

}

public class Settings : UnityModManager.ModSettings
{
    

    public override void Save(UnityModManager.ModEntry modEntry)
    {
        Save(this, modEntry);
    }

}