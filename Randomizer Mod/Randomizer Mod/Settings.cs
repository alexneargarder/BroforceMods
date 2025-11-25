using System.Collections.Generic;
using UnityModManagerNet;

namespace Randomizer_Mod
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool enableEnemyRandomization = true;

        public bool enableNormal = true;
        public bool enableWorms = true;
        public bool enableBosses = true;
        public bool enableLargeBosses = true;
        public bool enableVehicles = false;

        public bool enableNormalSummoned = true;
        public bool enableBossSummoned = true;

        public bool enableAmmoRandomization = false;
        public bool enableCratesTurningIntoAmmo = false;
        public bool unlockAllFlexPowers = false;

        public bool showNormal = false;
        public bool showWorms = false;
        public bool showBosses = false;
        public bool showLargeBosses = false;
        public bool showVehicles = false;
        public bool showAmmo = false;

        public List<int> enabledNormal;
        public List<int> enabledWorms;
        public List<int> enabledBosses;
        public List<int> enabledLargeBosses;
        public List<int> enabledVehicles;
        public List<int> enabledAmmoTypes;

        public bool enableInstantWin = true;
        public bool enableDeathField = true;
        public bool enableSpawnedEnemiesRandomization = true;

        public float enemyPercent = 100.0f;

        public float normalEnemyPercent = 100.0f;
        public float wormPercent = 2.0f;
        public float bossPercent = 5.0f;
        public float largeBossPercent = 3.0f;
        public float vehiclePercent = 1.0f;
        public float ammoRandomizationPercent = 100.0f;
        public float cratesToAmmoPercent = 1.0f;

        public bool defaultSettings = true;

        public bool scaleUIWithWindowWidth = true;

        public bool DEBUG = false;
        public int debugMookType = 0;
        public int debugMookTypeSummoned = 0;

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save( this, modEntry );
        }

    }
}
