using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;
using UnityModManagerNet;

namespace Utility_Mod
{
    public enum EnemyPaintMode
    {
        TimeBased = 0,
        DistanceBased = 1
    }

    public class Settings : RocketLib.XmlModSettings
    {
        public override int SettingsVersion { get; set; } = 1;

        // Show / hide each section
        public bool showGeneralOptions = false;
        public bool showLevelOptions = false;
        public bool showCheatOptions = false;
        public bool showTeleportOptions = false;
        public bool showDebugOptions = false;
        public bool showRightClickOptions = false;
        public bool showKeybindingOptions = false;
        public bool showSettingsProfilesOptions = false;

        // General Options
        public bool skipIntro = true;
        public bool cameraShake = false;
        public bool enableSkip = true;
        public bool scaleUIWithWindowWidth = false;
        public bool endingSkip = true;
        public bool disableConfirm = false;
        public bool quickMainMenu = false;
        public bool helicopterWait = false;
        public bool skipAllCutscenes = false;
        public bool disableFlagNoise = false;
        public bool disableHelicopterNoise = false;

        // Level Controls
        public bool loopCurrent = false;
        public int campaignNum = 0;
        public int levelNum = 0;
        public bool goToLevelOnStartup = false;
        public int goToLevelControllerNum = 0;

        // Cheat Options
        public bool invulnerable = false;
        public bool infiniteSpecials = false;
        public bool disableEnemySpawn = false;
        public bool quickLoadScene = false;
        public bool oneHitEnemies = false;
        public bool slowTime = false;
        public float slowTimeFactor = 0.35f;
        public string sceneToLoad;
        public bool infiniteLives = false;
        public bool disableGravity = false;
        public bool enableFlight = false;

        // Teleport Options
        public bool changeSpawn = false;
        public bool changeSpawnFinal = false;
        public float[] waypointsX = new float[] { 0f, 0f, 0f, 0f, 0f };
        public float[] waypointsY = new float[] { 0f, 0f, 0f, 0f, 0f };
        public float SpawnPositionX = 0;
        public float SpawnPositionY = 0;

        [XmlIgnore]
        public Dictionary<string, Vector2> levelSpawnPositions = new Dictionary<string, Vector2>();

        [XmlArray( "LevelSpawnPositions" )]
        public RocketLib.SerializableKeyValuePair<string, Vector2>[] SerializedSpawnPositions
        {
            get => levelSpawnPositions.ToSerializableArray();
            set => levelSpawnPositions = value.ToDictionary();
        }

        // DEBUG Options
        public bool printAudioPlayed = false;
        public float zoomLevel = 1f;
        public bool setZoom = false;
        public bool suppressAnnouncer = false;
        public bool maxCageSpawns = false;
        public bool showMousePosition = false;
        public bool? showTestDummyDPS = true;
        public bool showCursor = false;
        public bool captureUnityLogs = false;
        public bool captureUnityErrors = true;
        public bool captureUnityWarnings = true;
        public bool captureUnityInfo = true;
        public float gameSpeedMultiplier = 1.0f;
        public float gameSpeedStep = 0.01f;
        public bool isGamePaused = false;

        // Context Menu Settings
        public bool contextMenuEnabled = false;
        public float contextMenuHoldDuration = 0.2f;
        public bool showHoldProgressIndicator = false;
        public bool enableRecentItems = true;
        public int maxRecentItems = 5;
        public float menuBackgroundR = 0.1f;  // Dark gray background
        public float menuBackgroundG = 0.1f;
        public float menuBackgroundB = 0.1f;
        public float menuBackgroundAlpha = 0.9f;  // 90% opaque
        public float menuTextR = 1.0f;  // White text
        public float menuTextG = 1.0f;
        public float menuTextB = 1.0f;
        public float menuHighlightR = 0.2f;  // Lighter gray for hover
        public float menuHighlightG = 0.3f;
        public float menuHighlightB = 0.4f;
        public float menuHighlightAlpha = 0.9f;
        public int menuFontSize = 16;  // Default font size
        public MenuAction selectedQuickAction = null;  // Stores the selected quick action
        public List<MenuAction> recentlyUsedItems = new List<MenuAction>();  // List of recently used menu items
        public EnemyPaintMode enemyPaintMode = EnemyPaintMode.TimeBased;
        public float enemyPaintDelay = 0.5f;  // Time delay between enemy spawns in paint mode
        public float enemyPaintDistance = 2f;  // Distance threshold in blocks for distance-based enemy painting (1 block = 16 units)
        public float blockPaintDistance = 1f;  // Distance threshold in blocks for block/doodad painting (1 block = 16 units)
        public bool enableLevelEditReplay = false;  // Global toggle for automatically replaying level edits
        public bool isRecordingLevelEdits = false;  // Whether we're currently recording edits

        [XmlIgnore]
        public Dictionary<string, LevelEditRecord> levelEditRecords = new Dictionary<string, LevelEditRecord>();

        [XmlIgnore]
        public Dictionary<string, LevelEditSlots> levelEditSlots = new Dictionary<string, LevelEditSlots>();


        // Level Edit Manager window position
        public float levelEditManagerWindowX = 0;
        public float levelEditManagerWindowY = 0;

        [XmlArray( "LevelEditRecords" )]
        public RocketLib.SerializableKeyValuePair<string, LevelEditRecord>[] SerializedEditRecords
        {
            get => levelEditRecords.ToSerializableArray();
            set => levelEditRecords = value.ToDictionary();
        }

        [XmlArray( "LevelEditSlots" )]
        public RocketLib.SerializableKeyValuePair<string, LevelEditSlots>[] SerializedEditSlots
        {
            get => levelEditSlots.ToSerializableArray();
            set => levelEditSlots = value.ToDictionary();
        }

        // Settings Profile System
        public string lastLoadedProfileName = null;

        private string GetProfilePath( string profileName )
        {
            string profilesDir = Path.Combine( Main.mod.ConfigPath, "Profiles" );
            return Path.Combine( profilesDir, profileName + ".xml" );
        }

        public void SaveToProfile( string profileName )
        {
            try
            {
                string profilesDir = Path.Combine( Main.mod.ConfigPath, "Profiles" );
                if ( !Directory.Exists( profilesDir ) )
                {
                    Directory.CreateDirectory( profilesDir );
                }

                string profilePath = GetProfilePath( profileName );

                var serializer = new XmlSerializer( typeof( Settings ) );
                using ( var fileStream = new FileStream( profilePath, FileMode.Create ) )
                {
                    serializer.Serialize( fileStream, this );
                }

                lastLoadedProfileName = profileName;
                Save( Main.mod );
            }
            catch ( Exception ex )
            {
                Main.mod.Logger.Error( $"Failed to save profile {profileName}: {ex.Message}" );
            }
        }

        public bool LoadFromProfile( string profileName )
        {
            try
            {
                string profilePath = GetProfilePath( profileName );
                if ( !File.Exists( profilePath ) )
                {
                    Main.mod.Logger.Error( $"Profile file not found: {profileName}" );
                    return false;
                }

                Settings loadedSettings;
                var serializer = new XmlSerializer( typeof( Settings ) );
                using ( var fileStream = new FileStream( profilePath, FileMode.Open ) )
                {
                    loadedSettings = (Settings)serializer.Deserialize( fileStream );
                }

                // Copy all fields from loaded settings to this instance
                // We need to be careful not to overwrite certain runtime-only fields
                var fields = typeof( Settings ).GetFields();
                foreach ( var field in fields )
                {
                    // Skip lastLoadedProfileName - we want to set it to the profile we're loading, not copy it
                    if ( field.Name == "lastLoadedProfileName" )
                        continue;

                    if ( field.GetCustomAttributes( typeof( XmlIgnoreAttribute ), false ).Length == 0 )
                    {
                        field.SetValue( this, field.GetValue( loadedSettings ) );
                    }
                }

                levelEditRecords = loadedSettings.levelEditRecords ?? new Dictionary<string, LevelEditRecord>();
                levelEditSlots = loadedSettings.levelEditSlots ?? new Dictionary<string, LevelEditSlots>();
                levelSpawnPositions = loadedSettings.levelSpawnPositions ?? new Dictionary<string, Vector2>();

                lastLoadedProfileName = profileName;
                Save( Main.mod );
                return true;
            }
            catch ( Exception ex )
            {
                Main.mod.Logger.Error( $"Failed to load profile {profileName}: {ex.Message}" );
                return false;
            }
        }

        public List<string> GetAvailableProfiles()
        {
            var profiles = new List<string>();
            try
            {
                string profilesDir = Path.Combine( Main.mod.ConfigPath, "Profiles" );
                if ( Directory.Exists( profilesDir ) )
                {
                    var files = Directory.GetFiles( profilesDir, "*.xml" );
                    foreach ( var file in files )
                    {
                        profiles.Add( Path.GetFileNameWithoutExtension( file ) );
                    }
                }
            }
            catch ( Exception ex )
            {
                Main.mod.Logger.Error( $"Failed to get available profiles: {ex.Message}" );
            }
            return profiles;
        }

        public bool DeleteProfile( string profileName )
        {
            try
            {
                string profilePath = GetProfilePath( profileName );
                if ( File.Exists( profilePath ) )
                {
                    File.Delete( profilePath );

                    if ( lastLoadedProfileName == profileName )
                    {
                        lastLoadedProfileName = null;
                        Save( Main.mod );
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch ( Exception ex )
            {
                Main.mod.Logger.Error( $"Failed to delete profile {profileName}: {ex.Message}" );
                return false;
            }
        }

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            // Always ensure version is saved as non-zero
            if ( SettingsVersion == 0 )
                SettingsVersion = 1;

            // Test serialization first to avoid data loss
            try
            {
                var serializer = new System.Xml.Serialization.XmlSerializer( typeof( Settings ) );
                using ( var stringWriter = new System.IO.StringWriter() )
                {
                    serializer.Serialize( stringWriter, this );
                    // If we got here, serialization succeeded
                    Save( this, modEntry );
                }
            }
            catch ( System.Exception ex )
            {
                modEntry.Logger.Error( $"Failed to serialize settings - not saving to prevent data loss: {ex.Message}" );
                if ( ex.InnerException != null )
                {
                    modEntry.Logger.Error( $"Inner exception: {ex.InnerException.Message}" );
                }
            }
        }

    }
}
