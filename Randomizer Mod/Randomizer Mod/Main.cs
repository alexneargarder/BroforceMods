/**
 * TODO
 * 
 * Investigate if warlocks only spawn exploding guys or if they are being randomized
 * 
 * Look into enemies not running correctly after being spawned
 * 
 * Have enemies that should spawn with parachutes actually spawn with them
 * 
 * Add an option to randomize the level order
 * 
 * Try to get Lost Souls and Armoured guys working, need to adjust their spawn location probably
 * 
 * Make sure tooltips are readable
 * 
 * Maybe look into only loading prefabs once to improve performance
 * 
 * Maybe fix satan going into an unskippable cutscene when he is killed the second time
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
using Networking;

namespace Randomizer_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;

        public static bool DEBUG = false;

        
        public static string[] mookTypes = new string[] { "ZHellBigGuy", "ZHellDog", "ZMook Grenadier", "ZMook", "ZMookBazooka", "ZMookBigGuy", 
            "ZMookDog", "ZMookGeneral", "ZMookHellBoomer", "ZMookHellSoulCatcher", "ZMookJetpack", "ZMookNinja", "ZMookRiotShield",
        "ZMookScout", "ZMookSkinless", "ZMookSuicide", "ZMookUndead", "ZMookUndeadStartDead", "ZMookUndeadSuicide", "ZMookWarlock", "ZSatan" };

        public static string[] alienTypes = new string[] { "ZAlienBrute", "ZAlienFaceHugger",
        "ZAlienMelter", "ZAlienMosquito", "ZAlienXenomorph", "ZAlienXenomorphBrainBox" };

        // Don't spawn at all
        public static string[] mookTypesNotWorking = new string[] { "Pig Rotten", "Pig", "Seagull", "WarlockPortal Suicide", "WarlockPortal", "WarlockPortalLarge", 
            "ZConradBroneBanks", "ZHellDogEgg" };

        // Don't spawn at all
        public static string[] alienTypesNotWorking = new string[] { "Alien SandWorm Facehugger Launcher Behind", "Alien SandWorm Facehugger Launcher",
        "AlienGiantBoss SandWorm", "AlienGiantSandWorm", "AlienMiniBoss SandWorm", "SandwormHeadGibHolder" };

        // Likely Fixable - "ZHellLostSouls" explode on spawn
        // "ZMook Backup" don't shoot and freeze when thrown
        // Likely Fixable - "ZMookArmouredGuy" spawn in the wrong place and die, probably too big to fit in some locations
        // "ZMookCaptain" stands still and does nothing
        // "ZMookCaptainCutscene" isn't really a real enemy
        // "ZMookCaptainExpendabro" Only seem to be for alerting enemies
        // "ZMookHellArmouredBigGuy" Doesn't shoot, seems unfinished
        // "ZMookMortar" unfinished mortar enemy
        // "ZSatan Mook" screams and disappears
        // "ZSatanCutscene" uninteractable
        public static string[] mookTypesGlitchy = new string[] { "ZHellLostSoul", "ZMook Backup", "ZMookArmouredGuy", "ZMookCaptain", "ZMookCaptainCutscene",
        "ZMookCaptainExpendabro", "ZMookHellArmouredBigGuy", "ZMookMortar", "ZSatan Mook", "ZSatanCutscene" };

        public static string[] mookBossTypes = new string[] { "DolfLundgrenSoldier", "SatanMinibossStage1" };

        public static int debugMookType = 0;
        public static string debugMookString = "0";

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
            settings.enableEnemyRandomization = GUILayout.Toggle(settings.enableEnemyRandomization, new GUIContent("Enable Enemy Randomization", "Randomly replaces enemies of one type with another"));
            settings.allowEnemiesToBeBosses = GUILayout.Toggle(settings.allowEnemiesToBeBosses, new GUIContent("Allow Enemies to become Bosses", "Chance for enemies to become certain minibosses"));

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Randomized Enemy Percent: " + settings.enemyPercent);
                settings.enemyPercent = GUILayout.HorizontalSlider(settings.enemyPercent, 0, 100);
            }
            GUILayout.EndHorizontal();
            

            // DEBUG
            if (DEBUG)
            {
                debugMookString = GUILayout.TextField(debugMookString);

                int temp;
                if (int.TryParse(debugMookString, out temp))
                    debugMookType = temp;

                if (debugMookType > (mookTypes.Length + alienTypes.Length + mookBossTypes.Length) - 1 )
                    debugMookType = (mookTypes.Length + alienTypes.Length + mookBossTypes.Length) - 1;

                if (debugMookType < 0)
                    debugMookType = 0;


                if (debugMookType < Main.mookTypes.Length)
                {
                    GUILayout.Label("Current Mook Type: " + mookTypes[debugMookType]);
                }
                else if ( debugMookType < (mookTypes.Length + alienTypes.Length) )
                {
                    GUILayout.Label("Current Alien Type: " + alienTypes[debugMookType - Main.mookTypes.Length]);
                }
                else
                {
                    GUILayout.Label("Current Boss Type: " + mookBossTypes[debugMookType - mookTypes.Length - alienTypes.Length]);
                }
                
            }

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

        public static void Log(String str)
        {
            mod.Logger.Log(str);
        }

    }

    public class Settings : UnityModManager.ModSettings
    {
        public bool enableEnemyRandomization;
        public bool allowEnemiesToBeBosses;
        public float enemyPercent;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }

    /*[HarmonyPatch(typeof(MapController), "SpawnMook_Networked")]
    static class MapController_SpawnMook_NetworkedPatch
    {
        public static void Prefix(Mook mookPrefab, float x, float y, float xI, float yI, bool tumble, bool useParachuteDelay, bool useParachute, bool onFire, bool isAlert)
        {
            if (!Main.enabled)
            {
                return;
            }

            Main.Log("spawn mook called");

        }
    }*/

    [HarmonyPatch(typeof(Mook), "Awake")]
    static class Mook_Awake
    {
        public static void Prefix(Mook __instance)
        {
            if (!Main.enabled)
            {
                return;
            }

            //Main.Log("awake mook called");

            //GameObject gameObject = InstantiationController.GetPrefabFromLegacyResourceName("Mooks/ZMookBazooka");

            //Main.Log("tempObject null?: " + (gameObject == null ? "true" : "false"));

            /*Main.Log("\n\n");
            Component[] allComponents;
            allComponents = gameObject.GetComponents(typeof(Component));
            foreach (Component comp in allComponents)
            {
                Main.Log("attached: " + comp.name + " also " + comp.GetType());
            }
            Main.Log("\n\n");*/

            //MookBazooka prefab = gameObject.GetComponent<MookBazooka>();

            //Main.Log("prefab null? " + (prefab == null ? "true" : "false"));

            //MapController.SpawnMook_Local(prefab, __instance.X + 5, __instance.Y + 5, __instance.xI, __instance.yI, __instance.canTumble, false, __instance.parachute, false, false );
            //Main.Log("position: " + __instance.X + " " + __instance.Y + " " + __instance.xI + " " + __instance.yI);
            //Main.Log("position transform: " + __instance.transform.position.x + " " + __instance.transform.position.y );


            //prefab.transform.SetX(__instance.transform.position.x);
            //prefab.transform.SetY(__instance.transform.position.y);


            //MapController.SpawnMook_Networked(prefab, __instance.transform.position.x, __instance.transform.position.y, 0, 0, __instance.canTumble, false, __instance.parachute, false, false );

            //Main.Log("exiting awake\n\n");
            return;
        }
    }

    [HarmonyPatch(typeof(Mook), "Start")]
    static class Mook_Start
    {
        static System.Random rnd = new System.Random();

        public static void Postfix(Mook __instance)
        {
            if (!Main.enabled || !Main.settings.enableEnemyRandomization)
            {
                return;
            }

            // Avoid replacing enemies that have already been replaced
            if (__instance.playerNum == -1000)
            {
                //Main.Log("returning due to playernum: " + __instance.mookType);
                __instance.playerNum = -1;
                return;
            }

            GameObject gameObject = null;

            if ( (Main.settings.enemyPercent > rnd.Next(0, 100)) || Main.DEBUG )
            {
                int chosen = 0;
                if (!Main.settings.allowEnemiesToBeBosses)
                {
                    chosen = rnd.Next(0, Main.mookTypes.Length + Main.alienTypes.Length);
                }
                else
                {
                    chosen = rnd.Next(0, Main.mookTypes.Length + Main.alienTypes.Length + Main.mookBossTypes.Length);
                }

                if (Main.DEBUG)
                {
                    chosen = Main.debugMookType;
                }

                if (chosen < Main.mookTypes.Length)
                {
                    if (chosen == 3)
                        chosen += 1;
                    gameObject = InstantiationController.GetPrefabFromLegacyResourceName("Mooks/" + Main.mookTypes[chosen]);
                }
                else if ( chosen < Main.mookTypes.Length + Main.alienTypes.Length )
                {
                    chosen = chosen - Main.mookTypes.Length;

                    gameObject = InstantiationController.GetPrefabFromLegacyResourceName("Aliens/" + Main.alienTypes[chosen]);
                }
                else
                {
                    chosen = chosen - Main.mookTypes.Length - Main.alienTypes.Length;

                    gameObject = InstantiationController.GetPrefabFromLegacyResourceName("Mooks/" + Main.mookBossTypes[chosen]);
                }

                Mook prefab = gameObject.GetComponent<Mook>();

                prefab.playerNum = -1000;

                MapController.SpawnMook_Networked(prefab, __instance.X, __instance.Y + 2f, 0, 0, __instance.canTumble, false, false, false, false);

                __instance.DestroyCharacter();
            }
            else
            {
                //Main.Log("randomly not chosen");
            }

        }
    }

    /*[HarmonyPatch(typeof(NetworkSpawnableManifest), "GetPrefabFromLegacyResourceName")]
    static class NetworkSpawnableManifest_GetPrefabFromLegacyResourceName
    {
        public static void Prefix(NetworkSpawnableManifest __instance)
        {
            return;
            if (!Main.enabled)
            {
                return;
            }

            Dictionary<string, SpawnablePrefab> dict = Traverse.Create(__instance).Field("resourceNameLookupDictionary").GetValue() as Dictionary<string, SpawnablePrefab>;

            for (int i = 0; i < dict.Keys.Count; ++i)
            {
                Main.Log(dict.Keys.ElementAt(i));
            }


            return;
        }
    }*/

    /*[HarmonyPatch(typeof(InstantiationController), "GetPrefabFromLegacyResourceName")]
    static class InstantiationController_GetPrefabFromLegacyResourceName
    {
        public static void Prefix(string resourceName)
        {
            if (!Main.enabled)
            {
                return;
            }

            Main.Log("getting prefab: " + resourceName);

        }
    }*/
}