/**
 * TODO
 * 
 * Replace prefab in spawn mook local with randomly selected one
 * 
 * Add option to have dolflundgren not instantly win you the level when he dies
 * 
 * Add option to have satan spawn his death field once he dies
 * 
 * Add an option to randomize the level order
 * 
 * Make sure tooltips are readable
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

        public static string[] alienTypes = new string[] { "ZAlienBrute", "ZAlienFaceHugger",
        "ZAlienMelter", "ZAlienMosquito", "ZAlienXenomorph", "ZAlienXenomorphBrainBox" };

        public static string[] mookBossTypes = new string[] { "DolfLundgrenSoldier", "SatanMinibossStage1" };


        // Don't spawn at all
        public static string[] mookTypesNotWorking = new string[] { "Pig Rotten", "Pig", "Seagull", "WarlockPortal Suicide", "WarlockPortal", "WarlockPortalLarge",
            "ZConradBroneBanks", "ZHellDogEgg" };

        // Don't spawn at all
        public static string[] alienTypesNotWorking = new string[] { "Alien SandWorm Facehugger Launcher Behind", "Alien SandWorm Facehugger Launcher",
        "AlienGiantBoss SandWorm", "AlienGiantSandWorm", "AlienMiniBoss SandWorm", "SandwormHeadGibHolder" };

        // "ZMook Backup" don't shoot and freeze when thrown
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

        public static string[] debugMookTypeList = new string[] { "mook",
            "mookSuicide",
            "moookRiotShield",
            "mookBigGuy",
            "mookScout",
            "mookDog",
            "mookArmoured",
            "mookGrenadier",
            "mookBazooka",
            "mookNinja",
            "mookJetpack",
            "mookXenomorphBrainbox",
            "skinnedMook",
            "mookGeneral",
            "satan",
            "alienFaceHugger",
            "alienXenomorph",
            "alienBrute",
            "alienBaneling",
            "alienMosquito",
            "ZHellDog - class HellDog",
            "ZMookUndead - class UndeadTrooper",
            "ZMookUndeadStartDead - class UndeadTrooper",
            "ZMookWarlock - class Warlock",
            "ZMookHellBoomer - class MookHellBoomer",
            "ZMookUndeadSuicide - class MookSuicideUndead",
            "ZHellBigGuy - class MookHellBigGuy",
            "ZHellLostSoul - class HellLostSoul",
            "ZMookHellSoulCatcher - class MookHellSoulCatcher",
            "Hell BoneWorm - class AlienGiantSandWorm",
            "Hell MiniBoss BoneWorm - class HellBoneWormMiniboss",
            "Hell MiniBoss BoneWorm Behind - class HellBoneWormMiniboss",
            "Satan Miniboss",
            "mookDolfLundgren",
            "mookMammothTank",
            "mookKopterMiniBoss",
            "goliathMech"
        };

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
            settings.DEBUG = true;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.enableEnemyRandomization = GUILayout.Toggle(settings.enableEnemyRandomization, new GUIContent("Enable Enemy Randomization", "Randomly replaces enemies of one type with another"));
            settings.enableWorms = GUILayout.Toggle(settings.enableWorms, new GUIContent("Allow Enemies to become Worms", "Chance for enemies to become Sandworms or Boneworms"));
            settings.enableBosses = GUILayout.Toggle(settings.enableBosses, new GUIContent("Allow Enemies to become Bosses", "Chance for enemies to become certain minibosses"));
            settings.enableLargeBosses = GUILayout.Toggle(settings.enableLargeBosses, new GUIContent("Allow Enemies to become Large Bosses", "Chance for enemies to become certain large minibosses"));

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

                if (settings.debugMookType > debugMookTypeList.Length - 1)
                    settings.debugMookType = debugMookTypeList.Length - 1;

                if (settings.debugMookType < 0)
                    settings.debugMookType = 0;


                GUILayout.Label("Current Enemey Type: " + debugMookTypeList[settings.debugMookType]);

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
        public bool enableBosses;
        public bool enableLargeBosses;
        public bool enableWorms;
        public bool enableInstantWin;
        public bool enableDeathField;
        public float enemyPercent;

        public bool DEBUG;
        public int debugMookType;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }

    [HarmonyPatch(typeof(MapController), "SpawnMook_Local")]
    static class MapController_SpawnMook_Local
    {
        public static void Prefix(Mook mookPrefab, float x, float y, float xI, float yI, bool tumble, bool useParachuteDelay, bool useParachute, bool onFire, bool isAlert)
        {
            if (!Main.enabled)
            {
                return;
            }

            Main.Log("spawn mook local called");

        }
    }

    [HarmonyPatch(typeof(MapController), "SpawnMook_Networked")]
    static class MapController_SpawnMook_NetworkedPatch
    {
        public static void Prefix(Mook mookPrefab, float x, float y, float xI, float yI, bool tumble, bool useParachuteDelay, bool useParachute, bool onFire, bool isAlert)
        {
            if (!Main.enabled)
            {
                return;
            }

            Main.Log("spawn mook networked called");

        }
    }

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

            return;

            //Main.Log("in start: " + __instance.mookType);

            // Avoid replacing enemies that have already been replaced
            if (__instance.playerNum == -1000)
            {
                //Main.Log("returning due to playernum: " + __instance.mookType);
                //__instance.playerNum = -1;
                return;
            }

            GameObject gameObject = null;

            if ((Main.settings.enemyPercent > rnd.Next(0, 100)))
            {
                int chosen = 0;
                float offset = 0f;

                if (!Main.settings.enableBosses)
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
                else if (chosen < Main.mookTypes.Length + Main.alienTypes.Length)
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
            }

        }
    }

    [HarmonyPatch(typeof(Map), "PlaceDoodad")]
    static class Map_PlaceDoodad
    {
        public static bool Prefix(Map __instance, ref DoodadInfo doodad, ref GameObject __result)
        {
            if (!Main.enabled)
            {
                return true;
            }

            //Main.Log("place doodad called: " + doodad.type);

            if (doodad.type == DoodadType.Mook || doodad.type == DoodadType.Alien || doodad.type == DoodadType.HellEnemy)
            {
                GridPos position = doodad.position;
                position.c -= Map.lastXLoadOffset;
                position.r -= Map.lastYLoadOffset;

                Vector3 vector = new Vector3((float)(position.c * 16), (float)(position.r * 16), 5f);

                TestVanDammeAnim original = null;

                int num = 6;

                if (Main.settings.DEBUG)
                {
                    num = Main.settings.debugMookType;
                }


                if (num < 20)
                {
                    switch (num)
                    {
                        // Mooks
                        case 0:
                            original = __instance.activeTheme.mook;
                            break;
                        case 1:
                            original = __instance.activeTheme.mookSuicide;
                            break;
                        case 2:
                            original = __instance.activeTheme.mookRiotShield;
                            break;
                        case 3:
                            original = __instance.activeTheme.mookBigGuy;
                            break;
                        case 4:
                            original = __instance.activeTheme.mookScout;
                            break;
                        case 5:
                            original = __instance.activeTheme.mookDog;
                            break;
                        case 6:
                            original = __instance.activeTheme.mookArmoured;
                            break;
                        case 7:
                            original = __instance.activeTheme.mookGrenadier;
                            break;
                        case 8:
                            original = __instance.activeTheme.mookBazooka;
                            break;
                        case 9:
                            original = __instance.activeTheme.mookNinja;
                            break;
                        case 10:
                            original = __instance.sharedObjectsReference.Asset.mookJetpack;
                            break;
                        case 11:
                            original = __instance.activeTheme.mookXenomorphBrainbox;
                            break;
                        case 12:
                            original = __instance.activeTheme.skinnedMook;
                            break;
                        case 13:
                            original = __instance.activeTheme.mookGeneral;
                            break;
                        // Satan
                        case 14:
                            original = __instance.activeTheme.satan;
                            break;
                        // Aliens
                        case 15:
                            original = __instance.activeTheme.alienFaceHugger;
                            break;
                        case 16:
                            original = __instance.activeTheme.alienXenomorph;
                            break;
                        case 17:
                            original = __instance.activeTheme.alienBrute;
                            break;
                        case 18:
                            original = __instance.activeTheme.alienBaneling;
                            break;
                        case 19:
                            original = __instance.activeTheme.alienMosquito;
                            break;
                        default:
                            original = original = __instance.activeTheme.mook;
                            break;
                    }

                    __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(original, vector, Quaternion.identity).gameObject;
                }
                // Hell Enemies
                else if (num < 29)
                {
                    num = num - 20;

                    switch ( num )
                    {
                        case 0:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[0], vector, Quaternion.identity);
                            break;
                        case 1:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[1], vector, Quaternion.identity);
                            break;
                        case 2:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[2], vector, Quaternion.identity);
                            break;
                        case 3:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[3], vector, Quaternion.identity);
                            break;
                        case 4:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[4], vector, Quaternion.identity);
                            break;
                        case 5:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[5], vector, Quaternion.identity);
                            break;
                        case 6:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[6], vector, Quaternion.identity);
                            break;
                        // Lost Soul
                        case 7:
                            vector.y += 5;
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[8], vector, Quaternion.identity);
                            break;
                        case 8:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[10], vector, Quaternion.identity);
                            break;
                        default:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[0], vector, Quaternion.identity);
                            break;
                    }
                    
                }
                else
                {
                    num = num - 29;

                    // Worms
                    if ( Main.settings.enableWorms )
                    {
                        if ( num < 3 )
                        {
                            switch (num)
                            {
                                // Sandworm
                                case 0:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[7], vector, Quaternion.identity);
                                    break;
                                // Boneworm
                                case 1:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[12], vector, Quaternion.identity);
                                    break;
                                // Boneworm Behind
                                case 2:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[13], vector, Quaternion.identity);
                                    break;

                            }
                        }
                    }
                    // Normal Size Bosses
                    if ( Main.settings.enableBosses )
                    {
                        num = num - (3 * (Main.settings.enableWorms ? 1 : 0));
                        if (num < 3)
                        {
                            switch (num)
                            {
                                case 0:
                                    SatanMiniboss satanMiniboss = UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.satanMiniboss, vector, Quaternion.identity) as SatanMiniboss;
                                    if (satanMiniboss != null)
                                    {
                                        __result = satanMiniboss.gameObject;
                                    }
                                    break;
                                case 1:
                                    __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(__instance.activeTheme.mookDolfLundgren, vector, Quaternion.identity).gameObject;
                                    break;
                                
                            }
                        }
                    }
                    // Large Bosses
                    if ( Main.settings.enableLargeBosses )
                    {
                        num = num - (2 * (Main.settings.enableBosses ? 1 : 0));
                        if ( num < 3 )
                        {
                            switch ( num )
                            {
                                case 0:
                                    __result = UnityEngine.Object.Instantiate<Unit>(__instance.activeTheme.mookMammothTank, vector, Quaternion.identity).gameObject;
                                    break;
                                case 1:
                                    __result = UnityEngine.Object.Instantiate<Unit>(__instance.activeTheme.mookKopterMiniBoss, vector, Quaternion.identity).gameObject;
                                    break;
                                case 2:
                                    __result = UnityEngine.Object.Instantiate<Unit>(__instance.activeTheme.goliathMech, vector, Quaternion.identity).gameObject;
                                    break;
                            }
                        }
                    }
                }


                if (__result != null)
                {
                    doodad.entity = __result;
                    __result.transform.parent = __instance.transform;
                    Block component = __result.GetComponent<Block>();
                    if (component != null)
                    {
                        component.OnSpawned();
                    }
                    Registry.RegisterDeterminsiticGameObject(__result.gameObject);
                    if (component != null)
                    {
                        component.FirstFrame();
                    }
                }

                return false;
            }

            return true;

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