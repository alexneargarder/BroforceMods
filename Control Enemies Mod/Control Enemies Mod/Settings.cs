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

    public class SaveGame
    {
        public SaveGame(int[] currentScore, int[] requiredScore, int currentHeroNum)
        {
            this.currentScore = currentScore;
            this.requiredScore = requiredScore;
            this.currentHeroNum = currentHeroNum;
        }

        public SaveGame()
        {
            this.currentScore = new int[] { 0, 0, 0, 0 };
            this.requiredScore = new int[] { 0, 0, 0, 0 };
            this.currentHeroNum = 0;
        }

        public int[] currentScore;
        public int[] requiredScore;
        public int currentHeroNum;
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
        public bool noLifeLossOnSuicide = false;

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
        public bool spawnAsEnemyEnabled = false;
        public float spawnAsEnemyChance = 100f;
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
        public bool ghostControlledEnemiesAffectCamera = true;
        public int scoreToWin = 10;
        public int scoreIncrement = 5;
        public int heroLives = 4;
        public int ghostLives = 4;
        public int extraLiveOnBossLevel = 1;
        public int startingHeroPlayer = 0;
        public float automaticallyFindEnemyCooldown = 3f;

        // Save info
        public SaveGame[] saveGames = new SaveGame[] { new SaveGame(), new SaveGame(), new SaveGame(), new SaveGame(), new SaveGame() };

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }
}
