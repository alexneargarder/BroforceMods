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

        public static string[] actualCampaignNames = new string[]
        {
            "WM_Intro(mouse)",
            "WM_Mission1(mouse)",
            "WM_Mission2 (mouse)",
            "WM_City1(mouse)",
            "WM_Bombardment(mouse)",
            "WM_Village1(mouse)",
            "WM_City2(mouse)",
            "WM_Bombardment2(mouse)",
            "WM_KazakhstanIndustrial(mouse)",
            "WM_KazakhstanRainy(mouse)",
            "WM_AlienMission1(mouse)",
            "WM_AlienMission2(mouse)",
            "WM_AlienMission3(mouse)",
            "WM_AlienMission4(mouse)",
            "WM_HELL",
            "WM_Whitehouse",
            "Challenge_Alien",
            "Challenge_Bombardment1",
            "Challenge_Ammo",
            "Challenge_Dash",
            "Challenge_Mech1",
            "Challenge_MacBrover",
            "Challenge_TimeBro",
            "MuscleTemple_1",
            "MuscleTemple_2",
            "MuscleTemple_3",
            "MuscleTemple_4",
            "WM_Intro(mouse)"
        };

        public static Dropdown unitDropdown;

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
            "Mook Launcher Tank", "Cannon Tank", "Rocket Tank", "Artillery Truck", "Blimp", "Drill carrier", "Mook Truck", "Turret", "Motorbike", "Motorbike Nuclear", "Dump Truck",
            "Pig", "Rotten Pig", "Villager 1", "Villager 2"
        };

        public static Dropdown objectDropdown;

        public static string[] objectList = new string[]
        {
            "Dirt", "Explosive Barrel", "Red Explosive Barrel", "Propane Tank", "Rescue Cage", "Crate", "Ammo Crate", "Time Ammo Crate", "RC Car Ammo Crate", "Air Strike Ammo Crate", "Mech Drop Ammo Crate", "Alien Pheromones Ammo Crate", "Steroids Ammo Crate", "Pig Ammo Crate",
            "Flex Ammo Crate", "Beehive", "Alien Egg", "Alien Egg Explosive"
        };

        public static Dropdown controllerDropdown;

        public static string[] controllerList = new string[]
        {
            "Keyboard 1", "Keyboard 2", "Keyboard 3", "Keyboard 4", "Controller 1", "Controller 2", "Controller 3", "Controller 4"
        };

        public static string teleportX = "0";
        public static string teleportY = "0";

        public static bool loadedScene = false;
        public static float waitForLoad = 4.0f;

        public static bool skipNextMenu = false;

        public static bool loadedLevel = false;

        public static float levelStartedCounter = 0f;

        public static TestVanDammeAnim currentCharacter;
        public static Helicopter helicopter;

        private static float _windowWidth = -1f;

        #region UMM
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUpdate = OnUpdate;
            try
            {
                settings = Settings.Load<Settings>(modEntry);
            }
            catch
            {
                // Settings format changed
                settings = new Settings();
            }

            mod = modEntry;

            try
            {
                var harmony = new Harmony(modEntry.Info.Id);
                var assembly = Assembly.GetExecutingAssembly();
                harmony.PatchAll(assembly);
            }
            catch (Exception ex)
            {
                Main.Log(ex.ToString());
            }

            lastCampaignNum = -1;
            campaignNum = new Dropdown(125, 150, 200, 300, new string[] { "Campaign 1", "Campaign 2", "Campaign 3", "Campaign 4", "Campaign 5",
                "Campaign 6", "Campaign 7", "Campaign 8", "Campaign 9", "Campaign 10", "Campaign 11", "Campaign 12", "Campaign 13", "Campaign 14", "Campaign 15",
                "White House", "Alien Challenge", "Bombardment Challenge", "Ammo Challenge", "Dash Challenge", "Mech Challenge", "MacBrover Challenge", "Time Bro Challenge",
                "Muscle Temple 1", "Muscle Temple 2", "Muscle Temple 3", "Muscle Temple 4", "Muscle Temple 5" }, settings.campaignNum);


            levelNum = new Dropdown(400, 150, 150, 300, levelList, settings.levelNum);

            unitDropdown = new Dropdown(400, 150, 200, 300, unitList, settings.selectedEnemy);

            objectDropdown = new Dropdown(400, 150, 200, 300, objectList, (int)settings.selectedObject);

            controllerDropdown = new Dropdown( 400, 150, 200, 300, controllerList, settings.goToLevelControllerNum );
            controllerDropdown.ToolTip = "Sets which controller controls player 1 when using the Go To Level button, if no players have joined yet";

            return true;
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

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            if (!enabled) return;

            // Handle instant scene loading
            if (!loadedScene && settings.quickLoadScene)
            {
                waitForLoad -= dt;
                if (waitForLoad < 0)
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
                    catch {}
                    loadedScene = true;
                }
            }

            // Middle click to change right click function
            if ( settings.middleClickToChangeRightClick )
            {
                if ( Input.GetMouseButtonDown(2) )
                {
                    if ( settings.currentRightClick == RightClick.SpawnObject )
                    {
                        settings.currentRightClick = RightClick.TeleportToCursor;
                    }
                    else
                    {
                        ++settings.currentRightClick;
                    }
                }
            }

            // Right click to teleport to cursor
            if (settings.currentRightClick == RightClick.TeleportToCursor)
            {
                try
                {
                    if (Input.GetMouseButtonDown(1))
                    {
                        Camera camera = (Traverse.Create(typeof(SetResolutionCamera)).Field("mainCamera").GetValue() as Camera);
                        Vector3 newPos = camera.ScreenToWorldPoint(Input.mousePosition);

                        currentCharacter = HeroController.players[0].character;

                        Main.TeleportToCoords(newPos.x, newPos.y);
                    }
                }
                catch {}
            }

            // Right click to spawn enemies
            if (settings.currentRightClick == RightClick.SpawnEnemy)
            {
                try
                {
                    if (Input.GetMouseButtonDown(1))
                    {
                        Camera camera = (Traverse.Create(typeof(SetResolutionCamera)).Field("mainCamera").GetValue() as Camera);
                        Vector3 newPos = camera.ScreenToWorldPoint(Input.mousePosition);

                        SpawnUnit(settings.selectedEnemy, newPos);
                    }
                }
                catch {}
            }
            

            // Right click to spawn objects
            if (settings.currentRightClick == RightClick.SpawnObject)
            {
                try
                {
                    if (Input.GetMouseButton(1))
                    {
                        Camera camera = (Traverse.Create(typeof(SetResolutionCamera)).Field("mainCamera").GetValue() as Camera);
                        Vector3 newPos = camera.ScreenToWorldPoint(Input.mousePosition);
                        int column = (int)Mathf.Round(newPos.x / 16f);
                        int row = (int)Mathf.Round(newPos.y / 16f);

                        if (Map.blocks[column, row] == null || Map.blocks[column, row].destroyed)
                        {
                            switch (settings.selectedObject)
                            {
                                case CurrentObject.Dirt:
                                case CurrentObject.ExplosiveBarrel:
                                case CurrentObject.RedExplosiveBarrel:
                                case CurrentObject.PropaneTank:
                                case CurrentObject.Crate:
                                case CurrentObject.AmmoCrate:
                                case CurrentObject.TimeAmmoCrate:
                                case CurrentObject.RCCarAmmoCrate:
                                case CurrentObject.AirStrikeAmmoCrate:
                                case CurrentObject.MechDropAmmoCrate:
                                case CurrentObject.AlienPheromonesAmmoCrate:
                                case CurrentObject.SteroidsAmmoCrate:
                                case CurrentObject.PigAmmoCrate:
                                case CurrentObject.FlexAmmoCrate:
                                case CurrentObject.BeeHive:
                                case CurrentObject.AlienEgg:
                                case CurrentObject.AlienEggExplosive:
                                    SpawnBlock(row, column);
                                    break;
                                default:
                                    SpawnDoodad(row, column);
                                    break;
                            }
                        }
                    }
                }
                catch {}
            }

            // Set zoom level
            levelStartedCounter += dt;
            if ( settings.setZoom )
            {
                try
                {
                    if (levelStartedCounter > 1.5f && SortOfFollow.zoomLevel != settings.zoomLevel)
                    {
                        SortOfFollow.zoomLevel = settings.zoomLevel;
                    }
                }
                catch {}
            }

            // Suppress announcer
            if (Map.MapData != null && settings.suppressAnnouncer)
            {
                Map.MapData.suppressAnnouncer = true;
            }

            // Handle flight
            if (settings.enableFlight)
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

                    if (HeroController.players[0].character.right)
                    {
                        HeroController.players[0].character.xI = 300;
                    }
                    else if (HeroController.players[0].character.left)
                    {
                        HeroController.players[0].character.xI = -300;
                    }
                    else
                    {
                        HeroController.players[0].character.xI = 0;
                    }
                }
                catch {}
            }

            if (settings.showCursor)
            {
                Cursor.visible = true;
            }
        }
        #endregion

        #region UI
        private static GUILayoutOption ScaledWidth(float width)
        {
            if (settings.scaleUIWithWindowWidth && _windowWidth > 0)
            {
                float scaleFactor = _windowWidth / 1200f;
                return GUILayout.Width(width * scaleFactor);
            }
            return GUILayout.Width(width);
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            // Capture window width for UI scaling
            if (_windowWidth < 0)
            {
                try
                {
                    GUILayout.BeginHorizontal();
                    if (Event.current.type == EventType.Repaint)
                    {
                        Rect rect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));
                        if (rect.width > 1)
                        {
                            _windowWidth = rect.width;
                        }
                    }
                    GUILayout.Label(" ");
                    GUILayout.EndHorizontal();
                }
                catch (Exception)
                {
                }
                return;
            }

            string previousToolTip = string.Empty;

            GUIStyle headerStyle = new GUIStyle(GUI.skin.button);

            if (settings.quickLoadScene)
            {
                if (loadedScene && HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null)
                {
                    currentCharacter = HeroController.players[0].character;
                }
            }
            else if (HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null)
            {
                currentCharacter = HeroController.players[0].character;
            }
            else
            {
                currentCharacter = null;
            }

            headerStyle.fontStyle = FontStyle.Bold;
            // 163, 232, 255
            headerStyle.normal.textColor = new Color(0.639216f, 0.909804f, 1f);


            if (GUILayout.Button("General Options", headerStyle))
            {
                settings.showGeneralOptions = !settings.showGeneralOptions;
            }

            if (settings.showGeneralOptions)
            {
                ShowGeneralOptions(modEntry, ref previousToolTip);
            } // End General Options

            if (GUILayout.Button("Level Controls", headerStyle))
            {
                settings.showLevelOptions = !settings.showLevelOptions;
            }

            if (settings.showLevelOptions)
            {
                ShowLevelControls(modEntry, ref previousToolTip);
            } // End Level Controls

            if (GUILayout.Button("Cheat Options", headerStyle))
            {
                settings.showCheatOptions = !settings.showCheatOptions;
            }

            if (settings.showCheatOptions)
            {
                ShowCheatOptions(modEntry, ref previousToolTip);
            } // End Cheat Options

            if (GUILayout.Button("Teleport Options", headerStyle))
            {
                settings.showTeleportOptions = !settings.showTeleportOptions;
            }

            if (settings.showTeleportOptions)
            {
                ShowTeleportOptions(modEntry, ref previousToolTip);
            } // End Teleport Options

            if (GUILayout.Button("Debug Options", headerStyle))
            {
                settings.showDebugOptions = !settings.showDebugOptions;
            }

            if (settings.showDebugOptions)
            {
                ShowDebugOptions(modEntry, ref previousToolTip);
            } // End Debug Options
        }

        static void ShowGeneralOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip)
        {
            GUILayout.BeginHorizontal();
            {
                settings.cameraShake = GUILayout.Toggle(settings.cameraShake, new GUIContent("Camera Shake",
                    "Disable this to have camera shake automatically set to 0 at the start of a level"), ScaledWidth(100f));

                settings.enableSkip = GUILayout.Toggle(settings.enableSkip, new GUIContent("Helicopter Skip",
                    "Skips helicopter on world map and immediately takes you into a level"), ScaledWidth(120f));

                settings.endingSkip = GUILayout.Toggle(settings.endingSkip, new GUIContent("Ending Skip",
                    "Speeds up the ending"), ScaledWidth(100f));

                settings.quickMainMenu = GUILayout.Toggle(settings.quickMainMenu, new GUIContent("Speed up Main Menu Loading", "Makes menu options show up immediately rather than after the eagle screech"), ScaledWidth(190f));

                settings.helicopterWait = GUILayout.Toggle(settings.helicopterWait, new GUIContent("Helicopter Wait", "Makes helicopter wait for all alive players before leaving"), ScaledWidth(110f));

                settings.disableConfirm = GUILayout.Toggle(settings.disableConfirm, new GUIContent("Fix Mod Window Disappearing",
                    "Disables confirmation screen when restarting or returning to map/menu"), GUILayout.ExpandWidth(false));
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            {
                settings.skipBreakingCutscenes = GUILayout.Toggle(settings.skipBreakingCutscenes, new GUIContent("Disable Broken Cutscenes", "Prevents cutscenes that destroy the mod window from playing, includes all the flex powerup and ammo crate unlock cutscenes."), ScaledWidth(170f));

                Rect lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 800;

                settings.skipAllCutscenes = GUILayout.Toggle(settings.skipAllCutscenes, new GUIContent("Disable All Cutscenes", "Disables all bro unlock, boss fight, and powerup unlock cutscenes."), ScaledWidth(170f));

                settings.scaleUIWithWindowWidth = GUILayout.Toggle(settings.scaleUIWithWindowWidth, new GUIContent("Scale UI with Window Width", "Scales UI elements based on window width"), ScaledWidth(200f));

                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(25);
        }

        static void ShowLevelControls(UnityModManager.ModEntry modEntry, ref string previousToolTip)
        {
            bool dropdownActive = false;

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

                        DetermineLevelsInCampaign();

                        levelNum.OnGUI(modEntry);

                        Main.settings.levelNum = levelNum.indexNumber;

                        if (GUILayout.Button(new GUIContent("Go to level"), ScaledWidth(100)))
                        {
                            GoToLevel();
                        }

                        if (GUI.tooltip != previousToolTip)
                        {
                            Rect lastRect = campaignNum.dropDownRect;
                            lastRect.y += 20;
                            lastRect.width += 500;

                            GUI.Label(lastRect, GUI.tooltip);
                            previousToolTip = GUI.tooltip;
                        }
                    }
                    GUILayout.EndHorizontal();

                    dropdownActive = campaignNum.show || levelNum.show;

                    GUILayout.Space(25);

                    GUILayout.BeginHorizontal( new GUILayoutOption[] { GUILayout.MinHeight( ( controllerDropdown.show ) ? 350 : 30 ), GUILayout.ExpandWidth( false ) }  );
                    {
                        GUILayout.Space( 1 );
                        if ( !dropdownActive )
                        {
                            Main.settings.goToLevelOnStartup = GUILayout.Toggle( Main.settings.goToLevelOnStartup, new GUIContent( "Go to level on startup", "Spawns you into the level as soon as the game starts." ), ScaledWidth( 150 ) );

                            Rect lastRect = GUILayoutUtility.GetLastRect();
                            lastRect.y += 25;
                            lastRect.width += 500;

                            controllerDropdown.OnGUI( modEntry );
                            Main.settings.goToLevelControllerNum = controllerDropdown.indexNumber;

                            if ( GUI.tooltip != previousToolTip )
                            {
                                GUI.Label( lastRect, GUI.tooltip );
                                previousToolTip = GUI.tooltip;
                            }
                        }
                    }
                    GUILayout.EndHorizontal();

                    dropdownActive = dropdownActive || controllerDropdown.show;

                    GUILayout.Space( 25 );

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(1);

                        if (!dropdownActive)
                        {

                            if (GUILayout.Button(new GUIContent("Previous Level", "This only works in game"), new GUILayoutOption[] { ScaledWidth(150), GUILayout.ExpandWidth(false) }))
                            {
                                ChangeLevel(-1);
                            }

                            Rect lastRect = GUILayoutUtility.GetLastRect();
                            lastRect.y += 20;
                            lastRect.width += 500;

                            if (GUILayout.Button(new GUIContent("Next Level", "This only works in game"), new GUILayoutOption[] { ScaledWidth(150), GUILayout.ExpandWidth(false) }))
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

            GUILayout.Space( 15 );
        }

        static void ShowCheatOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip)
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

            if (GUILayout.Button("Summon Mech", ScaledWidth(140)))
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

            GUILayout.BeginHorizontal(ScaledWidth(400));

            GUILayout.Label("Time Slow Factor: " + settings.timeSlowFactor);

            if (settings.timeSlowFactor != (settings.timeSlowFactor = GUILayout.HorizontalSlider(settings.timeSlowFactor, 0, 5, ScaledWidth(200))))
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

            GUILayout.BeginHorizontal(ScaledWidth(500));

            GUILayout.Label("Scene to Load: ");

            settings.sceneToLoad = GUILayout.TextField(settings.sceneToLoad, ScaledWidth(200));

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

        static void ShowTeleportOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip)
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
                GUILayout.Label("Y", ScaledWidth(10));
                GUILayout.Space(10);
                teleportY = GUILayout.TextField(teleportY, ScaledWidth(100));

                if (GUILayout.Button("Teleport", ScaledWidth(100)))
                {
                    float x, y;
                    if (float.TryParse(teleportX, out x) && float.TryParse(teleportY, out y))
                    {
                        if (currentCharacter != null)
                        {
                            TeleportToCoords(x, y);
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.BeginHorizontal(ScaledWidth(400));

            bool teleportEnabled = settings.currentRightClick == RightClick.TeleportToCursor;
            if (teleportEnabled != (teleportEnabled = GUILayout.Toggle(teleportEnabled, "Teleport to Cursor on Right Click")))
            {
                if (teleportEnabled)
                {
                    settings.currentRightClick = RightClick.TeleportToCursor;
                }
                else
                {
                    settings.currentRightClick = RightClick.None;
                }
            }

            GUILayout.Space(10);

            settings.changeSpawn = GUILayout.Toggle(settings.changeSpawn, "Spawn at Custom Waypoint");

            GUILayout.Space(10);

            settings.changeSpawnFinal = GUILayout.Toggle(settings.changeSpawnFinal, "Spawn at Final Checkpoint");

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Save Position for Custom Spawn", ScaledWidth(250)))
                {
                    if (currentCharacter != null && GameState.Instance != null)
                    {
                        SaveCustomSpawnForCurrentLevel(currentCharacter.X, currentCharacter.Y);
                        Log($"Saved spawn position for {GetCurrentLevelKey()}");
                    }
                }

                if (GUILayout.Button("Teleport to Custom Spawn Position", ScaledWidth(300)))
                {
                    if (currentCharacter != null && HasCustomSpawnForCurrentLevel())
                    {
                        Vector2 spawn = GetCustomSpawnForCurrentLevel();
                        TeleportToCoords(spawn.x, spawn.y);
                    }
                }

                if (GUILayout.Button("Clear Custom Spawn", ScaledWidth(150)))
                {
                    if (GameState.Instance != null)
                    {
                        ClearCustomSpawnForCurrentLevel();
                    }
                }

                if (HasCustomSpawnForCurrentLevel())
                {
                    Vector2 spawn = GetCustomSpawnForCurrentLevel();
                    GUILayout.Label($"Saved position: {spawn.x:F2}, {spawn.y:F2}");
                }
                else
                {
                    GUILayout.Label("Saved position: None");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Teleport to Current Checkpoint ", ScaledWidth(250)))
                {
                    if (currentCharacter != null)
                    {
                        Vector3 checkPoint = HeroController.GetCheckPointPosition(0, Map.IsCheckPointAnAirdrop(HeroController.GetCurrentCheckPointID()));
                        TeleportToCoords(checkPoint.x, checkPoint.y);
                    }
                }

                if (GUILayout.Button("Teleport to Final Checkpoint", ScaledWidth(200)))
                {
                    if (currentCharacter != null)
                    {
                        Vector3 checkPoint = GetFinalCheckpointPos();
                        TeleportToCoords(checkPoint.x, checkPoint.y);
                    }
                }
            }
            GUILayout.EndHorizontal();

            for (int i = 0; i < settings.waypointsX.Length; ++i)
            {
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Save Position to Waypoint " + (i + 1), ScaledWidth(250)))
                    {
                        if (currentCharacter != null)
                        {
                            settings.waypointsX[i] = currentCharacter.X;
                            settings.waypointsY[i] = currentCharacter.Y;
                        }
                    }

                    if (GUILayout.Button("Teleport to Waypoint " + (i + 1), ScaledWidth(200)))
                    {
                        if (currentCharacter != null)
                        {
                            TeleportToCoords(settings.waypointsX[i], settings.waypointsY[i]);
                        }
                    }

                    GUILayout.Label("Saved position: " + settings.waypointsX[i] + ", " + settings.waypointsY[i]);
                }
                GUILayout.EndHorizontal();
            }
        }

        static void ShowDebugOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip)
        {
            GUILayout.BeginHorizontal();

            settings.printAudioPlayed = GUILayout.Toggle(settings.printAudioPlayed, new GUIContent("Print Audio Played", "Prints the name of the Audio Clip to the Log"));

            GUILayout.EndHorizontal();


            GUILayout.Space(10);


            GUILayout.BeginHorizontal();

            settings.suppressAnnouncer = GUILayout.Toggle(settings.suppressAnnouncer, new GUIContent("Suppress Announcer", "Disables the Countdown at the start of levels"));

            GUILayout.EndHorizontal();


            GUILayout.Space(10);


            GUILayout.BeginHorizontal();

            settings.maxCageSpawns = GUILayout.Toggle(settings.maxCageSpawns, new GUIContent("Max Cage Spawns", "Forces every cage that spawns on the map to contain a prisoner"));

            GUILayout.EndHorizontal();

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 300;
            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(30);


            if (settings.setZoom != (settings.setZoom = GUILayout.Toggle(settings.setZoom, new GUIContent("Set Zoom Level", "Overrides the default zoom level of the camera"))))
            {
                if (!settings.setZoom)
                {
                    SortOfFollow.zoomLevel = 1;
                }
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            GUILayout.Label(settings.zoomLevel.ToString("0.00"), ScaledWidth(100));

            settings.zoomLevel = GUILayout.HorizontalSlider(settings.zoomLevel, 0, 10);

            GUILayout.Space(10);

            GUILayout.EndHorizontal();

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 300;
            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(30);

            settings.middleClickToChangeRightClick = GUILayout.Toggle(settings.middleClickToChangeRightClick, "Middle Click to Change Right Click Function");
            
            GUILayout.Space(10);

            settings.showCursor = GUILayout.Toggle(settings.showCursor, "Make Cursor Always Visible");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal(new GUILayoutOption[] { GUILayout.MinHeight((unitDropdown.show) ? 350 : 30), GUILayout.ExpandWidth(false) });
            unitDropdown.OnGUI(modEntry);
            Main.settings.selectedEnemy = unitDropdown.indexNumber;

            bool spawnEnemyEnabled = settings.currentRightClick == RightClick.SpawnEnemy;
            if (spawnEnemyEnabled != (spawnEnemyEnabled = GUILayout.Toggle(spawnEnemyEnabled, "Spawn Enemy On Right Click")))
            {
                if (spawnEnemyEnabled)
                {
                    settings.currentRightClick = RightClick.SpawnEnemy;
                }
                else
                {
                    settings.currentRightClick = RightClick.None;
                }

            }
            GUILayout.EndHorizontal();


            GUILayout.Space(10);


            GUILayout.BeginHorizontal(new GUILayoutOption[] { GUILayout.MinHeight((objectDropdown.show) ? 350 : 100), GUILayout.ExpandWidth(false) });
            objectDropdown.OnGUI(modEntry);
            Main.settings.selectedObject = (CurrentObject) objectDropdown.indexNumber;

            bool spawnObjectEnabled = settings.currentRightClick == RightClick.SpawnObject;
            if (spawnObjectEnabled != (spawnObjectEnabled = GUILayout.Toggle(spawnObjectEnabled, "Spawn Object On Right Click")))
            {
                if (spawnObjectEnabled)
                {
                    settings.currentRightClick = RightClick.SpawnObject;
                }
                else
                {
                    settings.currentRightClick = RightClick.None;
                }

            }
            GUILayout.EndHorizontal();
        }
        #endregion

        #region Modding
        static void TeleportToCoords(float x, float y)
        {
            if ( currentCharacter != null )
            {
                for ( int i = 0; i < 4; ++i )
                {
                    if ( HeroController.PlayerIsAlive(i) )
                    {
                        HeroController.players[i].character.X = x;
                        HeroController.players[i].character.Y = y;
                    }
                }
            }
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
                // Pig
                case 62:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.activeTheme.animals[0], vector, Quaternion.identity).gameObject;
                    break;
                // Rotten Pig
                case 63:
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.activeTheme.animals[2], vector, Quaternion.identity).gameObject;
                    break;
                // Villager1
                case 64:
                    __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(Map.Instance.activeTheme.villager1[0], vector, Quaternion.identity).gameObject;
                    break;
                // Villager2
                case 65:
                    __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(Map.Instance.activeTheme.villager1[1], vector, Quaternion.identity).gameObject;
                    break;
            }

            if (original != null)
            {
                __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(original, vector, Quaternion.identity).gameObject;
            }

            if ( __result != null )
            {
                __result.gameObject.transform.parent = Map.Instance.transform;
                Registry.RegisterDeterminsiticGameObject(__result.gameObject);
            }


        }

        static Block CreateBlock(int x, int y)
        {
            Traverse trav = Traverse.Create(Map.Instance);
            GroundType placeGroundType = GroundType.Earth;
            Block[,] newBlocks = Map.blocks;
            Vector3 vector = new Vector3((float)(x * 16), (float)(y * 16), 5f);
            Block currentBlock = null;
            //Block currentBackgroundBlock = null;
            switch ( settings.selectedObject )
            {
                case CurrentObject.Dirt:
                    return Map.Instance.PlaceGround(GroundType.Earth, x, y, ref Map.blocks, true);
                case CurrentObject.ExplosiveBarrel:
                    placeGroundType = GroundType.Barrel;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.activeTheme.blockPrefabBarrels[1], vector, Quaternion.identity);
                    break;
                case CurrentObject.RedExplosiveBarrel:
                    placeGroundType = GroundType.Barrel;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.activeTheme.blockPrefabBarrels[0], vector, Quaternion.identity);
                    break;
                case CurrentObject.PropaneTank:
                    placeGroundType = GroundType.Barrel;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.activeTheme.blockPrefabBarrels[2], vector, Quaternion.identity);
                    break;
                case CurrentObject.Crate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.activeTheme.blockPrefabWood[0], vector, Quaternion.identity);
                    break;
                case CurrentObject.AmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.activeTheme.crateAmmo, vector, Quaternion.identity);
                    break;
                case CurrentObject.TimeAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.sharedObjectsReference.Asset.crateTimeCop, vector, Quaternion.identity);
                    break;
                case CurrentObject.RCCarAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.sharedObjectsReference.Asset.crateRCCar, vector, Quaternion.identity);
                    break;
                case CurrentObject.AirStrikeAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.sharedObjectsReference.Asset.crateAirstrike, vector, Quaternion.identity);
                    break;
                case CurrentObject.MechDropAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.sharedObjectsReference.Asset.crateMechDrop, vector, Quaternion.identity);
                    break;
                case CurrentObject.AlienPheromonesAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.sharedObjectsReference.Asset.crateAlienPheromonesDrop, vector, Quaternion.identity);
                    break;
                case CurrentObject.SteroidsAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.sharedObjectsReference.Asset.crateSteroids, vector, Quaternion.identity);
                    break;
                case CurrentObject.PigAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.sharedObjectsReference.Asset.cratePiggy, vector, Quaternion.identity);
                    break;
                case CurrentObject.FlexAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>(Map.Instance.sharedObjectsReference.Asset.cratePerk, vector, Quaternion.identity);
                    break;
                case CurrentObject.BeeHive:
                    placeGroundType = GroundType.Beehive;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.blockBeeHive, vector, Quaternion.identity );
                    break;
                case CurrentObject.AlienEgg:
                    placeGroundType = GroundType.AlienEgg;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.blockAlienEgg, vector, Quaternion.identity );
                    break;
                case CurrentObject.AlienEggExplosive:
                    placeGroundType = GroundType.AlienEggExplosive;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.explosiveAlienEgg, vector, Quaternion.identity );
                    break;
            }
            if (placeGroundType != GroundType.Cage && (placeGroundType != GroundType.AlienFlesh || newBlocks != Map.backGroundBlocks))
            {
                newBlocks[x, y] = currentBlock;
                Traverse groundTypesTrav = trav.Field("groundTypes");
                (groundTypesTrav.GetValue() as GroundType[,])[x, y] = placeGroundType;
            }
            if (currentBlock != null)
            {
                currentBlock.OnSpawned();
                if (currentBlock.groundType == GroundType.Earth && currentBlock.size == 2)
                {
                    Map.SetBlockEmpty(newBlocks[x + 1, y], x + 1, y);
                    newBlocks[x + 1, y] = currentBlock;
                    Map.SetBlockEmpty(newBlocks[x, y - 1], x, y - 1);
                    newBlocks[x, y - 1] = currentBlock;
                    Map.SetBlockEmpty(newBlocks[x + 1, y - 1], x + 1, y - 1);
                    newBlocks[x + 1, y - 1] = currentBlock;
                }
            }
            if (currentBlock != null)
            {
                currentBlock.transform.parent = Map.Instance.transform;
                trav.Field("currentBlock").SetValue(currentBlock);
            }

            return currentBlock;
        }

        static void SpawnBlock(int row, int column)
        {
            Block block = CreateBlock(column, row);
            if (block.groundType != GroundType.Bridge || block.groundType != GroundType.Bridge2 || block.groundType != GroundType.AlienBridge)
            {
                if (row > 0 && Map.blocks[row - 1, column] != null)
                {
                    block.HideLeft();
                }
                if (row < Map.MapData.Width - 1 && Map.blocks[row + 1, column] != null)
                {
                    block.HideRight();
                }
                if (column > 0 && Map.blocks[row, column - 1] != null)
                {
                    block.HideBelow();
                }
                if (column < Map.MapData.Height - 1 && Map.blocks[row, column + 1] != null)
                {
                    block.HideAbove();
                }
            }
            Block aboveBlock = null;
            Block belowBlock = null;
            if (column < Map.MapData.Height - 1 && Map.blocks[row, column + 1] != null)
            {
                aboveBlock = Map.blocks[row, column + 1];
            }
            if (column > 0 && Map.blocks[row, column - 1] != null)
            {
                belowBlock = Map.blocks[row, column - 1];
            }
            block.SetupBlock(column, row, aboveBlock, belowBlock);
            block.RegisterBlockOnNetwork();
            if ( block is DirtBlock )
            {
                DirtBlock dirtBlock = (block as DirtBlock);
                dirtBlock.addDecorations = false;
                dirtBlock.backgroundPrefabs = null;
                dirtBlock.backgroundEdgesPrefabs = null;
            }
            block.FirstFrame();
        }

        static void SpawnDoodad(int row, int column)
        {
            DoodadInfo doodad = new DoodadInfo();
            doodad.position = new GridPoint(column, row);
            doodad.variation = 0;

            GameObject result = null;

            GridPoint gridPoint = new GridPoint(doodad.position.collumn, doodad.position.row);
            gridPoint.collumn -= Map.lastXLoadOffset;
            gridPoint.row -= Map.lastYLoadOffset;

            Vector3 vector = new Vector3((float)(gridPoint.c * 16), (float)(gridPoint.r * 16), 5f);

            if (GameModeController.IsHardcoreMode)
            {
                Map.havePlacedCageForHardcore = true;
                Map.cagesSinceLastHardcoreCage = 0;
            }

            switch ( settings.selectedObject )
            {
                case CurrentObject.RescueCage:
                    result = (UnityEngine.Object.Instantiate<Block>(Map.Instance.activeTheme.blockPrefabCage, vector, Quaternion.identity) as Cage).gameObject;
                    result.GetComponent<Cage>().row = gridPoint.row;
                    result.GetComponent<Cage>().collumn = gridPoint.collumn;
                    break;
            }

            doodad.entity = result;
            result.transform.parent = Map.Instance.transform;
            Block component = result.GetComponent<Block>();
            if (component != null)
            {
                component.OnSpawned();
            }
            Registry.RegisterDeterminsiticGameObject(result.gameObject);
            if (component != null)
            {
                component.FirstFrame();
            }
        }

        static void DetermineLevelsInCampaign()
        {
            Main.settings.campaignNum = campaignNum.indexNumber;

            if (lastCampaignNum != campaignNum.indexNumber)
            {
                int actualCampaignNum = campaignNum.indexNumber + 1;
                int numberOfLevels = 1;
                switch (actualCampaignNum)
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

        public static void GoToLevel()
        {
            // Setup player 1 if they haven't yet joined (like if we're calling this function on the main menu
            if ( !HeroController.IsPlayerPlaying(0) )
            {
                int playerNumber = 0;
                PID pid = PID.MyID;
                string playerName = PlayerOptions.Instance.playerName;
                int controllerJoin = settings.goToLevelControllerNum;

                HeroController.PIDS[playerNumber] = pid;
                HeroController.playerControllerIDs[playerNumber] = controllerJoin;
                HeroController.SetPlayerName( playerNumber, playerName );
                HeroController.SetIsPlaying( playerNumber, true );
            }

            LevelSelectionController.ResetLevelAndGameModeToDefault();
            GameState.Instance.ResetToDefault();

            int campaignIndex = campaignNum.indexNumber;
            int levelIndex = levelNum.indexNumber;

            if (campaignIndex >= 0 && campaignIndex < actualCampaignNames.Length)
            {
                GameState.Instance.campaignName = actualCampaignNames[campaignIndex];
                LevelSelectionController.CurrentLevelNum = levelIndex;
            }
            else
            {
                GameState.Instance.campaignName = actualCampaignNames[0];
                LevelSelectionController.CurrentLevelNum = 0;
            }

            GameState.Instance.loadMode = MapLoadMode.Campaign;
            GameState.Instance.gameMode = GameMode.Campaign;
            GameState.Instance.returnToWorldMap = true;
            GameState.Instance.sceneToLoad = LevelSelectionController.CampaignScene;
            GameState.Instance.sessionID = Connect.GetIncrementedSessionID().AsByte;

            GameModeController.LoadNextScene( GameState.Instance );
        }

        static void ChangeLevel(int levelNum)
        {
            LevelSelectionController.CurrentLevelNum += levelNum;

            Map.ClearSuperCheckpointStatus();
            GameModeController.RestartLevel();
        }

        static void UnlockTerritory(WorldTerritory3D territory)
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
            for (int i = 0; i < Map.checkPoints.Count; ++i)
            {
                if ((bool)Traverse.Create(Map.checkPoints[i]).Field("isFinal").GetValue())
                {
                    return Map.checkPoints[i].transform.position;
                }
            }
            return Map.checkPoints[Map.checkPoints.Count - 1].transform.position;
        }

        public static string GetCurrentLevelKey()
        {
            return $"{GameState.Instance.campaignName}_{GameState.Instance.levelNumber}";
        }

        public static bool HasCustomSpawnForCurrentLevel()
        {
            string key = GetCurrentLevelKey();
            return settings.levelSpawnPositions.ContainsKey(key);
        }

        public static Vector2 GetCustomSpawnForCurrentLevel()
        {
            string key = GetCurrentLevelKey();
            if (settings.levelSpawnPositions.TryGetValue(key, out Vector2 position))
            {
                return position;
            }
            return Vector2.zero;
        }

        public static void SaveCustomSpawnForCurrentLevel(float x, float y)
        {
            string key = GetCurrentLevelKey();
            settings.levelSpawnPositions[key] = new Vector2(x, y);
        }

        public static void ClearCustomSpawnForCurrentLevel()
        {
            string key = GetCurrentLevelKey();
            settings.levelSpawnPositions.Remove(key);
        }
        #endregion
    }
}
