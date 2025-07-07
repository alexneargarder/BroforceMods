using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityModManagerNet;

namespace Utility_Mod
{
    public enum RightClick
    {
        None = 0,
        TeleportToCursor = 1,
        SpawnEnemy = 2,
        SpawnObject = 3
    }

    public enum CurrentObject
    {
        Dirt = 0,
        ExplosiveBarrel = 1,
        RedExplosiveBarrel = 2,
        PropaneTank = 3,
        RescueCage = 4,
        Crate = 5,
        AmmoCrate = 6,
        TimeAmmoCrate = 7,
        RCCarAmmoCrate = 8,
        AirStrikeAmmoCrate = 9,
        MechDropAmmoCrate = 10,
        AlienPheromonesAmmoCrate = 11,
        SteroidsAmmoCrate = 12,
        PigAmmoCrate = 13,
        FlexAmmoCrate = 14,
        BeeHive = 15,
        AlienEgg = 16,
        AlienEggExplosive = 17,
    }

    public class Settings : UnityModManager.ModSettings
    {
        // Show / hide each section
        public bool showGeneralOptions = false;
        public bool showLevelOptions = false;
        public bool showCheatOptions = false;
        public bool showTeleportOptions = false;
        public bool showDebugOptions = false;

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
        public RightClick currentRightClick = RightClick.TeleportToCursor;
        public bool changeSpawn = false;
        public bool changeSpawnFinal = false;
        public float[] waypointsX = new float[] { 0f, 0f, 0f, 0f, 0f };
        public float[] waypointsY = new float[] { 0f, 0f, 0f, 0f, 0f };
        public float SpawnPositionX = 0;
        public float SpawnPositionY = 0;

        // DEBUG Options
        public int selectedEnemy = 0;
        public bool printAudioPlayed = false;
        public float zoomLevel = 1f;
        public bool setZoom = false;
        public bool suppressAnnouncer = false;
        public bool maxCageSpawns = false;
        public bool middleClickToChangeRightClick = true;
        public bool showCursor = false;
        public CurrentObject selectedObject = 0;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }
}
