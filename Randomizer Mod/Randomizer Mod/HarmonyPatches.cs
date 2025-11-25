using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Randomizer_Mod
{
    [HarmonyPatch( typeof( MapController ), "SpawnMook_Networked" )]
    static class MapController_SpawnMook_Networked
    {
        public static System.Random rnd = new System.Random();
        public static Mook getRandomMookPrefab()
        {
            Mook mookPrefab = null;

            double enemyConvertChance = rnd.NextDouble() * ( 100 );

            if ( enemyConvertChance > Main.settings.enemyPercent || Map_PlaceDoodad.mapInstance == null )
            {
                return mookPrefab;
            }

            double enemyType = rnd.NextDouble() * ( (int)( ( Main.settings.enableNormalSummoned ? Main.settings.normalEnemyPercent : 0 ) + ( Main.settings.enableBossSummoned ? Main.settings.bossPercent : 0 ) ) );

            if ( Main.settings.DEBUG )
                enemyType = ( Main.settings.debugMookTypeSummoned < Main.normalList.Length ) ? 0 : (int)( Main.settings.normalEnemyPercent + 1 );

            if ( Main.settings.enableNormalSummoned && enemyType < Main.settings.normalEnemyPercent && Main.settings.enabledNormal.Count > 0 )
            {
                int num = rnd.Next( 0, Main.settings.enabledNormal.Count );
                num = Main.settings.enabledNormal[num];

                if ( Main.settings.DEBUG )
                    num = Main.settings.debugMookTypeSummoned;

                switch ( num )
                {
                    // Mooks
                    case 0:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mook as Mook;
                        break;
                    case 1:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookSuicide as Mook;
                        break;
                    case 2:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookBigGuy as Mook;
                        break;
                    case 3:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookSuicideBigGuy as Mook;
                        break;
                    case 4:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.mookBigGuyStrong.GetComponent<Mook>();
                        break;
                    case 5:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookBigGuyElite as Mook;
                        break;
                    case 6:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookScout as Mook;
                        break;
                    case 7:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookRiotShield as Mook;
                        break;
                    case 8:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookArmoured as Mook;
                        break;
                    case 9:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.mechBrown.GetComponent<Mook>();
                        break;
                    case 10:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.mookJetpack;
                        break;
                    case 11:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookGrenadier as Mook;
                        break;
                    case 12:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookBazooka as Mook;
                        break;
                    case 13:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookJetpackBazooka as Mook;
                        break;
                    case 14:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookNinja as Mook;
                        break;
                    case 15:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.treasureMook.GetComponent<Mook>();
                        break;
                    case 16:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookDog as Mook;
                        break;
                    case 17:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.skinnedMook as Mook;
                        break;
                    case 18:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookGeneral as Mook;
                        break;
                    case 19:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookAlarmist as Mook;
                        break;
                    case 20:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookStrong as Mook;
                        break;
                    case 21:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookScientist as Mook;
                        break;
                    case 22:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.snake as Mook;
                        break;
                    // Satan
                    case 23:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.satan as Mook;
                        break;
                    // Aliens
                    case 24:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.alienFaceHugger as Mook;
                        break;
                    case 25:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.alienXenomorph as Mook;
                        break;
                    case 26:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.alienBrute as Mook;
                        break;
                    case 27:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.alienBaneling as Mook;
                        break;
                    case 28:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.alienMosquito as Mook;
                        break;
                    case 29:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookXenomorphBrainbox as Mook;
                        break;
                    // HellDog
                    case 30:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.hellEnemies[0].GetComponent<Mook>();
                        break;
                    // ZMookUndead
                    case 31:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.hellEnemies[1].GetComponent<Mook>();
                        break;
                    // ZMookUndeadStartDead
                    case 32:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.hellEnemies[2].GetComponent<Mook>();
                        break;
                    // ZMookWarlock
                    case 33:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.hellEnemies[3].GetComponent<Mook>();
                        break;
                    // ZMookHellBoomer
                    case 34:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.hellEnemies[4].GetComponent<Mook>();
                        break;
                    // ZMookUndeadSuicide
                    case 35:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.hellEnemies[5].GetComponent<Mook>();
                        break;
                    // ZHellBigGuy
                    case 36:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.hellEnemies[6].GetComponent<Mook>();
                        break;
                    // Lost Soul
                    case 37:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.hellEnemies[8].GetComponent<Mook>();
                        break;
                    // ZMookHellSoulCatcher
                    case 38:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.hellEnemies[10].GetComponent<Mook>();
                        break;
                }
            }
            else if ( Main.settings.enableBossSummoned && enemyType - ( Main.settings.enableNormal ? Main.settings.normalEnemyPercent : 0 ) < Main.settings.bossPercent && Main.settings.enabledBosses.Count > 0 )
            {
                int num = rnd.Next( 0, Main.settings.enabledBosses.Count );
                num = Main.settings.enabledBosses[num];

                if ( Main.settings.DEBUG )
                    num = Main.settings.debugMookTypeSummoned - Main.normalList.Length;

                switch ( num )
                {
                    case 0:
                        mookPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.satanMiniboss.GetComponent<Mook>();
                        break;
                    case 1:
                        mookPrefab = Map_PlaceDoodad.mapInstance.activeTheme.mookDolfLundgren as Mook;
                        break;
                }
            }

            return mookPrefab;
        }

        public static void Prefix( ref Mook mookPrefab, float x, float y, float xI, float yI, bool tumble, bool useParachuteDelay, bool useParachute, bool onFire, bool isAlert )
        {
            if ( !Main.enabled || !Main.settings.enableSpawnedEnemiesRandomization )
            {
                return;
            }

            if ( !( mookPrefab is TankBroTank || mookPrefab is MookArmouredGuy ) && ( Main.settings.enemyPercent > rnd.NextDouble() * ( 100 ) ) )
            {
                Mook replacePrefab = getRandomMookPrefab();
                if ( replacePrefab != null )
                {
                    mookPrefab = replacePrefab;
                }
            }
        }
    }

    [HarmonyPatch( typeof( MapController ), "SpawnMook_Local" )]
    static class MapController_SpawnMook_Local
    {
        public static void Prefix( ref Mook mookPrefab, float x, float y, float xI, float yI, bool tumble, bool useParachuteDelay, bool useParachute, bool onFire, bool isAlert )
        {
            if ( !Main.enabled || !Main.settings.enableSpawnedEnemiesRandomization )
            {
                return;
            }

            if ( ( Main.settings.enemyPercent > MapController_SpawnMook_Networked.rnd.NextDouble() * ( 100 ) ) )
            {
                Mook replacePrefab = MapController_SpawnMook_Networked.getRandomMookPrefab();
                if ( replacePrefab != null )
                {
                    mookPrefab = replacePrefab;
                }
            }
        }
    }

    [HarmonyPatch( typeof( Map ), "PlaceDoodad" )]
    static class Map_PlaceDoodad
    {
        static readonly System.Random rnd = new System.Random();
        const int NUMWORMS = 5;
        const int NUMBOSSES = 2;
        const int NUMLARGEBOSSES = 4;

        public static Map mapInstance;

        public static bool Prefix( Map __instance, ref DoodadInfo doodad, ref GameObject __result )
        {
            if ( !Main.enabled )
            {
                return true;
            }

            mapInstance = __instance;

            if ( Main.settings.enableEnemyRandomization && ( doodad.type == DoodadType.Mook || doodad.type == DoodadType.Alien || doodad.type == DoodadType.HellEnemy ) &&
                ( Main.settings.enableNormal || Main.settings.enableWorms || Main.settings.enableBosses || Main.settings.enableLargeBosses || Main.settings.enableVehicles ) )
            {
                double enemyConvertChance = rnd.NextDouble() * 100;

                if ( enemyConvertChance > Main.settings.enemyPercent )
                {
                    return true;
                }

                GridPoint position = doodad.position;
                position.c -= Map.lastXLoadOffset;
                position.r -= Map.lastYLoadOffset;

                Vector3 vector = new Vector3( (float)( position.c * 16 ), (float)( position.r * 16 ), 5f );

                TestVanDammeAnim original = null;

                double enemyType = rnd.NextDouble() * ( (int)( ( Main.settings.enableNormal ? Main.settings.normalEnemyPercent : 0 ) + ( Main.settings.enableWorms ? Main.settings.wormPercent : 0 ) +
                    ( Main.settings.enableBosses ? Main.settings.bossPercent : 0 ) + ( Main.settings.enableLargeBosses ? Main.settings.largeBossPercent : 0 ) + ( Main.settings.enableVehicles ? Main.settings.vehiclePercent : 0 ) ) );

                if ( !Main.settings.DEBUG )
                {
                    if ( Main.settings.enableNormal && enemyType < Main.settings.normalEnemyPercent && Main.settings.enabledNormal.Count > 0 )
                    {
                        int num = rnd.Next( 0, Main.settings.enabledNormal.Count );
                        num = Main.settings.enabledNormal[num];

                        switch ( num )
                        {
                            // Mooks
                            case 0:
                                original = __instance.activeTheme.mook;
                                break;
                            case 1:
                                original = __instance.activeTheme.mookSuicide;
                                break;
                            case 2:
                                original = __instance.activeTheme.mookBigGuy;
                                break;
                            case 3:
                                original = __instance.activeTheme.mookSuicideBigGuy;
                                break;
                            case 4:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mookBigGuyStrong, vector, Quaternion.identity ).gameObject;
                                break;
                            case 5:
                                original = __instance.activeTheme.mookBigGuyElite;
                                break;
                            case 6:
                                original = __instance.activeTheme.mookScout;
                                break;
                            case 7:
                                original = __instance.activeTheme.mookRiotShield;
                                break;
                            case 8:
                                original = __instance.activeTheme.mookArmoured;
                                break;
                            case 9:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mechBrown, vector, Quaternion.identity ).gameObject;
                                break;
                            case 10:
                                original = __instance.sharedObjectsReference.Asset.mookJetpack;
                                break;
                            case 11:
                                original = __instance.activeTheme.mookGrenadier;
                                break;
                            case 12:
                                original = __instance.activeTheme.mookBazooka;
                                break;
                            case 13:
                                original = __instance.activeTheme.mookJetpackBazooka;
                                break;
                            case 14:
                                original = __instance.activeTheme.mookNinja;
                                break;
                            case 15:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.treasureMook, vector, Quaternion.identity ).gameObject;
                                break;
                            case 16:
                                original = __instance.activeTheme.mookDog;
                                break;
                            case 17:
                                original = __instance.activeTheme.skinnedMook;
                                break;
                            case 18:
                                original = __instance.activeTheme.mookGeneral;
                                break;
                            case 19:
                                original = __instance.activeTheme.mookAlarmist;
                                break;
                            case 20:
                                original = __instance.activeTheme.mookStrong;
                                break;
                            case 21:
                                original = __instance.activeTheme.mookScientist;
                                break;
                            case 22:
                                original = __instance.activeTheme.snake;
                                break;
                            // Satan
                            case 23:
                                original = __instance.activeTheme.satan;
                                break;
                            // Aliens
                            case 24:
                                original = __instance.activeTheme.alienFaceHugger;
                                break;
                            case 25:
                                original = __instance.activeTheme.alienXenomorph;
                                break;
                            case 26:
                                original = __instance.activeTheme.alienBrute;
                                break;
                            case 27:
                                original = __instance.activeTheme.alienBaneling;
                                break;
                            case 28:
                                original = __instance.activeTheme.alienMosquito;
                                break;
                            case 29:
                                original = __instance.activeTheme.mookXenomorphBrainbox;
                                break;
                            // HellDog
                            case 30:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[0], vector, Quaternion.identity );
                                break;
                            // ZMookUndead
                            case 31:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[1], vector, Quaternion.identity );
                                break;
                            // ZMookUndeadStartDead
                            case 32:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[2], vector, Quaternion.identity );
                                break;
                            // ZMookWarlock
                            case 33:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[3], vector, Quaternion.identity );
                                break;
                            // ZMookHellBoomer
                            case 34:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[4], vector, Quaternion.identity );
                                break;
                            // ZMookUndeadSuicide
                            case 35:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[5], vector, Quaternion.identity );
                                break;
                            // ZHellBigGuy
                            case 36:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[6], vector, Quaternion.identity );
                                break;
                            // Lost Soul
                            case 37:
                                vector.y += 5;
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[8], vector, Quaternion.identity );
                                break;
                            // ZMookHellSoulCatcher
                            case 38:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[10], vector, Quaternion.identity );
                                break;
                        }

                        if ( original != null )
                        {
                            __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>( original, vector, Quaternion.identity ).gameObject;
                        }
                    }
                    // Worms
                    else if ( Main.settings.enableWorms && ( enemyType - ( Main.settings.enableNormal ? Main.settings.normalEnemyPercent : 0 ) ) < Main.settings.wormPercent && Main.settings.enabledWorms.Count > 0 )
                    {
                        int num = rnd.Next( 0, Main.settings.enabledWorms.Count );
                        num = Main.settings.enabledWorms[num];

                        switch ( num )
                        {
                            // Sandworm
                            case 0:
                                // Make sure we aren't replacing a boneworm or alien brute so we don't prevent a level end trigger
                                // This is only relevant for Campaign 15 Level 11 and Alien Challenge
                                if ( ( doodad.type == DoodadType.HellEnemy && ( doodad.variation == 12 || doodad.variation == 13 ) ) || ( doodad.type == DoodadType.Alien && doodad.variation == 2 ) )
                                {
                                    return true;
                                }
                                else
                                {
                                    __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[7], vector, Quaternion.identity );
                                }
                                break;
                            // Boneworm
                            case 1:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[12], vector, Quaternion.identity );
                                break;
                            // Boneworm Behind
                            case 2:
                                __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[13], vector, Quaternion.identity );
                                break;
                            // Alien Worm
                            case 3:
                                __result = ( UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.alienMinibossSandWorm, vector, Quaternion.identity ) as AlienMinibossSandWorm ).gameObject;
                                break;
                            // Alien Facehugger Worm
                            case 4:
                                __result = ( UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitter, vector, Quaternion.identity ) as AlienMinibossSandWorm ).gameObject;
                                break;
                            // Alien Facehugger Worm Behind
                            case 5:
                                __result = ( UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitterBehind, vector, Quaternion.identity ) as AlienMinibossSandWorm ).gameObject;
                                break;
                        }
                    }
                    // Bosses
                    else if ( Main.settings.enableBosses && ( enemyType - Main.settings.normalEnemyPercent - ( Main.settings.enableWorms ? Main.settings.wormPercent : 0 ) ) < Main.settings.bossPercent && Main.settings.enabledBosses.Count > 0 )
                    {
                        int num = rnd.Next( 0, Main.settings.enabledBosses.Count );
                        num = Main.settings.enabledBosses[num];


                        switch ( num )
                        {
                            case 0:
                                SatanMiniboss satanMiniboss = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.satanMiniboss, vector, Quaternion.identity ) as SatanMiniboss;
                                if ( satanMiniboss != null )
                                {
                                    __result = satanMiniboss.gameObject;
                                }
                                break;
                            case 1:
                                __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>( __instance.activeTheme.mookDolfLundgren, vector, Quaternion.identity ).gameObject;
                                break;

                        }
                    }
                    // Large Bosses
                    else if ( Main.settings.enableLargeBosses && ( enemyType - ( Main.settings.enableNormal ? Main.settings.normalEnemyPercent : 0 ) - ( Main.settings.enableWorms ? Main.settings.wormPercent : 0 ) -
                        ( Main.settings.enableBosses ? Main.settings.bossPercent : 0 ) ) < Main.settings.largeBossPercent && Main.settings.enabledLargeBosses.Count > 0 )
                    {
                        int num = rnd.Next( 0, Main.settings.enabledLargeBosses.Count );
                        num = Main.settings.enabledLargeBosses[num];

                        switch ( num )
                        {
                            case 0:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookMammothTank, vector, Quaternion.identity ).gameObject;
                                break;
                            case 1:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookKopterMiniBoss, vector, Quaternion.identity ).gameObject;
                                break;
                            case 2:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.goliathMech, vector, Quaternion.identity ).gameObject;
                                break;
                            // Large Alien Worm
                            case 3:
                                __result = ( UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.alienGiantSandWormBoss, vector, Quaternion.identity ) as AlienMinibossSandWorm ).gameObject;
                                break;
                        }
                    }
                    // Vehicles
                    else if ( Main.settings.enableVehicles && ( enemyType - ( Main.settings.enableNormal ? Main.settings.normalEnemyPercent : 0 ) - ( Main.settings.enableWorms ? Main.settings.wormPercent : 0 ) -
                        ( Main.settings.enableBosses ? Main.settings.bossPercent : 0 ) - ( Main.settings.enableLargeBosses ? Main.settings.largeBossPercent : 0 ) ) < Main.settings.vehiclePercent && Main.settings.enabledVehicles.Count > 0 )
                    {
                        int num = rnd.Next( 0, Main.settings.enabledVehicles.Count );
                        num = Main.settings.enabledVehicles[num];

                        switch ( num )
                        {
                            case 0:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookTankMookLauncher, vector, Quaternion.identity ).gameObject;
                                break;
                            case 1:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookTankCannon, vector, Quaternion.identity ).gameObject;
                                break;
                            case 2:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookTankRockets, vector, Quaternion.identity ).gameObject;
                                break;
                            case 3:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookArtilleryTruck, vector, Quaternion.identity ).gameObject;
                                break;
                            case 4:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookBlimp, vector, Quaternion.identity ).gameObject;
                                break;
                            case 5:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookDrillCarrier, vector, Quaternion.identity ).gameObject;
                                break;
                            case 6:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookTruck, vector, Quaternion.identity ).gameObject;
                                break;
                            case 7:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.sandbag, vector, Quaternion.identity ).gameObject;
                                break;
                            case 8:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mookMotorBike, vector, Quaternion.identity ).gameObject;
                                break;
                            case 9:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mookMotorBikeNuclear, vector, Quaternion.identity ).gameObject;
                                break;
                            case 10:
                                __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mookDumpTruck, vector, Quaternion.identity ).gameObject;
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
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[0], vector, Quaternion.identity );
                            break;
                        // ZMookUndead
                        case 21:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[1], vector, Quaternion.identity );
                            break;
                        // ZMookUndeadStartDead
                        case 22:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[2], vector, Quaternion.identity );
                            break;
                        // ZMookWarlock
                        case 23:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[3], vector, Quaternion.identity );
                            break;
                        // ZMookHellBoomer
                        case 24:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[4], vector, Quaternion.identity );
                            break;
                        // ZMookUndeadSuicide
                        case 25:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[5], vector, Quaternion.identity );
                            break;
                        // ZHellBigGuy
                        case 26:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[6], vector, Quaternion.identity );
                            break;
                        // Lost Soul
                        case 27:
                            vector.y += 5;
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[8], vector, Quaternion.identity );
                            break;
                        // ZMookHellSoulCatcher
                        case 28:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[10], vector, Quaternion.identity );
                            break;
                        // Sandworm
                        case 29:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[7], vector, Quaternion.identity );
                            break;
                        // Boneworm
                        case 30:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[12], vector, Quaternion.identity );
                            break;
                        // Boneworm Behind
                        case 31:
                            __result = UnityEngine.Object.Instantiate<GameObject>( __instance.sharedObjectsReference.Asset.hellEnemies[13], vector, Quaternion.identity );
                            break;
                        // Alien Worm
                        case 32:
                            __result = ( UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.alienMinibossSandWorm, vector, Quaternion.identity ) as AlienMinibossSandWorm ).gameObject;
                            break;
                        // Alien Facehugger Worm
                        case 33:
                            __result = ( UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitter, vector, Quaternion.identity ) as AlienMinibossSandWorm ).gameObject;
                            break;
                        // Alien Facehugger Worm Behind
                        case 34:
                            __result = ( UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.alienSandWormFacehuggerSpitterBehind, vector, Quaternion.identity ) as AlienMinibossSandWorm ).gameObject;
                            break;
                        case 35:
                            SatanMiniboss satanMiniboss = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.satanMiniboss, vector, Quaternion.identity ) as SatanMiniboss;
                            if ( satanMiniboss != null )
                            {
                                __result = satanMiniboss.gameObject;
                            }
                            break;
                        case 36:
                            __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>( __instance.activeTheme.mookDolfLundgren, vector, Quaternion.identity ).gameObject;
                            break;
                        case 37:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookMammothTank, vector, Quaternion.identity ).gameObject;
                            break;
                        case 38:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookKopterMiniBoss, vector, Quaternion.identity ).gameObject;
                            break;
                        case 39:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.goliathMech, vector, Quaternion.identity ).gameObject;
                            break;
                        // Large Alien Worm
                        case 40:
                            __result = ( UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.alienGiantSandWormBoss, vector, Quaternion.identity ) as AlienMinibossSandWorm ).gameObject;
                            break;
                        case 41:
                            original = __instance.activeTheme.evilAgent; // Glitchy textures
                            break;
                        case 42:
                            original = __instance.activeTheme.mookMortar; // Fake enemy used for summoning mortar strikes it appears
                            break;
                        case 43:
                            original = __instance.activeTheme.mookJetpackBazooka;
                            break;
                        case 44:
                            original = __instance.activeTheme.mookAlarmist; // Ends the level if you kill him next to checkpoint
                            break;
                        case 45:
                            original = __instance.activeTheme.mookStrong;
                            break;
                        case 46:
                            original = __instance.activeTheme.mookBigGuyElite;
                            break;
                        case 47:
                            original = __instance.activeTheme.mookScientist;
                            break;
                        case 48:
                            original = __instance.activeTheme.mookSuicideBigGuy;
                            break;
                        case 49:
                            original = __instance.activeTheme.snake;
                            break;
                        case 50:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookTankMookLauncher, vector, Quaternion.identity ).gameObject;
                            break;
                        case 51:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookTankCannon, vector, Quaternion.identity ).gameObject;
                            break;
                        case 52:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookTankRockets, vector, Quaternion.identity ).gameObject;
                            break;
                        case 53:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookArtilleryTruck, vector, Quaternion.identity ).gameObject;
                            break;
                        case 54:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookBlimp, vector, Quaternion.identity ).gameObject;
                            break;
                        case 55:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookDrillCarrier, vector, Quaternion.identity ).gameObject;
                            break;
                        case 56:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.mookTruck, vector, Quaternion.identity ).gameObject;
                            break;
                        case 57:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mechBrown, vector, Quaternion.identity ).gameObject;
                            break;
                        case 58:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.treasureMook, vector, Quaternion.identity ).gameObject;
                            break;
                        case 59:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mookBigGuyStrong, vector, Quaternion.identity ).gameObject;
                            break;
                        case 60:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.activeTheme.sandbag, vector, Quaternion.identity ).gameObject;
                            break;
                        case 61:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mookMotorBike, vector, Quaternion.identity ).gameObject;
                            break;
                        case 62:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mookMotorBikeNuclear, vector, Quaternion.identity ).gameObject;
                            break;
                        case 63:
                            __result = UnityEngine.Object.Instantiate<Unit>( __instance.sharedObjectsReference.Asset.mookDumpTruck, vector, Quaternion.identity ).gameObject;
                            break;
                    }

                    if ( original != null )
                    {
                        __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>( original, vector, Quaternion.identity ).gameObject;
                    }
                }

                if ( __result != null )
                {
                    doodad.entity = __result;
                    __result.transform.parent = __instance.transform;
                    Block component = __result.GetComponent<Block>();
                    if ( component != null )
                    {
                        component.OnSpawned();
                    }
                    Registry.RegisterDeterminsiticGameObject( __result.gameObject );
                    if ( component != null )
                    {
                        component.FirstFrame();
                    }

                    return false;
                }
            }
            else if ( ( doodad.type == DoodadType.AmmoCrate || doodad.type == DoodadType.Crate ) && Main.settings.enableAmmoRandomization && Main.settings.enabledAmmoTypes.Count > 0 )
            {
                if ( Main.settings.ammoRandomizationPercent > rnd.NextDouble() * 100 )
                {
                    doodad.type = DoodadType.AmmoCrate;
                    doodad.variation = Main.settings.enabledAmmoTypes[rnd.Next( 0, Main.settings.enabledAmmoTypes.Count )];
                }
            }

            return true;
        }
    }

    [HarmonyPatch( typeof( Map ), "PlaceGround" )]
    static class Map_PlaceGround
    {
        static readonly System.Random rnd = new System.Random();
        public static Block getRandomBlockPrefab()
        {
            Block blockPrefab = null;

            if ( Map_PlaceDoodad.mapInstance != null )
            {
                int ammoType = rnd.Next( 0, Main.settings.enabledAmmoTypes.Count );

                switch ( Main.settings.enabledAmmoTypes[ammoType] )
                {
                    case 0:
                        blockPrefab = Map_PlaceDoodad.mapInstance.activeTheme.crateAmmo;
                        break;
                    case 1:
                        blockPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.crateTimeCop;
                        break;
                    case 2:
                        blockPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.crateRCCar;
                        break;
                    case 3:
                        blockPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.crateAirstrike;
                        break;
                    case 4:
                        blockPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.crateMechDrop;
                        break;
                    case 5:
                        blockPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.crateAlienPheromonesDrop;
                        break;
                    case 6:
                        blockPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.crateSteroids;
                        break;
                    case 7:
                        blockPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.cratePiggy;
                        break;
                    case 8:
                        blockPrefab = Map_PlaceDoodad.mapInstance.sharedObjectsReference.Asset.cratePerk;
                        break;
                }
            }

            return blockPrefab;
        }

        public static bool Prefix( Map __instance, ref GroundType placeGroundType, ref int x, ref int y, ref Block[,] newBlocks, ref bool addToRegistry, ref Block __result, ref GroundType[,] ___groundTypes, ref Block ___currentBlock )
        {
            if ( !Main.enabled )
            {
                return true;
            }

            if ( Main.settings.enableCratesTurningIntoAmmo && placeGroundType == GroundType.WoodenBlock && Main.settings.cratesToAmmoPercent > rnd.NextDouble() * 100 )
            {
                Map_PlaceDoodad.mapInstance = __instance;

                Vector3 vector = new Vector3( (float)( x * 16 ), (float)( y * 16 ), 5f );
                Block currentBlock = UnityEngine.Object.Instantiate<Block>( getRandomBlockPrefab(), vector, Quaternion.identity );
                if ( placeGroundType != GroundType.Cage && ( placeGroundType != GroundType.AlienFlesh || newBlocks != Map.backGroundBlocks ) )
                {
                    newBlocks[x, y] = currentBlock;
                    ___groundTypes[x, y] = placeGroundType;
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
                    currentBlock.transform.parent = __instance.transform;
                    __result = currentBlock;
                    ___currentBlock = currentBlock;
                }
                return false;
            }
            return true;
        }
    }

    // Fixes camera locking onto satan after he dies
    [HarmonyPatch( typeof( SatanMiniboss ), "ForceCameraToFollow" )]
    static class SatanMiniboss_ForceCameraToFollow
    {
        public static bool Prefix( SatanMiniboss __instance )
        {
            if ( !Main.enabled )
            {
                return true;
            }

            return false;
        }
    }

    // Makes satan death field spawn when he dies
    [HarmonyPatch( typeof( SatanMiniboss ), "AnimateDeath" )]
    static class SatanMiniboss_AnimateDeath
    {
        public static void Prefix( SatanMiniboss __instance )
        {
            if ( !Main.enabled || !Main.settings.enableDeathField )
            {
                return;
            }

            __instance.primedToSelfDestruct = true;
        }
    }


    // Allows disabling Dolph Lundgren's instant win
    [HarmonyPatch( typeof( DolphLundrenSoldier ), "Gib" )]
    static class DolphLundrenSoldier_Gib
    {
        public static void Prefix()
        {
            if ( !Main.enabled || Main.settings.enableInstantWin || ( LevelSelectionController.currentCampaign.name == "WM_Village1(mouse)" && LevelSelectionController.CurrentLevelNum == 3 ) )
            {
                return;
            }

            Main.ignoreNextWin = true;
        }
    }

    // Allows disabling Terrorkopter and MammothTank instant wins
    [HarmonyPatch( typeof( MinibossEndCheck ), "Update" )]
    static class MinibossEndCheck_Update
    {
        public static void Prefix( MinibossEndCheck __instance, Unit ___miniBossUnit, int ___explosionDeathCount, ref float ___explosionDeathCounter )
        {
            if ( !Main.enabled || Main.settings.enableInstantWin )
            {
                return;
            }

            if ( LevelSelectionController.currentCampaign.name == "WM_Mission1(mouse)" && LevelSelectionController.CurrentLevelNum == 3 )
                return;

            if ( LevelSelectionController.currentCampaign.name == "WM_Bombardment(mouse)" && LevelSelectionController.CurrentLevelNum == 2 )
                return;


            if ( ___miniBossUnit.health <= 0 )
            {
                if ( ___explosionDeathCount > 0 )
                {

                    if ( ___explosionDeathCounter + Time.deltaTime > 0.33f )
                    {

                        if ( ( ___explosionDeathCount - 1 ) <= 0 )
                        {
                            Main.ignoreNextWin = true;
                        }
                    }
                }

            }

        }
    }

    // Allows disabling GoliathMech instant win
    [HarmonyPatch( typeof( GoliathMech ), "Death" )]
    static class GoliathMech_Death
    {
        public static void Prefix()
        {
            if ( !Main.enabled || Main.settings.enableInstantWin || ( LevelSelectionController.currentCampaign.name == "WM_KazakhstanRainy(mouse)" && LevelSelectionController.CurrentLevelNum == 3 ) )
            {
                return;
            }

            Main.ignoreNextWin = true;
        }

        public static void Postfix( GoliathMech __instance )
        {
            if ( !Main.enabled || Main.settings.enableInstantWin || ( LevelSelectionController.currentCampaign.name == "WM_KazakhstanRainy(mouse)" && LevelSelectionController.CurrentLevelNum == 3 ) )
            {
                return;
            }

            __instance.actionState = ActionState.Idle;
        }
    }

    // Checks if we need to skip this win because it was from a miniboss death
    [HarmonyPatch( typeof( GameModeController ), "LevelFinish" )]
    static class GameModeController_LevelFinish
    {
        public static bool Prefix()
        {
            if ( !Main.enabled || !Main.ignoreNextWin )
            {
                return true;
            }

            Main.ignoreNextWin = false;

            return false;
        }
    }

    // Enables all pickup powers if setting is checked
    [HarmonyPatch( typeof( PlayerProgress ), "GetUnlockedFlexPickups" )]
    static class PlayerProgress_GetUnlockedFlexPickups
    {
        public static bool Prefix( ref List<PickupType> __result )
        {
            if ( !Main.enabled || !Main.settings.unlockAllFlexPowers )
            {
                return true;
            }

            __result = new List<PickupType>();

            __result.Add( PickupType.FlexAirJump );
            __result.Add( PickupType.FlexGoldenLight );
            __result.Add( PickupType.FlexInvulnerability );
            __result.Add( PickupType.FlexTeleport );

            return false;
        }
    }
}
