/**
 * TODO
 * 
 * Add tooltips for cheat options
 * 
 **/
#define DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using UnityEngine.SceneManagement;


namespace Utility_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        public static Dropdown campaignNum;
        public static int lastCampaignNum;
        public static Dropdown levelNum;
        public static int lastCampaignCompleted = -1;
        public static string[] levelList = new string[] { "Level 1", "Level 2", "Level 3", "Level 4", "Level 5", "Level 6", "Level 7", "Level 8", "Level 9", "Level 10",
            "Level 11", "Level 12", "Level 13", "Level 14", "Level 15"};
        public static string[] terrainList = new string[]
        {
            "vietnamterrain",
            "cambodiaterrain",
            "indonesiaterrain",
            "hongkongterrain",
            "indiaterrain",
            "philippinesterrain",
            "southkoreaterrain",
            "newguineaterrain",
            "kazakhstanterrain",
            "ukraineterrain",
            "panamaterrain",
            "congoterrain",
            "hawaiiterrain",
            "amazonterrain",
            "hellterrain",
            "usaterrain",
            "ellenchallengeterrain",
            "bombardchallengeterrain",
            "ammochallengeterrain",
            "dashchallengeterrain",
            "mechchallengeterrain",
            "macbroverchallengeterrain",
            "timebrochallengeterrain",
            "chinaeastterrainmuscle",
            "australiaterrainmuscle",
            "afghanistanterrainmuscle",
            "chileterrainmuscle",
            "chinawestterrain"
        };

        public static string[] campaignList = new string[]
        {
            "VIETMAN",
            "CAMBODIA",
            "INDONESIA",
            "HONG KONG",
            "INDIA",
            "PHILIPPINES",
            "SOUTH KOREA",
            "NEW GUINEA",
            "KAZAKHSTAN",
            "UKRAINE",
            "PANAMA",
            "DEM. REP. OF CONGO",
            "HAWAII",
            "THE AMAZON RAINFOREST",
            "UNITED STATES OF AMERICA",
            "WHITE HOUSE",
            "Alien Challenge",
            "Bombardment Challenge",
            "Ammo Challenge",
            "Dash Challenge",
            "Mech Challenge",
            "MacBrover Challenge",
            "Time Bro Challenge",
            "MUSCLETEMPLE1",
            "MUSCLETEMPLE2",
            "MUSCLETEMPLE3",
            "MUSCLETEMPLE4",
            "MUSCLETEMPLE5"
        };

        public static string teleportX = "0";
        public static string teleportY = "0";

        public static bool loadedScene = false;
        public static float waitForLoad = 4.0f;

        public static bool skipNextMenu = false;

        public static float levelStartedCounter = 0f;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUpdate = OnUpdate;
            settings = Settings.Load<Settings>(modEntry);
            mod = modEntry;

            try
            {
                var harmony = new Harmony(modEntry.Info.Id);
                var assembly = Assembly.GetExecutingAssembly();
                harmony.PatchAll(assembly);
            }
            catch(Exception ex)
            {
                Main.Log(ex.ToString());
            }

            lastCampaignNum = -1;
            campaignNum = new Dropdown(125, 150, 200, 300, new string[] { "Campaign 1", "Campaign 2", "Campaign 3", "Campaign 4", "Campaign 5",
                "Campaign 6", "Campaign 7", "Campaign 8", "Campaign 9", "Campaign 10", "Campaign 11", "Campaign 12", "Campaign 13", "Campaign 14", "Campaign 15",
                "White House", "Alien Challenge", "Bombardment Challenge", "Ammo Challenge", "Dash Challenge", "Mech Challenge", "MacBrover Challenge", "Time Bro Challenge",
                "Muscle Temple 1", "Muscle Temple 2", "Muscle Temple 3", "Muscle Temple 4", "Muscle Temple 5" }, settings.campaignNum);


            levelNum = new Dropdown(400, 150, 150, 300, levelList, settings.levelNum);

#if DEBUG
            unitDropdown = new Dropdown(400, 150, 200, 300, unitList, settings.selectedEnemy);
#endif

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            string previousToolTip;

            
            
            GUILayout.BeginHorizontal();
            {
                settings.cameraShake = GUILayout.Toggle(settings.cameraShake, new GUIContent("Camera Shake",
                    "Disable this to have camera shake automatically set to 0 at the start of a level"), GUILayout.Width(100f));

                Rect lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                settings.enableSkip = GUILayout.Toggle(settings.enableSkip, new GUIContent("Helicopter Skip",
                    "Skips helicopter on world map and immediately takes you into a level"), GUILayout.Width(120f));

                GUI.Label(lastRect, GUI.tooltip);

                settings.endingSkip = GUILayout.Toggle(settings.endingSkip, new GUIContent("Ending Skip",
                    "Speeds up the ending"), GUILayout.Width(100f));

                GUI.Label(lastRect, GUI.tooltip);

                settings.quickMainMenu = GUILayout.Toggle(settings.quickMainMenu, "Speed up Main Menu Loading", GUILayout.Width(200f));

                settings.disableConfirm = GUILayout.Toggle(settings.disableConfirm, new GUIContent("Fix Mod Window Disappearing",
                    "Disables confirmation screen when restarting or returning to map/menu"), GUILayout.ExpandWidth(false));

                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();


            GUILayout.Space(25);

            GUIStyle headerStyle = new GUIStyle( GUI.skin.button );


            headerStyle.fontStyle = FontStyle.Bold;
            // 163, 232, 255
            headerStyle.normal.textColor = new Color(0.639216f, 0.909804f, 1f);


            if ( GUILayout.Button("Level Controls", headerStyle) )
            {
                settings.showLevelOptions = !settings.showLevelOptions;
            }

            if ( settings.showLevelOptions )
            {
                ShowLevelControls(modEntry, ref previousToolTip);
            } // End Level Controls


            TestVanDammeAnim currentCharacter = null;

            if (settings.quickLoadScene)
            {
                if ( loadedScene && HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null)
                {
                    currentCharacter = HeroController.players[0].character;
                }
            }
            else if (HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null)
            {
                currentCharacter = HeroController.players[0].character;
            }


            if (GUILayout.Button("Cheat Options", headerStyle))
            {
                settings.showCheatOptions = !settings.showCheatOptions;
            }

            if ( settings.showCheatOptions )
            {
                ShowCheatOptions(modEntry, ref previousToolTip, currentCharacter);
            } // End Cheat Options



            if (GUILayout.Button("Teleport Options", headerStyle))
            {
                settings.showTeleportOptions = !settings.showTeleportOptions;
            }

            if ( settings.showTeleportOptions )
            {
                ShowTeleportOptions(modEntry, ref previousToolTip, currentCharacter);   
            } // End Teleport Options

#if DEBUG
            if (GUILayout.Button("Debug Options", headerStyle))
            {
                settings.showDebugOptions = !settings.showDebugOptions;
            }

            if ( settings.showDebugOptions )
            {
                ShowDebugOptions(modEntry, ref previousToolTip, currentCharacter);
            }
#endif
        }

        static void ShowLevelControls(UnityModManager.ModEntry modEntry, ref string previousToolTip)
        {
            GUILayout.BeginHorizontal();
            {
                settings.loopCurrent = GUILayout.Toggle(settings.loopCurrent, new GUIContent("Loop Current Level", "After beating a level you replay the current one instead of moving on"), GUILayout.ExpandWidth(false));

                Rect lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 300;

                if (GUI.tooltip != previousToolTip)
                {
                    GUI.Label(lastRect, GUI.tooltip);
                    previousToolTip = GUI.tooltip;
                }

                GUILayout.Space(20);

                if (GUILayout.Button(new GUIContent("Unlock All Levels", "Only works on the world map screen"), GUILayout.ExpandWidth(false)))
                {
                    if (HarmonyPatches.WorldMapController_Update_Patch.instance != null)
                    {
                        WorldTerritory3D[] territories = Traverse.Create(HarmonyPatches.WorldMapController_Update_Patch.instance).Field("territories3D").GetValue() as WorldTerritory3D[];
                        foreach (WorldTerritory3D ter in territories)
                        {
                            if (ter.properties.state != TerritoryState.Liberated && ter.properties.state != TerritoryState.AlienLiberated)
                            {
                                UnlockTerritory(ter);
                            }
                        }
                    }
                }

            }
            GUILayout.EndHorizontal();


            GUILayout.Space(25);


            GUILayout.BeginHorizontal(new GUILayoutOption[] { GUILayout.MinHeight((campaignNum.show || levelNum.show) ? 350 : 100), GUILayout.ExpandWidth(false) });
            {
                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    {
                        campaignNum.OnGUI(modEntry);

                        determineLevelsInCampaign();

                        levelNum.OnGUI(modEntry);

                        Main.settings.levelNum = levelNum.indexNumber;

                        if (GUILayout.Button(new GUIContent("Go to level", "This only works on the world map screen"), GUILayout.Width(100)))
                        {
                            GoToLevel();
                        }

                        if (GUI.tooltip != previousToolTip)
                        {
                            Rect lastRect = campaignNum.dropDownRect;
                            lastRect.y += 20;
                            lastRect.width += 300;

                            GUI.Label(lastRect, GUI.tooltip);
                            previousToolTip = GUI.tooltip;
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(25);

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(1);

                        if (!campaignNum.show && !levelNum.show)
                        {

                            if (GUILayout.Button(new GUIContent("Previous Level", "This only works in game"), new GUILayoutOption[] { GUILayout.Width(150), GUILayout.ExpandWidth(false) }))
                            {
                                ChangeLevel(-1);
                            }

                            Rect lastRect = GUILayoutUtility.GetLastRect();
                            lastRect.y += 20;
                            lastRect.width += 300;

                            if (GUILayout.Button(new GUIContent("Next Level", "This only works in game"), new GUILayoutOption[] { GUILayout.Width(150), GUILayout.ExpandWidth(false) }))
                            {
                                ChangeLevel(1);
                            }

                            if (GUI.tooltip != previousToolTip)
                            {
                                GUI.Label(lastRect, GUI.tooltip);
                                previousToolTip = GUI.tooltip;
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }

        static void ShowCheatOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip, TestVanDammeAnim currentCharacter)
        {
            GUILayout.BeginHorizontal();
            {
                if (settings.invulnerable != (settings.invulnerable = GUILayout.Toggle(settings.invulnerable, "Invincibility")))
                {
                    if (settings.invulnerable && currentCharacter != null)
                    {
                        currentCharacter.SetInvulnerable(float.MaxValue, false);
                    }
                    else if (currentCharacter != null)
                    {
                        currentCharacter.SetInvulnerable(0, false);
                    }
                }

                GUILayout.Space(15);

                if (settings.infiniteLives != (settings.infiniteLives = GUILayout.Toggle(settings.infiniteLives, "Infinite Lives")))
                {
                    if (currentCharacter != null)
                    {
                        if (settings.infiniteLives)
                        {
                            for (int i = 0; i < 4; ++i)
                            {
                                HeroController.SetLives(i, int.MaxValue);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < 4; ++i)
                            {
                                HeroController.SetLives(i, 1);
                            }
                        }
                    }
                }

                GUILayout.Space(15);

                settings.infiniteSpecials = GUILayout.Toggle(settings.infiniteSpecials, "Infinite Specials");

                GUILayout.Space(15);

                settings.disableGravity = GUILayout.Toggle(settings.disableGravity, "Disable Gravity");

                GUILayout.Space(15);

                settings.enableFlight = GUILayout.Toggle(settings.enableFlight, "Enable Flight");

                GUILayout.Space(15);

                settings.disableEnemySpawn = GUILayout.Toggle(settings.disableEnemySpawn, "Disable Enemy Spawns");

                GUILayout.Space(10);

                settings.oneHitEnemies = GUILayout.Toggle(settings.oneHitEnemies, new GUIContent("Instant Kill Enemies", "Sets all enemies to one health"));
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            if (GUILayout.Button("Summon Mech", GUILayout.Width(140)))
            {
                if (currentCharacter != null)
                {
                    ProjectileController.SpawnGrenadeOverNetwork(ProjectileController.GetMechDropGrenadePrefab(), currentCharacter, currentCharacter.X + Mathf.Sign(currentCharacter.transform.localScale.x) * 8f, currentCharacter.Y + 8f, 0.001f, 0.011f, Mathf.Sign(currentCharacter.transform.localScale.x) * 200f, 150f, currentCharacter.playerNum);
                }
            }

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 300;
            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(25);

            GUILayout.BeginHorizontal(GUILayout.Width(400));

            GUILayout.Label("Time Slow Factor: " + settings.timeSlowFactor);

            if (settings.timeSlowFactor != (settings.timeSlowFactor = GUILayout.HorizontalSlider(settings.timeSlowFactor, 0, 5, GUILayout.Width(200))))
            {
                Main.StartTimeSlow();
            }

            GUILayout.EndHorizontal();

            if (settings.slowTime != (settings.slowTime = GUILayout.Toggle(settings.slowTime, "Slow Time")))
            {
                if (settings.slowTime)
                {
                    StartTimeSlow();
                }
                else
                {
                    StopTimeSlow();
                }
            }

            GUILayout.Space(25);

            GUILayout.BeginHorizontal(GUILayout.Width(500));

            GUILayout.Label("Scene to Load: ");

            settings.sceneToLoad = GUILayout.TextField(settings.sceneToLoad, GUILayout.Width(200));

            GUILayout.EndHorizontal();

            settings.quickLoadScene = GUILayout.Toggle(settings.quickLoadScene, "Immediately load chosen scene", GUILayout.Width(200));

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Load Current Scene", GUILayout.Width(200)))
            {
                if (!Main.settings.cameraShake)
                {
                    PlayerOptions.Instance.cameraShakeAmount = 0f;
                }

                Utility.SceneLoader.LoadScene(settings.sceneToLoad);
            }

            if (GUILayout.Button("Get Current Scene", GUILayout.Width(200)))
            {
                for (int i = 0; i < SceneManager.sceneCount; ++i)
                {
                    Main.Log("Scene Name: " + SceneManager.GetSceneAt(i).name);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(25);
        }

        static void ShowTeleportOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip, TestVanDammeAnim currentCharacter)
        {
            GUILayout.BeginHorizontal();
            {
                if (currentCharacter != null)
                {
                    GUILayout.Label("Position: " + currentCharacter.X.ToString("0.00") + ", " + currentCharacter.Y.ToString("0.00"));
                }
                else
                {
                    GUILayout.Label("Position: ");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("X", GUILayout.Width(10));
                teleportX = GUILayout.TextField(teleportX, GUILayout.Width(100));
                GUILayout.Space(20);
                GUILayout.Label("Y", GUILayout.Width(10));
                GUILayout.Space(10);
                teleportY = GUILayout.TextField(teleportY, GUILayout.Width(100));

                if (GUILayout.Button("Teleport", GUILayout.Width(100)))
                {
                    float x, y;
                    if (float.TryParse(teleportX, out x) && float.TryParse(teleportY, out y))
                    {
                        if (currentCharacter != null)
                        {
                            currentCharacter.X = x;
                            currentCharacter.Y = y;
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.BeginHorizontal(GUILayout.Width(400));

            if ( settings.teleportToMouseCursor != (settings.teleportToMouseCursor = GUILayout.Toggle(settings.teleportToMouseCursor, "Teleport to Cursor on Right Click")) )
            {
#if DEBUG
                if ( settings.teleportToMouseCursor )
                {
                    settings.spawnEnemyOnRightClick = false;
                }
#endif
            }

            GUILayout.Space(10);

            settings.changeSpawn = GUILayout.Toggle(settings.changeSpawn, "Spawn at Custom Waypoint");

            GUILayout.Space(10);

            settings.changeSpawnFinal = GUILayout.Toggle(settings.changeSpawnFinal, "Spawn at Final Checkpoint");

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Save Position for Custom Spawn", GUILayout.Width(250)))
                {
                    if (currentCharacter != null)
                    {
                        settings.SpawnPositionX = currentCharacter.X;
                        settings.SpawnPositionY = currentCharacter.Y;
                    }
                }

                if (GUILayout.Button("Teleport to Custom Spawn Position", GUILayout.Width(300)))
                {
                    if (currentCharacter != null)
                    {
                        currentCharacter.X = settings.SpawnPositionX;
                        currentCharacter.Y = settings.SpawnPositionY;
                    }
                }

                GUILayout.Label("Saved position: " + settings.SpawnPositionX + ", " + settings.SpawnPositionY);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Teleport to Current Checkpoint ", GUILayout.Width(250)))
                {
                    if (currentCharacter != null)
                    {
                        Vector3 checkPoint = HeroController.GetCheckPointPosition(0, Map.IsCheckPointAnAirdrop(HeroController.GetCurrentCheckPointID()));
                        currentCharacter.X = checkPoint.x;
                        currentCharacter.Y = checkPoint.y;
                    }
                }

                if (GUILayout.Button("Teleport to Final Checkpoint", GUILayout.Width(200)))
                {
                    if (currentCharacter != null)
                    {
                        Vector3 checkPoint = GetFinalCheckpointPos();
                        currentCharacter.X = checkPoint.x;
                        currentCharacter.Y = checkPoint.y;
                    }
                }
            }
            GUILayout.EndHorizontal();

            for (int i = 0; i < settings.waypointsX.Length; ++i)
            {
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Save Position to Waypoint " + (i + 1), GUILayout.Width(250)))
                    {
                        if (currentCharacter != null)
                        {
                            settings.waypointsX[i] = currentCharacter.X;
                            settings.waypointsY[i] = currentCharacter.Y;
                        }
                    }

                    if (GUILayout.Button("Teleport to Waypoint " + (i + 1), GUILayout.Width(200)))
                    {
                        if (currentCharacter != null)
                        {
                            currentCharacter.X = settings.waypointsX[i];
                            currentCharacter.Y = settings.waypointsY[i];
                        }
                    }

                    GUILayout.Label("Saved position: " + settings.waypointsX[i] + ", " + settings.waypointsY[i]);
                }
                GUILayout.EndHorizontal();
            }
        }

        static Dropdown unitDropdown;

        #region DEBUG
        public static string[] unitList = new string[]
        {
            // Normal
            "Mook", "Suicide Mook", "Bruiser", "Suicide Bruiser", "Strong Bruiser", "Elite Bruiser", "Scout Mook", "Riot Shield Mook", "Mech", "Brown Mech", "Jetpack Mook", "Grenadier Mook", "Bazooka Mook", "Jetpack Bazooka Mook", "Ninja Mook",
            "Treasure Mook", "Attack Dog", "Skinned Mook", "Mook General", "Alarmist", "Strong Mook", "Scientist Mook", "Snake", "Satan", 
            // Aliens
            "Facehugger", "Xenomorph", "Brute", "Screecher", "Baneling", "Xenomorph Brainbox",
            // Hell
            "Hellhound", "Undead Mook", "Undead Mook (Start Dead)", "Warlock", "Boomer", "Undead Suicide Mook", "Executioner", "Lost Soul", "Soul Catcher",
            "SandWorm", "Boneworm", "Boneworm Behind", "Alien Worm", "Alien Facehugger Worm", "Alien Facehugger Worm Behind",
            "Satan", "CR666",
            "Stealth Tank", "Terrorkopter", "Terrorbot", "Large Alien Worm",
            "Mook Launcher Tank", "Cannon Tank", "Rocket Tank", "Artillery Truck", "Blimp", "Drill carrier", "Mook Truck", "Turret", "Motorbike", "Motorbike Nuclear", "Dump Truck"
        };

        static void ShowDebugOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip, TestVanDammeAnim currentCharacter)
        {
            GUILayout.BeginHorizontal();

            settings.printAudioPlayed = GUILayout.Toggle(settings.printAudioPlayed, "Print Audio Played");

            GUILayout.EndHorizontal();


            GUILayout.Space(10);


            GUILayout.BeginHorizontal();

            settings.suppressAnnouncer = GUILayout.Toggle(settings.suppressAnnouncer, "Suppress Announcer");

            GUILayout.EndHorizontal();


            GUILayout.Space(10);


            GUILayout.BeginHorizontal();

            settings.maxCageSpawns = GUILayout.Toggle(settings.maxCageSpawns, "Max Cage Spawns");

            GUILayout.EndHorizontal();


            GUILayout.Space(20);


            GUILayout.Space(20);


            if ( settings.setZoom != (settings.setZoom = GUILayout.Toggle(settings.setZoom, "Set Zoom Level")) )
            {
                if ( !settings.setZoom )
                {
                    SortOfFollow.zoomLevel = 1;
                }
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            GUILayout.Label(settings.zoomLevel.ToString("0.00"), GUILayout.Width(100));

            settings.zoomLevel = GUILayout.HorizontalSlider(settings.zoomLevel, 0, 10);

            GUILayout.Space(10);

            GUILayout.EndHorizontal();


            GUILayout.Space(20);


            GUILayout.BeginHorizontal(new GUILayoutOption[] { GUILayout.MinHeight((unitDropdown.show) ? 350 : 100), GUILayout.ExpandWidth(false) });
            unitDropdown.OnGUI(modEntry);
            Main.settings.selectedEnemy = unitDropdown.indexNumber;

            if ( settings.spawnEnemyOnRightClick != (settings.spawnEnemyOnRightClick = GUILayout.Toggle(settings.spawnEnemyOnRightClick, "Spawn Enemy On Right Click") ) )
            {
                if ( settings.spawnEnemyOnRightClick )
                {
                    settings.teleportToMouseCursor = false;
                }
            }
            GUILayout.EndHorizontal();
        }

        static void SpawnUnit(int unit, Vector3 vector)
        {
            TestVanDammeAnim original = null;
            GameObject __result = null;

            switch (unit)
            {
                // Mooks
                case 0:
                    original = Map.Instance.activeTheme.mook;
                    break;
                case 1:
                    original = Map.Instance.activeTheme.mookSuicide;
                    break;
                case 2:
                    original = Map.Instance.activeTheme.mookBigGuy;
                    break;
                case 3:
                    original = Map.Instance.activeTheme.mookSuicideBigGuy;
                    break;
                case 4:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.mookBigGuyStrong, vector, Quaternion.identity).gameObject;
                    break;
                case 5:
                    original = Map.Instance.activeTheme.mookBigGuyElite;
                    break;
                case 6:
                    original = Map.Instance.activeTheme.mookScout;
                    break;
                case 7:
                    original = Map.Instance.activeTheme.mookRiotShield;
                    break;
                case 8:
                    original = Map.Instance.activeTheme.mookArmoured;
                    break;
                case 9:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.mechBrown, vector, Quaternion.identity).gameObject;
                    break;
                case 10:
                    original = Map.Instance.sharedObjectsReference.Asset.mookJetpack;
                    break;
                case 11:
                    original = Map.Instance.activeTheme.mookGrenadier;
                    break;
                case 12:
                    original = Map.Instance.activeTheme.mookBazooka;
                    break;
                case 13:
                    original = Map.Instance.activeTheme.mookJetpackBazooka;
                    break;
                case 14:
                    original = Map.Instance.activeTheme.mookNinja;
                    break;
                case 15:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.treasureMook, vector, Quaternion.identity).gameObject;
                    break;
                case 16:
                    original = Map.Instance.activeTheme.mookDog;
                    break;
                case 17:
                    original = Map.Instance.activeTheme.skinnedMook;
                    break;
                case 18:
                    original = Map.Instance.activeTheme.mookGeneral;
                    break;
                case 19:
                    original = Map.Instance.activeTheme.mookAlarmist;
                    break;
                case 20:
                    original = Map.Instance.activeTheme.mookStrong;
                    break;
                case 21:
                    original = Map.Instance.activeTheme.mookScientist;
                    break;
                case 22:
                    original = Map.Instance.activeTheme.snake;
                    break;
                // Satan
                case 23:
                    original = Map.Instance.activeTheme.satan;
                    break;
                // Aliens
                case 24:
                    original = Map.Instance.activeTheme.alienFaceHugger;
                    break;
                case 25:
                    original = Map.Instance.activeTheme.alienXenomorph;
                    break;
                case 26:
                    original = Map.Instance.activeTheme.alienBrute;
                    break;
                case 27:
                    original = Map.Instance.activeTheme.alienBaneling;
                    break;
                case 28:
                    original = Map.Instance.activeTheme.alienMosquito;
                    break;
                case 29:
                    original = Map.Instance.activeTheme.mookXenomorphBrainbox;
                    break;
                // HellDog
                case 30:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[0], vector, Quaternion.identity);
                    break;
                // ZMookUndead
                case 31:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[1], vector, Quaternion.identity);
                    break;
                // ZMookUndeadStartDead
                case 32:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[2], vector, Quaternion.identity);
                    break;
                // ZMookWarlock
                case 33:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[3], vector, Quaternion.identity);
                    break;
                // ZMookHellBoomer
                case 34:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[4], vector, Quaternion.identity);
                    break;
                // ZMookUndeadSuicide
                case 35:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[5], vector, Quaternion.identity);
                    break;
                // ZHellBigGuy
                case 36:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[6], vector, Quaternion.identity);
                    break;
                // Lost Soul
                case 37:
                    vector.y += 5;
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[8], vector, Quaternion.identity);
                    break;
                // ZMookHellSoulCatcher
                case 38:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[10], vector, Quaternion.identity);
                    break;
                // Sandworm
                case 39:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[7], vector, Quaternion.identity);
                    break;
                // Boneworm
                case 40:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[12], vector, Quaternion.identity);
                    break;
                // Boneworm Behind
                case 41:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[13], vector, Quaternion.identity);
                    break;
                // Alien Worm
                case 42:
                    __result = (UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.alienMinibossSandWorm, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                    break;
                // Alien Facehugger Worm
                case 43:
                    __result = (UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitter, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                    break;
                // Alien Facehugger Worm Behind
                case 44:
                    __result = (UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitterBehind, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                    break;
                case 45:
                    SatanMiniboss satanMiniboss = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.satanMiniboss, vector, Quaternion.identity) as SatanMiniboss;
                    if (satanMiniboss != null)
                    {
                        __result = satanMiniboss.gameObject;
                    }
                    break;
                case 46:
                    __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(Map.Instance.activeTheme.mookDolfLundgren, vector, Quaternion.identity).gameObject;
                    break;
                case 47:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.mookMammothTank, vector, Quaternion.identity).gameObject;
                    break;
                case 48:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.mookKopterMiniBoss, vector, Quaternion.identity).gameObject;
                    break;
                case 49:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.goliathMech, vector, Quaternion.identity).gameObject;
                    break;
                // Large Alien Worm
                case 50:
                    __result = (UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.alienGiantSandWormBoss, vector, Quaternion.identity) as AlienMinibossSandWorm).gameObject;
                    break;
                case 51:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.mookTankMookLauncher, vector, Quaternion.identity).gameObject;
                    break;
                case 52:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.mookTankCannon, vector, Quaternion.identity).gameObject;
                    break;
                case 53:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.mookTankRockets, vector, Quaternion.identity).gameObject;
                    break;
                case 54:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.mookArtilleryTruck, vector, Quaternion.identity).gameObject;
                    break;
                case 55:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.mookBlimp, vector, Quaternion.identity).gameObject;
                    break;
                case 56:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.mookDrillCarrier, vector, Quaternion.identity).gameObject;
                    break;
                case 57:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.mookTruck, vector, Quaternion.identity).gameObject;
                    break;
                case 58:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.activeTheme.sandbag, vector, Quaternion.identity).gameObject;
                    break;
                case 59:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.mookMotorBike, vector, Quaternion.identity).gameObject;
                    break;
                case 60:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.mookMotorBikeNuclear, vector, Quaternion.identity).gameObject;
                    break;
                case 61:
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.mookDumpTruck, vector, Quaternion.identity).gameObject;
                    break;
            }

            if (original != null)
            {
                __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(original, vector, Quaternion.identity).gameObject;
            }
        }
        #endregion

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            return true;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            if (!enabled) return;

            if (!loadedScene && settings.quickLoadScene)
            {
                waitForLoad -= dt;
                if ( waitForLoad < 0 )
                {
                    try
                    {
                        if (!Main.settings.cameraShake)
                        {
                            PlayerOptions.Instance.cameraShakeAmount = 0f;
                        }

                        Utility.SceneLoader.LoadScene("WorldMap3D");
                        Utility.SceneLoader.LoadScene(settings.sceneToLoad);
                    }
                    catch { }
                    loadedScene = true;
                }
            }

            if ( settings.teleportToMouseCursor )
            {
                try
                {
                    if (Input.GetMouseButtonUp(1))
                    {
                        Camera camera = (Traverse.Create(typeof(SetResolutionCamera)).Field("mainCamera").GetValue() as Camera);
                        Vector3 newPos = camera.ScreenToWorldPoint(Input.mousePosition);

                        HeroController.players[0].character.X = newPos.x;
                        HeroController.players[0].character.Y = newPos.y;
                    }
                }
                catch
                {}
            }

#if DEBUG
            try
            {
                levelStartedCounter += dt;

                if (settings.setZoom && levelStartedCounter > 1.5f && SortOfFollow.zoomLevel != settings.zoomLevel)
                {
                    SortOfFollow.zoomLevel = settings.zoomLevel;
                }
                if ( settings.spawnEnemyOnRightClick )
                {
                    if (Input.GetMouseButtonUp(1))
                    {
                        Camera camera = (Traverse.Create(typeof(SetResolutionCamera)).Field("mainCamera").GetValue() as Camera);
                        Vector3 newPos = camera.ScreenToWorldPoint(Input.mousePosition);

                        SpawnUnit(settings.selectedEnemy, newPos);
                    }
                }
            }
            catch
            { }
            if ( Map.MapData != null && settings.suppressAnnouncer )
            {
                Map.MapData.suppressAnnouncer = true;
            }
#endif

            if ( settings.enableFlight )
            {
                try
                {
                    HeroController.players[0].character.speed = 300;
                    if (HeroController.players[0].character.up)
                    {
                        HeroController.players[0].character.yI = 300;
                    }
                    else if (HeroController.players[0].character.down)
                    {
                        HeroController.players[0].character.yI = -300;
                    }
                    else
                    {
                        HeroController.players[0].character.yI = 0;
                    }

                    if ( HeroController.players[0].character.right )
                    {
                        HeroController.players[0].character.xI = 300;
                    }
                    else if (HeroController.players[0].character.left )
                    {
                        HeroController.players[0].character.xI = -300;
                    }
                    else
                    {
                        HeroController.players[0].character.xI = 0;
                    }
                }
                catch
                {}
            }
        }

        static void determineLevelsInCampaign()
        {
            Main.settings.campaignNum = campaignNum.indexNumber;

            if (lastCampaignNum != campaignNum.indexNumber)
            {
                int actualCampaignNum = campaignNum.indexNumber + 1;
                int numberOfLevels = 1;
                switch (actualCampaignNum )
                {
                    case 1: numberOfLevels = 4; break;
                    case 2: numberOfLevels = 4; break;
                    case 3: numberOfLevels = 4; break;
                    case 4: numberOfLevels = 4; break;
                    case 5: numberOfLevels = 3; break;
                    case 6: numberOfLevels = 4; break;
                    case 7: numberOfLevels = 5; break;
                    case 8: numberOfLevels = 4; break;
                    case 9: numberOfLevels = 4; break;
                    case 10: numberOfLevels = 4; break;
                    case 11: numberOfLevels = 6; break;
                    case 12: numberOfLevels = 5; break;
                    case 13: numberOfLevels = 5; break;
                    case 14: numberOfLevels = 5; break;
                    case 15: numberOfLevels = 14; break;
                    default: numberOfLevels = 1; break;
                }
                

                if (levelNum.indexNumber + 1 > numberOfLevels)
                {
                    levelNum.indexNumber = numberOfLevels - 1;
                }
                levelNum = new Dropdown(400, 150, 125, 300, levelList.Take(numberOfLevels).ToArray(), levelNum.indexNumber, levelNum.show);
            }
            lastCampaignNum = campaignNum.indexNumber;
        }

        static void GoToLevel()
        {

            string terrainName = "vietmanterrain";
            string territoryName = "VIETMAN";

            territoryName = campaignList[campaignNum.indexNumber];
            terrainName = terrainList[campaignNum.indexNumber];

            HarmonyPatches.WorldMapController_Update_Patch.GoToLevel(territoryName, levelNum.indexNumber, terrainName);
        }

        static void ChangeLevel(int levelNum)
        {
            // GameModeController instance = Traverse.Create(typeof(GameModeController)).Field("instance").GetValue() as GameModeController;

            LevelSelectionController.CurrentLevelNum += levelNum;

            Map.ClearSuperCheckpointStatus();
            GameModeController.RestartLevel();
        }

        static void UnlockTerritory( WorldTerritory3D territory )
        {
            switch (territory.properties.territoryName)
            {
                case "VIETMAN": territory.SetState(TerritoryState.TerroristBase); break;
                case "Bombardment Challenge": territory.SetState(TerritoryState.TerroristBase); break;
                case "HAWAII": territory.SetState(TerritoryState.Infested); break;
                case "INDONESIA": territory.SetState(TerritoryState.TerroristBase); break;
                case "DEM. REP. OF CONGO": territory.SetState(TerritoryState.Infested); break;
                case "CAMBODIA": territory.SetState(TerritoryState.TerroristBase); break;
                case "MUSCLETEMPLE3": territory.SetState(TerritoryState.TerroristBase); break;
                case "MUSCLETEMPLE2": territory.SetState(TerritoryState.TerroristBase); break;
                case "MUSCLETEMPLE4": territory.SetState(TerritoryState.TerroristBase); break;
                case "UKRAINE": territory.SetState(TerritoryState.TerroristBase); break;
                case "SOUTH KOREA": territory.SetState(TerritoryState.TerroristBase); break;
                case "KAZAKHSTAN": territory.SetState(TerritoryState.TerroristBase); break;
                case "INDIA": territory.SetState(TerritoryState.TerroristBurning); break;
                case "MacBrover Challenge": territory.SetState(TerritoryState.TerroristBase); break;
                case "PHILIPPINES": territory.SetState(TerritoryState.TerroristBase); break;
                case "Dash Challenge": territory.SetState(TerritoryState.TerroristBase); break;
                case "THE AMAZON RAINFOREST": territory.SetState(TerritoryState.Infested); break;
                case "PANAMA": territory.SetState(TerritoryState.TerroristBase); break;
                case "WHITE HOUSE": territory.SetState(TerritoryState.Empty); break;
                case "UNITED STATES OF AMERICA": territory.SetState(TerritoryState.Hell); break;
                case "HONG KONG": territory.SetState(TerritoryState.TerroristBase); break;
                case "Mech Challenge": territory.SetState(TerritoryState.TerroristBase); break;
                case "Ammo Challenge": territory.SetState(TerritoryState.TerroristBase); break;
                case "Time Bro Challenge": territory.SetState(TerritoryState.TerroristBase); break;
                case "NEW GUINEA": territory.SetState(TerritoryState.TerroristBurning); break;
                case "MUSCLETEMPLE1": territory.SetState(TerritoryState.TerroristBase); break;
                case "MUSCLETEMPLE5": territory.SetState(TerritoryState.TerroristBase); break;
                case "Alien Challenge": territory.SetState(TerritoryState.TerroristBase); break;
            }

        }

        public static void StartTimeSlow()
        {
            HeroController.TimeBroBoost(float.MaxValue);
            Time.timeScale = settings.timeSlowFactor;
            HeroController.TimeBroBoostHeroes(float.MaxValue);
        }

        public static void StopTimeSlow()
        {
            HeroController.CancelTimeBroBoost();
            HeroController.TimeBroBoostHeroes(0);
        }

        public static Vector3 GetFinalCheckpointPos()
        {
            for ( int i = 0; i < Map.checkPoints.Count; ++i )
            {
                if ((bool)Traverse.Create(Map.checkPoints[i]).Field("isFinal").GetValue())
                {
                    return Map.checkPoints[i].transform.position;
                }
            }
            return Map.checkPoints[Map.checkPoints.Count - 1].transform.position;
        }
        public static void Log(String str)
        {
            mod.Logger.Log(str);
        }
    }
}
