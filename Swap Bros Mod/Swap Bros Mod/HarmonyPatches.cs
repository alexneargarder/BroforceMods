using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Swap_Bros_Mod
{
    public class HarmonyPatches
    {
        [HarmonyPatch( typeof( Player ), "SpawnHero" )]
        static class Player_SpawnHero_Patch
        {
            public static void Prefix( Player __instance, ref HeroType nextHeroType )
            {
                if ( !Main.enabled )
                    return;

                try
                {
                    if ( Main.manualSpawn )
                    {
                        Main.manualSpawn = false;
                        return;
                    }

                    int curPlayer = __instance.playerNum;

                    // Give BroMaker unlocks priority over everything else
                    if ( Main.settings.enableBromaker && Main.CheckIfCustomBroJustUnlocked( curPlayer ) )
                    {
                        return;
                    }

                    // Give BroMaker forced bros priority over everything else
                    if ( Main.settings.enableBromaker && Main.CheckIfForcedCustomBro() )
                    {
                        return;
                    }

                    if ( !Main.settings.alwaysChosen )
                    {
                        if ( GameState.Instance.hardCoreMode && !Main.settings.ignoreCurrentUnlocked )
                        {
                            Main.CreateBroList();
                        }

                        // Set next hero to one of the enabled ones to ensure we don't spawn as a disabled character
                        if ( Main.settings.filterBros && Main.brosRemoved && !GameModeController.IsHardcoreMode )
                        {
                            // Check if ignore is enabled and map has a forced bro
                            if ( !Main.settings.ignoreForcedBros && Map.MapData.forcedBro != HeroType.Random )
                            {
                                nextHeroType = Map.MapData.forcedBro;

                                int nextHero = Main.currentBroList.IndexOf( Main.HeroTypeToString( nextHeroType ) );

                                if ( nextHero == -1 )
                                {
                                    nextHero = 0;
                                }

                                Main.settings.selGridInt[curPlayer] = nextHero;
                            }
                            // Check if ignore is disabled and map has multiple forced bros
                            else if ( !Main.settings.ignoreForcedBros && Map.MapData.forcedBros != null && Map.MapData.forcedBros.Count() > 0 )
                            {
                                string nextHeroName = Main.currentBroListUnseen[UnityEngine.Random.Range( 0, Main.currentBroListUnseen.Count() )];

                                nextHeroType = Main.StringToHeroType( nextHeroName );

                                int nextHero = Main.currentBroList.IndexOf( nextHeroName );

                                if ( nextHero == -1 )
                                {
                                    nextHero = 0;
                                }

                                Main.settings.selGridInt[curPlayer] = nextHero;
                            }
                            else
                            {
                                int nextHero = 0;

                                // If we're using vanilla bro selection and there are still bros that we haven't spawned as, prioritize those first
                                if ( Main.settings.useVanillaBroSelection && Main.currentBroListUnseen.Count() > 0 )
                                {
                                    // Check if a previous character exists and ensure we don't spawn as them if possible
                                    string previousCharacter = string.Empty;
                                    if ( __instance.character != null )
                                    {
                                        if ( !( Main.settings.enableBromaker && Main.CheckIfCustomBro( __instance.character, ref previousCharacter ) ) )
                                        {
                                            previousCharacter = Main.HeroTypeToString( __instance.character.heroType );
                                        }

                                        // Don't remove bro unless this is a new list
                                        if ( Main.currentBroListUnseen.Contains( previousCharacter ) && Main.currentBroListUnseen.Count() > 1 )
                                        {
                                            Main.currentBroListUnseen.Remove( previousCharacter );
                                        }
                                        else
                                        {
                                            previousCharacter = string.Empty;
                                        }
                                    }
                                    nextHero = Main.currentBroList.IndexOf( Main.currentBroListUnseen[UnityEngine.Random.Range( 0, Main.currentBroListUnseen.Count() )] );
                                    if ( previousCharacter != string.Empty )
                                    {
                                        Main.currentBroListUnseen.Add( previousCharacter );
                                    }

                                    if ( nextHero == -1 )
                                    {
                                        nextHero = 0;
                                    }
                                }
                                else
                                {
                                    nextHero = UnityEngine.Random.Range( 0, Main.currentBroList.Count() );
                                }

                                // Check if bro is custom or not
                                if ( Main.IsBroCustom( nextHero ) )
                                {
                                    Main.MakeCustomBroSpawn( curPlayer, Main.currentBroList[nextHero] );
                                    nextHeroType = HeroType.Rambro;
                                }
                                else
                                {
                                    if ( Main.settings.enableBromaker )
                                        Main.DisableCustomBroSpawning( curPlayer );

                                    nextHeroType = Main.StringToHeroType( Main.currentBroList[nextHero] );
                                }

                                Main.settings.selGridInt[curPlayer] = nextHero;
                            }
                        }
                        else
                        {
                            Main.SetSelectedBro( __instance.playerNum, nextHeroType );
                        }
                        return;
                    }
                    // Ensure we aren't overwriting forced bros, even if always chosen is enabled
                    else if ( !Main.settings.ignoreForcedBros && Map.MapData != null && ( Map.MapData.forcedBro != HeroType.Random || ( Map.MapData.forcedBros != null && Map.MapData.forcedBros.Count() > 0 ) ) )
                    {
                        return;
                    }

                    // If we're in IronBro and don't want to force spawn a bro we haven't unlocked
                    if ( GameState.Instance.hardCoreMode && !Main.settings.ignoreCurrentUnlocked )
                    {
                        // Make sure list of available hardcore bros is up-to-date
                        Main.CreateBroList();
                        // If Bromaker is enabled and selected character is custom
                        if ( Main.settings.enableBromaker && Main.IsBroCustom( Main.settings.selGridInt[curPlayer] ) )
                        {
                            Main.MakeCustomBroSpawn( curPlayer, Main.GetSelectedBroName( curPlayer ) );
                            // Ensure we don't spawn boondock bros because one gets left over
                            nextHeroType = HeroType.Rambro;
                        }
                        else
                        {
                            if ( Main.settings.enableBromaker )
                                Main.DisableCustomBroSpawning( curPlayer );
                            nextHeroType = Main.GetSelectedBroHeroType( curPlayer );
                        }
                    }
                    // If bro spawning is a custom bro
                    else if ( Main.settings.enableBromaker && Main.IsBroCustom( Main.settings.selGridInt[curPlayer] ) )
                    {
                        Main.MakeCustomBroSpawn( curPlayer, Main.GetSelectedBroName( curPlayer ) );
                        // Ensure we don't spawn boondock bros because one gets left over
                        nextHeroType = HeroType.Rambro;
                    }
                    // If we're just spawning a normal character
                    else
                    {
                        if ( Main.settings.enableBromaker )
                            Main.DisableCustomBroSpawning( curPlayer );
                        nextHeroType = Main.GetSelectedBroHeroType( curPlayer );
                    }

                }
                catch ( Exception ex )
                {
                    Main.Log( "Exception occurred while spawning bro: " + ex.ToString() );
                }
            }
            static void Postfix( Player __instance, ref HeroType nextHeroType )
            {
                if ( !Main.enabled )
                    return;

                if ( Main.settings.enableBromaker )
                {
                    Main.EnableCustomBroSpawning();
                    string name = "";
                    if ( Main.CheckIfCustomBro( __instance.character, ref name ) )
                    {
                        if ( name != Main.GetSelectedBroName( __instance.playerNum ) )
                        {
                            Main.settings.selGridInt[__instance.playerNum] = Main.currentBroList.IndexOf( name );
                            if ( Main.settings.selGridInt[__instance.playerNum] == -1 )
                            {
                                Main.CreateBroList();
                                Main.settings.selGridInt[__instance.playerNum] = Main.currentBroList.IndexOf( name );
                            }
                        }

                        Main.currentBroListUnseen.Remove( name );
                    }
                    else
                    {
                        Main.currentBroListUnseen.Remove( Main.HeroTypeToString( nextHeroType ) );
                    }
                }
                else
                {
                    Main.currentBroListUnseen.Remove( Main.HeroTypeToString( nextHeroType ) );
                }

                if ( Main.currentBroListUnseen.Count() == 0 )
                {
                    if ( !Main.settings.ignoreForcedBros && Map.MapData != null && Map.MapData.forcedBros != null && Map.MapData.forcedBros.Count > 0 )
                    {
                        Main.currentBroListUnseen.Clear();
                        for ( int i = 0; i < Map.MapData.forcedBros.Count(); ++i )
                        {
                            Main.currentBroListUnseen.Add( Main.HeroTypeToString( Map.MapData.forcedBros[i] ) );
                        }
                    }
                    else
                    {
                        Main.currentBroListUnseen.AddRange( Main.currentBroList );
                    }
                }
            }
        }


        [HarmonyPatch( typeof( Player ), "GetInput" )]
        static class Player_GetInput_Patch
        {
            public static void Postfix( Player __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                int curPlayer = __instance.playerNum;
                bool leftPressed = Main.swapLeftKey.IsDown( __instance.playerNum );
                bool rightPressed = Main.swapRightKey.IsDown( __instance.playerNum );

                if ( ( ( ( leftPressed || rightPressed ) && Main.cooldown == 0f && __instance.IsAlive() ) || ( Main.settings.clickingEnabled && Main.switched[curPlayer] ) ) && __instance.character.pilottedUnit == null )
                {
                    // If clicking is enabled and player clicked a bro
                    if ( Main.settings.clickingEnabled && Main.switched[curPlayer] )
                    {
                        Main.SwapToSpecificBro( curPlayer, Main.settings.selGridInt[curPlayer] );
                        Main.switched[curPlayer] = false;
                        return;
                    }

                    // If our list of characters is out of date, update it
                    Main.EnsureBroListUpdated();

                    int targetIndex = Main.settings.selGridInt[curPlayer];

                    if ( leftPressed )
                    {
                        targetIndex--;
                        if ( targetIndex < 0 )
                        {
                            targetIndex = Main.maxBroNum;
                        }
                    }
                    else if ( rightPressed )
                    {
                        targetIndex++;
                        if ( targetIndex > Main.maxBroNum )
                        {
                            targetIndex = 0;
                        }
                    }

                    if ( Main.SwapToSpecificBro( curPlayer, targetIndex ) )
                    {
                        Main.cooldown = Main.settings.swapCoolDown;
                    }
                }
                return;
            }
        }

        [HarmonyPatch( typeof( Player ), "Update" )]
        static class Player_Update_Patch
        {
            static void Prefix( Player __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }
                if ( Main.cooldown > 0f )
                {
                    __instance.character.SetInvulnerable( 0f, false );
                    Main.cooldown -= Time.unscaledDeltaTime;
                    if ( Main.cooldown < 0f )
                    {
                        Main.cooldown = 0f;
                    }
                }
            }
        }

        [HarmonyPatch( typeof( TestVanDammeAnim ), "SetInvulnerable" )]
        static class TestVanDammeAnim_SetInvulnerable_Patch
        {
            static bool Prefix( TestVanDammeAnim __instance, float time, bool restartBubble = true )
            {
                if ( !Main.enabled )
                {
                    return true;
                }
                if ( time == 0f && !restartBubble )
                {
                    Traverse.Create( typeof( TestVanDammeAnim ) ).Field( "invulnerableTime" ).SetValue( 0 );
                    __instance.invulnerable = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( Map ), "Awake" )]
        static class Map_Awake_Patch
        {
            static void Postfix()
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Clear bro list
                Main.currentBroListUnseen.Clear();

                if ( !Main.settings.ignoreForcedBros && Map.MapData != null && Map.MapData.forcedBros != null && Map.MapData.forcedBros.Count > 0 )
                {
                    Main.currentBroListUnseen.Clear();
                    for ( int i = 0; i < Map.MapData.forcedBros.Count(); ++i )
                    {
                        Main.currentBroListUnseen.Add( Main.HeroTypeToString( Map.MapData.forcedBros[i] ) );
                    }
                }
                else
                {
                    Main.currentBroListUnseen.AddRange( Main.currentBroList );
                }
            }
        }
    }
}
