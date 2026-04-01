using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public static class ScriptContext
    {
        public static Harmony Harmony { get; internal set; }
        public static Action<string> Logger { get; internal set; }
        public static List<GameObject> GameObjects { get; internal set; }
        public static Dictionary<string, string> Args { get; internal set; }

        public static string GetArg(string key, string defaultValue = "")
        {
            if (Args == null) return defaultValue;
            string value;
            return Args.TryGetValue(key, out value) ? value : defaultValue;
        }

        internal static void Clear()
        {
            Harmony = null;
            Logger = null;
            GameObjects = null;
            Args = null;
        }
    }
}
