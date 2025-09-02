using System;
using System.Collections.Generic;
using System.Reflection;

namespace Utility_Mod
{
    /// <summary>
    /// Provides integration with the Swap Bros Mod through reflection.
    /// This allows Utility Mod to interact with Swap Bros without a hard dependency.
    /// </summary>
    public static class SwapBrosIntegration
    {
        #region Fields

        private static bool _initialized = false;
        private static bool _isAvailable = false;

        // Cached reflection data
        private static Type _mainType;
        private static FieldInfo _enabledField;
        private static FieldInfo _currentBroListField;
        private static FieldInfo _settingsField;
        private static FieldInfo _selGridIntField;
        private static MethodInfo _ensureBroListUpdatedMethod;
        private static MethodInfo _swapToSpecificBroMethod;

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether Swap Bros Mod is available and enabled
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                if ( !_initialized )
                {
                    Initialize();
                }
                bool result = _isAvailable && IsEnabled();
                return result;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the integration by finding and caching all necessary reflection data
        /// </summary>
        private static void Initialize()
        {
            try
            {
                // Try to find the Swap Bros Mod assembly
                Assembly swapBrosAssembly = null;
                foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
                {
                    if ( assembly.GetName().Name == "Swap Bros Mod" )
                    {
                        swapBrosAssembly = assembly;
                        break;
                    }
                }

                if ( swapBrosAssembly == null )
                {
                    _initialized = true;
                    _isAvailable = false;
                    return;
                }

                // Find the Main type
                _mainType = swapBrosAssembly.GetType( "Swap_Bros_Mod.Main" );
                if ( _mainType == null )
                {
                    _initialized = true;
                    _isAvailable = false;
                    return;
                }

                // Cache all reflection data
                _enabledField = _mainType.GetField( "enabled", BindingFlags.Public | BindingFlags.Static );
                _currentBroListField = _mainType.GetField( "currentBroList", BindingFlags.Public | BindingFlags.Static );
                _settingsField = _mainType.GetField( "settings", BindingFlags.Public | BindingFlags.Static );
                _ensureBroListUpdatedMethod = _mainType.GetMethod( "EnsureBroListUpdated", BindingFlags.Public | BindingFlags.Static );
                _swapToSpecificBroMethod = _mainType.GetMethod( "SwapToSpecificBro", BindingFlags.Public | BindingFlags.Static );

                // Get selGridInt field from settings type if settings field is found
                if ( _settingsField != null )
                {
                    var settingsValue = _settingsField.GetValue( null );
                    if ( settingsValue != null )
                    {
                        _selGridIntField = settingsValue.GetType().GetField( "selGridInt" );
                    }
                }

                // Verify we found everything we need
                _isAvailable = _enabledField != null &&
                               _currentBroListField != null &&
                               _ensureBroListUpdatedMethod != null &&
                               _swapToSpecificBroMethod != null;

                _initialized = true;
            }
            catch ( Exception ex )
            {
                Main.Log( $"Failed to initialize Swap Bros integration: {ex.Message}" );
                _initialized = true;
                _isAvailable = false;
            }
        }

        /// <summary>
        /// Checks if Swap Bros Mod is currently enabled
        /// </summary>
        private static bool IsEnabled()
        {
            try
            {
                if ( _enabledField != null )
                {
                    return (bool)_enabledField.GetValue( null );
                }
            }
            catch { }
            return false;
        }

        #endregion

        #region Swap Bros API

        /// <summary>
        /// Gets the current list of available bros from Swap Bros Mod
        /// </summary>
        /// <returns>List of bro names, or empty list if unavailable</returns>
        public static List<string> GetAvailableBros()
        {
            if ( !IsAvailable )
            {
                return new List<string>();
            }

            try
            {
                // Ensure the bro list is up to date
                _ensureBroListUpdatedMethod.Invoke( null, null );

                // Get the current bro list
                var broList = _currentBroListField.GetValue( null ) as List<string>;
                return broList ?? new List<string>();
            }
            catch ( Exception ex )
            {
                Main.Log( $"Failed to get available bros: {ex.Message}" );
                return new List<string>();
            }
        }

        /// <summary>
        /// Gets the current bro index for the specified player
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <returns>Current bro index, or -1 if unable to determine</returns>
        public static int GetCurrentBroIndex( int playerNum )
        {
            if ( !IsAvailable || playerNum < 0 || playerNum > 3 )
            {
                return -1;
            }

            try
            {
                if ( _settingsField != null && _selGridIntField != null )
                {
                    var settings = _settingsField.GetValue( null );
                    if ( settings != null )
                    {
                        var selGridInt = _selGridIntField.GetValue( settings ) as int[];
                        if ( selGridInt != null && playerNum < selGridInt.Length )
                        {
                            return selGridInt[playerNum];
                        }
                    }
                }
            }
            catch { }

            return -1;
        }

        /// <summary>
        /// Attempts to swap the player to a specific bro
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <param name="broIndex">Index of the bro in the available bros list</param>
        /// <returns>True if swap was successful</returns>
        public static bool SwapToBro( int playerNum, int broIndex )
        {
            if ( !IsAvailable )
            {
                return false;
            }

            try
            {
                var result = _swapToSpecificBroMethod.Invoke( null, new object[] { playerNum, broIndex } );
                return (bool)result;
            }
            catch ( Exception ex )
            {
                Main.Log( $"Failed to swap to bro: {ex.Message}" );
                return false;
            }
        }

        #endregion
    }
}