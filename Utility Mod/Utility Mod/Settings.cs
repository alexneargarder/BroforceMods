using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;
using UnityModManagerNet;
using RocketLib;
using RocketLib.Extensions;

namespace Utility_Mod
{
    public enum EnemyPaintMode
    {
        TimeBased = 0,
        DistanceBased = 1
    }

    public class Settings : UnityModManager.ModSettings
    {
        // Version marker - if this is still 0 after loading, we know settings weren't loaded from file
        public int SettingsVersion = 0;
        
        // Show / hide each section
        public bool showGeneralOptions = false;
        public bool showLevelOptions = false;
        public bool showCheatOptions = false;
        public bool showTeleportOptions = false;
        public bool showDebugOptions = false;
        public bool showRightClickOptions = false;
        public bool showKeybindingOptions = false;

        // General Options
        public bool cameraShake = false;
        public bool enableSkip = true;
        public bool scaleUIWithWindowWidth = false;
        public bool endingSkip = true;
        public bool disableConfirm = true;
        public bool quickMainMenu = false;
        public bool helicopterWait = false;
        public bool skipBreakingCutscenes = true;
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
        public float timeSlowFactor = 0.35f;
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

        [XmlArray("LevelSpawnPositions")]
        public SerializableKeyValuePair<string, Vector2>[] SerializedSpawnPositions
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
        public bool showCursor = false;
        public bool captureUnityLogs = false;  
        public bool captureUnityErrors = true;  
        public bool captureUnityWarnings = true;  
        public bool captureUnityInfo = true;

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
        
        [XmlArray("LevelEditRecords")]
        public SerializableKeyValuePair<string, LevelEditRecord>[] SerializedEditRecords
        {
            get => levelEditRecords.ToSerializableArray();
            set => levelEditRecords = value.ToDictionary();
        }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            // Always ensure version is saved as non-zero
            if (SettingsVersion == 0)
                SettingsVersion = 1;
                
            Save(this, modEntry);
        }

    }
}
