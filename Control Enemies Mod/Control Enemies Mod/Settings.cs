using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityModManagerNet;

namespace Control_Enemies_Mod
{
    public enum SwapBehavior
    {
        KillEnemy = 0,
        StunEnemy = 1,
        DeleteEnemy = 2,
        Nothing = 3
    }

    public enum SpawnMode
    {
        Ghost = 0,
        Automatic = 1
    }

    public class Settings : UnityModManager.ModSettings
    {
        // Show / Hide sections
        public bool showGeneralOptions = false;
        public bool showPossessionOptions = false;
        public bool showSpawnAsEnemyOptions = false;
        public bool showCompetitiveOptions = false;

        // General Settings
        public bool allowWallClimbing = false;
        public bool disableFallDamage = true;
        public bool disableTaunting = true;
        public bool enableSprinting = true;
        public bool extraControlsToggle = false;

        // Possession Settings
        public bool possessionModeEnabled = true;
        public bool loseLifeOnDeath = true;
        public bool loseLifeOnSwitch = false;
        public bool respawnFromCorpse = true;
        public SwapBehavior leavingEnemy = SwapBehavior.KillEnemy;
        public SwapBehavior swappingEnemies = SwapBehavior.DeleteEnemy;
        public float swapCooldown = 2f;
        public float fireRate = 0.5f;

        // Spawn as Enemy Settings
        public float spawnAsEnemyChance = 100f;
        public bool spawnAsEnemyEnabled = false;
        public bool alwaysChosen = false;
        public bool filterEnemies = false;
        public int[] selGridInt = { 0, 0, 0, 0 };
        public bool[] showSettings = { true, false, false, false };
        public List<string> enabledUnits = new List<string>();
        public float spawnSwapCooldown = 0.5f;
        public bool clickingSwapEnabled = true;

        // Competitive Settings
        public bool competitiveModeEnabled = false;
        public SpawnMode spawnMode = SpawnMode.Ghost;
        public int heroLives = 3;
        public int ghostLives = 3;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }
}
