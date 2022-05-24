/**
 * TODO
 * 
 * Add option to have dolflundgren not instantly win you the level when he dies
 * 
 * Add option to have satan spawn his death field once he dies
 * 
 * Fix stage 6 boss level and satan boss level being unbeatable (because they get replaced)
 * 
 * Add an option to randomize the level order
 * 
 * Make sure tooltips are readable
 * 
 * Maybe look into only loading prefabs once to improve performance
 * 
 **/

/**
 * DONE
 * 
 * Maybe fix satan going into an unskippable cutscene when he is killed the second time
 * 
 * Try to get Lost Souls and Armoured guys working, need to adjust their spawn location probably
 * 
 * Investigate if warlocks only spawn exploding guys or if they are being randomized ( think this is fixed )
 * 
 * Have enemies that should spawn with parachutes actually spawn with them
 * 
 * Look into enemies not running correctly after being spawned
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


        public static string[] mookTypes = new string[] { "ZHellBigGuy", "ZHellDog", "ZMook Grenadier", "ZMook", "ZMookBazooka", "ZMookBigGuy", 
            "ZMookDog", "ZMookGeneral", "ZMookHellBoomer", "ZMookHellSoulCatcher", "ZMookJetpack", "ZMookNinja", "ZMookRiotShield",
        "ZMookScout", "ZMookSkinless", "ZMookSuicide", "ZMookUndead", "ZMookUndeadStartDead", "ZMookUndeadSuicide", "ZMookWarlock", "ZSatan", "ZHellLostSoul", "ZMookArmouredGuy"};

        /*public static string[] mookTypes = new string[] { "ZHellBigGuy", "ZHellDog", "ZMook Grenadier", "ZMookBazooka",
           "ZMookGeneral", "ZMookHellBoomer", "ZMookHellSoulCatcher", "ZMookJetpack", "ZMookNinja", "ZMookRiotShield",
        "ZMookScout", "ZMookSkinless", "ZMookUndead", "ZMookUndeadStartDead", "ZMookUndeadSuicide", "ZMookWarlock", "ZHellLostSoul", "ZMookArmouredGuy"};*/

        public static string[] alienTypes = new string[] { "ZAlienBrute", "ZAlienFaceHugger",
        "ZAlienMelter", "ZAlienMosquito", "ZAlienXenomorph", "ZAlienXenomorphBrainBox" };

        public static string[] mookBossTypes = new string[] { "DolfLundgrenSoldier", "SatanMinibossStage1" };

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
        public static string[] mookTypesGlitchy = new string[] { "ZMook Backup", "ZMookCaptain", "ZMookCaptainCutscene",
        "ZMookCaptainExpendabro", "ZMookHellArmouredBigGuy", "ZMookMortar", "ZSatan Mook", "ZSatanCutscene" };


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

            debugMookString = settings.debugMookType.ToString();
            settings.DEBUG = false;

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
            if (settings.DEBUG)
            {
                debugMookString = GUILayout.TextField(debugMookString);

                int temp;
                if (int.TryParse(debugMookString, out temp))
                    settings.debugMookType = temp;

                if (settings.debugMookType > (mookTypes.Length + alienTypes.Length + mookBossTypes.Length) - 1 )
                    settings.debugMookType = (mookTypes.Length + alienTypes.Length + mookBossTypes.Length) - 1;

                if (settings.debugMookType < 0)
                    settings.debugMookType = 0;


                if (settings.debugMookType < Main.mookTypes.Length)
                {
                    GUILayout.Label("Current Mook Type: " + mookTypes[settings.debugMookType]);
                }
                else if ( settings.debugMookType < (mookTypes.Length + alienTypes.Length) )
                {
                    GUILayout.Label("Current Alien Type: " + alienTypes[settings.debugMookType - Main.mookTypes.Length]);
                }
                else
                {
                    GUILayout.Label("Current Boss Type: " + mookBossTypes[settings.debugMookType - mookTypes.Length - alienTypes.Length]);
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

        public bool DEBUG;
        public int debugMookType;

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

            //Main.Log("in start: " + __instance.mookType);

            // Avoid replacing enemies that have already been replaced
            if (__instance.playerNum == -1000)
            {
                //Main.Log("returning due to playernum: " + __instance.mookType);
                //__instance.playerNum = -1;
                return;
            }

            GameObject gameObject = null;

            if ( (Main.settings.enemyPercent > rnd.Next(0, 100)) )
            {
                int chosen = 0;
                float offset = 0f;

                if (!Main.settings.allowEnemiesToBeBosses)
                {
                    chosen = rnd.Next(0, Main.mookTypes.Length + Main.alienTypes.Length);
                }
                else
                {
                    chosen = rnd.Next(0, Main.mookTypes.Length + Main.alienTypes.Length + Main.mookBossTypes.Length);
                }

                if (Main.settings.DEBUG)
                {
                    chosen = Main.settings.debugMookType;
                }

                if (chosen < Main.mookTypes.Length)
                {
                    if (chosen == 21)
                    {
                        offset = 10f;
                    }

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


                Mook result = MapController.SpawnMook_Networked(prefab, __instance.X, __instance.Y + 2f + offset, 0, 0, false, __instance.parachute, __instance.IsParachuteActive, false, false);

                result.playerNum = -1000;

                __instance.DestroyCharacter();
            }
            else
            {
                __instance.playerNum = -1000;
                Main.Log("randomly not chosen");
            }

        }
    }

    [HarmonyPatch(typeof(SatanMiniboss), "ForceCameraToFollow")]
    static class SatanMiniboss_ForceCameraToFollow
    {
        public static bool Prefix(SatanMiniboss __instance)
        {
            if (!Main.enabled)
            {
                return true;
            }

            return false;
        }
    }


}