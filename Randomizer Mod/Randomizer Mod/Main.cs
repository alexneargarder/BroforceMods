/**
 * TODO
 * 
 * Add new enemy types
 * 
 * Add an option to randomize the level order
 * 

 * 
 **/

/**
 * DONE
 * 
 * Make sure tooltips are readable
 * 
 * Add option to have dolflundgren not instantly win you the level when he dies
 * 
 * Add option to have satan spawn his death field once he dies
 * 
 * Replace prefab in spawn mook local with randomly selected one
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
        public static string debugMookStringSummoned = "0";

        public static string[] debugMookTypeList = new string[] { "mook",
            "mookSuicide",
            "mookRiotShield",
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
            "Alien Worm",
            "Alien Facehugger Worm",
            "Alien Facehugger Worm Behind",
            "Satan Miniboss",
            "mookDolfLundgren",
            "mookMammothTank",
            "mookKopterMiniBoss",
            "goliathMech",
            "Large Alien Worm"
        };

        public static bool ignoreNextWin = false;

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
            debugMookStringSummoned = settings.debugMookTypeSummoned.ToString();

            return true;
        }

        public static string[] wormList = new string[]
        {
            "SandWorm", "Boneworm", "Boneworm Behind", "Alien Worm", "Alien Facehugger Worm", "Alien Facehugger Worm Behind"
        };

        public static string[] bossList = new string[]
        {
            "Satan", "CR666"
        };

        public static string[] largeBossList = new string[]
        {
            "Stealth Tank", "Terrorkopter", "Terrorbot", "Large Alien Worm", 
        };

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.enableEnemyRandomization = GUILayout.Toggle(settings.enableEnemyRandomization, new GUIContent("Enable Enemy Randomization", "Randomly replaces enemies of one type with another"));

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 500;

            GUI.Label(lastRect, GUI.tooltip);
            string previousToolTip = GUI.tooltip;

            GUILayout.Space(20);

            settings.enableWorms = GUILayout.Toggle(settings.enableWorms, new GUIContent("Allow Enemies to become Worms", "Chance for enemies to become Sandworms or Boneworms"));

            

            if ( previousToolTip != GUI.tooltip)
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label(lastRect, GUI.tooltip);
            }

            previousToolTip = GUI.tooltip;


            GUILayout.Space(20);

            if ( GUILayout.Button("Worms" ) )
            {
                settings.showWorms = !settings.showWorms;
            }

            if (settings.showWorms)
            {
                GUILayout.BeginHorizontal();
                {
                    for ( int i = 0; i < wormList.Length; ++i )
                    {
                        bool containsBefore = settings.enabledWorms.Contains(i);

                        if ( containsBefore != GUILayout.Toggle(containsBefore, wormList[i] ) )
                        {
                            if ( containsBefore )
                            {
                                settings.enabledWorms.Remove(i);
                            }
                            else
                            {
                                settings.enabledWorms.Add(i);
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(25);

            settings.enableBosses = GUILayout.Toggle(settings.enableBosses, new GUIContent("Allow Enemies to become Bosses", "Chance for enemies to become certain minibosses"));

            if (previousToolTip != GUI.tooltip)
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label(lastRect, GUI.tooltip);
            }

            previousToolTip = GUI.tooltip;

            GUILayout.Space(20);

            if (GUILayout.Button("Bosses"))
            {
                settings.showBosses = !settings.showBosses;
            }

            if (settings.showBosses)
            {
                GUILayout.BeginHorizontal();
                {
                    for (int i = 0; i < bossList.Length; ++i)
                    {
                        bool containsBefore = settings.enabledBosses.Contains(i);

                        if (containsBefore != GUILayout.Toggle(containsBefore, bossList[i]))
                        {
                            if (containsBefore)
                            {
                                settings.enabledBosses.Remove(i);
                            }
                            else
                            {
                                settings.enabledBosses.Add(i);
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(25);

            settings.enableLargeBosses = GUILayout.Toggle(settings.enableLargeBosses, new GUIContent("Allow Enemies to become Large Bosses", "Chance for enemies to become certain large minibosses"));

            if (previousToolTip != GUI.tooltip)
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label(lastRect, GUI.tooltip);
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space(20);

            if (GUILayout.Button("Large Bosses"))
            {
                settings.showLargeBosses = !settings.showLargeBosses;
            }

            if (settings.showLargeBosses)
            {
                GUILayout.BeginHorizontal();
                {
                    for (int i = 0; i < largeBossList.Length; ++i)
                    {
                        bool containsBefore = settings.enabledLargeBosses.Contains(i);

                        if (containsBefore != GUILayout.Toggle(containsBefore, largeBossList[i]))
                        {
                            if (containsBefore)
                            {
                                settings.enabledLargeBosses.Remove(i);
                            }
                            else
                            {
                                settings.enabledLargeBosses.Add(i);
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(25);

            settings.enableInstantWin = GUILayout.Toggle(settings.enableInstantWin, new GUIContent("Killing Bosses triggers level finish", "If this is enabled, killing a boss will trigger a level end even if you're not on a boss level"));

            if (previousToolTip != GUI.tooltip)
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label(lastRect, GUI.tooltip);
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space(20);
            
            settings.enableDeathField = GUILayout.Toggle(settings.enableDeathField, new GUIContent("Enable Satan Death Field", "This causes randomly spawned satan bosses to spawn their death fields after they are fully killed"));
            if (previousToolTip != GUI.tooltip)
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label(lastRect, GUI.tooltip);
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space(20);

            settings.enableSpawnedEnemiesRandomization = GUILayout.Toggle(settings.enableSpawnedEnemiesRandomization, 
                new GUIContent("Enable Summoned Enemy Randomization", "This causes enemies summoned by parachute, doors, bosses, or other enemies to be randomized as well"));

            if (previousToolTip != GUI.tooltip)
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label(lastRect, GUI.tooltip);
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space(25);

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(new GUIContent("Randomized Enemy Percent: ", 
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type."), GUILayout.Width(200));

                GUILayout.Label(settings.enemyPercent.ToString("0.00"), GUILayout.Width(100));
           

                settings.enemyPercent = GUILayout.HorizontalSlider(settings.enemyPercent, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(new GUIContent("Normal Enemy Chance: ", 
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type."), GUILayout.Width(200));

                GUILayout.Label(settings.normalEnemyPercent.ToString("0.00"), GUILayout.Width(100));


                settings.normalEnemyPercent = GUILayout.HorizontalSlider(settings.normalEnemyPercent, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(new GUIContent("Worm Chance: ", 
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type."), GUILayout.Width(200));

                GUILayout.Label(settings.wormPercent.ToString("0.00"), GUILayout.Width(100));


                settings.wormPercent = GUILayout.HorizontalSlider(settings.wormPercent, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(new GUIContent("Boss Chance: ",
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type."), GUILayout.Width(200));

                GUILayout.Label(settings.bossPercent.ToString("0.00"), GUILayout.Width(100));


                settings.bossPercent = GUILayout.HorizontalSlider(settings.bossPercent, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(new GUIContent("Large Boss Chance: ",
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type."), GUILayout.Width(200));

                GUILayout.Label(settings.largeBossPercent.ToString("0.00"), GUILayout.Width(100));


                settings.largeBossPercent = GUILayout.HorizontalSlider(settings.largeBossPercent, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (previousToolTip != GUI.tooltip)
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 900;

                GUI.Label(lastRect, GUI.tooltip);
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space(50);



            //settings.DEBUG = GUILayout.Toggle(settings.DEBUG, "DEBUG");

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


                GUILayout.Label("Current Enemy Type: " + debugMookTypeList[settings.debugMookType]);

                GUILayout.Space(15);

                debugMookStringSummoned = GUILayout.TextField(debugMookStringSummoned);

                if (int.TryParse(debugMookStringSummoned, out temp))
                    settings.debugMookTypeSummoned = temp;

                if (settings.debugMookTypeSummoned > (mookTypes.Length + alienTypes.Length + mookBossTypes.Length) - 1)
                    settings.debugMookTypeSummoned = (mookTypes.Length + alienTypes.Length + mookBossTypes.Length) - 1;

                if (settings.debugMookTypeSummoned < 0)
                    settings.debugMookTypeSummoned = 0;

                int parsednum = settings.debugMookTypeSummoned;
                string parsedstring;

                if (parsednum >= mookTypes.Length)
                {
                    parsednum -= mookTypes.Length;

                    if (parsednum >= alienTypes.Length)
                    {
                        parsednum -= alienTypes.Length;

                        parsedstring = mookBossTypes[parsednum];
                    }
                    else
                    {
                        parsedstring = alienTypes[parsednum];
                    }    
                }
                else
                {
                    parsedstring = mookTypes[parsednum];
                }
                
                GUILayout.Label("Current Summoned Enemy Type: " + parsedstring);

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

        public bool enableWorms;
        public bool enableBosses;
        public bool enableLargeBosses;

        public bool showWorms;
        public bool showBosses;
        public bool showLargeBosses;

        public List<int> enabledWorms;
        public List<int> enabledBosses;
        public List<int> enabledLargeBosses;

        public bool enableInstantWin;
        public bool enableDeathField;
        public bool enableSpawnedEnemiesRandomization;

        public float enemyPercent;

        public float normalEnemyPercent;
        public float wormPercent;
        public float bossPercent;
        public float largeBossPercent;

        public bool DEBUG;
        public int debugMookType;
        public int debugMookTypeSummoned;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }

    [HarmonyPatch(typeof(MapController), "SpawnMook_Networked")]
    static class MapController_SpawnMook_Networked
    {
        public static System.Random rnd = new System.Random();
        public static Mook getRandomMookPrefab()
        {
            int chosen;

            GameObject gameObject = null;

            int enemyType = rnd.Next(0, (int)(Main.settings.normalEnemyPercent + (Main.settings.enableBosses ? Main.settings.bossPercent : 0)));

            if (Main.settings.DEBUG)
                enemyType = (Main.settings.debugMookTypeSummoned < Main.mookTypes.Length + Main.alienTypes.Length) ? 0 : (int)(Main.settings.normalEnemyPercent + 1);

            if (enemyType < Main.settings.normalEnemyPercent )
            {
                chosen = rnd.Next(0, Main.mookTypes.Length + Main.alienTypes.Length);

                if (Main.settings.DEBUG)
                    chosen = Main.settings.debugMookTypeSummoned;

                if (chosen < Main.mookTypes.Length)
                {

                    gameObject = InstantiationController.GetPrefabFromLegacyResourceName("Mooks/" + Main.mookTypes[chosen]);

                }
                else if (chosen < Main.mookTypes.Length + Main.alienTypes.Length)
                {
                    chosen = chosen - Main.mookTypes.Length;

                    gameObject = InstantiationController.GetPrefabFromLegacyResourceName("Aliens/" + Main.alienTypes[chosen]);
                }
            }
            else
            {
                chosen = rnd.Next(0, Main.mookBossTypes.Length);

                if (Main.settings.DEBUG)
                    chosen = Main.settings.debugMookTypeSummoned - Main.mookTypes.Length - Main.alienTypes.Length;

                gameObject = InstantiationController.GetPrefabFromLegacyResourceName("Mooks/" + Main.mookBossTypes[chosen]);
            }

            Mook mookPrefab = gameObject.GetComponent<Mook>();

            return mookPrefab;
        }

        public static void Prefix(ref Mook mookPrefab, float x, float y, float xI, float yI, bool tumble, bool useParachuteDelay, bool useParachute, bool onFire, bool isAlert)
        {
            if (!Main.enabled || !Main.settings.enableSpawnedEnemiesRandomization )
            {
                return;
            }

            if ((Main.settings.enemyPercent > rnd.Next(0, 100)))
            {
                mookPrefab = getRandomMookPrefab();
            }

        }
    }

    [HarmonyPatch(typeof(MapController), "SpawnMook_Local")]
    static class MapController_SpawnMook_Local
    {
        public static void Prefix(ref Mook mookPrefab, float x, float y, float xI, float yI, bool tumble, bool useParachuteDelay, bool useParachute, bool onFire, bool isAlert)
        {
            if (!Main.enabled || !Main.settings.enableSpawnedEnemiesRandomization)
            {
                return;
            }

            if ((Main.settings.enemyPercent > MapController_SpawnMook_Networked.rnd.Next(0, 100)))
            {
                mookPrefab = MapController_SpawnMook_Networked.getRandomMookPrefab();
            }

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


            // Avoid replacing enemies that have already been replaced
            if (__instance.playerNum == -1000)
            {
                return;
            }

            

        }
    }

    [HarmonyPatch(typeof(Map), "PlaceDoodad")]
    static class Map_PlaceDoodad
    {
        static System.Random rnd = new System.Random();
        const int NUMWORMS = 5;
        const int NUMBOSSES = 2;
        const int NUMLARGEBOSSES = 4;

        public static bool Prefix(Map __instance, ref DoodadInfo doodad, ref GameObject __result)
        {
            if (!Main.enabled || !Main.settings.enableEnemyRandomization)
            {
                return true;
            }

            //Main.Log("place doodad called: " + doodad.type);

            if (doodad.type == DoodadType.Mook || doodad.type == DoodadType.Alien || doodad.type == DoodadType.HellEnemy)
            {
                int enemyConvertChance = rnd.Next(0, 100);

                if ( enemyConvertChance > Main.settings.enemyPercent )
                {
                    return true;
                }

                GridPoint position = doodad.position;
                position.c -= Map.lastXLoadOffset;
                position.r -= Map.lastYLoadOffset;

                Vector3 vector = new Vector3((float)(position.c * 16), (float)(position.r * 16), 5f);

                TestVanDammeAnim original = null;

                int enemyType = rnd.Next(0, (int)( Main.settings.normalEnemyPercent + ( Main.settings.enableWorms ? Main.settings.wormPercent : 0 ) + 
                    ( Main.settings.enableBosses ? Main.settings.bossPercent : 0 ) + ( Main.settings.enableLargeBosses ? Main.settings.largeBossPercent : 0 ) ) );

                if ( !Main.settings.DEBUG )
                {
                    if (enemyType < Main.settings.normalEnemyPercent)
                    {
                        int num = rnd.Next(0, 29);

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
                            }

                            __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(original, vector, Quaternion.identity).gameObject;
                        }
                        // Hell Enemies
                        else if (num < 29)
                        {
                            num = num - 20;

                            switch (num)
                            {
                                // HellDog
                                case 0:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[0], vector, Quaternion.identity);
                                    break;
                                // ZMookUndead
                                case 1:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[1], vector, Quaternion.identity);
                                    break;
                                // ZMookUndeadStartDead
                                case 2:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[2], vector, Quaternion.identity);
                                    break;
                                // ZMookWarlock
                                case 3:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[3], vector, Quaternion.identity);
                                    break;
                                // ZMookHellBoomer
                                case 4:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[4], vector, Quaternion.identity);
                                    break;
                                // ZMookUndeadSuicide
                                case 5:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[5], vector, Quaternion.identity);
                                    break;
                                // ZHellBigGuy
                                case 6:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[6], vector, Quaternion.identity);
                                    break;
                                // Lost Soul
                                case 7:
                                    vector.y += 5;
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[8], vector, Quaternion.identity);
                                    break;
                                // ZMookHellSoulCatcher
                                case 8:
                                    __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[10], vector, Quaternion.identity);
                                    break;
                            }

                        }

                    }
                    // Worms
                    else if (Main.settings.enableWorms && (enemyType - Main.settings.normalEnemyPercent) < Main.settings.wormPercent && Main.settings.enabledWorms.Count > 0)
                    {
                        int num = rnd.Next(0, Main.settings.enabledWorms.Count);
                        num = Main.settings.enabledWorms[num];

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
                            // Alien Worm
                            case 3:
                                __result = (UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.alienMinibossSandWorm, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                                break;
                            // Alien Facehugger Worm
                            case 4:
                                __result = (UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitter, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                                break;
                            // Alien Facehugger Worm Behind
                            case 5:
                                __result = (UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitterBehind, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                                break;
                        }
                    }
                    // Bosses
                    else if (Main.settings.enableBosses && (enemyType - Main.settings.normalEnemyPercent - (Main.settings.enableWorms ? Main.settings.wormPercent : 0)) < Main.settings.bossPercent && Main.settings.enabledBosses.Count > 0)
                    {
                        int num = rnd.Next(0, Main.settings.enabledBosses.Count);
                        num = Main.settings.enabledBosses[num];


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
                    // Large Bosses
                    else if (Main.settings.enableLargeBosses && (enemyType - Main.settings.normalEnemyPercent - (Main.settings.enableWorms ? Main.settings.wormPercent : 0) -
                        (Main.settings.enableBosses ? Main.settings.bossPercent : 0)) < Main.settings.largeBossPercent && Main.settings.enabledLargeBosses.Count > 0)
                    {
                        int num = rnd.Next(0, Main.settings.enabledLargeBosses.Count);
                        num = Main.settings.enabledLargeBosses[num];

                        switch (num)
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
                            // Large Alien Worm
                            case 3:
                                __result = (UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.alienGiantSandWormBoss, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                                break;
                        }
                    }
                }
                else
                {
                    switch ( Main.settings.debugMookType )
                    {
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
                        // HellDog
                        case 20:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[0], vector, Quaternion.identity);
                            break;
                        // ZMookUndead
                        case 21:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[1], vector, Quaternion.identity);
                            break;
                        // ZMookUndeadStartDead
                        case 22:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[2], vector, Quaternion.identity);
                            break;
                        // ZMookWarlock
                        case 23:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[3], vector, Quaternion.identity);
                            break;
                        // ZMookHellBoomer
                        case 24:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[4], vector, Quaternion.identity);
                            break;
                        // ZMookUndeadSuicide
                        case 25:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[5], vector, Quaternion.identity);
                            break;
                        // ZHellBigGuy
                        case 26:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[6], vector, Quaternion.identity);
                            break;
                        // Lost Soul
                        case 27:
                            vector.y += 5;
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[8], vector, Quaternion.identity);
                            break;
                        // ZMookHellSoulCatcher
                        case 28:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[10], vector, Quaternion.identity);
                            break;
                        // Sandworm
                        case 29:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[7], vector, Quaternion.identity);
                            break;
                        // Boneworm
                        case 30:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[12], vector, Quaternion.identity);
                            break;
                        // Boneworm Behind
                        case 31:
                            __result = UnityEngine.Object.Instantiate<GameObject>(__instance.sharedObjectsReference.Asset.hellEnemies[13], vector, Quaternion.identity);
                            break;
                        // Alien Worm
                        case 32:
                            __result = (UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.alienMinibossSandWorm, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                            break;
                        // Alien Facehugger Worm
                        case 33:
                            __result = (UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitter, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                            break;
                        // Alien Facehugger Worm Behind
                        case 34:
                            __result = (UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitterBehind, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                            break;
                        case 35:
                            SatanMiniboss satanMiniboss = UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.satanMiniboss, vector, Quaternion.identity) as SatanMiniboss;
                            if (satanMiniboss != null)
                            {
                                __result = satanMiniboss.gameObject;
                            }
                            break;
                        case 36:
                            __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(__instance.activeTheme.mookDolfLundgren, vector, Quaternion.identity).gameObject;
                            break;
                        case 37:
                            __result = UnityEngine.Object.Instantiate<Unit>(__instance.activeTheme.mookMammothTank, vector, Quaternion.identity).gameObject;
                            break;
                        case 38:
                            __result = UnityEngine.Object.Instantiate<Unit>(__instance.activeTheme.mookKopterMiniBoss, vector, Quaternion.identity).gameObject;
                            break;
                        case 39:
                            __result = UnityEngine.Object.Instantiate<Unit>(__instance.activeTheme.goliathMech, vector, Quaternion.identity).gameObject;
                            break;
                        // Large Alien Worm
                        case 40:
                            __result = (UnityEngine.Object.Instantiate<Unit>(__instance.sharedObjectsReference.Asset.alienGiantSandWormBoss, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                            break;
                    }

                    if (original != null)
                    {
                        __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(original, vector, Quaternion.identity).gameObject;
                    }
                }


                /*Main.Log("\n\n");
                Component[] allComponents;
                allComponents = __result.GetComponents(typeof(Component));
                foreach (Component comp in allComponents)
                {
                    Main.Log("attached: " + comp.name + " also " + comp.GetType());
                }
                Main.Log("\n\n");*/


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


    // Fixes camera locking onto satan after he dies
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

    // Makes satan death field spawn when he dies
    [HarmonyPatch(typeof(SatanMiniboss), "AnimateDeath")]
    static class SatanMiniboss_AnimateDeath
    {
        public static void Prefix(SatanMiniboss __instance)
        {
            if (!Main.enabled || !Main.settings.enableDeathField)
            {
                return;
            }

            __instance.primedToSelfDestruct = true;
        }
    }


    // Allows disabling Dolph Lundgren's instant win
    [HarmonyPatch(typeof(DolphLundrenSoldier), "Gib")]
    static class DolphLundrenSoldier_Gib
    {
        public static void Prefix()
        {
            if (!Main.enabled || Main.settings.enableInstantWin || ( LevelSelectionController.currentCampaign.name == "WM_Village1(mouse)" && LevelSelectionController.CurrentLevelNum == 3 ) )
            {
                return;
            }

            Main.ignoreNextWin = true;
        }
    }

    // Allows disabling Terrorkopter and MammothTank instant wins
    [HarmonyPatch(typeof(MinibossEndCheck), "Update")]
    static class MinibossEndCheck_Update
    {
        public static void Prefix(MinibossEndCheck __instance)
        {
            if (!Main.enabled || Main.settings.enableInstantWin)
            {
                return;
            }

            if (LevelSelectionController.currentCampaign.name == "WM_Mission1(mouse)" && LevelSelectionController.CurrentLevelNum == 3)
                return;

            if (LevelSelectionController.currentCampaign.name == "WM_Bombardment(mouse)" && LevelSelectionController.CurrentLevelNum == 2)
                return;
           

            Traverse trav = Traverse.Create(__instance);

            Unit miniBossUnit = trav.Field("miniBossUnit").GetValue<Unit>();

            if ( miniBossUnit.health <= 0 )
            {
                float explosionDeathCount = trav.Field("explosionDeathCount").GetValue<float>();

                if ( explosionDeathCount > 0 )
                {
                    float explosionDeathCounter = trav.Field("finishedCounter").GetValue<float>();

                    if (explosionDeathCounter + Time.deltaTime > 0.33f)
                    {

                        if ((explosionDeathCount - 1) <= 0)
                        {
                            Main.ignoreNextWin = true;
                        }
                    }
                }
                
            }

        }
    }

    // Allows disabling GoliathMech instant win
    [HarmonyPatch(typeof(GoliathMech), "Death")]
    static class GoliathMech_Death
    {
        public static void Prefix()
        {
            if (!Main.enabled || Main.settings.enableInstantWin || (LevelSelectionController.currentCampaign.name == "WM_KazakhstanRainy(mouse)" && LevelSelectionController.CurrentLevelNum == 3))
            {
                return;
            }

            Main.ignoreNextWin = true;
        }

        public static void Postfix(GoliathMech __instance)
        {
            if (!Main.enabled || Main.settings.enableInstantWin || (LevelSelectionController.currentCampaign.name == "WM_KazakhstanRainy(mouse)" && LevelSelectionController.CurrentLevelNum == 3))
            {
                return;
            }

            __instance.actionState = ActionState.Idle;
        }
    }

    // Checks if we need to skip this win because it was from a miniboss death
    [HarmonyPatch(typeof(GameModeController), "LevelFinish")]
    static class GameModeController_LevelFinish
    {
        public static bool Prefix()
        {
            if (!Main.enabled || !Main.ignoreNextWin )
            {
                return true;
            }

            Main.ignoreNextWin = false;

            return false;
        }
    }




}