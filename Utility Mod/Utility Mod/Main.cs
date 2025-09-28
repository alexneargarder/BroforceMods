using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RocketLib;
using RocketLib.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;


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

        // Settings Profiles UI variables
        public static int selectedProfileIndex = -1;
        public static string newProfileName = "";
        public static bool isRenamingProfile = false;

        public static string[] levelList = new string[] { "Level 1", "Level 2", "Level 3", "Level 4", "Level 5", "Level 6", "Level 7", "Level 8", "Level 9", "Level 10",
            "Level 11", "Level 12", "Level 13", "Level 14", "Level 15"};

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

        public static string[] campaignDisplayNames = new string[]
        {
            "Campaign 1", "Campaign 2", "Campaign 3", "Campaign 4", "Campaign 5",
            "Campaign 6", "Campaign 7", "Campaign 8", "Campaign 9", "Campaign 10",
            "Campaign 11", "Campaign 12", "Campaign 13", "Campaign 14", "Campaign 15",
            "White House", "Alien Challenge", "Bombardment Challenge", "Ammo Challenge",
            "Dash Challenge", "Mech Challenge", "MacBrover Challenge", "Time Bro Challenge",
            "Muscle Temple 1", "Muscle Temple 2", "Muscle Temple 3", "Muscle Temple 4", "Muscle Temple 5"
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
        private static bool keybindingMode = false;

        #region Keybinds
        // Dictionary to store all keybindings
        private static readonly Dictionary<string, KeyBindingForPlayers> keybindings = new Dictionary<string, KeyBindingForPlayers>();

        // List of all keybinding action names
        private static readonly List<string> keybindingActions = new List<string>
        {
            // Level Controls
            "Previous Level",
            "Next Level",
            "Go to level",
            "Unlock All Levels",
            "Loop Current Level",
            "Restart Current Level",
            
            // Cheat Options
            "Invincibility",
            "Infinite Lives",
            "Infinite Specials",
            "Disable Gravity",
            "Enable Flight",
            "Disable Enemy Spawns",
            "Instant Kill Enemies",
            "Summon Mech",
            "Slow Time",
            
            // Teleport Options
            "Teleport",
            "Teleport to Cursor on Right Click",
            "Spawn at Custom Waypoint",
            "Spawn at Final Checkpoint",
            "Save Position for Custom Spawn",
            "Teleport to Custom Spawn Position",
            "Clear Custom Spawn",
            "Teleport to Current Checkpoint",
            "Teleport to Final Checkpoint",
            "Save Position to Waypoint 1",
            "Save Position to Waypoint 2",
            "Save Position to Waypoint 3",
            "Save Position to Waypoint 4",
            "Save Position to Waypoint 5",
            "Teleport to Waypoint 1",
            "Teleport to Waypoint 2",
            "Teleport to Waypoint 3",
            "Teleport to Waypoint 4",
            "Teleport to Waypoint 5",
            
            // Debug Options
            "Print Audio Played",
            "Suppress Announcer",
            "Max Cage Spawns",
            "Set Zoom Level",
            "Make Cursor Always Visible",
            "Pause/Unpause Game",
            "Increase Game Speed",
            "Decrease Game Speed",
            "Reset Game Speed",
            
            // General Options
            "Skip Intro",
            "Camera Shake",
            "Helicopter Skip",
            "Ending Skip",
            "Speed up Main Menu Loading",
            "Helicopter Wait",
            "Fix Mod Window Disappearing",
            "Disable Broken Cutscenes",
            "Disable All Cutscenes",
            
            // Context Menu Options
            "Quick Clone (Under Cursor)"
        };

        // List to track which keybindings have keys assigned
        private static readonly List<string> activeKeybindings = new List<string>();

        // Dictionary to track which keybindings are currently being assigned
        private static readonly Dictionary<string, bool> keybindingsBeingAssigned = new Dictionary<string, bool>();
        #endregion

        #region UMM
        static bool Load( UnityModManager.ModEntry modEntry )
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUpdate = OnUpdate;
            modEntry.OnLateUpdate = OnLateUpdate;

            mod = modEntry;

            try
            {
                settings = Settings.Load<Settings>( modEntry );

                // Check if settings were actually loaded from file
                // If SettingsVersion is still 0, we got a fresh object (not loaded from file)
                if ( settings.SettingsVersion == 0 )
                {
                    var settingsPath = System.IO.Path.Combine( modEntry.Path, "Settings.xml" );
                    // Settings file exists but couldn't be loaded - attempt recovery
                    settings = RocketLib.Utils.SettingsRecovery.TryRecoverSettings<Settings>( settingsPath, settings );
                }

                // Always ensure version is set to non-zero so it saves properly
                if ( settings.SettingsVersion == 0 )
                    settings.SettingsVersion = 1;
            }
            catch
            {
                // Settings format changed - use recovery
                var settingsPath = System.IO.Path.Combine( modEntry.Path, "Settings.xml" );
                settings = RocketLib.Utils.SettingsRecovery.TryRecoverSettings<Settings>( settingsPath );

                // Always ensure version is set to non-zero
                if ( settings.SettingsVersion == 0 )
                    settings.SettingsVersion = 1;
            }

            try
            {
                var harmony = new Harmony( modEntry.Info.Id );
                var assembly = Assembly.GetExecutingAssembly();
                harmony.PatchAll( assembly );
            }
            catch ( Exception ex )
            {
                Main.Log( ex.ToString() );
            }

            // Load all keybindings
            foreach ( string action in keybindingActions )
            {
                keybindings[action] = AllModKeyBindings.LoadKeyBinding( "Utility Mod", action );
            }

            // Initialize active keybindings list
            activeKeybindings.Clear();
            keybindingsBeingAssigned.Clear();
            foreach ( var kvp in keybindings )
            {
                if ( kvp.Value.HasAnyKeysAssigned() )
                {
                    activeKeybindings.Add( kvp.Key );
                }
            }

            lastCampaignNum = -1;
            campaignNum = new Dropdown( 125, 150, 200, 300, campaignDisplayNames, settings.campaignNum );


            levelNum = new Dropdown( 400, 150, 150, 300, levelList, settings.levelNum );

            controllerDropdown = new Dropdown( 400, 150, 200, 300, controllerList, settings.goToLevelControllerNum );
            controllerDropdown.ToolTip = "Sets which controller controls player 1 when using the Go To Level button, if no players have joined yet";

            // Create GameObject for mouse position display
            GameObject mouseDisplayGO = new GameObject( "UtilityMod_MousePositionDisplay" );
            RocketLibUtils.MakeObjectUnpausable( mouseDisplayGO );
            mouseDisplayGO.AddComponent<MousePositionDisplay>();
            UnityEngine.Object.DontDestroyOnLoad( mouseDisplayGO );

            // Create GameObject for context menu manager
            GameObject contextMenuGO = new GameObject( "UtilityMod_ContextMenuManager" );
            RocketLibUtils.MakeObjectUnpausable( contextMenuGO );
            contextMenuGO.AddComponent<ContextMenuManager>();
            UnityEngine.Object.DontDestroyOnLoad( contextMenuGO );

            // Initialize Unity log capture if enabled
            if ( settings.captureUnityLogs )
            {
                Application.logMessageReceived += OnUnityLogMessageReceived;
                Log( "Unity log capture initialized (enabled in settings)" );
            }

            return true;
        }

        static void OnSaveGUI( UnityModManager.ModEntry modEntry )
        {
            settings.Save( modEntry );
        }

        static bool OnToggle( UnityModManager.ModEntry modEntry, bool value )
        {
            enabled = value;

            // Clean up Unity log capture when mod is disabled
            if ( !value && settings != null && settings.captureUnityLogs )
            {
                Application.logMessageReceived -= OnUnityLogMessageReceived;
                Log( "Unity log capture disabled (mod toggled off)" );
            }

            return true;
        }

        public static void Log( String str )
        {
            if ( mod != null )
            {
                mod.Logger.Log( str );
            }
        }

        private static void OnUnityLogMessageReceived( string condition, string stackTrace, LogType type )
        {
            // Check if we should capture this log type
            if ( !settings.captureUnityLogs ) return;

            bool shouldCapture = false;
            string colorTag = "";
            string colorCloseTag = "";

            switch ( type )
            {
                case LogType.Error:
                case LogType.Exception:
                    shouldCapture = settings.captureUnityErrors;
                    colorTag = "<color=red>";
                    colorCloseTag = "</color>";
                    break;
                case LogType.Warning:
                    shouldCapture = settings.captureUnityWarnings;
                    colorTag = "<color=yellow>";
                    colorCloseTag = "</color>";
                    break;
                case LogType.Log:
                case LogType.Assert:
                    shouldCapture = settings.captureUnityInfo;
                    colorTag = "<color=#00BFFF>"; // DeepSkyBlue for better visibility
                    colorCloseTag = "</color>";
                    break;
            }

            if ( !shouldCapture ) return;

            // Format and log the message with color
            Log( $"[Unity] {colorTag}{condition}{colorCloseTag}" );

            // Include stack trace for errors and exceptions (also colored)
            if ( !string.IsNullOrEmpty( stackTrace ) && ( type == LogType.Error || type == LogType.Exception ) )
            {
                Log( $"[Unity] {colorTag}Stack trace:\n{stackTrace}{colorCloseTag}" );
            }
        }

        static void OnUpdate( UnityModManager.ModEntry modEntry, float dt )
        {
            if ( !enabled ) return;

            // Update current character reference
            if ( HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null )
            {
                currentCharacter = HeroController.players[0].character;
            }
            else
            {
                currentCharacter = null;
            }

            // Check active keybindings
            bool shiftHeld = Input.GetKey( KeyCode.LeftShift ) || Input.GetKey( KeyCode.RightShift );
            foreach ( string action in activeKeybindings )
            {
                if ( keybindings[action].PressedDown( 0 ) )
                {
                    ExecuteAction( action, shiftHeld );
                }
            }

            // Handle instant scene loading
            if ( settings.quickLoadScene && !loadedScene )
            {
                waitForLoad -= dt;
                if ( waitForLoad < 0 )
                {
                    try
                    {
                        if ( !Main.settings.cameraShake )
                        {
                            PlayerOptions.Instance.cameraShakeAmount = 0f;
                        }

                        Utility.SceneLoader.LoadScene( "WorldMap3D" );
                        Utility.SceneLoader.LoadScene( settings.sceneToLoad );
                    }
                    catch { }
                    loadedScene = true;
                }
            }

            // Set zoom level
            levelStartedCounter += dt;
            if ( settings.setZoom )
            {
                try
                {
                    if ( levelStartedCounter > 1.5f && SortOfFollow.zoomLevel != settings.zoomLevel )
                    {
                        SortOfFollow.zoomLevel = settings.zoomLevel;
                    }
                }
                catch { }
            }

            // Suppress announcer
            if ( settings.suppressAnnouncer && Map.MapData != null )
            {
                Map.MapData.suppressAnnouncer = true;
            }

            // Handle flight
            if ( settings.enableFlight )
            {
                try
                {
                    HeroController.players[0].character.speed = 300;
                    if ( HeroController.players[0].character.up )
                    {
                        HeroController.players[0].character.yI = 300;
                    }
                    else if ( HeroController.players[0].character.down )
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
                    else if ( HeroController.players[0].character.left )
                    {
                        HeroController.players[0].character.xI = -300;
                    }
                    else
                    {
                        HeroController.players[0].character.xI = 0;
                    }
                }
                catch { }
            }

            if ( settings.showCursor )
            {
                Cursor.visible = true;
            }
        }

        static void OnLateUpdate( UnityModManager.ModEntry modEntry, float dt )
        {
            if ( settings.showCursor )
            {
                Cursor.visible = true;
            }
        }
        #endregion

        #region UI
        private static GUILayoutOption ScaledWidth( float width )
        {
            if ( settings.scaleUIWithWindowWidth && _windowWidth > 0 )
            {
                float scaleFactor = _windowWidth / 1200f;
                return GUILayout.Width( width * scaleFactor );
            }
            return GUILayout.Width( width );
        }

        static void OnGUI( UnityModManager.ModEntry modEntry )
        {
            // Capture window width for UI scaling
            if ( _windowWidth < 0 )
            {
                try
                {
                    GUILayout.BeginHorizontal();
                    if ( Event.current.type == EventType.Repaint )
                    {
                        Rect rect = GUILayoutUtility.GetRect( 0, 0, GUILayout.ExpandWidth( true ) );
                        if ( rect.width > 1 )
                        {
                            _windowWidth = rect.width;
                        }
                    }
                    GUILayout.Label( " " );
                    GUILayout.EndHorizontal();
                }
                catch ( Exception )
                {
                }
                return;
            }

            string previousToolTip = string.Empty;

            GUIStyle headerStyle = new GUIStyle( GUI.skin.button );

            if ( settings.quickLoadScene )
            {
                if ( loadedScene && HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null )
                {
                    currentCharacter = HeroController.players[0].character;
                }
            }
            else if ( HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null )
            {
                currentCharacter = HeroController.players[0].character;
            }
            else
            {
                currentCharacter = null;
            }

            headerStyle.fontStyle = FontStyle.Bold;
            // 163, 232, 255
            headerStyle.normal.textColor = new Color( 0.639216f, 0.909804f, 1f );


            if ( GUILayout.Button( "General Options", headerStyle ) )
            {
                settings.showGeneralOptions = !settings.showGeneralOptions;
            }

            if ( settings.showGeneralOptions )
            {
                ShowGeneralOptions( modEntry, ref previousToolTip );
            } // End General Options

            if ( GUILayout.Button( "Level Controls", headerStyle ) )
            {
                settings.showLevelOptions = !settings.showLevelOptions;
            }

            if ( settings.showLevelOptions )
            {
                ShowLevelControls( modEntry, ref previousToolTip );
            } // End Level Controls

            if ( GUILayout.Button( "Cheat Options", headerStyle ) )
            {
                settings.showCheatOptions = !settings.showCheatOptions;
            }

            if ( settings.showCheatOptions )
            {
                ShowCheatOptions( modEntry, ref previousToolTip );
            } // End Cheat Options

            if ( GUILayout.Button( "Teleport Options", headerStyle ) )
            {
                settings.showTeleportOptions = !settings.showTeleportOptions;
            }

            if ( settings.showTeleportOptions )
            {
                ShowTeleportOptions( modEntry, ref previousToolTip );
            } // End Teleport Options

            if ( GUILayout.Button( "Debug Options", headerStyle ) )
            {
                settings.showDebugOptions = !settings.showDebugOptions;
            }

            if ( settings.showDebugOptions )
            {
                ShowDebugOptions( modEntry, ref previousToolTip );
            } // End Debug Options

            if ( GUILayout.Button( "Right Click Options", headerStyle ) )
            {
                settings.showRightClickOptions = !settings.showRightClickOptions;
            }

            if ( settings.showRightClickOptions )
            {
                ShowRightClickOptions( modEntry, ref previousToolTip );
            } // End Right Click Options

            if ( GUILayout.Button( "Keybindings", headerStyle ) )
            {
                settings.showKeybindingOptions = !settings.showKeybindingOptions;
            }

            if ( settings.showKeybindingOptions )
            {
                ShowKeybindingOptions( modEntry, ref previousToolTip );
            } // End Keybinding Options

            if ( GUILayout.Button( "Settings Profiles", headerStyle ) )
            {
                settings.showSettingsProfilesOptions = !settings.showSettingsProfilesOptions;
            }

            if ( settings.showSettingsProfilesOptions )
            {
                ShowSettingsProfilesOptions( modEntry, ref previousToolTip );
            } // End Settings Profiles

            // Check for completed keybinding assignments
            List<string> completedAssignments = new List<string>();
            foreach ( var kvp in keybindingsBeingAssigned )
            {
                if ( kvp.Value && !keybindings[kvp.Key][0].isSettingKey )
                {
                    // Assignment completed, check if key was assigned or cancelled
                    if ( keybindings[kvp.Key].HasAnyKeysAssigned() )
                    {
                        if ( !activeKeybindings.Contains( kvp.Key ) )
                        {
                            activeKeybindings.Add( kvp.Key );
                        }
                    }
                    else
                    {
                        activeKeybindings.Remove( kvp.Key );
                    }
                    completedAssignments.Add( kvp.Key );
                }
            }

            // Remove completed assignments from tracking
            foreach ( string action in completedAssignments )
            {
                keybindingsBeingAssigned.Remove( action );
            }
        }

        static void ShowGeneralOptions( UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            if ( keybindingMode )
            {
                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Skip Intro" );
                    ShowKeybindingButton( "Camera Shake" );
                    ShowKeybindingButton( "Helicopter Skip" );
                    ShowKeybindingButton( "Ending Skip" );
                    ShowKeybindingButton( "Speed up Main Menu Loading" );
                    ShowKeybindingButton( "Helicopter Wait" );
                    ShowKeybindingButton( "Fix Mod Window Disappearing" );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Disable Broken Cutscenes" );
                    ShowKeybindingButton( "Disable All Cutscenes" );
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                {
                    settings.skipIntro = GUILayout.Toggle( settings.skipIntro, new GUIContent( "Skip Intro",
                        "Skip intro logos and go straight to main menu" ), ScaledWidth( 75f ) );

                    settings.cameraShake = GUILayout.Toggle( settings.cameraShake, new GUIContent( "Camera Shake",
                        "Disable this to have camera shake automatically set to 0 at the start of a level" ), ScaledWidth( 100f ) );

                    settings.enableSkip = GUILayout.Toggle( settings.enableSkip, new GUIContent( "Helicopter Skip",
                        "Skips helicopter on world map and immediately takes you into a level" ), ScaledWidth( 120f ) );

                    settings.endingSkip = GUILayout.Toggle( settings.endingSkip, new GUIContent( "Ending Skip",
                        "Speeds up the ending" ), ScaledWidth( 100f ) );

                    settings.quickMainMenu = GUILayout.Toggle( settings.quickMainMenu, new GUIContent( "Speed up Main Menu Loading", "Makes menu options show up immediately rather than after the eagle screech" ), ScaledWidth( 190f ) );

                    settings.helicopterWait = GUILayout.Toggle( settings.helicopterWait, new GUIContent( "Helicopter Wait", "Makes helicopter wait for all alive players before leaving" ), ScaledWidth( 110f ) );

                    settings.disableConfirm = GUILayout.Toggle( settings.disableConfirm, new GUIContent( "Disable confirmation menu",
                        "Disables confirmation screen when restarting or returning to map/menu" ), GUILayout.ExpandWidth( false ) );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                GUILayout.BeginHorizontal();
                {
                    settings.skipAllCutscenes = GUILayout.Toggle( settings.skipAllCutscenes, new GUIContent( "Disable All Cutscenes", "Disables all bro unlock, boss fight, and powerup unlock cutscenes." ), ScaledWidth( 170f ) );

                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    lastRect.y += 20;
                    lastRect.width += 800;

                    settings.scaleUIWithWindowWidth = GUILayout.Toggle( settings.scaleUIWithWindowWidth, new GUIContent( "Scale UI with Window Width", "Scales UI elements based on window width" ), ScaledWidth( 200f ) );

                    GUI.Label( lastRect, GUI.tooltip );
                    previousToolTip = GUI.tooltip;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space( 25 );
        }

        static void ShowLevelControls( UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            if ( keybindingMode )
            {
                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Loop Current Level" );
                    GUILayout.Space( 20 );
                    ShowKeybindingButton( "Restart Current Level" );
                    GUILayout.Space( 20 );
                    ShowKeybindingButton( "Unlock All Levels" );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 25 );

                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Go to level" );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 25 );

                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Previous Level" );
                    GUILayout.Space( 20 );
                    ShowKeybindingButton( "Next Level" );
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                bool dropdownActive = false;

                GUILayout.BeginHorizontal();
                {
                    settings.loopCurrent = GUILayout.Toggle( settings.loopCurrent, new GUIContent( "Loop Current Level", "After beating a level you replay the current one instead of moving on" ), GUILayout.ExpandWidth( false ) );

                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    lastRect.y += 20;
                    lastRect.width += 300;

                    if ( GUI.tooltip != previousToolTip )
                    {
                        GUI.Label( lastRect, GUI.tooltip );
                        previousToolTip = GUI.tooltip;
                    }

                    GUILayout.Space( 20 );

                    if ( GUILayout.Button( new GUIContent( "Restart Current Level", "Restarts the current level" ), GUILayout.ExpandWidth( false ) ) )
                    {
                        if ( currentCharacter != null )
                        {
                            Map.ClearSuperCheckpointStatus();
                            GameModeController.RestartLevel();
                        }
                    }

                    GUILayout.Space( 20 );

                    if ( GUILayout.Button( new GUIContent( "Unlock All Levels", "Only works on the world map screen" ), GUILayout.ExpandWidth( false ) ) )
                    {
                        if ( HarmonyPatches.WorldMapController_Update_Patch.instance != null )
                        {
                            WorldTerritory3D[] territories = Traverse.Create( HarmonyPatches.WorldMapController_Update_Patch.instance ).Field( "territories3D" ).GetValue() as WorldTerritory3D[];
                            foreach ( WorldTerritory3D ter in territories )
                            {
                                if ( ter.properties.state != TerritoryState.Liberated && ter.properties.state != TerritoryState.AlienLiberated )
                                {
                                    UnlockTerritory( ter );
                                }
                            }
                        }
                    }

                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 25 );

                GUILayout.BeginHorizontal( new GUILayoutOption[] { GUILayout.MinHeight( ( campaignNum.show || levelNum.show ) ? 350 : 100 ), GUILayout.ExpandWidth( false ) } );
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.BeginHorizontal();
                        {
                            campaignNum.OnGUI( modEntry );

                            DetermineLevelsInCampaign();

                            levelNum.OnGUI( modEntry );

                            Main.settings.levelNum = levelNum.indexNumber;

                            if ( GUILayout.Button( new GUIContent( "Go to level" ), ScaledWidth( 100 ) ) )
                            {
                                GoToLevel( campaignNum.indexNumber, levelNum.indexNumber );
                            }

                            if ( GUI.tooltip != previousToolTip )
                            {
                                Rect lastRect = campaignNum.dropDownRect;
                                lastRect.y += 20;
                                lastRect.width += 500;

                                GUI.Label( lastRect, GUI.tooltip );
                                previousToolTip = GUI.tooltip;
                            }
                        }
                        GUILayout.EndHorizontal();

                        dropdownActive = campaignNum.show || levelNum.show;

                        GUILayout.Space( 25 );

                        GUILayout.BeginHorizontal( new GUILayoutOption[] { GUILayout.MinHeight( ( controllerDropdown.show ) ? 350 : 30 ), GUILayout.ExpandWidth( false ) } );
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
                            GUILayout.Space( 1 );

                            if ( !dropdownActive )
                            {

                                if ( GUILayout.Button( new GUIContent( "Previous Level", "This only works in game" ), new GUILayoutOption[] { ScaledWidth( 150 ), GUILayout.ExpandWidth( false ) } ) )
                                {
                                    ChangeLevel( -1 );
                                }

                                Rect lastRect = GUILayoutUtility.GetLastRect();
                                lastRect.y += 20;
                                lastRect.width += 500;

                                if ( GUILayout.Button( new GUIContent( "Next Level", "This only works in game" ), new GUILayoutOption[] { ScaledWidth( 150 ), GUILayout.ExpandWidth( false ) } ) )
                                {
                                    ChangeLevel( 1 );
                                }

                                if ( GUI.tooltip != previousToolTip )
                                {
                                    GUI.Label( lastRect, GUI.tooltip );
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

            GUILayout.Space( 15 );
        }

        static void ShowCheatOptions( UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            if ( keybindingMode )
            {
                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Invincibility" );
                    ShowKeybindingButton( "Infinite Lives" );
                    ShowKeybindingButton( "Infinite Specials" );
                    ShowKeybindingButton( "Disable Gravity" );
                    ShowKeybindingButton( "Enable Flight" );
                    ShowKeybindingButton( "Disable Enemy Spawns" );
                    ShowKeybindingButton( "Instant Kill Enemies" );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                ShowKeybindingButton( "Summon Mech" );

                GUILayout.Space( 25 );

                ShowKeybindingButton( "Slow Time" );
            }
            else
            {
                GUILayout.BeginHorizontal();
                {
                    if ( settings.invulnerable != ( settings.invulnerable = GUILayout.Toggle( settings.invulnerable, "Invincibility" ) ) )
                    {
                        if ( settings.invulnerable && currentCharacter != null )
                        {
                            currentCharacter.SetInvulnerable( float.MaxValue, false );
                        }
                        else if ( currentCharacter != null )
                        {
                            currentCharacter.SetInvulnerable( 0, false );
                        }
                    }

                    GUILayout.Space( 15 );

                    if ( settings.infiniteLives != ( settings.infiniteLives = GUILayout.Toggle( settings.infiniteLives, "Infinite Lives" ) ) )
                    {
                        if ( currentCharacter != null )
                        {
                            if ( settings.infiniteLives )
                            {
                                for ( int i = 0; i < 4; ++i )
                                {
                                    HeroController.SetLives( i, int.MaxValue );
                                }
                            }
                            else
                            {
                                for ( int i = 0; i < 4; ++i )
                                {
                                    HeroController.SetLives( i, 1 );
                                }
                            }
                        }
                    }

                    GUILayout.Space( 15 );

                    settings.infiniteSpecials = GUILayout.Toggle( settings.infiniteSpecials, "Infinite Specials" );

                    GUILayout.Space( 15 );

                    settings.disableGravity = GUILayout.Toggle( settings.disableGravity, "Disable Gravity" );

                    GUILayout.Space( 15 );

                    settings.enableFlight = GUILayout.Toggle( settings.enableFlight, "Enable Flight" );

                    GUILayout.Space( 15 );

                    settings.disableEnemySpawn = GUILayout.Toggle( settings.disableEnemySpawn, "Disable Enemy Spawns" );

                    GUILayout.Space( 10 );

                    settings.oneHitEnemies = GUILayout.Toggle( settings.oneHitEnemies, new GUIContent( "Instant Kill Enemies", "Sets all enemies to one health" ) );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                if ( GUILayout.Button( "Summon Mech", ScaledWidth( 140 ) ) )
                {
                    if ( currentCharacter != null )
                    {
                        ProjectileController.SpawnGrenadeOverNetwork( ProjectileController.GetMechDropGrenadePrefab(), currentCharacter, currentCharacter.X + Mathf.Sign( currentCharacter.transform.localScale.x ) * 8f, currentCharacter.Y + 8f, 0.001f, 0.011f, Mathf.Sign( currentCharacter.transform.localScale.x ) * 200f, 150f, currentCharacter.playerNum );
                    }
                }

                Rect lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 300;
                if ( GUI.tooltip != previousToolTip )
                {
                    GUI.Label( lastRect, GUI.tooltip );
                    previousToolTip = GUI.tooltip;
                }

                GUILayout.Space( 25 );

                GUILayout.BeginHorizontal( ScaledWidth( 400 ) );

                GUILayout.Label( "Time Slow Factor: " + settings.slowTimeFactor );

                if ( settings.slowTimeFactor != ( settings.slowTimeFactor = GUILayout.HorizontalSlider( settings.slowTimeFactor, 0, 5, ScaledWidth( 200 ) ) ) )
                {
                    Main.StartTimeSlow();
                }

                GUILayout.EndHorizontal();

                if ( settings.slowTime != ( settings.slowTime = GUILayout.Toggle( settings.slowTime, "Slow Time" ) ) )
                {
                    if ( settings.slowTime )
                    {
                        StartTimeSlow();
                    }
                    else
                    {
                        StopTimeSlow();
                    }
                }

                GUILayout.Space( 25 );

                GUILayout.BeginHorizontal( ScaledWidth( 500 ) );

                GUILayout.Label( "Scene to Load: " );

                settings.sceneToLoad = GUILayout.TextField( settings.sceneToLoad, ScaledWidth( 200 ) );

                GUILayout.EndHorizontal();

                settings.quickLoadScene = GUILayout.Toggle( settings.quickLoadScene, "Immediately load chosen scene", GUILayout.Width( 200 ) );

                GUILayout.Space( 10 );

                GUILayout.BeginHorizontal();

                if ( GUILayout.Button( "Load Current Scene", GUILayout.Width( 200 ) ) )
                {
                    if ( !Main.settings.cameraShake )
                    {
                        PlayerOptions.Instance.cameraShakeAmount = 0f;
                    }

                    Utility.SceneLoader.LoadScene( settings.sceneToLoad );
                }

                if ( GUILayout.Button( "Get Current Scene", GUILayout.Width( 200 ) ) )
                {
                    for ( int i = 0; i < SceneManager.sceneCount; ++i )
                    {
                        Main.Log( "Scene Name: " + SceneManager.GetSceneAt( i ).name );
                    }
                }

                GUILayout.EndHorizontal();

                GUILayout.Space( 25 );
            }
        }

        static void ShowTeleportOptions( UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            if ( keybindingMode )
            {
                ShowKeybindingButton( "Teleport" );

                GUILayout.Space( 15 );

                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Spawn at Custom Waypoint" );
                    ShowKeybindingButton( "Spawn at Final Checkpoint" );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Save Position for Custom Spawn" );
                    ShowKeybindingButton( "Teleport to Custom Spawn Position" );
                    ShowKeybindingButton( "Clear Custom Spawn" );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Teleport to Current Checkpoint" );
                    ShowKeybindingButton( "Teleport to Final Checkpoint" );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                for ( int i = 0; i < 5; ++i )
                {
                    GUILayout.BeginHorizontal();
                    {
                        ShowKeybindingButton( "Save Position to Waypoint " + ( i + 1 ) );
                        ShowKeybindingButton( "Teleport to Waypoint " + ( i + 1 ) );
                    }
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                {
                    if ( currentCharacter != null )
                    {
                        GUILayout.Label( "Position: " + currentCharacter.X.ToString( "0.00" ) + ", " + currentCharacter.Y.ToString( "0.00" ) );
                    }
                    else
                    {
                        GUILayout.Label( "Position: " );
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label( "X", GUILayout.Width( 10 ) );
                    teleportX = GUILayout.TextField( teleportX, GUILayout.Width( 100 ) );
                    GUILayout.Space( 20 );
                    GUILayout.Label( "Y", ScaledWidth( 10 ) );
                    GUILayout.Space( 10 );
                    teleportY = GUILayout.TextField( teleportY, ScaledWidth( 100 ) );

                    if ( GUILayout.Button( "Teleport", ScaledWidth( 100 ) ) )
                    {
                        float x, y;
                        if ( float.TryParse( teleportX, out x ) && float.TryParse( teleportY, out y ) )
                        {
                            if ( currentCharacter != null )
                            {
                                TeleportToCoords( x, y );
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                GUILayout.BeginHorizontal( ScaledWidth( 400 ) );

                bool newChangeSpawn = GUILayout.Toggle( settings.changeSpawn, "Spawn at Custom Waypoint" );
                if ( newChangeSpawn != settings.changeSpawn )
                {
                    settings.changeSpawn = newChangeSpawn;
                    // If enabling this option, disable the other
                    if ( settings.changeSpawn )
                    {
                        settings.changeSpawnFinal = false;
                    }
                }

                GUILayout.Space( 10 );

                bool newChangeSpawnFinal = GUILayout.Toggle( settings.changeSpawnFinal, "Spawn at Final Checkpoint" );
                if ( newChangeSpawnFinal != settings.changeSpawnFinal )
                {
                    settings.changeSpawnFinal = newChangeSpawnFinal;
                    // If enabling this option, disable the other
                    if ( settings.changeSpawnFinal )
                    {
                        settings.changeSpawn = false;
                    }
                }

                GUILayout.EndHorizontal();

                GUILayout.Space( 10 );

                GUILayout.BeginHorizontal();
                {
                    if ( GUILayout.Button( "Save Position for Custom Spawn", ScaledWidth( 250 ) ) )
                    {
                        if ( currentCharacter != null && GameState.Instance != null )
                        {
                            SaveCustomSpawnForCurrentLevel( currentCharacter.X, currentCharacter.Y );
                        }
                    }

                    if ( GUILayout.Button( "Teleport to Custom Spawn Position", ScaledWidth( 300 ) ) )
                    {
                        if ( currentCharacter != null && HasCustomSpawnForCurrentLevel() )
                        {
                            Vector2 spawn = GetCustomSpawnForCurrentLevel();
                            TeleportToCoords( spawn.x, spawn.y );
                        }
                    }

                    if ( GUILayout.Button( "Clear Custom Spawn", ScaledWidth( 150 ) ) )
                    {
                        if ( GameState.Instance != null )
                        {
                            ClearCustomSpawnForCurrentLevel();
                        }
                    }

                    if ( HasCustomSpawnForCurrentLevel() )
                    {
                        Vector2 spawn = GetCustomSpawnForCurrentLevel();
                        GUILayout.Label( $"Saved position: {spawn.x:F2}, {spawn.y:F2}" );
                    }
                    else
                    {
                        GUILayout.Label( "Saved position: None" );
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 15 );

                GUILayout.BeginHorizontal();
                {
                    if ( GUILayout.Button( "Teleport to Current Checkpoint ", ScaledWidth( 250 ) ) )
                    {
                        if ( currentCharacter != null )
                        {
                            Vector3 checkPoint = HeroController.GetCheckPointPosition( 0, Map.IsCheckPointAnAirdrop( HeroController.GetCurrentCheckPointID() ) );
                            TeleportToCoords( checkPoint.x, checkPoint.y );
                        }
                    }

                    if ( GUILayout.Button( "Teleport to Final Checkpoint", ScaledWidth( 200 ) ) )
                    {
                        if ( currentCharacter != null )
                        {
                            Vector3 checkPoint = GetFinalCheckpointPos();
                            TeleportToCoords( checkPoint.x, checkPoint.y );
                        }
                    }
                }
                GUILayout.EndHorizontal();

                for ( int i = 0; i < settings.waypointsX.Length; ++i )
                {
                    GUILayout.BeginHorizontal();
                    {
                        if ( GUILayout.Button( "Save Position to Waypoint " + ( i + 1 ), ScaledWidth( 250 ) ) )
                        {
                            if ( currentCharacter != null )
                            {
                                settings.waypointsX[i] = currentCharacter.X;
                                settings.waypointsY[i] = currentCharacter.Y;
                            }
                        }

                        if ( GUILayout.Button( "Teleport to Waypoint " + ( i + 1 ), ScaledWidth( 200 ) ) )
                        {
                            if ( currentCharacter != null )
                            {
                                TeleportToCoords( settings.waypointsX[i], settings.waypointsY[i] );
                            }
                        }

                        GUILayout.Label( "Saved position: " + settings.waypointsX[i] + ", " + settings.waypointsY[i] );
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        static void ShowDebugOptions( UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            if ( keybindingMode )
            {
                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Print Audio Played" );
                    ShowKeybindingButton( "Suppress Announcer" );
                    ShowKeybindingButton( "Max Cage Spawns" );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space( 30 );

                ShowKeybindingButton( "Set Zoom Level" );

                GUILayout.Space( 30 );

                GUILayout.BeginHorizontal();
                {
                    ShowKeybindingButton( "Make Cursor Always Visible" );
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();

                settings.printAudioPlayed = GUILayout.Toggle( settings.printAudioPlayed, new GUIContent( "Print Audio Played", "Prints the name of the Audio Clip to the Log" ) );

                GUILayout.EndHorizontal();


                GUILayout.Space( 10 );


                GUILayout.BeginHorizontal();

                settings.suppressAnnouncer = GUILayout.Toggle( settings.suppressAnnouncer, new GUIContent( "Suppress Announcer", "Disables the Countdown at the start of levels" ) );

                GUILayout.EndHorizontal();


                GUILayout.Space( 10 );


                GUILayout.BeginHorizontal();

                settings.maxCageSpawns = GUILayout.Toggle( settings.maxCageSpawns, new GUIContent( "Max Cage Spawns", "Forces every cage that spawns on the map to contain a prisoner" ) );

                GUILayout.EndHorizontal();

                GUILayout.Space( 10 );

                GUILayout.BeginHorizontal();

                settings.showMousePosition = GUILayout.Toggle( settings.showMousePosition, new GUIContent( "Show Mouse Position", "Displays the world coordinates of the mouse cursor on screen" ) );

                Rect lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;
                if ( GUI.tooltip != previousToolTip )
                {
                    GUI.Label( lastRect, GUI.tooltip );
                    previousToolTip = GUI.tooltip;
                }

                GUILayout.EndHorizontal();

                GUILayout.Space( 30 );


                if ( settings.setZoom != ( settings.setZoom = GUILayout.Toggle( settings.setZoom, new GUIContent( "Set Zoom Level", "Overrides the default zoom level of the camera" ) ) ) )
                {
                    if ( !settings.setZoom )
                    {
                        SortOfFollow.zoomLevel = 1;
                    }
                }

                GUILayout.Space( 10 );

                GUILayout.BeginHorizontal();

                GUILayout.Label( settings.zoomLevel.ToString( "0.00" ), ScaledWidth( 100 ) );

                settings.zoomLevel = GUILayout.HorizontalSlider( settings.zoomLevel, 0, 10 );

                GUILayout.Space( 10 );

                GUILayout.EndHorizontal();

                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 600;
                if ( GUI.tooltip != previousToolTip )
                {
                    GUI.Label( lastRect, GUI.tooltip );
                    previousToolTip = GUI.tooltip;
                }

                GUILayout.Space( 20 );

                // Unity Log Capture Options
                bool previousCaptureState = settings.captureUnityLogs;
                settings.captureUnityLogs = GUILayout.Toggle( settings.captureUnityLogs,
                    new GUIContent( "Capture Unity Logs", "Captures Unity's Debug.Log output, warnings, and errors to UnityModManager's log file. Logs are color-coded: red for errors, yellow for warnings, blue for info." ) );

                lastRect = GUILayoutUtility.GetLastRect();

                // Handle enabling/disabling the log capture
                if ( previousCaptureState != settings.captureUnityLogs )
                {
                    if ( settings.captureUnityLogs )
                    {
                        Application.logMessageReceived += OnUnityLogMessageReceived;
                        Log( "Unity log capture enabled" );
                    }
                    else
                    {
                        Application.logMessageReceived -= OnUnityLogMessageReceived;
                        Log( "Unity log capture disabled" );
                    }
                }

                if ( settings.captureUnityLogs )
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space( 20 );
                    GUILayout.BeginVertical();

                    settings.captureUnityErrors = GUILayout.Toggle( settings.captureUnityErrors, "Capture Errors" );
                    settings.captureUnityWarnings = GUILayout.Toggle( settings.captureUnityWarnings, "Capture Warnings" );
                    settings.captureUnityInfo = GUILayout.Toggle( settings.captureUnityInfo, "Capture Info/Log Messages" );
                    lastRect = GUILayoutUtility.GetLastRect();

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }

                lastRect.y += 25;
                lastRect.width += 600;
                if ( GUI.tooltip != previousToolTip )
                {
                    GUI.Label( lastRect, GUI.tooltip );
                    previousToolTip = GUI.tooltip;
                }

                GUILayout.Space( 35 );
            }

            GUI.tooltip = string.Empty;


            GUILayout.Space( 10 );

            GUILayout.BeginHorizontal();
            string speedText = settings.isGamePaused ? "Game Speed: PAUSED" : $"Game Speed: {settings.gameSpeedMultiplier:F2}x";
            GUILayout.Label( speedText, ScaledWidth( 200 ) );
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            GUILayout.BeginHorizontal();
            GUILayout.Label( $"Step Size: {settings.gameSpeedStep:F2}", ScaledWidth( 150 ) );
            float newStep = GUILayout.HorizontalSlider( settings.gameSpeedStep, 0.01f, 0.50f, ScaledWidth( 200 ) );
            // Round to nearest 0.01
            settings.gameSpeedStep = Mathf.Round( newStep * 100f ) / 100f;
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            GUILayout.Label( "Time Control Keybindings:" );
            GUILayout.Space( 5 );

            string nothing = "";

            GUILayout.BeginHorizontal( GUILayout.ExpandWidth( false ) );
            if ( keybindings["Pause/Unpause Game"].OnGUI( out _, true, true, ref nothing, 0, true, false, false ) )
            {
                keybindingsBeingAssigned["Pause/Unpause Game"] = true;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space( 20 );

            GUILayout.BeginHorizontal( GUILayout.ExpandWidth( false ) );
            if ( keybindings["Decrease Game Speed"].OnGUI( out _, true, true, ref nothing, 0, true, false, false ) )
            {
                keybindingsBeingAssigned["Decrease Game Speed"] = true;
            }
            GUILayout.Space( 10 );
            if ( keybindings["Increase Game Speed"].OnGUI( out _, true, true, ref nothing, 0, true, false, false ) )
            {
                keybindingsBeingAssigned["Increase Game Speed"] = true;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space( 20 );

            GUILayout.BeginHorizontal( GUILayout.ExpandWidth( false ) );
            if ( keybindings["Reset Game Speed"].OnGUI( out _, true, true, ref nothing, 0, true, false, false ) )
            {
                keybindingsBeingAssigned["Reset Game Speed"] = true;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space( 25 );
            GUILayout.Label( "Hold Shift while pressing Increase/Decrease for 5x step size" );
        }

        static void ShowRightClickOptions( UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            GUIStyle headerStyle = new GUIStyle( GUI.skin.label );
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color( 0.639216f, 0.909804f, 1f );

            settings.showCursor = GUILayout.Toggle( settings.showCursor, "Make Cursor Always Visible" );

            GUILayout.Space( 10 );

            settings.contextMenuEnabled = GUILayout.Toggle( settings.contextMenuEnabled,
                new GUIContent( "Enable Right Click Menu", "Enables the context menu system. Right-click in-game to open menus with various actions." ),
                ScaledWidth( 200 ) );

            // Tooltip for context menu mode
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 25;
            lastRect.width += 700;
            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label( lastRect, GUI.tooltip );
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space( 35 );

            if ( !settings.contextMenuEnabled )
            {
                return;
            }

            GUILayout.Label( "General Options", headerStyle );

            // Add Help button
            GUILayout.BeginHorizontal();
            if ( GUILayout.Button( new GUIContent( "Show Help", "View keyboard shortcuts and usage instructions" ), ScaledWidth( 100 ) ) )
            {
                var contextMenuManager = UnityEngine.Object.FindObjectOfType<ContextMenuManager>();
                if ( contextMenuManager != null )
                {
                    contextMenuManager.ShowHelpMenu();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Hold Duration:", "How long to hold right-click before the menu opens (when no quick action is set)" ), ScaledWidth( 150 ) );
            GUILayout.Label( settings.contextMenuHoldDuration.ToString( "0.00" ) + "s", ScaledWidth( 50 ) );
            settings.contextMenuHoldDuration = GUILayout.HorizontalSlider( settings.contextMenuHoldDuration, 0.1f, 1.0f, ScaledWidth( 200 ) );
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            // Show Hold Progress Indicator
            GUILayout.BeginHorizontal();
            settings.showHoldProgressIndicator = GUILayout.Toggle( settings.showHoldProgressIndicator,
                new GUIContent( "Show Hold Progress Indicator", "Shows a circular progress indicator when holding right-click" ) );
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            // Recent Items
            GUILayout.BeginHorizontal();
            settings.enableRecentItems = GUILayout.Toggle( settings.enableRecentItems,
                new GUIContent( "Enable Recent Items", "Shows recently used items at the top of menus" ) );
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );
            if ( settings.enableRecentItems )
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label( new GUIContent( "Max Recent Items:", "Number of recently used actions to show at the top of context menus" ), ScaledWidth( 150 ) );
                GUILayout.Label( settings.maxRecentItems.ToString(), ScaledWidth( 30 ) );
                settings.maxRecentItems = (int)GUILayout.HorizontalSlider( settings.maxRecentItems, 1, 10, ScaledWidth( 150 ) );
                GUILayout.EndHorizontal();
            }

            GUILayout.Space( 10 );

            // Quick Clone keybinding
            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Quick Clone:", "Press this key to instantly clone the object (enemy or block) under your cursor. Press again to exit." ), ScaledWidth( 100 ) );
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 25;
            lastRect.width += 700;
            ShowKeybindingButton( "Quick Clone (Under Cursor)" );
            GUILayout.EndHorizontal();

            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label( lastRect, GUI.tooltip );
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space( 35 );

            // Paint Mode Options
            GUILayout.Label( "Paint Mode Options", headerStyle );
            GUILayout.Space( 10 );

            GUILayout.Label( new GUIContent( "Paint Mode Type:", "Hold Shift+Right-click and drag to continuously spawn enemies" ), GUI.skin.label );
            settings.enemyPaintMode = (EnemyPaintMode)GUILayout.SelectionGrid(
                (int)settings.enemyPaintMode,
                new GUIContent[] { new GUIContent( "Time-based", "Spawns enemies at regular time intervals while dragging" ), new GUIContent( "Distance-based", "Spawns enemies only when you've moved a certain distance" ) },
                2,
                ScaledWidth( 300 ) );
            GUILayout.Space( 5 );

            if ( settings.enemyPaintMode == EnemyPaintMode.TimeBased )
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label( new GUIContent( "Enemy Spawn Delay:", "Time between enemy spawns in paint mode" ), ScaledWidth( 150 ) );
                GUILayout.Label( settings.enemyPaintDelay.ToString( "0.0" ) + "s", ScaledWidth( 50 ) );
                settings.enemyPaintDelay = GUILayout.HorizontalSlider( settings.enemyPaintDelay, 0.1f, 2.0f, ScaledWidth( 200 ) );
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label( new GUIContent( "Enemy Spawn Distance:", "Distance in blocks you must move before spawning another enemy" ), ScaledWidth( 150 ) );
                GUILayout.Label( settings.enemyPaintDistance.ToString( "0" ) + " blocks", ScaledWidth( 80 ) );
                settings.enemyPaintDistance = Mathf.Round( GUILayout.HorizontalSlider( settings.enemyPaintDistance, 1f, 5f, ScaledWidth( 200 ) ) );
                GUILayout.EndHorizontal();
            }

            // Block/Doodad spawn distance
            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Block/Doodad Spawn Distance:", "Distance in blocks you must move before placing another block or doodad in paint mode" ), ScaledWidth( 200 ) );
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 25;
            lastRect.width += 500;
            GUILayout.Label( settings.blockPaintDistance.ToString( "0" ) + " blocks", ScaledWidth( 80 ) );
            settings.blockPaintDistance = Mathf.Round( GUILayout.HorizontalSlider( settings.blockPaintDistance, 1f, 5f, ScaledWidth( 200 ) ) );
            GUILayout.EndHorizontal();

            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label( lastRect, GUI.tooltip );
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space( 40 );

            // Style Customization Section
            GUILayout.Label( "Context Menu Style", headerStyle );
            GUILayout.Space( 10 );

            // Background Color
            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Background Color:", "RGB color of the context menu background" ), ScaledWidth( 150 ) );
            GUILayout.Label( "R:", ScaledWidth( 20 ) );
            settings.menuBackgroundR = GUILayout.HorizontalSlider( settings.menuBackgroundR, 0f, 1f, ScaledWidth( 80 ) );
            GUILayout.Label( "G:", ScaledWidth( 20 ) );
            settings.menuBackgroundG = GUILayout.HorizontalSlider( settings.menuBackgroundG, 0f, 1f, ScaledWidth( 80 ) );
            GUILayout.Label( "B:", ScaledWidth( 20 ) );
            settings.menuBackgroundB = GUILayout.HorizontalSlider( settings.menuBackgroundB, 0f, 1f, ScaledWidth( 80 ) );
            GUILayout.EndHorizontal();

            // Background Alpha
            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Background Transparency:", "How opaque the menu background is (100% = fully opaque)" ), ScaledWidth( 150 ) );
            GUILayout.Label( ( settings.menuBackgroundAlpha * 100 ).ToString( "0" ) + "%", ScaledWidth( 50 ) );
            settings.menuBackgroundAlpha = GUILayout.HorizontalSlider( settings.menuBackgroundAlpha, 0f, 1f, ScaledWidth( 200 ) );
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            // Text Color
            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Text Color:", "RGB color of the menu text" ), ScaledWidth( 150 ) );
            GUILayout.Label( "R:", ScaledWidth( 20 ) );
            settings.menuTextR = GUILayout.HorizontalSlider( settings.menuTextR, 0f, 1f, ScaledWidth( 80 ) );
            GUILayout.Label( "G:", ScaledWidth( 20 ) );
            settings.menuTextG = GUILayout.HorizontalSlider( settings.menuTextG, 0f, 1f, ScaledWidth( 80 ) );
            GUILayout.Label( "B:", ScaledWidth( 20 ) );
            settings.menuTextB = GUILayout.HorizontalSlider( settings.menuTextB, 0f, 1f, ScaledWidth( 80 ) );
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            // Highlight Color
            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Highlight Color:", "RGB color when hovering over menu items" ), ScaledWidth( 150 ) );
            GUILayout.Label( "R:", ScaledWidth( 20 ) );
            settings.menuHighlightR = GUILayout.HorizontalSlider( settings.menuHighlightR, 0f, 1f, ScaledWidth( 80 ) );
            GUILayout.Label( "G:", ScaledWidth( 20 ) );
            settings.menuHighlightG = GUILayout.HorizontalSlider( settings.menuHighlightG, 0f, 1f, ScaledWidth( 80 ) );
            GUILayout.Label( "B:", ScaledWidth( 20 ) );
            settings.menuHighlightB = GUILayout.HorizontalSlider( settings.menuHighlightB, 0f, 1f, ScaledWidth( 80 ) );
            GUILayout.EndHorizontal();

            // Highlight Alpha
            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Highlight Transparency:", "How opaque the hover highlight is (100% = fully opaque)" ), ScaledWidth( 150 ) );
            GUILayout.Label( ( settings.menuHighlightAlpha * 100 ).ToString( "0" ) + "%", ScaledWidth( 50 ) );
            settings.menuHighlightAlpha = GUILayout.HorizontalSlider( settings.menuHighlightAlpha, 0f, 1f, ScaledWidth( 200 ) );
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            // Font Size
            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Font Size:", "Size of text in context menus" ), ScaledWidth( 150 ) );
            GUILayout.Label( settings.menuFontSize.ToString(), ScaledWidth( 30 ) );
            settings.menuFontSize = (int)GUILayout.HorizontalSlider( settings.menuFontSize, 12, 24, ScaledWidth( 200 ) );
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            // Reset to Default Button
            if ( GUILayout.Button( new GUIContent( "Reset to Default Style", "Restore all style settings to their default values" ), ScaledWidth( 200 ) ) )
            {
                settings.menuBackgroundR = 0.1f;
                settings.menuBackgroundG = 0.1f;
                settings.menuBackgroundB = 0.1f;
                settings.menuBackgroundAlpha = 0.9f;
                settings.menuTextR = 1.0f;
                settings.menuTextG = 1.0f;
                settings.menuTextB = 1.0f;
                settings.menuHighlightR = 0.2f;
                settings.menuHighlightG = 0.3f;
                settings.menuHighlightB = 0.4f;
                settings.menuHighlightAlpha = 0.9f;
                settings.menuFontSize = 16;
            }

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 25;
            lastRect.width += 700;

            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label( lastRect, GUI.tooltip );
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space( 35 );

            // Level Edit Recording Section
            GUILayout.Label( "Level Edit Recording", headerStyle );
            GUILayout.Space( 10 );

            // Auto-replay toggle
            settings.enableLevelEditReplay = GUILayout.Toggle( settings.enableLevelEditReplay,
                new GUIContent( "Enable Auto-Replay", "Automatically replay saved level edits when levels load" ) );

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 25;
            lastRect.width += 700;

            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label( lastRect, GUI.tooltip );
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space( 30 );
        }

        static void ShowKeybindingOptions( UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            GUILayout.BeginVertical();
            {
                if ( GUILayout.Button( keybindingMode ? "Exit Keybinding Mode" : "Enter Keybinding Mode", ScaledWidth( 200 ) ) )
                {
                    keybindingMode = !keybindingMode;
                }

                if ( keybindingMode )
                {
                    GUILayout.Space( 20 );
                    GUILayout.Label( "Click on any action button to set its keybinding." );
                }
            }
            GUILayout.EndVertical();
            GUILayout.Space( 10 );
        }

        static void ShowSettingsProfilesOptions( UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            GUILayout.BeginHorizontal();
            string currentProfile = string.IsNullOrEmpty( settings.lastLoadedProfileName )
                ? "None"
                : settings.lastLoadedProfileName;
            GUILayout.Label( new GUIContent( $"Current Profile: {currentProfile}",
                "The last profile that was loaded. Settings may have been modified since." ) );

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 400;

            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label( lastRect, GUI.tooltip );
                previousToolTip = GUI.tooltip;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space( 25 );

            var profiles = settings.GetAvailableProfiles();
            if ( profiles.Count > 0 )
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label( "Saved Profiles:" );
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                selectedProfileIndex = GUILayout.SelectionGrid(
                    selectedProfileIndex,
                    profiles.ToArray(),
                    1,
                    GUILayout.Width( 300 )
                );
                GUILayout.EndHorizontal();

                GUILayout.Space( 10 );

                GUILayout.BeginHorizontal();

                if ( GUILayout.Button( new GUIContent( "Load Selected", "Replace current settings with the selected profile" ), GUILayout.Width( 140 ) ) )
                {
                    if ( selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count )
                    {
                        settings.LoadFromProfile( profiles[selectedProfileIndex] );
                    }
                }

                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 400;

                if ( GUILayout.Button( new GUIContent( "Save Current", "Overwrite the selected profile with current settings" ), GUILayout.Width( 140 ) ) )
                {
                    if ( selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count )
                    {
                        settings.SaveToProfile( profiles[selectedProfileIndex] );
                    }
                }

                if ( GUILayout.Button( new GUIContent( "Rename", "Rename the selected profile" ), GUILayout.Width( 110 ) ) )
                {
                    if ( selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count )
                    {
                        newProfileName = profiles[selectedProfileIndex];
                        isRenamingProfile = true;
                    }
                }

                if ( GUILayout.Button( new GUIContent( "Delete", "Delete the selected profile" ), GUILayout.Width( 100 ) ) )
                {
                    if ( selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count )
                    {
                        settings.DeleteProfile( profiles[selectedProfileIndex] );
                        selectedProfileIndex = -1;
                    }
                }

                if ( GUI.tooltip != previousToolTip )
                {
                    GUI.Label( lastRect, GUI.tooltip );
                    previousToolTip = GUI.tooltip;
                }

                GUILayout.EndHorizontal();

                GUILayout.Space( 25 );
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label( "No saved profiles yet." );
                GUILayout.EndHorizontal();
                GUILayout.Space( 10 );
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label( "New Profile Name:", GUILayout.Width( 140 ) );

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 400;

            newProfileName = GUILayout.TextField( newProfileName, GUILayout.Width( 200 ) );

            bool validName = !string.IsNullOrEmpty( newProfileName ) &&
                             ValidateProfileName( newProfileName );

            GUI.enabled = validName;

            if ( isRenamingProfile )
            {
                if ( GUILayout.Button( new GUIContent( "Apply Rename", "Rename the selected profile" ), GUILayout.Width( 150 ) ) )
                {
                    if ( selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count )
                    {
                        string oldName = profiles[selectedProfileIndex];
                        string oldPath = System.IO.Path.Combine( System.IO.Path.Combine( mod.Path, "Profiles" ), oldName + ".xml" );
                        string newPath = System.IO.Path.Combine( System.IO.Path.Combine( mod.Path, "Profiles" ), newProfileName + ".xml" );

                        if ( System.IO.File.Exists( oldPath ) && !System.IO.File.Exists( newPath ) )
                        {
                            System.IO.File.Move( oldPath, newPath );
                            if ( settings.lastLoadedProfileName == oldName )
                            {
                                settings.lastLoadedProfileName = newProfileName;
                                settings.Save( mod );
                            }
                        }
                    }
                    newProfileName = "";
                    isRenamingProfile = false;  // Exit rename mode
                }

                GUI.enabled = true;

                if ( GUILayout.Button( new GUIContent( "Cancel", "Cancel rename" ), GUILayout.Width( 80 ) ) )
                {
                    newProfileName = "";
                    isRenamingProfile = false;  // Exit rename mode
                }
            }
            else
            {
                if ( GUILayout.Button( new GUIContent( "Save As New", "Create a new profile with the current settings" ), GUILayout.Width( 150 ) ) )
                {
                    settings.SaveToProfile( newProfileName );
                    selectedProfileIndex = profiles.Count;
                    newProfileName = "";
                }
            }

            GUI.enabled = true;

            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label( lastRect, GUI.tooltip );
                previousToolTip = GUI.tooltip;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space( 25 );

            GUILayout.BeginHorizontal();
            GUILayout.Label( new GUIContent( "Import/Export:", "Share profiles via clipboard" ) );
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUI.enabled = selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count;
            if ( GUILayout.Button( new GUIContent( "Export to Clipboard", "Copy selected profile to clipboard as Base64" ), GUILayout.Width( 220 ) ) )
            {
                ExportProfileToClipboard( profiles[selectedProfileIndex] );
            }

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 400;

            GUI.enabled = true;

            if ( GUILayout.Button( new GUIContent( "Import from Clipboard", "Import a profile from Base64 in clipboard" ), GUILayout.Width( 220 ) ) )
            {
                ImportProfileFromClipboard();
            }

            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label( lastRect, GUI.tooltip );
                previousToolTip = GUI.tooltip;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space( 30 );
        }

        static bool ValidateProfileName( string name )
        {
            if ( string.IsNullOrEmpty( name ) ) return false;

            foreach ( char c in name )
            {
                if ( !char.IsLetterOrDigit( c ) && c != ' ' && c != '-' && c != '_' )
                {
                    return false;
                }
            }

            return true;
        }

        static void ExportProfileToClipboard( string profileName )
        {
            try
            {
                string profilePath = System.IO.Path.Combine( System.IO.Path.Combine( mod.Path, "Profiles" ), profileName + ".xml" );
                if ( System.IO.File.Exists( profilePath ) )
                {
                    string xmlContent = System.IO.File.ReadAllText( profilePath );
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes( xmlContent );
                    string base64 = System.Convert.ToBase64String( bytes );
                    GUIUtility.systemCopyBuffer = base64;
                }
            }
            catch ( Exception ex )
            {
                mod.Logger.Error( $"Failed to export profile: {ex.Message}" );
            }
        }

        static void ImportProfileFromClipboard()
        {
            try
            {
                string clipboard = GUIUtility.systemCopyBuffer;
                if ( !string.IsNullOrEmpty( clipboard ) )
                {
                    byte[] bytes = System.Convert.FromBase64String( clipboard );
                    string xmlContent = System.Text.Encoding.UTF8.GetString( bytes );

                    var serializer = new System.Xml.Serialization.XmlSerializer( typeof( Settings ) );
                    using ( var reader = new System.IO.StringReader( xmlContent ) )
                    {
                        Settings testSettings = (Settings)serializer.Deserialize( reader );

                        string profileName = "Imported";
                        int counter = 1;
                        var existingProfiles = settings.GetAvailableProfiles();
                        while ( existingProfiles.Contains( profileName ) )
                        {
                            profileName = $"Imported_{counter}";
                            counter++;
                        }

                        string profilesDir = System.IO.Path.Combine( mod.Path, "Profiles" );
                        if ( !System.IO.Directory.Exists( profilesDir ) )
                        {
                            System.IO.Directory.CreateDirectory( profilesDir );
                        }

                        string profilePath = System.IO.Path.Combine( profilesDir, profileName + ".xml" );
                        System.IO.File.WriteAllText( profilePath, xmlContent );

                        selectedProfileIndex = existingProfiles.Count;
                    }
                }
            }
            catch ( Exception ex )
            {
                mod.Logger.Error( $"Failed to import profile: {ex.Message}" );
            }
        }

        static void ShowKeybindingButton( string actionName )
        {
            if ( keybindings.ContainsKey( actionName ) )
            {
                GUILayout.BeginHorizontal( GUILayout.ExpandWidth( false ) );
                string nothing = "";
                bool startedAssignment = keybindings[actionName].OnGUI( out _, false, true, ref nothing, 0, true, false, true );

                // Track when a key assignment starts
                if ( startedAssignment )
                {
                    keybindingsBeingAssigned[actionName] = true;
                }

                GUILayout.EndHorizontal();
            }
        }
        #endregion

        #region Modding
        public static void TeleportToCoords( float x, float y )
        {
            if ( currentCharacter != null )
            {
                for ( int i = 0; i < 4; ++i )
                {
                    if ( HeroController.PlayerIsAlive( i ) )
                    {
                        HeroController.players[i].character.X = x;
                        HeroController.players[i].character.Y = y;
                    }
                }
            }
        }

        static void ExecuteAction( string actionName, bool shiftHeld = false )
        {
            if ( HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null )
            {
                currentCharacter = HeroController.players[0].character;
            }
            switch ( actionName )
            {
                // Level Controls
                case "Previous Level":
                    ChangeLevel( -1 );
                    break;

                case "Next Level":
                    ChangeLevel( 1 );
                    break;

                case "Go to level":
                    GoToLevel( campaignNum.indexNumber, levelNum.indexNumber );
                    break;

                case "Unlock All Levels":
                    if ( HarmonyPatches.WorldMapController_Update_Patch.instance != null )
                    {
                        WorldTerritory3D[] territories = Traverse.Create( HarmonyPatches.WorldMapController_Update_Patch.instance ).Field( "territories3D" ).GetValue() as WorldTerritory3D[];
                        foreach ( WorldTerritory3D ter in territories )
                        {
                            if ( ter.properties.state != TerritoryState.Liberated && ter.properties.state != TerritoryState.AlienLiberated )
                            {
                                UnlockTerritory( ter );
                            }
                        }
                    }
                    break;

                case "Loop Current Level":
                    settings.loopCurrent = !settings.loopCurrent;
                    break;

                case "Restart Current Level":
                    if ( currentCharacter != null )
                    {
                        Map.ClearSuperCheckpointStatus();
                        GameModeController.RestartLevel();
                    }
                    break;

                // Cheat Options
                case "Invincibility":
                    settings.invulnerable = !settings.invulnerable;
                    if ( settings.invulnerable && currentCharacter != null )
                    {
                        currentCharacter.SetInvulnerable( float.MaxValue, false );
                    }
                    else if ( currentCharacter != null )
                    {
                        currentCharacter.SetInvulnerable( 0, false );
                    }
                    break;

                case "Infinite Lives":
                    settings.infiniteLives = !settings.infiniteLives;
                    if ( currentCharacter != null )
                    {
                        if ( settings.infiniteLives )
                        {
                            for ( int i = 0; i < 4; ++i )
                            {
                                HeroController.SetLives( i, int.MaxValue );
                            }
                        }
                        else
                        {
                            for ( int i = 0; i < 4; ++i )
                            {
                                HeroController.SetLives( i, 1 );
                            }
                        }
                    }
                    break;

                case "Infinite Specials":
                    settings.infiniteSpecials = !settings.infiniteSpecials;
                    break;

                case "Disable Gravity":
                    settings.disableGravity = !settings.disableGravity;
                    break;

                case "Enable Flight":
                    settings.enableFlight = !settings.enableFlight;
                    break;

                case "Disable Enemy Spawns":
                    settings.disableEnemySpawn = !settings.disableEnemySpawn;
                    break;

                case "Instant Kill Enemies":
                    settings.oneHitEnemies = !settings.oneHitEnemies;
                    break;

                case "Summon Mech":
                    if ( currentCharacter != null )
                    {
                        ProjectileController.SpawnGrenadeOverNetwork( ProjectileController.GetMechDropGrenadePrefab(), currentCharacter, currentCharacter.X + Mathf.Sign( currentCharacter.transform.localScale.x ) * 8f, currentCharacter.Y + 8f, 0.001f, 0.011f, Mathf.Sign( currentCharacter.transform.localScale.x ) * 200f, 150f, currentCharacter.playerNum );
                    }
                    break;

                case "Slow Time":
                    settings.slowTime = !settings.slowTime;
                    if ( settings.slowTime )
                    {
                        StartTimeSlow();
                    }
                    else
                    {
                        StopTimeSlow();
                    }
                    break;

                // Teleport Options
                case "Teleport":
                    float xCoord, yCoord;
                    if ( float.TryParse( teleportX, out xCoord ) && float.TryParse( teleportY, out yCoord ) )
                    {
                        TeleportToCoords( xCoord, yCoord );
                    }
                    break;

                case "Spawn at Custom Waypoint":
                    settings.changeSpawn = !settings.changeSpawn;
                    // If enabling this option, disable the other
                    if ( settings.changeSpawn )
                    {
                        settings.changeSpawnFinal = false;
                    }
                    break;

                case "Spawn at Final Checkpoint":
                    settings.changeSpawnFinal = !settings.changeSpawnFinal;
                    // If enabling this option, disable the other
                    if ( settings.changeSpawnFinal )
                    {
                        settings.changeSpawn = false;
                    }
                    break;

                case "Save Position for Custom Spawn":
                    if ( currentCharacter != null )
                    {
                        SaveCustomSpawnForCurrentLevel( currentCharacter.X, currentCharacter.Y );
                    }
                    break;

                case "Teleport to Custom Spawn Position":
                    if ( HasCustomSpawnForCurrentLevel() )
                    {
                        Vector2 customSpawn = GetCustomSpawnForCurrentLevel();
                        TeleportToCoords( customSpawn.x, customSpawn.y );
                    }
                    break;

                case "Clear Custom Spawn":
                    ClearCustomSpawnForCurrentLevel();
                    break;

                case "Teleport to Current Checkpoint":
                    if ( currentCharacter != null )
                    {
                        Vector3 checkPoint = HeroController.GetCheckPointPosition( 0, Map.IsCheckPointAnAirdrop( HeroController.GetCurrentCheckPointID() ) );
                        TeleportToCoords( checkPoint.x, checkPoint.y );
                    }
                    break;

                case "Teleport to Final Checkpoint":
                    Vector3 finalCheckpointPos = GetFinalCheckpointPos();
                    TeleportToCoords( finalCheckpointPos.x, finalCheckpointPos.y );
                    break;

                case "Save Position to Waypoint 1":
                    if ( currentCharacter != null )
                    {
                        settings.waypointsX[0] = currentCharacter.X;
                        settings.waypointsY[0] = currentCharacter.Y;
                    }
                    break;

                case "Save Position to Waypoint 2":
                    if ( currentCharacter != null )
                    {
                        settings.waypointsX[1] = currentCharacter.X;
                        settings.waypointsY[1] = currentCharacter.Y;
                    }
                    break;

                case "Save Position to Waypoint 3":
                    if ( currentCharacter != null )
                    {
                        settings.waypointsX[2] = currentCharacter.X;
                        settings.waypointsY[2] = currentCharacter.Y;
                    }
                    break;

                case "Save Position to Waypoint 4":
                    if ( currentCharacter != null )
                    {
                        settings.waypointsX[3] = currentCharacter.X;
                        settings.waypointsY[3] = currentCharacter.Y;
                    }
                    break;

                case "Save Position to Waypoint 5":
                    if ( currentCharacter != null )
                    {
                        settings.waypointsX[4] = currentCharacter.X;
                        settings.waypointsY[4] = currentCharacter.Y;
                    }
                    break;

                case "Teleport to Waypoint 1":
                    TeleportToCoords( settings.waypointsX[0], settings.waypointsY[0] );
                    break;

                case "Teleport to Waypoint 2":
                    TeleportToCoords( settings.waypointsX[1], settings.waypointsY[1] );
                    break;

                case "Teleport to Waypoint 3":
                    TeleportToCoords( settings.waypointsX[2], settings.waypointsY[2] );
                    break;

                case "Teleport to Waypoint 4":
                    TeleportToCoords( settings.waypointsX[3], settings.waypointsY[3] );
                    break;

                case "Teleport to Waypoint 5":
                    TeleportToCoords( settings.waypointsX[4], settings.waypointsY[4] );
                    break;

                // Debug Options
                case "Print Audio Played":
                    settings.printAudioPlayed = !settings.printAudioPlayed;
                    break;

                case "Suppress Announcer":
                    settings.suppressAnnouncer = !settings.suppressAnnouncer;
                    break;

                case "Max Cage Spawns":
                    settings.maxCageSpawns = !settings.maxCageSpawns;
                    break;

                case "Set Zoom Level":
                    settings.setZoom = !settings.setZoom;
                    break;

                case "Make Cursor Always Visible":
                    settings.showCursor = !settings.showCursor;
                    break;

                case "Pause/Unpause Game":
                    settings.isGamePaused = !settings.isGamePaused;
                    if ( settings.isGamePaused )
                    {
                        Time.timeScale = 0f;
                    }
                    else
                    {
                        Time.timeScale = settings.gameSpeedMultiplier;
                    }
                    break;

                case "Increase Game Speed":
                    float increaseAmount = shiftHeld ? settings.gameSpeedStep * 5f : settings.gameSpeedStep;
                    if ( settings.isGamePaused )
                    {
                        settings.isGamePaused = false;
                        settings.gameSpeedMultiplier = increaseAmount;
                    }
                    else
                    {
                        settings.gameSpeedMultiplier += increaseAmount;
                    }
                    settings.gameSpeedMultiplier = Mathf.Min( settings.gameSpeedMultiplier, 2.0f );
                    Time.timeScale = settings.gameSpeedMultiplier;
                    break;

                case "Decrease Game Speed":
                    if ( !settings.isGamePaused )
                    {
                        float decreaseAmount = shiftHeld ? settings.gameSpeedStep * 5f : settings.gameSpeedStep;
                        settings.gameSpeedMultiplier -= decreaseAmount;
                        settings.gameSpeedMultiplier = Mathf.Max( settings.gameSpeedMultiplier, 0.01f );
                        Time.timeScale = settings.gameSpeedMultiplier;
                    }
                    break;

                case "Reset Game Speed":
                    settings.gameSpeedMultiplier = 1.0f;
                    settings.isGamePaused = false;
                    Time.timeScale = 1.0f;
                    break;

                // General Options
                case "Skip Intro":
                    settings.skipIntro = !settings.skipIntro;
                    break;

                case "Camera Shake":
                    settings.cameraShake = !settings.cameraShake;
                    break;

                case "Helicopter Skip":
                    settings.enableSkip = !settings.enableSkip;
                    break;

                case "Ending Skip":
                    settings.endingSkip = !settings.endingSkip;
                    break;

                case "Speed up Main Menu Loading":
                    settings.quickMainMenu = !settings.quickMainMenu;
                    break;

                case "Helicopter Wait":
                    settings.helicopterWait = !settings.helicopterWait;
                    break;

                case "Fix Mod Window Disappearing":
                    settings.disableConfirm = !settings.disableConfirm;
                    break;

                case "Disable All Cutscenes":
                    settings.skipAllCutscenes = !settings.skipAllCutscenes;
                    break;

                // Context Menu Options
                case "Quick Clone (Under Cursor)":
                    var contextMenuManager = UnityEngine.Object.FindObjectOfType<ContextMenuManager>();
                    if ( contextMenuManager != null )
                    {
                        contextMenuManager.QuickCloneUnderCursor();
                    }
                    break;
            }
        }

        public static void SpawnUnit( UnitType unitType, Vector3 vector )
        {
            // Use RocketLib's GetUnitPrefab method to get the correct prefab
            Unit unitPrefab = unitType.GetUnitPrefab();

            if ( unitPrefab == null )
            {
                return;
            }

            // Instantiate the unit
            GameObject spawnedUnit = UnityEngine.Object.Instantiate( unitPrefab, vector, Quaternion.identity ).gameObject;

            if ( spawnedUnit != null )
            {
                spawnedUnit.transform.parent = Map.Instance.transform;
                Registry.RegisterDeterminsiticGameObject( spawnedUnit );
            }
        }

        static Block CreateBlock( int x, int y, BlockType blockType, Block prefabOverride = null )
        {
            Traverse trav = Traverse.Create( Map.Instance );
            GroundType placeGroundType = GroundType.Earth;
            Block[,] newBlocks = Map.blocks;
            Vector3 vector = new Vector3( (float)( x * 16 ), (float)( y * 16 ), 5f );
            Block currentBlock = null;
            //Block currentBackgroundBlock = null;
            switch ( blockType )
            {
                // Terrain Blocks
                case BlockType.Dirt:
                    return Map.Instance.PlaceGround( GroundType.Earth, x, y, ref Map.blocks, true );
                case BlockType.CaveRock:
                    return Map.Instance.PlaceGround( GroundType.CaveRock, x, y, ref Map.blocks, true );
                case BlockType.SandyEarth:
                    return Map.Instance.PlaceGround( GroundType.Sand, x, y, ref Map.blocks, true );
                case BlockType.DesertSand:
                    return Map.Instance.PlaceGround( GroundType.DesertSand, x, y, ref Map.blocks, true );

                // Bridges/Platforms/Ladders
                case BlockType.Bridge:
                    return Map.Instance.PlaceGround( GroundType.Bridge, x, y, ref Map.blocks, true );
                case BlockType.Bridge2:
                    return Map.Instance.PlaceGround( GroundType.Bridge2, x, y, ref Map.blocks, true );
                case BlockType.MetalBridge:
                    return Map.Instance.PlaceGround( GroundType.MetalBridge, x, y, ref Map.blocks, true );
                case BlockType.SacredBridge:
                    return Map.Instance.PlaceGround( GroundType.SacredBridge, x, y, ref Map.blocks, true );
                case BlockType.AlienBridge:
                    return Map.Instance.PlaceGround( GroundType.AlienBridge, x, y, ref Map.blocks, true );
                case BlockType.MetalBridge2:
                    return Map.Instance.PlaceGround( GroundType.MetalBridge2, x, y, ref Map.blocks, true );
                case BlockType.Ladder:
                    return Map.Instance.PlaceGround( GroundType.Ladder, x, y, ref Map.blocks, true );
                case BlockType.AlienLadder:
                    return Map.Instance.PlaceGround( GroundType.AlienLadder, x, y, ref Map.blocks, true );
                case BlockType.MetalLadder:
                    return Map.Instance.PlaceGround( GroundType.MetalLadder, x, y, ref Map.blocks, true );
                case BlockType.CityLadder:
                    return Map.Instance.PlaceGround( GroundType.CityLadder, x, y, ref Map.blocks, true );
                case BlockType.DesertLadder:
                    return Map.Instance.PlaceGround( GroundType.DesertLadder, x, y, ref Map.blocks, true );

                // Building Materials & Structures
                case BlockType.Brick:
                    return Map.Instance.PlaceGround( GroundType.Brick, x, y, ref Map.blocks, true );
                case BlockType.BrickTop:
                    return Map.Instance.PlaceGround( GroundType.BrickTop, x, y, ref Map.blocks, true );
                case BlockType.Metal:
                    return Map.Instance.PlaceGround( GroundType.Metal, x, y, ref Map.blocks, true );
                case BlockType.Steel:
                    return Map.Instance.PlaceGround( GroundType.Steel, x, y, ref Map.blocks, true );
                case BlockType.Bunker:
                    return Map.Instance.PlaceGround( GroundType.Bunker, x, y, ref Map.blocks, true );
                case BlockType.Roof:
                    return Map.Instance.PlaceGround( GroundType.Roof, x, y, ref Map.blocks, true );
                case BlockType.WatchTower:
                    return Map.Instance.PlaceGround( GroundType.WatchTower, x, y, ref Map.blocks, true );
                case BlockType.Pipe:
                    return Map.Instance.PlaceGround( GroundType.Pipe, x, y, ref Map.blocks, true );
                case BlockType.ThatchRoof:
                    return Map.Instance.PlaceGround( GroundType.ThatchRoof, x, y, ref Map.blocks, true );
                case BlockType.Statue:
                    return Map.Instance.PlaceGround( GroundType.Statue, x, y, ref Map.blocks, true );
                case BlockType.BuriedRocket:
                    return Map.Instance.PlaceGround( GroundType.BuriedRocket, x, y, ref Map.blocks, true );
                case BlockType.TyreBlock:
                    return Map.Instance.PlaceGround( GroundType.TyreBlock, x, y, ref Map.blocks, true );
                case BlockType.FactoryRoof:
                    return Map.Instance.PlaceGround( GroundType.FactoryRoof, x, y, ref Map.blocks, true );
                case BlockType.DesertRoof:
                    return Map.Instance.PlaceGround( GroundType.DesertRoof, x, y, ref Map.blocks, true );
                case BlockType.DesertRoofRed:
                    return Map.Instance.PlaceGround( GroundType.DesertRoofRed, x, y, ref Map.blocks, true );
                case BlockType.TentRoof:
                    return Map.Instance.PlaceGround( GroundType.TentRoof, x, y, ref Map.blocks, true );

                // Special Blocks
                case BlockType.FallingBlock:
                    return Map.Instance.PlaceGround( GroundType.FallingBlock, x, y, ref Map.blocks, true );
                case BlockType.Sandbag:
                    return Map.Instance.PlaceGround( GroundType.Sandbag, x, y, ref Map.blocks, true );
                case BlockType.Boulder:
                    return Map.Instance.PlaceGround( GroundType.Boulder, x, y, ref Map.blocks, true );
                case BlockType.BigBlock:
                    return Map.Instance.PlaceGround( GroundType.BigBlock, x, y, ref Map.blocks, true );
                case BlockType.BoulderBig:
                    return Map.Instance.PlaceGround( GroundType.BoulderBig, x, y, ref Map.blocks, true );
                case BlockType.SacredBigBlock:
                    return Map.Instance.PlaceGround( GroundType.SacredBigBlock, x, y, ref Map.blocks, true );
                case BlockType.Vault:
                    return Map.Instance.PlaceGround( GroundType.Vault, x, y, ref Map.blocks, true );
                case BlockType.SmallCageBlock:
                    return Map.Instance.PlaceGround( GroundType.SmallCageBlock, x, y, ref Map.blocks, true );
                case BlockType.StandardCage:
                    return Map.Instance.PlaceGround( GroundType.StandardCage, x, y, ref Map.blocks, true );
                case BlockType.Quicksand:
                    return Map.Instance.PlaceGround( GroundType.Quicksand, x, y, ref Map.blocks, true );
                case BlockType.OilPipe:
                    return Map.Instance.PlaceGround( GroundType.OilPipe, x, y, ref Map.blocks, true );

                // Destructibles
                case BlockType.ExplosiveBarrel:
                    placeGroundType = GroundType.Barrel;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.blockPrefabBarrels[1], vector, Quaternion.identity );
                    break;
                case BlockType.RedExplosiveBarrel:
                    placeGroundType = GroundType.Barrel;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.blockPrefabBarrels[0], vector, Quaternion.identity );
                    break;
                case BlockType.PropaneTank:
                    placeGroundType = GroundType.Barrel;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.blockPrefabBarrels[2], vector, Quaternion.identity );
                    break;
                case BlockType.DesertOilBarrel:
                    return Map.Instance.PlaceGround( GroundType.DesertOilBarrel, x, y, ref Map.blocks, true );
                case BlockType.OilTank:
                    return Map.Instance.PlaceGround( GroundType.OilTank, x, y, ref Map.blocks, true );
                case BlockType.Crate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( prefabOverride ?? Map.Instance.activeTheme.blockPrefabWood[0], vector, Quaternion.identity );
                    break;
                case BlockType.AmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.crateAmmo, vector, Quaternion.identity );
                    break;
                case BlockType.TimeAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.sharedObjectsReference.Asset.crateTimeCop, vector, Quaternion.identity );
                    break;
                case BlockType.RCCarAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.sharedObjectsReference.Asset.crateRCCar, vector, Quaternion.identity );
                    break;
                case BlockType.AirStrikeAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.sharedObjectsReference.Asset.crateAirstrike, vector, Quaternion.identity );
                    break;
                case BlockType.MechDropAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.sharedObjectsReference.Asset.crateMechDrop, vector, Quaternion.identity );
                    break;
                case BlockType.AlienPheromonesAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.sharedObjectsReference.Asset.crateAlienPheromonesDrop, vector, Quaternion.identity );
                    break;
                case BlockType.SteroidsAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.sharedObjectsReference.Asset.crateSteroids, vector, Quaternion.identity );
                    break;
                case BlockType.PigAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.sharedObjectsReference.Asset.cratePiggy, vector, Quaternion.identity );
                    break;
                case BlockType.FlexAmmoCrate:
                    placeGroundType = GroundType.AmmoCrate;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.sharedObjectsReference.Asset.cratePerk, vector, Quaternion.identity );
                    break;
                case BlockType.MoneyCrate:
                    return Map.Instance.PlaceGround( GroundType.MoneyCrate, x, y, ref Map.blocks, true );
                case BlockType.PillsCrate:
                    return Map.Instance.PlaceGround( GroundType.PillsCrate, x, y, ref Map.blocks, true );
                case BlockType.BeeHive:
                    placeGroundType = GroundType.Beehive;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.blockBeeHive, vector, Quaternion.identity );
                    break;
                case BlockType.AlienEgg:
                    placeGroundType = GroundType.AlienEgg;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.blockAlienEgg, vector, Quaternion.identity );
                    break;
                case BlockType.AlienEggExplosive:
                    placeGroundType = GroundType.AlienEggExplosive;
                    currentBlock = UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.explosiveAlienEgg, vector, Quaternion.identity );
                    break;
                case BlockType.AlienEarth:
                    return Map.Instance.PlaceGround( GroundType.AlienEarth, x, y, ref Map.blocks, true );
                case BlockType.AlienFlesh:
                    return Map.Instance.PlaceGround( GroundType.AlienFlesh, x, y, ref Map.blocks, true );
                case BlockType.AlienExplodingFlesh:
                    return Map.Instance.PlaceGround( GroundType.AlienExplodingFlesh, x, y, ref Map.blocks, true );
                case BlockType.AlienBarbShooter:
                    return Map.Instance.PlaceGround( GroundType.AlienBarbShooter, x, y, ref Map.blocks, true );
                case BlockType.AlienDirt:
                    return Map.Instance.PlaceGround( GroundType.AlienDirt, x, y, ref Map.blocks, true );

                // Cave/Hell blocks
                case BlockType.CaveEarth:
                    return Map.Instance.PlaceGround( GroundType.CaveEarth, x, y, ref Map.blocks, true );
                case BlockType.Skulls:
                    return Map.Instance.PlaceGround( GroundType.Skulls, x, y, ref Map.blocks, true );
                case BlockType.Bones:
                    return Map.Instance.PlaceGround( GroundType.Bones, x, y, ref Map.blocks, true );
                case BlockType.HellRock:
                    return Map.Instance.PlaceGround( GroundType.HellRock, x, y, ref Map.blocks, true );
                case BlockType.DesertCaveRock:
                    return Map.Instance.PlaceGround( GroundType.DesertCaveRock, x, y, ref Map.blocks, true );
                case BlockType.DesertCaveEarth:
                    return Map.Instance.PlaceGround( GroundType.DesertCaveEarth, x, y, ref Map.blocks, true );

                // City/Desert/Sacred blocks
                case BlockType.DesertEarth:
                    return Map.Instance.PlaceGround( GroundType.DesertEarth, x, y, ref Map.blocks, true );
                case BlockType.CityEarth:
                    return Map.Instance.PlaceGround( GroundType.CityEarth, x, y, ref Map.blocks, true );
                case BlockType.SacredTemple:
                    return Map.Instance.PlaceGround( GroundType.SacredTemple, x, y, ref Map.blocks, true );
                case BlockType.CityBrick:
                    return Map.Instance.PlaceGround( GroundType.CityBrick, x, y, ref Map.blocks, true );
                case BlockType.DesertBrick:
                    return Map.Instance.PlaceGround( GroundType.DesertBrick, x, y, ref Map.blocks, true );
                case BlockType.CityAssMouth:
                    return Map.Instance.PlaceGround( GroundType.CityAssMouth, x, y, ref Map.blocks, true );
                case BlockType.CityRoad:
                    return Map.Instance.PlaceGround( GroundType.CityRoad, x, y, ref Map.blocks, true );
                case BlockType.SacredTempleGold:
                    return Map.Instance.PlaceGround( GroundType.SacredTempleGold, x, y, ref Map.blocks, true );
            }
            if ( placeGroundType != GroundType.Cage && ( placeGroundType != GroundType.AlienFlesh || newBlocks != Map.backGroundBlocks ) )
            {
                newBlocks[x, y] = currentBlock;
                Traverse groundTypesTrav = trav.Field( "groundTypes" );
                ( groundTypesTrav.GetValue() as GroundType[,] )[x, y] = placeGroundType;
            }
            if ( currentBlock != null )
            {
                currentBlock.OnSpawned();
                if ( currentBlock.groundType == GroundType.Earth && currentBlock.size == 2 )
                {
                    Map.SetBlockEmpty( newBlocks[x + 1, y], x + 1, y );
                    newBlocks[x + 1, y] = currentBlock;
                    Map.SetBlockEmpty( newBlocks[x, y - 1], x, y - 1 );
                    newBlocks[x, y - 1] = currentBlock;
                    Map.SetBlockEmpty( newBlocks[x + 1, y - 1], x + 1, y - 1 );
                    newBlocks[x + 1, y - 1] = currentBlock;
                }
            }
            if ( currentBlock != null )
            {
                currentBlock.transform.parent = Map.Instance.transform;
                trav.Field( "currentBlock" ).SetValue( currentBlock );
            }

            return currentBlock;
        }

        public static void SpawnBlock( Vector3 position, BlockType blockType )
        {
            int column = (int)Mathf.Round( position.x / 16f );
            int row = (int)Mathf.Round( position.y / 16f );

            // Bounds checking
            if ( column < 0 || column >= Map.MapData.Width || row < 0 || row >= Map.MapData.Height )
            {
                return;
            }

            // Check if space is empty
            if ( Map.blocks[column, row] != null && !Map.blocks[column, row].destroyed )
            {
                return;
            }

            SpawnBlockInternal( row, column, blockType );
        }

        public static void SpawnBlockInternal( int row, int column, BlockType blockType, Block prefabOverride = null )
        {
            Block block = null;

            if ( prefabOverride != null )
            {
                // When we have a prefab override, replicate the core PlaceGround logic
                // This handles the vast majority of blocks correctly

                // Calculate position (same as PlaceGround)
                Vector3 position = new Vector3( (float)( column * 16 ), (float)( row * 16 ), 5f );

                // Instantiate our exact prefab
                GameObject clonedObject = UnityEngine.Object.Instantiate( prefabOverride.gameObject, position, Quaternion.identity );
                block = clonedObject.GetComponent<Block>();

                if ( block != null )
                {
                    // Set up the block's grid position
                    block.row = row;
                    block.collumn = column;
                    block.initialRow = row;
                    block.initialColumn = column;

                    // Parent to Map
                    block.transform.parent = Map.Instance.transform;

                    // Add to the map's block array
                    Map.blocks[column, row] = block;

                    // Update groundTypes array (important for game logic)
                    var groundTypesField = Traverse.Create( Map.Instance ).Field( "groundTypes" );
                    var groundTypes = groundTypesField.GetValue() as GroundType[,];
                    if ( groundTypes != null )
                    {
                        groundTypes[column, row] = block.groundType;
                    }

                    // Call OnSpawned to initialize the block (random generators, etc)
                    block.OnSpawned();

                    // Register for networking
                    Registry.RegisterDeterminsiticGameObject( block.gameObject );

                    // Handle special case for 2x2 blocks (like big earth blocks)
                    if ( block.groundType == GroundType.Earth && block.size == 2 )
                    {
                        Map.SetBlockEmpty( Map.blocks[column + 1, row], column + 1, row );
                        Map.blocks[column + 1, row] = block;
                        Map.SetBlockEmpty( Map.blocks[column, row - 1], column, row - 1 );
                        Map.blocks[column, row - 1] = block;
                        Map.SetBlockEmpty( Map.blocks[column + 1, row - 1], column + 1, row - 1 );
                        Map.blocks[column + 1, row - 1] = block;
                    }
                }
            }
            else
            {
                // Use the original method
                block = CreateBlock( column, row, blockType );
            }

            if ( block == null ) return;
            if ( block.groundType != GroundType.Bridge || block.groundType != GroundType.Bridge2 || block.groundType != GroundType.AlienBridge )
            {
                if ( column > 0 && Map.blocks[column - 1, row] != null )
                {
                    block.HideLeft();
                }
                if ( column < Map.MapData.Width - 1 && Map.blocks[column + 1, row] != null )
                {
                    block.HideRight();
                }
                if ( row > 0 && Map.blocks[column, row - 1] != null )
                {
                    block.HideBelow();
                }
                if ( row < Map.MapData.Height - 1 && Map.blocks[column, row + 1] != null )
                {
                    block.HideAbove();
                }
            }
            Block aboveBlock = null;
            Block belowBlock = null;
            if ( row < Map.MapData.Height - 1 && Map.blocks[column, row + 1] != null )
            {
                aboveBlock = Map.blocks[column, row + 1];
            }
            if ( row > 0 && Map.blocks[column, row - 1] != null )
            {
                belowBlock = Map.blocks[column, row - 1];
            }
            block.SetupBlock( column, row, aboveBlock, belowBlock );
            block.RegisterBlockOnNetwork();
            if ( block is DirtBlock )
            {
                DirtBlock dirtBlock = ( block as DirtBlock );
                dirtBlock.addDecorations = false;
                dirtBlock.backgroundPrefabs = null;
                dirtBlock.backgroundEdgesPrefabs = null;
            }
            block.FirstFrame();
        }

        public static GameObject SpawnDoodad( Vector3 position, SpawnableDoodadType doodadType )
        {
            int column = (int)Mathf.Round( position.x / 16f );
            int row = (int)Mathf.Round( position.y / 16f );

            // Bounds checking
            if ( column < 0 || column >= Map.MapData.Width || row < 0 || row >= Map.MapData.Height )
            {
                return null;
            }

            return SpawnDoodadInternal( row, column, doodadType );
        }

        static GameObject SpawnDoodadInternal( int row, int column, SpawnableDoodadType doodadType )
        {
            DoodadInfo doodad = new DoodadInfo();
            doodad.position = new GridPoint( column, row );
            doodad.variation = 0;

            GameObject result = null;

            GridPoint gridPoint = new GridPoint( doodad.position.collumn, doodad.position.row );
            gridPoint.collumn -= Map.lastXLoadOffset;
            gridPoint.row -= Map.lastYLoadOffset;

            Vector3 vector = new Vector3( (float)( gridPoint.c * 16 ), (float)( gridPoint.r * 16 ), 5f );

            if ( GameModeController.IsHardcoreMode )
            {
                Map.havePlacedCageForHardcore = true;
                Map.cagesSinceLastHardcoreCage = 0;
            }

            switch ( doodadType )
            {
                // Checkpoints & Cages
                case SpawnableDoodadType.RescueCage:
                    result = ( UnityEngine.Object.Instantiate<Block>( Map.Instance.activeTheme.blockPrefabCage, vector, Quaternion.identity ) as Cage ).gameObject;
                    result.GetComponent<Cage>().row = gridPoint.row;
                    result.GetComponent<Cage>().collumn = gridPoint.collumn;
                    break;
                case SpawnableDoodadType.CheckPoint:
                    CheckPoint checkpoint = UnityEngine.Object.Instantiate<CheckPoint>( Map.Instance.activeTheme.checkPointPrefab, vector, Quaternion.identity );
                    result = checkpoint.gameObject;
                    break;

                // Hazards - use DoodadInfo with Map.PlaceDoodad
                case SpawnableDoodadType.Spikes:
                    doodad.type = DoodadType.Trap;
                    doodad.variation = 0;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
                case SpawnableDoodadType.Mines:
                    doodad.type = DoodadType.Trap;
                    doodad.variation = 1;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
                case SpawnableDoodadType.SawBlade:
                    doodad.type = DoodadType.Trap;
                    doodad.variation = 2;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
                case SpawnableDoodadType.HiddenExplosives:
                    doodad.type = DoodadType.HiddenExplosives;
                    doodad.variation = -1;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;

                // Doors
                case SpawnableDoodadType.Door:
                    doodad.type = DoodadType.Door;
                    doodad.variation = 0;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
                case SpawnableDoodadType.MookDoor:
                    doodad.type = DoodadType.MookDoor;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;

                // Environment
                case SpawnableDoodadType.ZiplinePoint:
                    doodad.type = DoodadType.Zipline;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
                case SpawnableDoodadType.Scaffolding:
                    doodad.type = DoodadType.Scaffolding;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
                case SpawnableDoodadType.Fence:
                    doodad.type = DoodadType.Fence;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
                case SpawnableDoodadType.Tree:
                    doodad.type = DoodadType.Tree;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
                case SpawnableDoodadType.Bush:
                    doodad.type = DoodadType.TreeBushes;
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;

                // Hanging Objects
                case SpawnableDoodadType.HangingVines:
                    doodad.type = DoodadType.HangingDoodads;
                    doodad.variation = 1;  // Hanging Vines is at index 1
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
                case SpawnableDoodadType.HangingBrazier:
                    doodad.type = DoodadType.HangingDoodads;
                    doodad.variation = 2;  // Brazier is at index 2
                    result = Map.Instance.PlaceDoodad( doodad );
                    break;
            }

            doodad.entity = result;
            if ( result != null )
            {
                result.transform.parent = Map.Instance.transform;
                Block component = result.GetComponent<Block>();
                if ( component != null )
                {
                    component.OnSpawned();
                }
                Registry.RegisterDeterminsiticGameObject( result.gameObject );
                if ( component != null )
                {
                    component.FirstFrame();
                }
            }

            return result;
        }

        static void DetermineLevelsInCampaign()
        {
            Main.settings.campaignNum = campaignNum.indexNumber;

            if ( lastCampaignNum != campaignNum.indexNumber )
            {
                int actualCampaignNum = campaignNum.indexNumber + 1;
                int numberOfLevels = 1;
                switch ( actualCampaignNum )
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


                if ( levelNum.indexNumber + 1 > numberOfLevels )
                {
                    levelNum.indexNumber = numberOfLevels - 1;
                }
                levelNum = new Dropdown( 400, 150, 125, 300, levelList.Take( numberOfLevels ).ToArray(), levelNum.indexNumber, levelNum.show );
            }
            lastCampaignNum = campaignNum.indexNumber;
        }

        public static void GoToLevel( int campaignIndex, int levelIndex )
        {
            if ( !Main.settings.cameraShake )
            {
                PlayerOptions.Instance.cameraShakeAmount = 0f;
            }

            // Setup player 1 if they haven't yet joined (like if we're calling this function on the main menu
            if ( !HeroController.IsPlayerPlaying( 0 ) )
            {
                // Setup save slot
                PlayerProgress.currentWorldMapSaveSlot = 0;
                GameState.Instance.currentWorldmapSave = PlayerProgress.Instance.saveSlots[0];

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

            if ( campaignIndex >= 0 && campaignIndex < actualCampaignNames.Length )
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

        static void ChangeLevel( int levelNum )
        {
            LevelSelectionController.CurrentLevelNum += levelNum;

            Map.ClearSuperCheckpointStatus();
            GameModeController.RestartLevel();
        }

        static void UnlockTerritory( WorldTerritory3D territory )
        {
            switch ( territory.properties.territoryName )
            {
                case "VIETMAN": territory.SetState( TerritoryState.TerroristBase ); break;
                case "Bombardment Challenge": territory.SetState( TerritoryState.TerroristBase ); break;
                case "HAWAII": territory.SetState( TerritoryState.Infested ); break;
                case "INDONESIA": territory.SetState( TerritoryState.TerroristBase ); break;
                case "DEM. REP. OF CONGO": territory.SetState( TerritoryState.Infested ); break;
                case "CAMBODIA": territory.SetState( TerritoryState.TerroristBase ); break;
                case "MUSCLETEMPLE3": territory.SetState( TerritoryState.TerroristBase ); break;
                case "MUSCLETEMPLE2": territory.SetState( TerritoryState.TerroristBase ); break;
                case "MUSCLETEMPLE4": territory.SetState( TerritoryState.TerroristBase ); break;
                case "UKRAINE": territory.SetState( TerritoryState.TerroristBase ); break;
                case "SOUTH KOREA": territory.SetState( TerritoryState.TerroristBase ); break;
                case "KAZAKHSTAN": territory.SetState( TerritoryState.TerroristBase ); break;
                case "INDIA": territory.SetState( TerritoryState.TerroristBurning ); break;
                case "MacBrover Challenge": territory.SetState( TerritoryState.TerroristBase ); break;
                case "PHILIPPINES": territory.SetState( TerritoryState.TerroristBase ); break;
                case "Dash Challenge": territory.SetState( TerritoryState.TerroristBase ); break;
                case "THE AMAZON RAINFOREST": territory.SetState( TerritoryState.Infested ); break;
                case "PANAMA": territory.SetState( TerritoryState.TerroristBase ); break;
                case "WHITE HOUSE": territory.SetState( TerritoryState.Empty ); break;
                case "UNITED STATES OF AMERICA": territory.SetState( TerritoryState.Hell ); break;
                case "HONG KONG": territory.SetState( TerritoryState.TerroristBase ); break;
                case "Mech Challenge": territory.SetState( TerritoryState.TerroristBase ); break;
                case "Ammo Challenge": territory.SetState( TerritoryState.TerroristBase ); break;
                case "Time Bro Challenge": territory.SetState( TerritoryState.TerroristBase ); break;
                case "NEW GUINEA": territory.SetState( TerritoryState.TerroristBurning ); break;
                case "MUSCLETEMPLE1": territory.SetState( TerritoryState.TerroristBase ); break;
                case "MUSCLETEMPLE5": territory.SetState( TerritoryState.TerroristBase ); break;
                case "Alien Challenge": territory.SetState( TerritoryState.TerroristBase ); break;
            }

        }

        public static void StartTimeSlow()
        {
            HeroController.TimeBroBoost( float.MaxValue );
            Time.timeScale = settings.slowTimeFactor;
            HeroController.TimeBroBoostHeroes( float.MaxValue );
        }

        public static void StopTimeSlow()
        {
            HeroController.CancelTimeBroBoost();
            HeroController.TimeBroBoostHeroes( 0 );
        }

        public static Vector3 GetFinalCheckpointPos()
        {
            for ( int i = 0; i < Map.checkPoints.Count; ++i )
            {
                if ( (bool)Traverse.Create( Map.checkPoints[i] ).Field( "isFinal" ).GetValue() )
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
            return settings.levelSpawnPositions.ContainsKey( key );
        }

        public static Vector2 GetCustomSpawnForCurrentLevel()
        {
            string key = GetCurrentLevelKey();
            if ( settings.levelSpawnPositions.TryGetValue( key, out Vector2 position ) )
            {
                return position;
            }
            return Vector2.zero;
        }

        public static void SaveCustomSpawnForCurrentLevel( float x, float y )
        {
            string key = GetCurrentLevelKey();
            settings.levelSpawnPositions[key] = new Vector2( x, y );
        }

        public static void ClearCustomSpawnForCurrentLevel()
        {
            string key = GetCurrentLevelKey();
            settings.levelSpawnPositions.Remove( key );
        }

        public static void SetWaypoint( int index, Vector2 position )
        {
            if ( index >= 0 && index < settings.waypointsX.Length )
            {
                settings.waypointsX[index] = position.x;
                settings.waypointsY[index] = position.y;
            }
        }

        public static void GoToWaypoint( int index )
        {
            if ( index >= 0 && index < settings.waypointsX.Length )
            {
                TeleportToCoords( settings.waypointsX[index], settings.waypointsY[index] );
            }
        }

        public static void SetCustomSpawnForCurrentLevel( Vector2 position )
        {
            SaveCustomSpawnForCurrentLevel( position.x, position.y );
        }

        public static void GoToCustomSpawn()
        {
            Vector2 spawn = GetCustomSpawnForCurrentLevel();
            if ( spawn != Vector2.zero )
            {
                TeleportToCoords( spawn.x, spawn.y );
            }
        }

        public static void ClearAllWaypoints()
        {
            for ( int i = 0; i < settings.waypointsX.Length; i++ )
            {
                settings.waypointsX[i] = 0;
                settings.waypointsY[i] = 0;
            }
        }

        public static void GoToPreviousLevel()
        {
            int currentLevel = LevelSelectionController.CurrentLevelNum;
            if ( currentLevel > 0 )
            {
                ChangeLevel( -1 );
            }
        }

        public static void GoToNextLevel()
        {
            ChangeLevel( 1 );
        }
        #endregion
    }
}
