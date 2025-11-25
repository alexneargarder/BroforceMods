using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RocketLib.UMM;
using UnityEngine;
using UnityModManagerNet;

namespace Randomizer_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;

        // Don't spawn at all
        //public static string[] mookTypesNotWorking = new string[] { "Pig Rotten", "Pig", "Seagull", "WarlockPortal Suicide", "WarlockPortal", "WarlockPortalLarge",
        //    "ZConradBroneBanks", "ZHellDogEgg" };

        // Don't spawn at all
        //public static string[] alienTypesNotWorking = new string[] { "Alien SandWorm Facehugger Launcher Behind", "Alien SandWorm Facehugger Launcher",
        //"AlienGiantBoss SandWorm", "AlienGiantSandWorm", "AlienMiniBoss SandWorm", "SandwormHeadGibHolder" };

        // "ZMook Backup" don't shoot and freeze when thrown
        // "ZMookCaptain" stands still and does nothing
        // "ZMookCaptainCutscene" isn't really a real enemy
        // "ZMookCaptainExpendabro" Only seem to be for alerting enemies
        // "ZMookHellArmouredBigGuy" Doesn't shoot, seems unfinished
        // "ZMookMortar" unfinished mortar enemy
        // "ZSatan Mook" screams and disappears
        // "ZSatanCutscene" uninteractable
        //public static string[] mookTypesGlitchy = new string[] { "ZMook Backup", "ZMookCaptain", "ZMookCaptainCutscene",
        //"ZMookCaptainExpendabro", "ZMookHellArmouredBigGuy", "ZMookMortar", "ZSatan Mook", "ZSatanCutscene" };

        public static string debugMookString = "0";
        public static string debugMookStringSummoned = "0";
        public static string debugAmmoTypeString = "0";
        public static int debugAmmoType = 0;

        public static string[] debugMookTypeList = new string[] {
            "mook",
            "mookSuicide",
            "mookRiotShield",
            "mookBigGuy",
            "mookScout",
            "mookDog",
            "mookArmoured",
            "mookGrenadier",
            "mookBazooka",
            "mookNinja",
            "mookJetpack",
            "mookXenomorphBrainbox",
            "skinnedMook",
            "mookGeneral",
            "satan",
            "alienFaceHugger",
            "alienXenomorph",
            "alienBrute",
            "alienBaneling",
            "alienMosquito",
            "ZHellDog - class HellDog",
            "ZMookUndead - class UndeadTrooper",
            "ZMookUndeadStartDead - class UndeadTrooper",
            "ZMookWarlock - class Warlock",
            "ZMookHellBoomer - class MookHellBoomer",
            "ZMookUndeadSuicide - class MookSuicideUndead",
            "ZHellBigGuy - class MookHellBigGuy",
            "ZHellLostSoul - class HellLostSoul",
            "ZMookHellSoulCatcher - class MookHellSoulCatcher",
            "Hell BoneWorm - class AlienGiantSandWorm",
            "Hell MiniBoss BoneWorm - class HellBoneWormMiniboss",
            "Hell MiniBoss BoneWorm Behind - class HellBoneWormMiniboss",
            "Alien Worm",
            "Alien Facehugger Worm",
            "Alien Facehugger Worm Behind",
            "Satan Miniboss",
            "mookDolfLundgren",
            "mookMammothTank",
            "mookKopterMiniBoss",
            "goliathMech",
            "Large Alien Worm",
            "evilAgent",
            "mookMortar",
            "mookJetpackBazooka",
            "mookAlarmist",
            "mookStrong",
            "mookBigGuyElite",
            "mookScientist",
            "mookSuicideBigGuy",
            "snake",
            "mookTankMookLauncher",
            "mookTankCannon",
            "mookTankRockets",
            "mookArtilleryTruck",
            "mookBlimp",
            "mookDrillCarrier",
            "mookTruck",
            "mechBrown",
            "treasureMook",
            "mookBigGuyStrong",
            "sandbag",
            "mookMotorBike",
            "mookMotorBikeNuclear",
            "mookDumpTruck"
        };

        public static string[] debugMookSummonedList = new string[]
        {
            // Normal
            "Mook", "Suicide Mook", "Bruiser", "Suicide Bruiser", "Elite Bruiser", "Scout Mook", "Riot Shield Mook", "Mech", "Jetpack Mook", "Grenadier Mook", "Bazooka Mook", "Jetpack Bazooka Mook", "Ninja Mook", "Attack Dog", "Skinned Mook", "Mook General",
            "Strong Mook", "Scientist Mook", "Snake", "Satan", 
            // Aliens
            "Facehugger", "Xenomorph", "Brute", "Screecher", "Baneling", "Xenomorph Brainbox",
            // Hell
            "Hellhound", "Undead Mook", "Undead Mook (Start Dead)", "Warlock", "Boomer", "Undead Suicide Mook", "Executioner", "Lost Soul", "Soul Catcher", "Satan Boss", "CR666"
        };

        public static bool ignoreNextWin = false;

        static bool Load( UnityModManager.ModEntry modEntry )
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = Settings.Load<Settings>( modEntry );

            if ( settings.defaultSettings )
            {
                settings.enabledNormal = new List<int>();
                settings.enabledWorms = new List<int>();
                settings.enabledBosses = new List<int>();
                settings.enabledLargeBosses = new List<int>();
                settings.enabledVehicles = new List<int>();
                settings.enabledAmmoTypes = new List<int>();
                for ( int i = 0; i < Main.normalList.Length; ++i )
                {
                    settings.enabledNormal.Add( i );
                }
                for ( int i = 0; i < Main.wormList.Length; ++i )
                {
                    settings.enabledWorms.Add( i );
                }
                for ( int i = 0; i < Main.bossList.Length; ++i )
                {
                    settings.enabledBosses.Add( i );
                }
                for ( int i = 0; i < Main.largeBossList.Length; ++i )
                {
                    settings.enabledLargeBosses.Add( i );
                }
                for ( int i = 0; i < Main.vehicleList.Length; ++i )
                {
                    settings.enabledVehicles.Add( i );
                }
                for ( int i = 0; i < Main.ammoList.Length; ++i )
                {
                    settings.enabledAmmoTypes.Add( i );
                }
                // Pig not enabled by default
                settings.enabledAmmoTypes.Remove( 7 );
                settings.defaultSettings = false;
            }

            mod = modEntry;
            var harmony = new Harmony( modEntry.Info.Id );
            var assembly = Assembly.GetExecutingAssembly();
            try
            {
                harmony.PatchAll( assembly );
            }
            catch ( Exception ex )
            {
                Log( "Harmony Patch Exception: " + ex.ToString() );
            }

            debugMookString = settings.debugMookType.ToString();
            debugMookStringSummoned = settings.debugMookTypeSummoned.ToString();

            return true;
        }

        public static string[] normalList = new string[]
        {
            // Normal
            "Mook", "Suicide Mook", "Bruiser", "Suicide Bruiser", "Strong Bruiser", "Elite Bruiser", "Scout Mook", "Riot Shield Mook", "Mech", "Brown Mech", "Jetpack Mook", "Grenadier Mook", "Bazooka Mook", "Jetpack Bazooka Mook", "Ninja Mook",
            "Treasure Mook", "Attack Dog", "Skinned Mook", "Mook General", "Alarmist", "Strong Mook", "Scientist Mook", "Snake", "Satan", 
            // Aliens
            "Facehugger", "Xenomorph", "Brute", "Screecher", "Baneling", "Xenomorph Brainbox",
            // Hell
            "Hellhound", "Undead Mook", "Undead Mook (Start Dead)", "Warlock", "Boomer", "Undead Suicide Mook", "Executioner", "Lost Soul", "Soul Catcher"
        };

        public static string[] wormList = new string[]
        {
            "SandWorm", "Boneworm", "Boneworm Behind", "Alien Worm", "Alien Facehugger Worm", "Alien Facehugger Worm Behind"
        };

        public static string[] bossList = new string[]
        {
            "Satan", "CR666"
        };

        public static string[] largeBossList = new string[]
        {
            "Stealth Tank", "Terrorkopter", "Terrorbot", "Large Alien Worm",
        };

        public static string[] vehicleList = new string[]
        {
            "Mook Launcher Tank", "Cannon Tank", "Rocket Tank", "Artillery Truck", "Blimp", "Drill carrier", "Mook Truck", "Turret", "Motorbike", "Motorbike Nuclear", "Dump Truck"
        };

        public static string[] ammoList = new string[]
        {
            "Ammo", "Slow Time", "RC Car", "Air Strike", "Mech Drop", "Alien Pheromones", "Steroids", "Pig", "Flex Power"
        };

        static void OnGUI( UnityModManager.ModEntry modEntry )
        {
            WindowScaling.Enabled = settings.scaleUIWithWindowWidth;
            WindowScaling.TryCaptureWidth();

            settings.enableEnemyRandomization = GUILayout.Toggle( settings.enableEnemyRandomization, new GUIContent( "Enable Enemy Randomization", "Randomly replaces enemies of one type with another" ) );

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 500;

            GUI.Label( lastRect, GUI.tooltip );
            string previousToolTip = GUI.tooltip;

            GUILayout.Space( 20 );

            GUILayout.BeginHorizontal();

            settings.enableNormal = GUILayout.Toggle( settings.enableNormal, new GUIContent( "Allow Enemies to become other Normal Enemies", "Chance for enemies to become other normal enemies, aliens, or hell enemies" ) );

            settings.enableNormalSummoned = GUILayout.Toggle( settings.enableNormalSummoned, new GUIContent( "Allow Summoned Enemies to become other Normal Enemies", "Chance for enemies summoned by parachute, doors, bosses, or other enemies to become other normal enemies, aliens, or hell enemies" ) );

            GUILayout.EndHorizontal();

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }

            previousToolTip = GUI.tooltip;


            GUILayout.Space( 20 );

            if ( GUILayout.Button( "Normal Enemies" ) )
            {
                settings.showNormal = !settings.showNormal;
            }

            if ( settings.showNormal )
            {
                GUILayout.BeginVertical();
                {
                    for ( int i = 0; i < normalList.Length; ++i )
                    {
                        if ( i != 0 && i % 5 == 0 )
                        {
                            GUILayout.EndHorizontal();
                            GUILayout.Space( 5 );
                        }
                        if ( i % 5 == 0 )
                        {
                            GUILayout.BeginHorizontal();
                        }
                        bool containsBefore = settings.enabledNormal.Contains( i );

                        if ( containsBefore != GUILayout.Toggle( containsBefore, normalList[i], WindowScaling.ScaledWidth( 175 ) ) )
                        {
                            if ( containsBefore )
                            {
                                settings.enabledNormal.Remove( i );
                            }
                            else
                            {
                                settings.enabledNormal.Add( i );
                            }
                        }
                        if ( i + 1 == normalList.Length )
                        {
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                GUILayout.EndVertical();
                GUILayout.Space( 20 );
                GUILayout.BeginHorizontal();
                if ( GUILayout.Button( "Select All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledNormal.Clear();
                    for ( int i = 0; i < normalList.Length; ++i )
                    {
                        settings.enabledNormal.Add( i );
                    }
                }
                GUILayout.Space( 10 );
                if ( GUILayout.Button( "Deselect All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledNormal.Clear();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space( 40 );

            settings.enableWorms = GUILayout.Toggle( settings.enableWorms, new GUIContent( "Allow Enemies to become Worms", "Chance for enemies to become Sandworms or Boneworms" ) );

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }

            previousToolTip = GUI.tooltip;


            GUILayout.Space( 20 );

            if ( GUILayout.Button( "Worms" ) )
            {
                settings.showWorms = !settings.showWorms;
            }

            if ( settings.showWorms )
            {
                GUILayout.BeginHorizontal();
                {
                    for ( int i = 0; i < wormList.Length; ++i )
                    {
                        bool containsBefore = settings.enabledWorms.Contains( i );

                        if ( containsBefore != GUILayout.Toggle( containsBefore, wormList[i] ) )
                        {
                            if ( containsBefore )
                            {
                                settings.enabledWorms.Remove( i );
                            }
                            else
                            {
                                settings.enabledWorms.Add( i );
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space( 20 );
                GUILayout.BeginHorizontal();
                if ( GUILayout.Button( "Select All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledWorms.Clear();
                    for ( int i = 0; i < wormList.Length; ++i )
                    {
                        settings.enabledWorms.Add( i );
                    }
                }
                GUILayout.Space( 10 );
                if ( GUILayout.Button( "Deselect All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledWorms.Clear();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space( 40 );

            GUILayout.BeginHorizontal();

            settings.enableBosses = GUILayout.Toggle( settings.enableBosses, new GUIContent( "Allow Enemies to become Bosses", "Chance for enemies to become certain minibosses" ) );

            settings.enableBossSummoned = GUILayout.Toggle( settings.enableBossSummoned, new GUIContent( "Allow Summoned Enemies to become Bosses", "Chance for enemies summoned by parachute, doors, bosses, or other enemies to become certain minibosses" ) );

            GUILayout.EndHorizontal();

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }

            previousToolTip = GUI.tooltip;

            GUILayout.Space( 20 );

            if ( GUILayout.Button( "Bosses" ) )
            {
                settings.showBosses = !settings.showBosses;
            }

            if ( settings.showBosses )
            {
                GUILayout.BeginHorizontal();
                {
                    for ( int i = 0; i < bossList.Length; ++i )
                    {
                        bool containsBefore = settings.enabledBosses.Contains( i );

                        if ( containsBefore != GUILayout.Toggle( containsBefore, bossList[i] ) )
                        {
                            if ( containsBefore )
                            {
                                settings.enabledBosses.Remove( i );
                            }
                            else
                            {
                                settings.enabledBosses.Add( i );
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space( 20 );
                GUILayout.BeginHorizontal();
                if ( GUILayout.Button( "Select All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledBosses.Clear();
                    for ( int i = 0; i < bossList.Length; ++i )
                    {
                        settings.enabledBosses.Add( i );
                    }
                }
                GUILayout.Space( 10 );
                if ( GUILayout.Button( "Deselect All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledBosses.Clear();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space( 40 );

            settings.enableLargeBosses = GUILayout.Toggle( settings.enableLargeBosses, new GUIContent( "Allow Enemies to become Large Bosses", "Chance for enemies to become certain large bosses" ) );

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space( 20 );

            if ( GUILayout.Button( "Large Bosses" ) )
            {
                settings.showLargeBosses = !settings.showLargeBosses;
            }

            if ( settings.showLargeBosses )
            {
                GUILayout.BeginHorizontal();
                for ( int i = 0; i < largeBossList.Length; ++i )
                {
                    bool containsBefore = settings.enabledLargeBosses.Contains( i );
                    if ( containsBefore != GUILayout.Toggle( containsBefore, largeBossList[i] ) )
                    {
                        if ( containsBefore )
                        {
                            settings.enabledLargeBosses.Remove( i );
                        }
                        else
                        {
                            settings.enabledLargeBosses.Add( i );
                        }
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space( 20 );
                GUILayout.BeginHorizontal();
                if ( GUILayout.Button( "Select All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledLargeBosses.Clear();
                    for ( int i = 0; i < largeBossList.Length; ++i )
                    {
                        settings.enabledLargeBosses.Add( i );
                    }
                }
                GUILayout.Space( 10 );
                if ( GUILayout.Button( "Deselect All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledLargeBosses.Clear();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space( 40 );

            settings.enableVehicles = GUILayout.Toggle( settings.enableVehicles, new GUIContent( "Allow Enemies to become Vehicles", "Chance for enemies to become certain vehicles" ) );

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space( 20 );

            if ( GUILayout.Button( "Vehicles" ) )
            {
                settings.showVehicles = !settings.showVehicles;
            }

            if ( settings.showVehicles )
            {
                GUILayout.BeginVertical();
                for ( int i = 0; i < vehicleList.Length; ++i )
                {
                    if ( i != 0 && i % 5 == 0 )
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.Space( 5 );
                    }
                    if ( i % 5 == 0 )
                    {
                        GUILayout.BeginHorizontal();
                    }
                    bool containsBefore = settings.enabledVehicles.Contains( i );

                    if ( containsBefore != GUILayout.Toggle( containsBefore, vehicleList[i], WindowScaling.ScaledWidth( 175 ) ) )
                    {
                        if ( containsBefore )
                        {
                            settings.enabledVehicles.Remove( i );
                        }
                        else
                        {
                            settings.enabledVehicles.Add( i );
                        }
                    }
                    if ( i + 1 == vehicleList.Length )
                    {
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndVertical();
                GUILayout.Space( 20 );
                GUILayout.BeginHorizontal();
                if ( GUILayout.Button( "Select All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledVehicles.Clear();
                    for ( int i = 0; i < vehicleList.Length; ++i )
                    {
                        settings.enabledVehicles.Add( i );
                    }
                }
                GUILayout.Space( 10 );
                if ( GUILayout.Button( "Deselect All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledVehicles.Clear();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space( 40 );

            GUILayout.BeginHorizontal();

            settings.enableAmmoRandomization = GUILayout.Toggle( settings.enableAmmoRandomization, new GUIContent( "Randomize Ammo Crates", "Enables crates being randomized to other pickup types" ) );

            settings.enableCratesTurningIntoAmmo = GUILayout.Toggle( settings.enableCratesTurningIntoAmmo, new GUIContent( "Convert Wooden Boxes to Ammo Crates", "Converts wooden boxes to ammo crates, percentage chance can be controlled below" ) );

            settings.unlockAllFlexPowers = GUILayout.Toggle( settings.unlockAllFlexPowers, new GUIContent( "Unlock All Flex Powers", "Unlocks all flex powers immediately when starting a new game" ) );

            GUILayout.EndHorizontal();

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space( 20 );

            if ( GUILayout.Button( "Ammo Types" ) )
            {
                settings.showAmmo = !settings.showAmmo;
            }

            if ( settings.showAmmo )
            {
                GUILayout.BeginVertical();
                for ( int i = 0; i < ammoList.Length; ++i )
                {
                    if ( i != 0 && i % 5 == 0 )
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.Space( 5 );
                    }
                    if ( i % 5 == 0 )
                    {
                        GUILayout.BeginHorizontal();
                    }
                    bool containsBefore = settings.enabledAmmoTypes.Contains( i );

                    if ( containsBefore != GUILayout.Toggle( containsBefore, ammoList[i], WindowScaling.ScaledWidth( 175 ) ) )
                    {
                        if ( containsBefore )
                        {
                            settings.enabledAmmoTypes.Remove( i );
                        }
                        else
                        {
                            settings.enabledAmmoTypes.Add( i );
                        }
                    }
                    if ( i + 1 == ammoList.Length )
                    {
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndVertical();
                GUILayout.Space( 20 );
                GUILayout.BeginHorizontal();
                if ( GUILayout.Button( "Select All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledAmmoTypes.Clear();
                    for ( int i = 0; i < ammoList.Length; ++i )
                    {
                        settings.enabledAmmoTypes.Add( i );
                    }
                }
                GUILayout.Space( 10 );
                if ( GUILayout.Button( "Deselect All", WindowScaling.ScaledWidth( 100 ) ) )
                {
                    settings.enabledAmmoTypes.Clear();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space( 40 );

            settings.enableInstantWin = GUILayout.Toggle( settings.enableInstantWin, new GUIContent( "Killing Bosses triggers level finish", "If this is enabled, killing a boss will trigger a level end even if you're not on a boss level" ) );

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space( 20 );

            settings.enableDeathField = GUILayout.Toggle( settings.enableDeathField, new GUIContent( "Enable Satan Death Field", "This causes randomly spawned satan bosses to spawn their death fields after they are fully killed" ) );
            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space( 20 );

            settings.enableSpawnedEnemiesRandomization = GUILayout.Toggle( settings.enableSpawnedEnemiesRandomization,
                new GUIContent( "Enable Summoned Enemy Randomization", "This causes enemies summoned by parachute, doors, bosses, or other enemies to be randomized as well" ) );

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space( 20 );

            settings.scaleUIWithWindowWidth = GUILayout.Toggle( settings.scaleUIWithWindowWidth, new GUIContent( "Scale UI with Window Width", "Scales UI elements based on window width" ) );

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                GUI.Label( lastRect, GUI.tooltip );
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space( 25 );

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label( new GUIContent( "Randomized Enemy Percent: ",
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type." ), WindowScaling.ScaledWidth( 200 ) );

                GUILayout.Label( settings.enemyPercent.ToString( "0.00" ), WindowScaling.ScaledWidth( 100 ) );


                settings.enemyPercent = GUILayout.HorizontalSlider( settings.enemyPercent, 0, 100 );
            }
            GUILayout.EndHorizontal();

            GUILayout.Space( 15 );

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label( new GUIContent( "Normal Enemy Chance: ",
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type." ), WindowScaling.ScaledWidth( 200 ) );

                GUILayout.Label( settings.normalEnemyPercent.ToString( "0.00" ), WindowScaling.ScaledWidth( 100 ) );


                settings.normalEnemyPercent = GUILayout.HorizontalSlider( settings.normalEnemyPercent, 0, 100 );
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label( new GUIContent( "Worm Chance: ",
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type." ), WindowScaling.ScaledWidth( 200 ) );

                GUILayout.Label( settings.wormPercent.ToString( "0.00" ), WindowScaling.ScaledWidth( 100 ) );


                settings.wormPercent = GUILayout.HorizontalSlider( settings.wormPercent, 0, 100 );
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label( new GUIContent( "Boss Chance: ",
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type." ), WindowScaling.ScaledWidth( 200 ) );

                GUILayout.Label( settings.bossPercent.ToString( "0.00" ), WindowScaling.ScaledWidth( 100 ) );


                settings.bossPercent = GUILayout.HorizontalSlider( settings.bossPercent, 0, 100 );
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label( new GUIContent( "Large Boss Chance: ",
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type." ), WindowScaling.ScaledWidth( 200 ) );

                GUILayout.Label( settings.largeBossPercent.ToString( "0.00" ), WindowScaling.ScaledWidth( 100 ) );


                settings.largeBossPercent = GUILayout.HorizontalSlider( settings.largeBossPercent, 0, 100 );
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label( new GUIContent( "Vehicle Chance: ",
                    "Randomized enemy percent determines the chance for each enemy to be randomized, each of the other sliders determines how likely that enemy is to become an enemy of that type." ), WindowScaling.ScaledWidth( 200 ) );

                GUILayout.Label( settings.vehiclePercent.ToString( "0.00" ), WindowScaling.ScaledWidth( 100 ) );


                settings.vehiclePercent = GUILayout.HorizontalSlider( settings.vehiclePercent, 0, 100 );
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label( new GUIContent( "Ammo Chance: ",
                    "Chance for ammo crates to be randomized to other ammo types." ), WindowScaling.ScaledWidth( 200 ) );

                GUILayout.Label( settings.ammoRandomizationPercent.ToString( "0.00" ), WindowScaling.ScaledWidth( 100 ) );


                settings.ammoRandomizationPercent = GUILayout.HorizontalSlider( settings.ammoRandomizationPercent, 0, 100 );
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label( new GUIContent( "Wooden Box to Ammo Chance: ",
                    "Chance for wooden boxes to be turned into random ammo crates" ), WindowScaling.ScaledWidth( 200 ) );

                GUILayout.Label( settings.cratesToAmmoPercent.ToString( "0.00" ), WindowScaling.ScaledWidth( 100 ) );


                settings.cratesToAmmoPercent = GUILayout.HorizontalSlider( settings.cratesToAmmoPercent, 0, 100 );
            }
            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );

            if ( previousToolTip != GUI.tooltip )
            {
                lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 900;

                GUI.Label( lastRect, GUI.tooltip );
            }
            previousToolTip = GUI.tooltip;

            GUILayout.Space( 50 );



            //settings.DEBUG = GUILayout.Toggle(settings.DEBUG, "DEBUG");

            // DEBUG
            if ( settings.DEBUG )
            {
                debugMookString = GUILayout.TextField( debugMookString );

                int temp;
                if ( int.TryParse( debugMookString, out temp ) )
                    settings.debugMookType = temp;

                if ( settings.debugMookType > debugMookTypeList.Length - 1 )
                    settings.debugMookType = debugMookTypeList.Length - 1;

                if ( settings.debugMookType < 0 )
                    settings.debugMookType = 0;


                GUILayout.Label( "Current Enemy Type: " + debugMookTypeList[settings.debugMookType] );

                GUILayout.Space( 15 );

                debugMookStringSummoned = GUILayout.TextField( debugMookStringSummoned );

                if ( int.TryParse( debugMookStringSummoned, out temp ) )
                    settings.debugMookTypeSummoned = temp;

                if ( settings.debugMookTypeSummoned > debugMookSummonedList.Length )
                    settings.debugMookTypeSummoned = debugMookSummonedList.Length - 1;

                if ( settings.debugMookTypeSummoned < 0 )
                    settings.debugMookTypeSummoned = 0;

                int parsednum = settings.debugMookTypeSummoned;

                GUILayout.Label( "Current Summoned Enemy Type: " + debugMookSummonedList[parsednum] );

            }

        }

        static void OnSaveGUI( UnityModManager.ModEntry modEntry )
        {
            settings.Save( modEntry );
        }

        static bool OnToggle( UnityModManager.ModEntry modEntry, bool value )
        {
            enabled = value;
            return true;
        }

        public static void Log( String str )
        {
            mod.Logger.Log( str );
        }

    }
}