using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public static class ScriptContext
    {
        private static readonly Action<string> DefaultLogger = msg => Main.Log("[Script] " + msg);

        private static Action<string> logger = DefaultLogger;

        public static Harmony Harmony { get; internal set; }
        public static List<GameObject> GameObjects { get; internal set; }
        public static Dictionary<string, string> Args { get; internal set; }

        public static Action<string> Logger
        {
            get { return logger; }
            internal set { logger = value ?? DefaultLogger; }
        }

        public static string GetArg(string key, string defaultValue = "")
        {
            if (Args == null) return defaultValue;
            string value;
            return Args.TryGetValue(key, out value) ? value : defaultValue;
        }

        internal sealed class Scope
        {
            internal Harmony Harmony;
            internal Action<string> Logger;
            internal List<GameObject> GameObjects;
            internal Dictionary<string, string> Args;
        }

        internal static Scope Enter(Harmony harmony, Action<string> scriptLogger,
            List<GameObject> gameObjects, Dictionary<string, string> args)
        {
            var saved = new Scope
            {
                Harmony = Harmony,
                Logger = logger,
                GameObjects = GameObjects,
                Args = Args
            };

            Harmony = harmony;
            Logger = scriptLogger;
            GameObjects = gameObjects;
            Args = args;

            return saved;
        }

        internal static void Exit(Scope saved)
        {
            Harmony = saved.Harmony;
            Logger = saved.Logger;
            GameObjects = saved.GameObjects;
            Args = saved.Args;
        }
    }
}
