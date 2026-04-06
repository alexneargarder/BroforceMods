using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace Swap_Bros_Mod
{
    public static class API
    {
        #region Bro Lists

        /// <summary>
        /// Gets the current filtered list of available bros.
        /// Refreshes the list before returning to ensure it's up to date.
        /// </summary>
        /// <returns>The current bro list based on active filters and game state</returns>
        public static List<string> GetAvailableBros()
        {
            EnsureBroListUpdated();
            return Main.currentBroList;
        }

        /// <summary>
        /// Gets the full list of vanilla bros.
        /// </summary>
        /// <returns>List of vanilla bro display names</returns>
        public static List<string> GetAllNormalBros()
        {
            return Main.allNormal;
        }

        /// <summary>
        /// Gets the list of Expendabros.
        /// </summary>
        /// <returns>List of Expendabros display names</returns>
        public static List<string> GetAllExpendabros()
        {
            return Main.allExpendabros;
        }

        /// <summary>
        /// Gets the list of unfinished bros.
        /// </summary>
        /// <returns>List of unfinished bro display names</returns>
        public static List<string> GetAllUnfinishedBros()
        {
            return Main.allUnfinished;
        }

        /// <summary>
        /// Ensures the bro list is up to date with the current game state.
        /// </summary>
        public static void EnsureBroListUpdated()
        {
            try
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Update list if BroMaker is enabled and the custom bro count has changed
                if ( Main.settings.enableBromaker && Main.CustomCountChanged() )
                {
                    Main.LoadCustomBros();
                    Main.CreateBroList();
                }
                // Update list if in IronBro mode and the number of unlocked bros has changed
                else if ( GameModeController.IsHardcoreMode && !Main.settings.ignoreCurrentUnlocked )
                {
                    if ( Main.GetHardcoreCount() != Main.currentBroList.Count )
                    {
                        Main.CreateBroList();
                    }
                }
            }
            catch ( Exception ex )
            {
                Main.Log( "Exception in EnsureBroListUpdated: " + ex.ToString() );
            }
        }

        #endregion

        #region Bro Selection State

        /// <summary>
        /// Gets the display name of the currently selected bro for a player.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <returns>Bro display name</returns>
        public static string GetSelectedBroName( int playerNum )
        {
            return Main.currentBroList[Main.settings.selGridInt[playerNum]];
        }

        /// <summary>
        /// Gets the HeroType of the currently selected bro for a player.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <returns>HeroType enum value</returns>
        public static HeroType GetSelectedBroHeroType( int playerNum )
        {
            return StringToHeroType( Main.currentBroList[Main.settings.selGridInt[playerNum]] );
        }

        /// <summary>
        /// Gets the index into currentBroList of the currently selected bro for a player.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <returns>Index into the available bros list</returns>
        public static int GetSelectedBroIndex( int playerNum )
        {
            return Main.settings.selGridInt[playerNum];
        }

        /// <summary>
        /// Sets which bro the player has selected, by HeroType.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <param name="nextHero">HeroType to select</param>
        public static void SetSelectedBro( int playerNum, HeroType nextHero )
        {
            if ( GameModeController.IsHardcoreMode && !Main.settings.ignoreCurrentUnlocked )
            {
                Main.settings.selGridInt[playerNum] = Main.currentBroList.IndexOf( HeroTypeToString( nextHero ) );
                if ( Main.settings.selGridInt[playerNum] == -1 )
                {
                    Main.settings.selGridInt[playerNum] = 0;
                }
            }
            else
            {
                Main.settings.selGridInt[playerNum] = Main.currentBroList.IndexOf( HeroTypeToString( nextHero ) );
            }
        }

        /// <summary>
        /// Sets which bro the player will spawn as next, by name (case-insensitive).
        /// Automatically enables "Always Spawn as Chosen" to ensure the selection takes effect.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <param name="broName">Bro display name (e.g., "Rambro", "Brommando")</param>
        /// <returns>True if bro was found and selection was set</returns>
        public static bool SetSelectedBroByName( int playerNum, string broName )
        {
            try
            {
                EnsureBroListUpdated();

                int broIndex = -1;
                for ( int i = 0; i < Main.currentBroList.Count; i++ )
                {
                    if ( string.Equals( Main.currentBroList[i], broName, StringComparison.OrdinalIgnoreCase ) )
                    {
                        broIndex = i;
                        break;
                    }
                }

                if ( broIndex == -1 )
                {
                    return false;
                }

                Main.settings.selGridInt[playerNum] = broIndex;
                Main.settings.alwaysChosen = true;
                return true;
            }
            catch ( Exception ex )
            {
                Main.Log( "Exception in SetSelectedBroByName: " + ex.ToString() );
                return false;
            }
        }

        #endregion

        #region Bro Swapping

        /// <summary>
        /// Swaps a player to a specific bro by index in the current bro list.
        /// Preserves position and velocity during the swap.
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <param name="broIndex">Index into currentBroList</param>
        /// <returns>True if the swap was successful</returns>
        public static bool SwapToSpecificBro( int playerNum, int broIndex )
        {
            try
            {
                if ( !Main.enabled )
                {
                    return false;
                }

                if ( playerNum < 0 || playerNum > 3 )
                {
                    return false;
                }

                if ( broIndex < 0 || broIndex >= Main.currentBroList.Count )
                {
                    return false;
                }

                var player = HeroController.players[playerNum];
                if ( player == null || !player.IsAlive() || player.character == null )
                {
                    return false;
                }

                // Check if player is in a state where they can swap
                if ( player.character.pilottedUnit != null )
                {
                    return false;
                }

                // Get current position and velocity
                Vector3 position = player.GetCharacterPosition();
                float xI = (float)Traverse.Create( player.character ).Field( "xI" ).GetValue();
                float yI = (float)Traverse.Create( player.character ).Field( "yI" ).GetValue();

                // Set the selected bro index
                Main.settings.selGridInt[playerNum] = broIndex;

                // Mark manual spawn to prevent the spawn patch from changing our selection
                Main.manualSpawn = true;

                // Handle custom bro spawning
                if ( Main.settings.enableBromaker && Main.IsBroCustom( broIndex ) )
                {
                    Main.MakeCustomBroSpawn( playerNum, GetSelectedBroName( playerNum ) );
                    player.SetSpawnPositon( player._character, Player.SpawnType.TriggerSwapBro, false, position );
                    player.SpawnHero( HeroType.Rambro );
                }
                else
                {
                    player.SetSpawnPositon( player._character, Player.SpawnType.TriggerSwapBro, false, position );
                    if ( Main.settings.enableBromaker )
                    {
                        Main.DisableCustomBroSpawning( playerNum );
                    }
                    player.SpawnHero( GetSelectedBroHeroType( playerNum ) );
                    if ( Main.settings.enableBromaker )
                    {
                        Main.EnableCustomBroSpawning();
                    }
                }

                // Restore position, velocity and remove invulnerability
                player.character.SetPositionAndVelocity( position.x, position.y, xI, yI );
                player.character.SetInvulnerable( 0f, false );

                return true;
            }
            catch ( Exception ex )
            {
                Main.Log( "Exception in SwapToSpecificBro: " + ex.ToString() );
                return false;
            }
        }

        /// <summary>
        /// Swaps a player to a specific bro by name (case-insensitive).
        /// </summary>
        /// <param name="playerNum">Player number (0-3)</param>
        /// <param name="broName">Bro display name (e.g., "Rambro", "Brommando")</param>
        /// <returns>True if the swap was successful</returns>
        public static bool SwapByName( int playerNum, string broName )
        {
            try
            {
                EnsureBroListUpdated();

                int broIndex = -1;
                for ( int i = 0; i < Main.currentBroList.Count; i++ )
                {
                    if ( string.Equals( Main.currentBroList[i], broName, StringComparison.OrdinalIgnoreCase ) )
                    {
                        broIndex = i;
                        break;
                    }
                }

                if ( broIndex == -1 )
                {
                    return false;
                }

                return SwapToSpecificBro( playerNum, broIndex );
            }
            catch ( Exception ex )
            {
                Main.Log( "Exception in SwapByName: " + ex.ToString() );
                return false;
            }
        }

        #endregion

        #region Name Conversion

        /// <summary>
        /// Converts a HeroType enum value to its display name.
        /// </summary>
        /// <param name="hero">HeroType enum value</param>
        /// <returns>Display name string, or empty string if unknown</returns>
        public static string HeroTypeToString( HeroType hero )
        {
            switch ( hero )
            {
                case HeroType.Rambro: return "Rambro";
                case HeroType.Brommando: return "Brommando";
                case HeroType.BaBroracus: return "B. A. Broracus";
                case HeroType.BrodellWalker: return "Brodell Walker";
                case HeroType.BroHard: return "Bro Hard";
                case HeroType.McBrover: return "MacBrover";
                case HeroType.Blade: return "Brade";
                case HeroType.BroDredd: return "Bro Dredd";
                case HeroType.Brononymous: return "Bro In Black";
                case HeroType.SnakeBroSkin: return "Snake Broskin";
                case HeroType.Brominator: return "Brominator";
                case HeroType.Brobocop: return "Brobocop";
                case HeroType.IndianaBrones: return "Indiana Brones";
                case HeroType.AshBrolliams: return "Ash Brolliams";
                case HeroType.Nebro: return "Mr. Anderbro";
                case HeroType.BoondockBros: return "The Boondock Bros";
                case HeroType.Brochete: return "Brochete";
                case HeroType.BronanTheBrobarian: return "Bronan the Brobarian";
                case HeroType.EllenRipbro: return "Ellen Ripbro";
                case HeroType.TimeBroVanDamme: return "Time Bro";
                case HeroType.BroniversalSoldier: return "Broniversal Soldier";
                case HeroType.ColJamesBroddock: return "Colonel James Broddock";
                case HeroType.CherryBroling: return "Cherry Broling";
                case HeroType.BroMax: return "Bro Max";
                case HeroType.TheBrode: return "The Brode";
                case HeroType.DoubleBroSeven: return "Double Bro Seven";
                case HeroType.Predabro: return "The Brodator";
                case HeroType.TheBrocketeer: return "The Brocketeer";
                case HeroType.BroveHeart: return "Broheart";
                case HeroType.TheBrofessional: return "The Brofessional";
                case HeroType.Broden: return "Broden";
                case HeroType.TheBrolander: return "The Brolander";
                case HeroType.DirtyHarry: return "Dirty Brory";
                case HeroType.TankBro: return "Tank Bro";
                case HeroType.BroLee: return "Bro Lee";
                case HeroType.BrondleFly: return "Seth Brondle";
                case HeroType.Xebro: return "Xebro";
                case HeroType.Desperabro: return "Desperabro";
                case HeroType.Broffy: return "Broffy the Vampire Slayer";
                case HeroType.BroGummer: return "Burt Brommer";
                case HeroType.DemolitionBro: return "Demolition Bro";

                // Expendabros
                case HeroType.BroneyRoss: return "Broney Ross";
                case HeroType.LeeBroxmas: return "Lee Broxmas";
                case HeroType.BronnarJensen: return "Bronnar Jensen";
                case HeroType.HaleTheBro: return "Bro Caesar";
                case HeroType.TrentBroser: return "Trent Broser";
                case HeroType.Broc: return "Broctor Death";
                case HeroType.TollBroad: return "Toll Broad";

                // Unfinished
                case HeroType.ChevBrolios: return "Chev Brolios";
                case HeroType.CaseyBroback: return "Casey Broback";
                case HeroType.ScorpionBro: return "The Scorpion Bro";
            }
            return "";
        }

        /// <summary>
        /// Converts a bro display name to its HeroType enum value.
        /// </summary>
        /// <param name="hero">Display name string</param>
        /// <returns>HeroType enum value, or HeroType.None if unknown</returns>
        public static HeroType StringToHeroType( string hero )
        {
            switch ( hero )
            {
                case "Rambro": return HeroType.Rambro;
                case "Brommando": return HeroType.Brommando;
                case "B. A. Broracus": return HeroType.BaBroracus;
                case "Brodell Walker": return HeroType.BrodellWalker;
                case "Bro Hard": return HeroType.BroHard;
                case "MacBrover": return HeroType.McBrover;
                case "Brade": return HeroType.Blade;
                case "Bro Dredd": return HeroType.BroDredd;
                case "Bro In Black": return HeroType.Brononymous;
                case "Snake Broskin": return HeroType.SnakeBroSkin;
                case "Brominator": return HeroType.Brominator;
                case "Brobocop": return HeroType.Brobocop;
                case "Indiana Brones": return HeroType.IndianaBrones;
                case "Ash Brolliams": return HeroType.AshBrolliams;
                case "Mr. Anderbro": return HeroType.Nebro;
                case "The Boondock Bros": return HeroType.BoondockBros;
                case "Brochete": return HeroType.Brochete;
                case "Bronan the Brobarian": return HeroType.BronanTheBrobarian;
                case "Ellen Ripbro": return HeroType.EllenRipbro;
                case "Time Bro": return HeroType.TimeBroVanDamme;
                case "Broniversal Soldier": return HeroType.BroniversalSoldier;
                case "Colonel James Broddock": return HeroType.ColJamesBroddock;
                case "Cherry Broling": return HeroType.CherryBroling;
                case "Bro Max": return HeroType.BroMax;
                case "The Brode": return HeroType.TheBrode;
                case "Double Bro Seven": return HeroType.DoubleBroSeven;
                case "The Brodator": return HeroType.Predabro;
                case "The Brocketeer": return HeroType.TheBrocketeer;
                case "Broheart": return HeroType.BroveHeart;
                case "The Brofessional": return HeroType.TheBrofessional;
                case "Broden": return HeroType.Broden;
                case "The Brolander": return HeroType.TheBrolander;
                case "Dirty Brory": return HeroType.DirtyHarry;
                case "Tank Bro": return HeroType.TankBro;
                case "Bro Lee": return HeroType.BroLee;
                case "Seth Brondle": return HeroType.BrondleFly;
                case "Xebro": return HeroType.Xebro;
                case "Desperabro": return HeroType.Desperabro;
                case "Broffy the Vampire Slayer": return HeroType.Broffy;
                case "Burt Brommer": return HeroType.BroGummer;
                case "Demolition Bro": return HeroType.DemolitionBro;

                // Expendabros
                case "Broney Ross": return HeroType.BroneyRoss;
                case "Lee Broxmas": return HeroType.LeeBroxmas;
                case "Bronnar Jensen": return HeroType.BronnarJensen;
                case "Bro Caesar": return HeroType.HaleTheBro;
                case "Trent Broser": return HeroType.TrentBroser;
                case "Broctor Death": return HeroType.Broc;
                case "Toll Broad": return HeroType.TollBroad;

                // Unfinished
                case "Chev Brolios": return HeroType.ChevBrolios;
                case "Casey Broback": return HeroType.CaseyBroback;
                case "The Scorpion Bro": return HeroType.ScorpionBro;
            }
            return HeroType.None;
        }

        #endregion
    }
}
