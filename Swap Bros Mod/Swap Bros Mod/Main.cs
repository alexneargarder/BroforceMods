using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BroMakerLib.CustomObjects;
using BroMakerLib.Loaders;
using BroMakerLib.Storages;
using HarmonyLib;
using RocketLib;
using UnityEngine;
using UnityModManagerNet;
using BSett = BroMakerLib.Settings;

namespace Swap_Bros_Mod
{
    public static class Main
    {
        public static KeyBindingForPlayers swapLeftKey;
        public static KeyBindingForPlayers swapRightKey;
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static float cooldown = 0;
        public static Settings settings;
        public static int currentCharIndex = 0;
        public static bool[] switched = { false, false, false, false };
        public static bool manualSpawn = false;
        public static bool creatingBroList = false;

        public static List<string> allNormal = new List<string> { "Rambro", "Brommando", "B. A. Broracus", "Brodell Walker", "Bro Hard", "MacBrover", "Brade", "Bro Dredd", "Bro In Black", "Snake Broskin", "Brominator",
                "Brobocop", "Indiana Brones", "Ash Brolliams", "Mr. Anderbro", "The Boondock Bros", "Brochete", "Bronan the Brobarian", "Ellen Ripbro", "Time Bro", "Broniversal Soldier",
                "Colonel James Broddock", "Cherry Broling", "Bro Max", "The Brode", "Double Bro Seven", "The Brodator", "The Brocketeer", "Broheart", "The Brofessional", "Broden",
                "The Brolander", "Dirty Brory", "Tank Bro", "Bro Lee", "Seth Brondle", "Xebro", "Desperabro", "Broffy the Vampire Slayer", "Burt Brommer", "Demolition Bro" };
        public static List<string> allExpendabros = new List<string> { "Broney Ross", "Lee Broxmas", "Bronnar Jensen", "Bro Caesar", "Trent Broser", "Broctor Death", "Toll Broad" };
        public static List<string> allUnfinished = new List<string> { "Chev Brolios", "Casey Broback", "The Scorpion Bro" };
        public static List<string> allCustomBros = new List<string>();
        public static List<string> actuallyAllCustomBros = new List<string>();
        public static int customBroCount = 0;
        public static List<string> allBros = new List<string>();
        public static int numCustomBros = 0;
        public static bool firstCustomBroLoad = true;

        public static List<string> currentBroList = new List<string>();
        public static List<string> currentBroListUnseen = new List<string>();
        public static string[] broList;
        public static string[] previousSelection = new string[] { "", "", "", "" };
        public static int maxBroNum = 40;
        public static bool isHardcore = false;

        public static bool changingEnabledBros = false;
        public static float displayWarningTime = 0f;
        public static bool[] filteredBroList;
        public static bool brosRemoved = false;
        public static GUIStyle buttonStyle;
        public static GUIStyle warningStyle;
        public static float cachedFilterWidth = 0f;

        public static bool Load( UnityModManager.ModEntry modEntry )
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = UnityModManager.ModSettings.Load<Settings>( modEntry );
            // Update selGridInt to an array
            if ( settings.selGridInt.Length < 4 )
            {
                settings.selGridInt = new int[] { 0, 0, 0, 0 };
            }
            var harmony = new Harmony( modEntry.Info.Id );
            var assembly = Assembly.GetExecutingAssembly();
            mod = modEntry;
            try
            {
                harmony.PatchAll( assembly );
            }
            catch ( Exception e )
            {
                mod.Logger.Error( e.ToString() );
            }

            LoadKeyBinding();

            // Check if bromaker is available unless it was manually toggled already
            if ( settings.enableBromakerDefault && !settings.enableBromaker )
            {
                try
                {
                    LoadCustomBros();
                    settings.enableBromaker = true;
                }
                catch
                {
                    settings.enableBromaker = false;
                }
            }
            else if ( settings.enableBromaker )
            {
                try
                {
                    LoadCustomBros();
                }
                catch
                {
                    Log( "BroMaker is not installed." );
                    settings.enableBromaker = false;
                }
            }

            CreateBroList();

            // Initialize enabled bro list
            if ( settings.enabledBros == null || settings.enabledBros.Count() == 0 )
            {
                allBros = new List<string>();
                allBros.AddRange( allNormal );
                if ( settings.includeExpendabros )
                {
                    allBros.AddRange( allExpendabros );
                }
                if ( settings.includeUnfinishedCharacters )
                {
                    allBros.AddRange( allUnfinished );
                }
                if ( settings.enableBromaker )
                {
                    allBros.AddRange( allCustomBros );
                }
                settings.enabledBros = new List<string>( allBros );
                filteredBroList = Enumerable.Repeat( true, allBros.Count() ).ToArray();
            }
            return true;
        }

        static void OnGUI( UnityModManager.ModEntry modEntry )
        {
            if ( buttonStyle == null )
            {
                buttonStyle = new GUIStyle( GUI.skin.button );
                buttonStyle.normal.textColor = Color.red;
                buttonStyle.hover.textColor = Color.red;
                buttonStyle.active.textColor = Color.red;

                buttonStyle.onHover.textColor = Color.green;
                buttonStyle.onNormal.textColor = Color.green;
                buttonStyle.onActive.textColor = Color.green;
            }

            if ( warningStyle == null )
            {
                warningStyle = new GUIStyle( GUI.skin.label );

                warningStyle.normal.textColor = Color.red;
                warningStyle.hover.textColor = Color.red;
                warningStyle.active.textColor = Color.red;
                warningStyle.onHover.textColor = Color.red;
                warningStyle.onNormal.textColor = Color.red;
                warningStyle.onActive.textColor = Color.red;
            }

            if ( isHardcore != GameModeController.IsHardcoreMode )
            {
                isHardcore = GameModeController.IsHardcoreMode;
                if ( !settings.ignoreCurrentUnlocked )
                {
                    CreateBroList();
                }
            }

            if ( settings.enableBromaker && CustomCountChanged() && !( GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked ) )
            {
                LoadCustomBros();
                CreateBroList();
            }

            if ( cachedFilterWidth <= 0 )
            {
                GUILayout.BeginHorizontal();
                Rect measureRect = GUILayoutUtility.GetRect( 0, 0, GUILayout.ExpandWidth( true ) );
                if ( Event.current.type == EventType.Repaint && measureRect.width > 1 )
                {
                    cachedFilterWidth = measureRect.width - 25f;
                }
                GUILayout.EndHorizontal();
                return;
            }

            GUILayout.BeginHorizontal();


            GUILayout.BeginVertical();

            settings.clickingEnabled = GUILayout.Toggle( settings.clickingEnabled, new GUIContent( "Clicking a new bro causes swap",
                "Clicking a bro in the menu below will swap you to that bro immediately" ), GUILayout.ExpandWidth( false ) );

            if ( settings.filterBros != ( settings.filterBros = GUILayout.Toggle( settings.filterBros, new GUIContent( "Filter Bros",
                "Only spawn as enabled characters" ), GUILayout.ExpandWidth( false ) ) ) )
            {
                CreateBroList();
            }

            settings.ignoreForcedBros = GUILayout.Toggle( settings.ignoreForcedBros, new GUIContent( "Ignore Forced Bros",
                "Controls whether levels which force you to use specific bros can be overidden by filtering or always spawn as chosen." ), GUILayout.ExpandWidth( false ) );

            // Display the tooltip from the element that has mouseover or keyboard focus
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 700;
            GUI.Label( lastRect, GUI.tooltip );

            GUILayout.EndVertical();


            GUILayout.BeginVertical();

            settings.alwaysChosen = GUILayout.Toggle( settings.alwaysChosen, new GUIContent( "Always spawn as chosen bro",
                "Prevents restarting a level or collecting a new bro from swapping to a new character" ), GUILayout.ExpandWidth( false ) );

            bool previousSetting = settings.ignoreCurrentUnlocked;
            if ( settings.ignoreCurrentUnlocked )
            {
                settings.ignoreCurrentUnlocked = GUILayout.Toggle( settings.ignoreCurrentUnlocked, new GUIContent( "Ignore currently unlocked bros in IronBro",
                    "Enabled: In IronBro you can switch to any bro regardless of who you have collected" ), GUILayout.ExpandWidth( false ) );
            }
            else
            {
                settings.ignoreCurrentUnlocked = GUILayout.Toggle( settings.ignoreCurrentUnlocked, new GUIContent( "Ignore currently unlocked bros in IronBro",
                    "Disabled: In IronBro you can only switch to bros you have collected" ), GUILayout.ExpandWidth( false ) );
            }
            if ( previousSetting != settings.ignoreCurrentUnlocked && isHardcore )
            {
                CreateBroList();
            }

            settings.useVanillaBroSelection = GUILayout.Toggle( settings.useVanillaBroSelection, new GUIContent( "Use Vanilla Randomization", settings.useVanillaBroSelection ? "Enabled: When filtering bros, prioritize bros that " +
                "you haven't played yet on this level" : "Disabled: When filtering bros, choose completely randomly from the enabled bros" ), GUILayout.ExpandWidth( false ) );

            GUI.Label( lastRect, GUI.tooltip );

            GUILayout.EndVertical();


            GUILayout.BeginVertical();

            if ( settings.includeExpendabros != ( settings.includeExpendabros = GUILayout.Toggle( settings.includeExpendabros, new GUIContent( "Include Expendabro Bros",
                "Include bros from Expendabros" ), GUILayout.ExpandWidth( false ) ) ) )
            {
                CreateBroList();
                CreateFilteredBroList();
            }

            if ( settings.enableBromaker != ( settings.enableBromaker = GUILayout.Toggle( settings.enableBromaker, new GUIContent( "Include BroMaker Bros",
                "Shows custom bros installed with BroMaker" ), GUILayout.ExpandWidth( false ) ) ) )
            {
                settings.enableBromakerDefault = false;
                if ( settings.enableBromaker )
                {
                    try
                    {
                        LoadCustomBros();
                        CreateBroList();
                        CreateFilteredBroList();
                    }
                    catch
                    {
                        Main.Log( "BroMaker is not installed." );
                        Main.settings.enableBromaker = false;
                    }
                }
                else
                {
                    CreateBroList();
                    CreateFilteredBroList();
                }
            }

            if ( settings.includeUnfinishedCharacters != ( settings.includeUnfinishedCharacters = GUILayout.Toggle( settings.includeUnfinishedCharacters, new GUIContent( "Include Unfinished Bros",
                "Include bros the developers didn't finish" ), GUILayout.ExpandWidth( false ) ) ) )
            {
                CreateBroList();
                CreateFilteredBroList();
            }

            GUI.Label( lastRect, GUI.tooltip );
            string previousToolTip = GUI.tooltip;

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space( 30 );

            GUILayout.BeginHorizontal();

            GUILayout.Label( String.Format( "Cooldown between swaps: {0:0.00}s", Main.settings.swapCoolDown ), GUILayout.Width( 225 ), GUILayout.ExpandWidth( false ) );
            Main.settings.swapCoolDown = GUILayout.HorizontalSlider( Main.settings.swapCoolDown, 0, 2 );

            GUILayout.EndHorizontal();

            GUILayout.Space( 20 );

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            for ( int i = 0; i < 4; ++i )
            {
                if ( GUILayout.Button( "Player " + ( i + 1 ) + " Options - " + ( settings.showSettings[i] ? "Hide" : "Show" ) ) )
                {
                    settings.showSettings[i] = !settings.showSettings[i];
                }
                if ( settings.showSettings[i] )
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label( "Swap Bro Left: ", GUILayout.ExpandWidth( false ) );
                    swapLeftKey.OnGUI( out _, false, true, ref previousToolTip, i, true, false, false );
                    GUILayout.Label( "Swap Bro Right: ", GUILayout.ExpandWidth( false ) );
                    swapRightKey.OnGUI( out _, false, true, ref previousToolTip, i, true, false, false );
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if ( GUILayout.Button( changingEnabledBros ? new GUIContent( "Save Changes", "" ) : new GUIContent( "Enter Filtering Mode",
                                               "Enable or disable bros for all players" ), GUILayout.ExpandWidth( false ), GUILayout.Width( 300 ) ) )
                    {
                        changingEnabledBros = !changingEnabledBros;
                        if ( changingEnabledBros )
                        {
                            displayWarningTime = 0f;
                            settings.filterBros = true;
                            CreateFilteredBroList();
                        }
                        else
                        {
                            // Check that at least one bro is enabled
                            bool atleastOne = false;
                            for ( int x = 0; x < filteredBroList.Length; ++x )
                            {
                                if ( filteredBroList[x] )
                                {
                                    atleastOne = true;
                                    break;
                                }
                            }
                            if ( atleastOne )
                            {
                                displayWarningTime = 0f;
                                UpdateFilteredBroList();
                                CreateBroList();
                            }
                            else
                            {
                                displayWarningTime = 10f;
                                changingEnabledBros = !changingEnabledBros;
                            }
                        }
                    }

                    lastRect = GUILayoutUtility.GetLastRect();
                    lastRect.y += 20;
                    lastRect.width += 700;

                    // Don't allow sorting method to change while filtering bros
                    if ( !changingEnabledBros )
                    {
                        GUILayout.FlexibleSpace();
                        // Display option to change sorting method
                        if ( GUILayout.Button( new GUIContent( settings.sortingMethodName, "Change the sorting used for displaying the bro list" ), GUILayout.Width( 300 ) ) )
                        {
                            switch ( settings.sorting )
                            {
                                case SortingMethod.UnlockOrder:
                                    settings.sorting = SortingMethod.AlphabeticalAZ;
                                    settings.sortingMethodName = "Sorting Method: Alphabetical A-Z";
                                    break;
                                case SortingMethod.AlphabeticalAZ:
                                    settings.sorting = SortingMethod.AlphabeticalZA;
                                    settings.sortingMethodName = "Sorting Method: Alphabetical Z-A";
                                    break;
                                case SortingMethod.AlphabeticalZA:
                                    settings.sorting = SortingMethod.UnlockOrder;
                                    settings.sortingMethodName = "Sorting Method: Unlock Order";
                                    break;
                            }
                            CreateBroList();
                        }
                    }

                    if ( displayWarningTime <= 0f )
                    {
                        if ( !GUI.tooltip.Equals( previousToolTip ) )
                        {
                            GUI.Label( lastRect, GUI.tooltip );
                        }
                        previousToolTip = GUI.tooltip;
                    }
                    else
                    {
                        displayWarningTime -= Time.unscaledDeltaTime;
                        GUI.Label( lastRect, "Must have at least one bro enabled", warningStyle );

                        previousToolTip = GUI.tooltip;
                    }

                    GUILayout.EndHorizontal();

                    GUILayout.Space( 25 );

                    GUILayout.BeginHorizontal();

                    if ( !creatingBroList )
                    {
                        // Display filtering menu
                        if ( changingEnabledBros )
                        {
                            int columnsPerRow = 5;
                            int totalBros = allBros.Count();

                            float buttonWidth = cachedFilterWidth / columnsPerRow;

                            GUILayout.BeginVertical();
                            GUILayout.BeginHorizontal();
                            for ( int j = 0; j < totalBros; ++j )
                            {
                                if ( j % columnsPerRow == 0 )
                                {
                                    GUILayout.EndHorizontal();
                                    GUILayout.BeginHorizontal();
                                }

                                filteredBroList[j] = GUILayout.Toggle( filteredBroList[j], allBros[j], buttonStyle, GUILayout.Height( 26 ), GUILayout.Width( buttonWidth ) );
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space( 20 );
                            GUILayout.BeginHorizontal();
                            if ( GUILayout.Button( "Select All", GUILayout.Width( 200 ) ) )
                            {
                                for ( int j = 0; j < filteredBroList.Length; ++j )
                                {
                                    filteredBroList[j] = true;
                                }
                            }
                            if ( GUILayout.Button( "Unselect All", GUILayout.Width( 200 ) ) )
                            {
                                for ( int j = 0; j < filteredBroList.Length; ++j )
                                {
                                    filteredBroList[j] = false;
                                }
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.EndVertical();
                        }
                        // Display bro selection menu
                        else
                        {
                            if ( settings.selGridInt[i] < 0 || settings.selGridInt[i] >= broList.Length )
                            {
                                settings.selGridInt[i] = 0;
                            }

                            if ( settings.selGridInt[i] <= maxBroNum )
                            {
                                if ( settings.clickingEnabled )
                                {
                                    if ( settings.selGridInt[i] != ( settings.selGridInt[i] = GUILayout.SelectionGrid( settings.selGridInt[i], broList, 5, GUILayout.Height( 30 * Mathf.Ceil( broList.Length / 5.0f ) ) ) )
                                        && ( Map.Instance != null ) )
                                    {
                                        switched[i] = true;
                                    }
                                }
                                else
                                {
                                    settings.selGridInt[i] = GUILayout.SelectionGrid( settings.selGridInt[i], broList, 5, GUILayout.Height( 30 * Mathf.Ceil( broList.Length / 5.0f ) ) );
                                }
                            }
                            else
                            {
                                CreateBroList();
                            }
                        }
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.Space( 10 );
                }
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
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

        public static void LoadKeyBinding()
        {
            swapLeftKey = AllModKeyBindings.LoadKeyBinding( "Swap Bros Mod", "Swap Left" );
            swapRightKey = AllModKeyBindings.LoadKeyBinding( "Swap Bros Mod", "Swap Right" );
        }

        public static void Log( String str )
        {
            mod.Logger.Log( str );
        }

        public static void CreateBroList()
        {
            creatingBroList = true;
            brosRemoved = false;

            for ( int i = 0; i < 4; ++i )
            {
                if ( currentBroList != null && settings.selGridInt[i] > 0 && settings.selGridInt[i] < currentBroList.Count() )
                {
                    previousSelection[i] = currentBroList[settings.selGridInt[i]];
                }
                else
                {
                    previousSelection[i] = string.Empty;
                }
            }
            // If in IronBro and not ignoring unlocked characters, only show unlocked ones
            if ( GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked )
            {
                currentBroList = new List<string>();
                for ( int i = 0; i < GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count(); ++i )
                {
                    currentBroList.Add( API.HeroTypeToString( GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros[i] ) );
                }

                if ( settings.enableBromaker )
                {
                    LoadCustomBros();
                    // Check if only custom bros in hardcore setting is enabled, and if so, remove the vanilla bros
                    if ( OnlyCustomBrosInHardcore() )
                    {
                        currentBroList.Clear();
                    }
                    currentBroList.AddRange( allCustomBros );
                }
            }
            else
            {
                if ( settings.filterBros && settings.enabledBros.Count() > 0 )
                {
                    currentBroList = new List<string>();
                    for ( int i = 0; i < allNormal.Count(); ++i )
                    {
                        if ( settings.enabledBros.Contains( allNormal[i] ) )
                        {
                            currentBroList.Add( allNormal[i] );
                        }
                        else
                        {
                            brosRemoved = true;
                        }
                    }

                    if ( settings.includeExpendabros )
                    {
                        for ( int i = 0; i < allExpendabros.Count(); ++i )
                        {
                            if ( settings.enabledBros.Contains( allExpendabros[i] ) )
                            {
                                currentBroList.Add( allExpendabros[i] );
                            }
                            else
                            {
                                brosRemoved = true;
                            }
                        }
                    }
                    if ( settings.includeUnfinishedCharacters )
                    {
                        for ( int i = 0; i < allUnfinished.Count(); ++i )
                        {
                            if ( settings.enabledBros.Contains( allUnfinished[i] ) )
                            {
                                currentBroList.Add( allUnfinished[i] );
                            }
                            else
                            {
                                brosRemoved = true;
                            }
                        }
                    }
                    if ( settings.enableBromaker )
                    {
                        for ( int i = 0; i < allCustomBros.Count(); ++i )
                        {
                            if ( settings.enabledBros.Contains( allCustomBros[i] ) )
                            {
                                currentBroList.Add( allCustomBros[i] );
                            }
                            else
                            {
                                brosRemoved = true;
                            }
                        }
                    }
                }
                else
                {
                    currentBroList = new List<string>();
                    currentBroList.AddRange( allNormal );

                    if ( settings.includeExpendabros )
                    {
                        currentBroList.AddRange( allExpendabros );
                    }
                    if ( settings.includeUnfinishedCharacters )
                    {
                        currentBroList.AddRange( allUnfinished );
                    }
                    if ( settings.enableBromaker )
                    {
                        currentBroList.AddRange( allCustomBros );
                    }
                }
            }

            maxBroNum = currentBroList.Count() - 1;

            if ( settings.sorting == SortingMethod.AlphabeticalAZ )
            {
                currentBroList.Sort();
            }
            else if ( settings.sorting == SortingMethod.AlphabeticalZA )
            {
                currentBroList.Sort();
                currentBroList.Reverse();
            }

            broList = currentBroList.ToArray();
            // Ensure currently selected bros are all still valid
            for ( int i = 0; i < 4; ++i )
            {
                if ( previousSelection[i] != string.Empty )
                {
                    settings.selGridInt[i] = currentBroList.IndexOf( previousSelection[i] );
                    if ( settings.selGridInt[i] == -1 )
                    {
                        settings.selGridInt[i] = 0;
                    }
                }

                if ( settings.selGridInt[i] >= currentBroList.Count() || settings.selGridInt[i] < 0 )
                {
                    settings.selGridInt[i] = 0;
                }
            }

            currentBroListUnseen.Clear();
            currentBroListUnseen.AddRange( currentBroList );
            creatingBroList = false;
        }

        public static void CreateFilteredBroList()
        {
            allBros = new List<string>();
            allBros.AddRange( allNormal );
            if ( settings.includeExpendabros )
            {
                allBros.AddRange( allExpendabros );
            }
            if ( settings.includeUnfinishedCharacters )
            {
                allBros.AddRange( allUnfinished );
            }
            if ( settings.enableBromaker )
            {
                allBros.AddRange( allCustomBros );
            }

            if ( settings.sorting == SortingMethod.AlphabeticalAZ )
            {
                allBros.Sort();
            }
            else if ( settings.sorting == SortingMethod.AlphabeticalZA )
            {
                allBros.Sort();
                allBros.Reverse();
            }

            filteredBroList = Enumerable.Repeat( false, allBros.Count() ).ToArray();
            // Find index of the enabled bro in allBros and set the corresponding index in filteredBroList to true
            for ( int i = 0; i < settings.enabledBros.Count(); ++i )
            {
                int index = allBros.IndexOf( settings.enabledBros[i] );
                if ( index != -1 )
                {
                    filteredBroList[index] = true;
                }
            }
        }

        public static void UpdateFilteredBroList()
        {
            settings.enabledBros.Clear();
            for ( int i = 0; i < filteredBroList.Length; ++i )
            {
                if ( filteredBroList[i] )
                {
                    settings.enabledBros.Add( allBros[i] );
                }
            }
        }

        public static bool CustomCountChanged()
        {
            if ( GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked )
            {
                return BroSpawnManager.GetAllSpawnableBrosNames().Count() != customBroCount;
            }
            else
            {
                return BroSpawnManager.GetAllUnlockedBrosNames().Count() != customBroCount;
            }
        }

        public static void LoadCustomBros()
        {
            if ( Main.firstCustomBroLoad )
            {
                Main.firstCustomBroLoad = false;
                // If bro is unlocked and we're filtering bros, add it to the enabled list
                BroSpawnManager.RegisterNotifyBroUnlocked( ( broName, bro ) =>
                {
                    if ( settings.filterBros && !settings.enabledBros.Contains( broName ) )
                    {
                        settings.enabledBros.Add( broName );
                        LoadCustomBros();
                        CreateBroList();
                        CreateFilteredBroList();
                    }
                } );
            }
            if ( GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked )
            {
                allCustomBros = BroSpawnManager.GetAllSpawnableBrosNames();
                actuallyAllCustomBros = BroSpawnManager.GetAllUnlockedBrosNames();
            }
            else
            {
                allCustomBros = BroSpawnManager.GetAllUnlockedBrosNames();
                actuallyAllCustomBros = allCustomBros;
            }

            customBroCount = allCustomBros.Count();
        }

        public static bool IsBroCustom( int index )
        {
            return actuallyAllCustomBros.Count() > 0 && actuallyAllCustomBros.Contains( currentBroList[index] );
        }



        public static bool CheckIfCustomBro( TestVanDammeAnim character, ref string name )
        {
            if ( character is ICustomHero hero )
            {
                name = hero.Info.name;
                return true;
            }

            return false;
        }

        public static bool CheckIfCustomBroJustUnlocked( int playerNum )
        {
            return LoadHero.willPlayCutscene[playerNum];
        }

        public static bool CheckIfForcedCustomBro()
        {
            return BroSpawnManager.ForceCustomThisLevel;
        }

        public static void MakeCustomBroSpawn( int curPlayer, string name )
        {
            LoadHero.willReplaceBro[curPlayer] = true;
            BSett.instance.overrideNextBroSpawn = true;
            BSett.instance.nextBroSpawn = name;
        }

        public static void EnableCustomBroSpawning()
        {
            BSett.instance.disableSpawning = false;
        }

        public static void DisableCustomBroSpawning( int curPlayer )
        {
            BSett.instance.disableSpawning = true;
            LoadHero.willReplaceBro[curPlayer] = false;
        }

        public static bool IsCustomBroSpawning( int curPlayer )
        {
            return LoadHero.willReplaceBro[curPlayer];
        }

        public static bool IsCustcenePlaying()
        {
            return LoadHero.playCutscene;
        }

        public static int GetHardcoreCount()
        {
            if ( settings.enableBromaker )
            {
                return GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count() + GetBromakerHardcoreAmount();
            }
            else
            {
                return GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count();
            }
        }

        public static int GetBromakerHardcoreAmount()
        {
            return BroSpawnManager.HardcoreAvailableBros.Count();
        }

        public static bool OnlyCustomBrosInHardcore()
        {
            return BSett.instance.onlyCustomInHardcore;
        }

    }
}


