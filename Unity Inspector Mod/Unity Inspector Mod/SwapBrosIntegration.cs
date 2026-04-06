using System;
using System.Collections.Generic;
using System.Reflection;
using UnityModManagerNet;

namespace Unity_Inspector_Mod
{
    public static class SwapBrosIntegration
    {
        private static bool _searched;
        private static bool _isAvailable;
        private static Action<string> _log;

        private static UnityModManager.ModEntry _modEntry;

        // Cached methods — all public static on Swap_Bros_Mod.Main
        private static MethodInfo _getAvailableBrosMethod;
        private static MethodInfo _getAllNormalBrosMethod;
        private static MethodInfo _getAllExpendabrosMethod;
        private static MethodInfo _getAllUnfinishedBrosMethod;
        private static MethodInfo _getSelectedBroIndexMethod;
        private static MethodInfo _getSelectedBroNameMethod;
        private static MethodInfo _swapToSpecificBroMethod;
        private static MethodInfo _swapByNameMethod;
        private static MethodInfo _setSelectedBroByNameMethod;

        /// <summary>
        /// Initialize with a logger. Call once during mod Load().
        /// </summary>
        /// <param name="logger">Logging callback for debug messages</param>
        public static void Initialize( Action<string> logger )
        {
            _log = logger;
        }

        /// <summary>
        /// Whether Swap Bros Mod is loaded and active in UMM.
        /// </summary>
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
            }
            catch ( Exception ex )
            {
                Log( "Failed to initialize Swap Bros integration: " + ex.Message );
                _isAvailable = false;
            }
        }

        #region Bro Listing

        /// <summary>
        /// Gets the current filtered list of available bros.
        /// </summary>
        /// <returns>List of bro display names, or empty list if unavailable</returns>
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

        /// <summary>
        /// Gets the full list of 40 vanilla bros.
        /// </summary>
        /// <returns>List of bro display names, or empty list if unavailable</returns>
        public static List<string> GetAllNormalBros()
        {
            if ( !IsAvailable || _getAllNormalBrosMethod == null ) return new List<string>();

            try
            {
                return _getAllNormalBrosMethod.Invoke( null, null ) as List<string> ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        /// <summary>
        /// Gets the list of Expendabros.
        /// </summary>
        /// <returns>List of bro display names, or empty list if unavailable</returns>
        public static List<string> GetAllExpendabros()
        {
            if ( !IsAvailable || _getAllExpendabrosMethod == null ) return new List<string>();

            try
            {
                return _getAllExpendabrosMethod.Invoke( null, null ) as List<string> ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        /// <summary>
        /// Gets the list of unfinished bros.
        /// </summary>
        /// <returns>List of bro display names, or empty list if unavailable</returns>
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

        /// <summary>
        /// Gets the current bro selection index for the specified player.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <returns>Index into the available bros list, or -1 if unavailable</returns>
        public static int GetCurrentBroIndex( int playerNum )
        {
            if ( !IsAvailable || _getSelectedBroIndexMethod == null || playerNum < 0 || playerNum > 3 )
                return -1;

            try
            {
                return (int)_getSelectedBroIndexMethod.Invoke( null, new object[] { playerNum } );
            }
            catch { return -1; }
        }

        /// <summary>
        /// Gets the current bro selection name for the specified player.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <returns>Bro display name, or null if unavailable</returns>
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

        /// <summary>
        /// Swaps the player to a specific bro by index in the available bros list.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <param name="broIndex">Index into the available bros list</param>
        /// <returns>True if the swap was successful</returns>
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

        /// <summary>
        /// Swaps the player to a specific bro by name (case-insensitive).
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <param name="broName">Bro display name (e.g., "Rambro", "Brommando")</param>
        /// <returns>True if the swap was successful</returns>
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

        /// <summary>
        /// Sets which bro the player will spawn as next (case-insensitive).
        /// Automatically enables "Always Spawn as Chosen" in Swap Bros Mod.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <param name="broName">Bro display name (e.g., "Rambro", "Brommando")</param>
        /// <returns>True if the bro was found and selection was set</returns>
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

        private static void Log( string message )
        {
            if ( _log != null )
            {
                _log( "[SwapBrosIntegration] " + message );
            }
        }
    }
}
