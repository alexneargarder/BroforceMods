using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RocketLib.Collections;
using UnityModManagerNet;

namespace Unity_Inspector_Mod
{
    public static class SwapBrosIntegration
    {
        private static bool _isAvailable;
        private static bool _lacksApi;
        private static Action<string> _log;

        private static UnityModManager.ModEntry _modEntry;

        private static MethodInfo _getAvailableBrosMethod;
        private static MethodInfo _getAllNormalBrosMethod;
        private static MethodInfo _getAllExpendabrosMethod;
        private static MethodInfo _getAllUnfinishedBrosMethod;
        private static MethodInfo _getSelectedBroIndexMethod;
        private static MethodInfo _getSelectedBroNameMethod;
        private static MethodInfo _swapToSpecificBroMethod;
        private static MethodInfo _swapByNameMethod;
        private static MethodInfo _setSelectedBroByNameMethod;
        private static MethodInfo _heroTypeToStringMethod;

        private static FieldInfo _settingsField;
        private static FieldInfo _ignoreCurrentUnlockedField;
        private static FieldInfo _alwaysChosenField;
        private static FieldInfo _selGridIntField;
        private static FieldInfo _allCustomBrosField;
        private static FieldInfo _actuallyAllCustomBrosField;
        private static FieldInfo _customBroCountField;
        private static MethodInfo _createBroListMethod;

        private static FieldInfo _enableBromakerField;
        private static FieldInfo _includeExpendabrosField;
        private static FieldInfo _includeUnfinishedField;
        private static FieldInfo _filterBrosField;
        private static FieldInfo _enabledBrosField;

        public static void Initialize( Action<string> logger )
        {
            _log = logger;
        }

        public static bool IsAvailable
        {
            get
            {
                if ( _modEntry == null || _modEntry.Assembly == null )
                {
                    Search();
                }
                return _isAvailable && _modEntry != null && _modEntry.Active;
            }
        }

        public static string OutdatedVersion
        {
            get
            {
                if ( !_lacksApi || _modEntry == null || !_modEntry.Active )
                {
                    return null;
                }
                return _modEntry.Info.Version;
            }
        }

        public sealed class IncompatibleException : InvalidOperationException
        {
            public IncompatibleException( string message ) : base( message ) { }
        }

        private static void Search()
        {
            _isAvailable = false;
            _lacksApi = false;

            try
            {
                _modEntry = UnityModManager.FindMod( "Swap Bros Mod" );
                if ( _modEntry == null || _modEntry.Assembly == null )
                {
                    return;
                }

                var mainType = _modEntry.Assembly.GetType( "Swap_Bros_Mod.API" );
                if ( mainType == null )
                {
                    _lacksApi = true;
                    return;
                }

                var pub = BindingFlags.Public | BindingFlags.Static;

                _getAvailableBrosMethod = mainType.GetMethod( "GetAvailableBros", pub );
                _getAllNormalBrosMethod = mainType.GetMethod( "GetAllNormalBros", pub );
                _getAllExpendabrosMethod = mainType.GetMethod( "GetAllExpendabros", pub );
                _getAllUnfinishedBrosMethod = mainType.GetMethod( "GetAllUnfinishedBros", pub );
                _getSelectedBroIndexMethod = mainType.GetMethod( "GetSelectedBroIndex", pub );
                _getSelectedBroNameMethod = mainType.GetMethod( "GetSelectedBroName", pub );
                _swapToSpecificBroMethod = mainType.GetMethod( "SwapToSpecificBro", pub );
                _swapByNameMethod = mainType.GetMethod( "SwapByName", pub );
                _setSelectedBroByNameMethod = mainType.GetMethod( "SetSelectedBroByName", pub );
                _heroTypeToStringMethod = mainType.GetMethod( "HeroTypeToString", pub, null, new[] { typeof( HeroType ) }, null );

                _isAvailable = _getAvailableBrosMethod != null &&
                               _swapToSpecificBroMethod != null;
                _lacksApi = !_isAvailable;

                var modMainType = _modEntry.Assembly.GetType( "Swap_Bros_Mod.Main" );
                if ( modMainType != null )
                {
                    _settingsField = modMainType.GetField( "settings", pub );
                    _allCustomBrosField = modMainType.GetField( "allCustomBros", pub );
                    _actuallyAllCustomBrosField = modMainType.GetField( "actuallyAllCustomBros", pub );
                    _customBroCountField = modMainType.GetField( "customBroCount", pub );
                    _createBroListMethod = modMainType.GetMethod( "CreateBroList", pub, null, Type.EmptyTypes, null );

                    if ( _settingsField != null )
                    {
                        var inst = BindingFlags.Public | BindingFlags.Instance;
                        _ignoreCurrentUnlockedField = _settingsField.FieldType.GetField( "ignoreCurrentUnlocked", inst );
                        _alwaysChosenField = _settingsField.FieldType.GetField( "alwaysChosen", inst );
                        _selGridIntField = _settingsField.FieldType.GetField( "selGridInt", inst );
                        _enableBromakerField = _settingsField.FieldType.GetField( "enableBromaker", inst );
                        _includeExpendabrosField = _settingsField.FieldType.GetField( "includeExpendabros", inst );
                        _includeUnfinishedField = _settingsField.FieldType.GetField( "includeUnfinishedCharacters", inst );
                        _filterBrosField = _settingsField.FieldType.GetField( "filterBros", inst );
                        _enabledBrosField = _settingsField.FieldType.GetField( "enabledBros", inst );
                    }
                }
            }
            catch ( Exception ex )
            {
                Log( "Failed to initialize Swap Bros integration: " + ex.Message );
                _isAvailable = false;
            }
        }

        #region Bro Listing
        public static List<string> GetAvailableBros()
        {
            if ( !IsAvailable )
            {
                return new List<string>();
            }

            try
            {
                return _getAvailableBrosMethod.Invoke( null, null ) as List<string> ?? new List<string>();
            }
            catch ( Exception ex )
            {
                Log( "Failed to get available bros: " + ex.Message );
                return new List<string>();
            }
        }

        public static List<string> GetAllNormalBros()
        {
            if ( !IsAvailable || _getAllNormalBrosMethod == null )
            {
                return new List<string>();
            }

            try
            {
                return _getAllNormalBrosMethod.Invoke( null, null ) as List<string> ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        public static List<string> GetAllExpendabros()
        {
            if ( !IsAvailable || _getAllExpendabrosMethod == null )
            {
                return new List<string>();
            }

            try
            {
                return _getAllExpendabrosMethod.Invoke( null, null ) as List<string> ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        public static List<string> GetAllUnfinishedBros()
        {
            if ( !IsAvailable || _getAllUnfinishedBrosMethod == null )
            {
                return new List<string>();
            }

            try
            {
                return _getAllUnfinishedBrosMethod.Invoke( null, null ) as List<string> ?? new List<string>();
            }
            catch { return new List<string>(); }
        }
        #endregion

        #region Current State
        public static int GetCurrentBroIndex( int playerNum )
        {
            if ( !IsAvailable || _getSelectedBroIndexMethod == null || playerNum < Nums.PLAYER_MIN || playerNum >= Nums.PLAYER_MAX )
            {
                return -1;
            }

            try
            {
                return (int)_getSelectedBroIndexMethod.Invoke( null, new object[] { playerNum } );
            }
            catch { return -1; }
        }

        public static string GetSelectedBroName( int playerNum )
        {
            if ( !IsAvailable || _getSelectedBroNameMethod == null )
            {
                return null;
            }

            try
            {
                return _getSelectedBroNameMethod.Invoke( null, new object[] { playerNum } ) as string;
            }
            catch { return null; }
        }

        public static string GetLiveBroName( TestVanDammeAnim character )
        {
            if ( character == null )
            {
                return null;
            }

            string custom = CustomHeroName( character );
            if ( custom != null )
            {
                return custom;
            }

            if ( IsAvailable && _heroTypeToStringMethod != null )
            {
                try
                {
                    string name = _heroTypeToStringMethod.Invoke( null, new object[] { character.heroType } ) as string;
                    if ( !string.IsNullOrEmpty( name ) )
                    {
                        return name;
                    }
                }
                catch { }
            }

            return character.heroType.ToString();
        }

        public static bool IsCustomBro( TestVanDammeAnim character )
        {
            return character != null && CustomHeroName( character ) != null;
        }

        private static string CustomHeroName( TestVanDammeAnim character )
        {
            try
            {
                foreach ( Type iface in character.GetType().GetInterfaces() )
                {
                    if ( iface.FullName != "BroMakerLib.CustomObjects.ICustomHero" )
                    {
                        continue;
                    }

                    PropertyInfo infoProperty = iface.GetProperty( "Info" );
                    object info = infoProperty == null ? null : infoProperty.GetValue( character, null );
                    FieldInfo nameField = info == null ? null : info.GetType().GetField( "name", BindingFlags.Public | BindingFlags.Instance );
                    string name = nameField == null ? null : nameField.GetValue( info ) as string;
                    return string.IsNullOrEmpty( name ) ? null : name;
                }
            }
            catch ( Exception ex )
            {
                Log( "Failed to read a BroMaker bro's name: " + ex.Message );
            }

            return null;
        }
        #endregion

        #region Roster Diagnosis
        public static string FindKnownBro( string broName, out string origin )
        {
            origin = null;
            string match;

            if ( ( match = FindIn( GetAllNormalBros(), broName ) ) != null )
            {
                origin = "vanilla";
            }
            else if ( ( match = FindIn( GetAllExpendabros(), broName ) ) != null )
            {
                origin = "expendabros";
            }
            else if ( ( match = FindIn( GetAllUnfinishedBros(), broName ) ) != null )
            {
                origin = "unfinished";
            }
            else
            {
                List<string> unlocked;
                string failure;
                match = FindIn( ReadBroMakerBros( out unlocked, out failure ), broName );
                if ( match != null )
                {
                    origin = "custom";
                }
            }

            return match;
        }

        public static List<string> AllKnownBros()
        {
            var names = new List<string>();
            names.AddRange( GetAllNormalBros() );
            names.AddRange( GetAllExpendabros() );
            names.AddRange( GetAllUnfinishedBros() );

            List<string> unlocked;
            string failure;
            List<string> custom = ReadBroMakerBros( out unlocked, out failure );
            if ( custom != null )
            {
                names.AddRange( custom );
            }

            return names;
        }

        public static bool IsInCurrentList( string broName )
        {
            return FindIn( GetAvailableBros(), broName ) != null;
        }

        public static string UnreachableReason( string broName, string origin, bool ignoringUnlocks, bool suggestIgnoreUnlocks )
        {
            object settings = null;
            try { settings = _settingsField == null ? null : _settingsField.GetValue( null ); }
            catch { }

            Func<FieldInfo, bool> option = field =>
            {
                try { return settings != null && field != null && (bool)field.GetValue( settings ); }
                catch { return false; }
            };

            string hint = suggestIgnoreUnlocks ? " - pass ignoreUnlocks: true to swap it in anyway" : "";
            bool ironBroGate = !ignoringUnlocks && GameModeController.IsHardcoreMode && !option( _ignoreCurrentUnlockedField );

            if ( origin == "custom" )
            {
                if ( !option( _enableBromakerField ) )
                {
                    return "Swap Bros Mod's Include BroMaker Bros option is off";
                }
                if ( ironBroGate )
                {
                    return "this IronBro run's roster does not hold it" + hint;
                }

                List<string> unlocked;
                string failure;
                ReadBroMakerBros( out unlocked, out failure );
                if ( !ignoringUnlocks && unlocked != null && FindIn( unlocked, broName ) == null )
                {
                    return "it is locked" + hint;
                }
            }
            else
            {
                if ( ironBroGate )
                {
                    return "this IronBro run's roster does not hold it" + hint;
                }
                if ( origin == "expendabros" && !option( _includeExpendabrosField ) )
                {
                    return "Swap Bros Mod's Include Expendabro Bros option is off";
                }
                if ( origin == "unfinished" && !option( _includeUnfinishedField ) )
                {
                    return "Swap Bros Mod's Include Unfinished Bros option is off";
                }
            }

            if ( option( _filterBrosField ) )
            {
                List<string> enabled = null;
                try { enabled = _enabledBrosField == null ? null : _enabledBrosField.GetValue( settings ) as List<string>; }
                catch { }

                if ( enabled != null && enabled.Count > 0 && !enabled.Contains( broName ) )
                {
                    return "Swap Bros Mod's Filter Bros option leaves it out";
                }
            }

            return "Swap Bros Mod's settings do not say why";
        }

        private static FieldInfo _gameModeBeforeBrodownField;

        public static string NextSpawnOverride( int playerNum )
        {
            object settings = null;
            try { settings = _settingsField == null ? null : _settingsField.GetValue( null ); }
            catch { }
            if ( settings == null )
            {
                return null;
            }

            Func<string, bool> option = name =>
            {
                try
                {
                    FieldInfo field = settings.GetType().GetField( name, BindingFlags.Public | BindingFlags.Instance );
                    return field != null && (bool)field.GetValue( settings );
                }
                catch { return false; }
            };

            if ( option( "enableBromaker" ) )
            {
                bool[] cutscene = BroMakerStatic( "BroMakerLib.Loaders.LoadHero", "willPlayCutscene" ) as bool[];
                if ( cutscene != null && playerNum < cutscene.Length && cutscene[playerNum] )
                {
                    return "BroMaker spawns a just-unlocked custom bro next";
                }
                if ( BroMakerStatic( "BroMakerLib.Storages.BroSpawnManager", "ForceCustomThisLevel" ) as bool? == true )
                {
                    return "BroMaker forces custom bros on this level";
                }
            }

            if ( option( "ignoreForcedBros" ) )
            {
                return null;
            }

            if ( IsVersusPickMode() )
            {
                return "this game mode spawns each player's pick";
            }

            MapData map = Map.MapData;
            if ( map != null && ( map.forcedBro != HeroType.Random || ( map.forcedBros != null && map.forcedBros.Count > 0 ) ) )
            {
                return "this level forces its bros";
            }
            return null;
        }

        private static bool IsVersusPickMode()
        {
            if ( GameModeController.GameMode == GameMode.BroDown )
            {
                if ( _gameModeBeforeBrodownField == null )
                {
                    _gameModeBeforeBrodownField = typeof( GameModeController ).GetField( "gameModeBeforeBrodown", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );
                }
                object before = _gameModeBeforeBrodownField == null ? null : _gameModeBeforeBrodownField.GetValue( null );
                return before is GameMode && ( (GameMode)before == GameMode.DeathMatch || (GameMode)before == GameMode.Race );
            }
            return GameModeController.IsDeathMatchMode || GameModeController.GameMode == GameMode.Race;
        }

        private static object BroMakerStatic( string typeName, string member )
        {
            try
            {
                foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
                {
                    if ( assembly.GetName().Name != "BroMakerLib" )
                    {
                        continue;
                    }

                    Type type = assembly.GetType( typeName );
                    if ( type == null )
                    {
                        return null;
                    }

                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                    FieldInfo field = type.GetField( member, flags );
                    if ( field != null )
                    {
                        return field.GetValue( null );
                    }
                    PropertyInfo property = type.GetProperty( member, flags );
                    return property == null ? null : property.GetValue( null, null );
                }
            }
            catch ( Exception ex )
            {
                Log( $"Reading BroMakerLib's {typeName}.{member} failed: {ex.GetBaseException().Message}" );
            }
            return null;
        }

        public static bool? ReadAlwaysChosen()
        {
            try
            {
                object settings = _settingsField == null ? null : _settingsField.GetValue( null );
                if ( settings == null || _alwaysChosenField == null )
                {
                    return null;
                }
                return (bool)_alwaysChosenField.GetValue( settings );
            }
            catch { return null; }
        }

        private static string FindIn( IEnumerable<string> names, string broName )
        {
            if ( names == null )
            {
                return null;
            }
            return names.FirstOrDefault( name => string.Equals( name, broName, StringComparison.OrdinalIgnoreCase ) );
        }
        #endregion

        #region Swapping
        public static bool SwapToBro( int playerNum, int broIndex )
        {
            if ( !IsAvailable )
            {
                return false;
            }

            try
            {
                return (bool)_swapToSpecificBroMethod.Invoke( null, new object[] { playerNum, broIndex } );
            }
            catch ( Exception ex )
            {
                Log( "Failed to swap to bro: " + ex.Message );
                return false;
            }
        }

        public static bool SwapToBroByName( int playerNum, string broName )
        {
            if ( !IsAvailable || _swapByNameMethod == null )
            {
                return false;
            }

            try
            {
                return (bool)_swapByNameMethod.Invoke( null, new object[] { playerNum, broName } );
            }
            catch ( Exception ex )
            {
                Log( "Failed to swap to bro by name: " + ex.Message );
                return false;
            }
        }
        #endregion

        #region Next Spawn Selection
        public static bool SetNextBroByName( int playerNum, string broName )
        {
            if ( !IsAvailable || _setSelectedBroByNameMethod == null )
            {
                return false;
            }

            try
            {
                return (bool)_setSelectedBroByNameMethod.Invoke( null, new object[] { playerNum, broName } );
            }
            catch ( Exception ex )
            {
                Log( "Failed to set next bro by name: " + ex.Message );
                return false;
            }
        }
        #endregion

        #region Unlock Override
        public static object RunIgnoringUnlocks( string broName, Func<bool> swap )
        {
            string missing = MissingUnlockMembers();
            if ( missing != null )
            {
                throw new IncompatibleException( $"Swap Bros Mod has no {missing} member, so its unlock gates cannot be lifted" );
            }

            object settings = _settingsField.GetValue( null );
            if ( settings == null )
            {
                throw new IncompatibleException( "Swap Bros Mod's settings are not loaded, so its unlock gates cannot be lifted" );
            }

            bool savedIgnore = (bool)_ignoreCurrentUnlockedField.GetValue( settings );
            bool savedAlwaysChosen = (bool)_alwaysChosenField.GetValue( settings );
            int[] savedSelection = (int[])( (int[])_selGridIntField.GetValue( settings ) ).Clone();
            object savedCustom = _allCustomBrosField.GetValue( null );
            object savedActuallyCustom = _actuallyAllCustomBrosField.GetValue( null );
            object savedCustomCount = _customBroCountField.GetValue( null );

            bool rosterGate = GameModeController.IsHardcoreMode && !savedIgnore;
            int unlockedCount;
            string bromakerFailure;
            List<string> unlockedBros;
            List<string> installedBros = ReadBroMakerBros( out unlockedBros, out bromakerFailure );
            unlockedCount = unlockedBros == null ? 0 : unlockedBros.Count;
            IEnumerable<string> customInEffect = installedBros ?? savedCustom as IEnumerable<string>;
            bool swapped = false;

            try
            {
                _ignoreCurrentUnlockedField.SetValue( settings, true );

                if ( installedBros != null )
                {
                    _allCustomBrosField.SetValue( null, installedBros );
                    _actuallyAllCustomBrosField.SetValue( null, installedBros );
                    // CustomCountChanged compares the unlocked count to customBroCount, so matching it stops a reload over this list.
                    _customBroCountField.SetValue( null, unlockedCount );
                }

                _createBroListMethod.Invoke( null, null );
                swapped = swap();
            }
            finally
            {
                _ignoreCurrentUnlockedField.SetValue( settings, savedIgnore );
                _alwaysChosenField.SetValue( settings, savedAlwaysChosen );
                _allCustomBrosField.SetValue( null, savedCustom );
                _actuallyAllCustomBrosField.SetValue( null, savedActuallyCustom );
                _customBroCountField.SetValue( null, savedCustomCount );
                try
                {
                    _createBroListMethod.Invoke( null, null );
                }
                finally
                {
                    _selGridIntField.SetValue( settings, savedSelection );
                }
            }

            return new
            {
                rosterGate,
                ignoreCurrentUnlocked = (bool)_ignoreCurrentUnlockedField.GetValue( settings ),
                broFrom = swapped ? BroOrigin( broName, customInEffect ) : null,
                customListReplaced = installedBros != null,
                source = installedBros != null ? "GetAllBros" : "unchanged",
                note = bromakerFailure
            };
        }

        private static string BroOrigin( string broName, IEnumerable<string> customBros )
        {
            Func<IEnumerable<string>, bool> holds = names =>
                names != null && names.Any( name => string.Equals( name, broName, StringComparison.OrdinalIgnoreCase ) );

            if ( holds( GetAllNormalBros() ) || holds( GetAllExpendabros() ) || holds( GetAllUnfinishedBros() ) )
            {
                return "vanilla";
            }

            return holds( customBros ) ? "custom" : null;
        }

        private static string MissingUnlockMembers()
        {
            if ( _settingsField == null )
            {
                return "Main.settings";
            }
            if ( _ignoreCurrentUnlockedField == null )
            {
                return "Settings.ignoreCurrentUnlocked";
            }
            if ( _alwaysChosenField == null )
            {
                return "Settings.alwaysChosen";
            }
            if ( _selGridIntField == null || _selGridIntField.FieldType != typeof( int[] ) )
            {
                return "Settings.selGridInt";
            }
            if ( _allCustomBrosField == null )
            {
                return "Main.allCustomBros";
            }
            if ( _actuallyAllCustomBrosField == null )
            {
                return "Main.actuallyAllCustomBros";
            }
            if ( _customBroCountField == null || _customBroCountField.FieldType != typeof( int ) )
            {
                return "Main.customBroCount";
            }
            if ( _createBroListMethod == null )
            {
                return "Main.CreateBroList()";
            }
            return null;
        }

        private static List<string> ReadBroMakerBros( out List<string> unlockedNames, out string failure )
        {
            unlockedNames = null;
            failure = null;

            try
            {
                Type spawnManager = null;
                foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
                {
                    if ( assembly.GetName().Name == "BroMakerLib" )
                    {
                        spawnManager = assembly.GetType( "BroMakerLib.Storages.BroSpawnManager" );
                        break;
                    }
                }

                if ( spawnManager == null )
                {
                    failure = "BroMakerLib is not loaded - the custom bro list was left unchanged";
                    return null;
                }

                var pub = BindingFlags.Public | BindingFlags.Static;
                MethodInfo allBrosMethod = spawnManager.GetMethod( "GetAllBros", pub, null, Type.EmptyTypes, null );
                MethodInfo unlockedMethod = spawnManager.GetMethod( "GetAllUnlockedBrosNames", pub, null, Type.EmptyTypes, null );
                if ( allBrosMethod == null || unlockedMethod == null )
                {
                    failure = "BroMakerLib has no BroSpawnManager.GetAllBros or GetAllUnlockedBrosNames - the custom bro list was left unchanged";
                    return null;
                }

                var allBros = allBrosMethod.Invoke( null, null ) as IEnumerable;
                var unlocked = unlockedMethod.Invoke( null, null ) as ICollection;
                if ( allBros == null || unlocked == null )
                {
                    failure = "BroMakerLib's bro lists are not loaded - the custom bro list was left unchanged";
                    return null;
                }

                var names = new List<string>();
                foreach ( object hero in allBros )
                {
                    PropertyInfo nameProperty = hero == null ? null : hero.GetType().GetProperty( "name", BindingFlags.Public | BindingFlags.Instance );
                    string name = nameProperty == null ? null : nameProperty.GetValue( hero, null ) as string;
                    if ( !string.IsNullOrEmpty( name ) && !names.Contains( name ) )
                    {
                        names.Add( name );
                    }
                }

                unlockedNames = new List<string>();
                foreach ( object name in unlocked )
                {
                    if ( name is string )
                    {
                        unlockedNames.Add( (string)name );
                    }
                }

                return names;
            }
            catch ( Exception ex )
            {
                failure = $"Reading BroMakerLib's installed bros failed ({ex.GetBaseException().Message}) - the custom bro list was left unchanged";
                return null;
            }
        }
        #endregion

        private static void Log( string message )
        {
            if ( _log != null )
            {
                _log( "[SwapBrosIntegration] " + message );
            }
        }
    }
}
