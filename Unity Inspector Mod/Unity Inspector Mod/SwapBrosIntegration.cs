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
        private static bool _searched;
        private static bool _isAvailable;
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

        private static FieldInfo _settingsField;
        private static FieldInfo _ignoreCurrentUnlockedField;
        private static FieldInfo _alwaysChosenField;
        private static FieldInfo _selGridIntField;
        private static FieldInfo _allCustomBrosField;
        private static FieldInfo _actuallyAllCustomBrosField;
        private static FieldInfo _customBroCountField;
        private static MethodInfo _createBroListMethod;

        public static void Initialize( Action<string> logger )
        {
            _log = logger;
        }

        public static bool IsAvailable
        {
            get
            {
                if ( !_searched ) Search();
                return _isAvailable && _modEntry != null && _modEntry.Active;
            }
        }

        private static void Search()
        {
            _searched = true;

            try
            {
                _modEntry = UnityModManager.FindMod( "Swap Bros Mod" );
                if ( _modEntry == null || _modEntry.Assembly == null )
                    return;

                var mainType = _modEntry.Assembly.GetType( "Swap_Bros_Mod.API" );
                if ( mainType == null )
                    return;

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

                _isAvailable = _getAvailableBrosMethod != null &&
                               _swapToSpecificBroMethod != null;

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
            if ( !IsAvailable ) return new List<string>();

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
            if ( !IsAvailable || _getAllNormalBrosMethod == null ) return new List<string>();

            try
            {
                return _getAllNormalBrosMethod.Invoke( null, null ) as List<string> ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        public static List<string> GetAllExpendabros()
        {
            if ( !IsAvailable || _getAllExpendabrosMethod == null ) return new List<string>();

            try
            {
                return _getAllExpendabrosMethod.Invoke( null, null ) as List<string> ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        public static List<string> GetAllUnfinishedBros()
        {
            if ( !IsAvailable || _getAllUnfinishedBrosMethod == null ) return new List<string>();

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
                return -1;

            try
            {
                return (int)_getSelectedBroIndexMethod.Invoke( null, new object[] { playerNum } );
            }
            catch { return -1; }
        }

        public static string GetCurrentBroName( int playerNum )
        {
            if ( !IsAvailable || _getSelectedBroNameMethod == null ) return null;

            try
            {
                return _getSelectedBroNameMethod.Invoke( null, new object[] { playerNum } ) as string;
            }
            catch { return null; }
        }

        #endregion

        #region Swapping

        public static bool SwapToBro( int playerNum, int broIndex )
        {
            if ( !IsAvailable ) return false;

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
            if ( !IsAvailable || _swapByNameMethod == null ) return false;

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
            if ( !IsAvailable || _setSelectedBroByNameMethod == null ) return false;

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
                throw new InvalidOperationException( $"Swap Bros Mod has no {missing} member, so its unlock gates cannot be lifted" );
            }

            object settings = _settingsField.GetValue( null );
            if ( settings == null )
            {
                throw new InvalidOperationException( "Swap Bros Mod's settings are not loaded, so its unlock gates cannot be lifted" );
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
            List<string> installedBros = ReadBroMakerInstalledBros( out unlockedCount, out bromakerFailure );
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
                _createBroListMethod.Invoke( null, null );
                _selGridIntField.SetValue( settings, savedSelection );
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
            if ( _settingsField == null ) return "Main.settings";
            if ( _ignoreCurrentUnlockedField == null ) return "Settings.ignoreCurrentUnlocked";
            if ( _alwaysChosenField == null ) return "Settings.alwaysChosen";
            if ( _selGridIntField == null || _selGridIntField.FieldType != typeof( int[] ) ) return "Settings.selGridInt";
            if ( _allCustomBrosField == null ) return "Main.allCustomBros";
            if ( _actuallyAllCustomBrosField == null ) return "Main.actuallyAllCustomBros";
            if ( _customBroCountField == null || _customBroCountField.FieldType != typeof( int ) ) return "Main.customBroCount";
            if ( _createBroListMethod == null ) return "Main.CreateBroList()";
            return null;
        }

        private static List<string> ReadBroMakerInstalledBros( out int unlockedCount, out string failure )
        {
            unlockedCount = 0;
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

                unlockedCount = unlocked.Count;
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
