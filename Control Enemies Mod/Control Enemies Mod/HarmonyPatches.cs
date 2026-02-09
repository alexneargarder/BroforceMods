using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Localisation;
using RocketLib.Utils;
using UnityEngine;
using static RocketLib.Utils.UnitTypes;
using Net = Networking.Networking;

namespace Control_Enemies_Mod
{
    public class HarmonyPatches
    {
        #region General Patches
        // Disable AI of enemy we're controlling
        [HarmonyPatch( typeof( PolymorphicAI ), "Update" )]
        static class PolymorphicAI_Update_Patch
        {
            public static bool Prefix( PolymorphicAI __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    if ( Main.settings.competitiveModeEnabled && !Main.revealed[__instance.gameObject.GetComponent<TestVanDammeAnim>().playerNum] )
                    {
                        __instance.mentalState = MentalState.Idle;
                    }
                    else
                    {
                        __instance.mentalState = MentalState.Alerted;
                    }
                    return false;
                }

                return true;
            }
        }

        // Disable AI of enemy we're controlling
        [HarmonyPatch( typeof( PolymorphicAI ), "LateUpdate" )]
        static class PolymorphicAI_LateUpdate_Patch
        {
            public static bool Prefix( PolymorphicAI __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    if ( Main.settings.competitiveModeEnabled && !Main.revealed[__instance.gameObject.GetComponent<TestVanDammeAnim>().playerNum] )
                    {
                        __instance.mentalState = MentalState.Idle;
                    }
                    else
                    {
                        __instance.mentalState = MentalState.Alerted;
                    }
                    return false;
                }

                return true;
            }
        }

        // Disable AI of enemy we're controlling
        [HarmonyPatch( typeof( TestVanDammeAnim ), "GetEnemyMovement" )]
        static class TestVanDammeAnim_GetEnemyMovement_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }
                else if ( __instance.name == "p" )
                {
                    __instance.left = __instance.right = false;
                    return false;
                }

                return true;
            }
        }

        // Disable taunting for mooks
        [HarmonyPatch( typeof( TestVanDammeAnim ), "SetGestureAnimation" )]
        static class TestVanDammeAnim_SetGestureAnimation_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance, ref GestureElement.Gestures gesture )
            {
                if ( !Main.enabled || !Main.settings.disableTaunting )
                {
                    return true;
                }

                if ( gesture == GestureElement.Gestures.Flex && __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Make player respawn at mook if that option is enabled
        [HarmonyPatch( typeof( TestVanDammeAnim ), "Death", new Type[] { typeof( float ), typeof( float ), typeof( DamageObject ) } )]
        public static class TestVanDammeAnim_Death_Patch
        {
            public static bool outOfBounds = false;

            public static void Prefix( TestVanDammeAnim __instance, ref float xI, ref float yI, ref DamageObject damage )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    // Track whether we're dying from out-of-bounds to know if we should be able to respawn from corpse
                    if ( damage != null )
                    {
                        outOfBounds = damage.damageType == DamageType.OutOfBounds;
                    }
                }
            }

            public static void Postfix( TestVanDammeAnim __instance, ref float xI, ref float yI, ref DamageObject damage )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                try
                {
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        if ( __instance.playerNum >= 0 && __instance.playerNum < 4 )
                        {
                            Main.PlayerDiedInCompetitiveMode( __instance, damage );
                        }
                    }
                    else if ( __instance.name == "c" && __instance.playerNum >= 0 && __instance.playerNum < 4 )
                    {
                        // If we're not respawning from a corpse or we don't have enough lives left, release the unit
                        if ( !( Main.settings.respawnFromCorpse && HeroController.players[__instance.playerNum].Lives > 0 && !outOfBounds && Main.previousCharacter[__instance.playerNum] != null ) )
                        {
                            SatanMiniboss satan = __instance as SatanMiniboss;
                            if ( satan == null || satan.IsInStage2() )
                            {
                                Main.LeaveUnit( __instance, __instance.playerNum, true );
                            }
                        }
                    }
                }
                catch ( Exception ex )
                {
                    Main.Log( "Exception in death: " + ex.ToString() );
                }
            }
        }

        // Check if player died by falling out of bounds, which doesn't call the death function
        [HarmonyPatch( typeof( TestVanDammeAnim ), "Gib" )]
        static class TestVanDammeAnim_Gib_Patch
        {
            public static void Prefix( TestVanDammeAnim __instance, ref DamageType damageType )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                try
                {
                    if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && HeroController.players[__instance.playerNum].character == __instance )
                    {
                        if ( Main.settings.competitiveModeEnabled )
                        {
                            Main.PlayerDiedInCompetitiveMode( __instance );
                        }
                        else if ( __instance.name == "c" )
                        {
                            if ( !TestVanDammeAnim_Death_Patch.outOfBounds )
                            {
                                TestVanDammeAnim_Death_Patch.outOfBounds = damageType == DamageType.OutOfBounds;
                            }
                            typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                        }
                    }
                }
                catch ( Exception ex )
                {
                    Main.Log( "Exception in gib: " + ex.ToString() );
                }
            }
        }

        // Prevent game from reporting death to prevent respawn
        [HarmonyPatch( typeof( TestVanDammeAnim ), "ReduceLives" )]
        public static class TestVanDammeAnim_ReduceLives_Patch
        {
            public static bool ignoreNextLifeLoss = false;

            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // Competitive mode 
                if ( Main.settings.competitiveModeEnabled )
                {
                }
                // Don't report death if we don't want to lose lives when dying as a mook or if we want to respawn at their corpse
                else if ( __instance.name == "c" && __instance.playerNum >= 0 && __instance.playerNum < 4 && Main.currentlyEnemy[__instance.playerNum] )
                {
                    bool chestBurstDeath = false;
                    if ( __instance is AlienFaceHugger )
                    {
                        AlienFaceHugger faceHugger = __instance as AlienFaceHugger;
                        if ( faceHugger.insemenationCompleted && faceHugger.layEggsInsideBros )
                        {
                            chestBurstDeath = true;
                            AlienXenomorph_Start_Patch.controlNextAlien = true;
                            AlienXenomorph_Start_Patch.controllerPlayerNum = __instance.playerNum;
                        }
                    }
                    // If we're respawning from corpse and didn't die from out of bounds or if we're spawning from a chestburster
                    if ( ( Main.settings.respawnFromCorpse && !TestVanDammeAnim_Death_Patch.outOfBounds && Main.previousCharacter[__instance.playerNum] != null ) || chestBurstDeath )
                    {
                        if ( Main.settings.loseLifeOnDeath && !chestBurstDeath && !( Main.previousDeathType[__instance.playerNum] == DeathType.Suicide && Main.settings.noLifeLossOnSuicide ) )
                        {
                            HeroController.players[__instance.playerNum].RemoveLife();
                        }

                        // Setup timer to countdown to respawn
                        if ( HeroController.players[__instance.playerNum].Lives > 0 )
                        {
                            Main.countdownToRespawn[__instance.playerNum] = chestBurstDeath ? 10f : 0.6f;
                        }
                        // Actually die since we're out of lives
                        else
                        {
                            return true;
                        }

                        return false;
                    }
                    // Don't lose life if the setting is enabled and we're controlling a character
                    else if ( !Main.settings.loseLifeOnDeath && Main.previousCharacter[__instance.playerNum] != null )
                    {
                        ignoreNextLifeLoss = true;
                    }
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( Mook ), "NotifyDeathType" )]
        static class Mook_NotifyDeathType_Patch
        {
            public static void Prefix( Mook __instance, DeathType ___deathType )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" && Main.previousDeathType[__instance.playerNum] != DeathType.Suicide )
                {
                    Main.previousDeathType[__instance.playerNum] = ___deathType;
                }
            }
        }

        // Prevent life loss when controlling enemy if life loss is disabled
        [HarmonyPatch( typeof( Player ), "RemoveLife" )]
        public static class Player_RemoveLife_Patch
        {
            public static bool allowLifeLoss = false;

            public static bool Prefix( Player __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( TestVanDammeAnim_ReduceLives_Patch.ignoreNextLifeLoss )
                {
                    TestVanDammeAnim_ReduceLives_Patch.ignoreNextLifeLoss = false;
                    Main.previousDeathType[__instance.playerNum] = DeathType.None;
                    return false;
                }


                // Disable life loss for suicide enemies
                if ( Main.settings.noLifeLossOnSuicide && __instance.character != null && __instance.character.name == "c" && Main.currentUnitType[__instance.playerNum].IsSuicideUnit() && Main.previousDeathType[__instance.playerNum] == DeathType.Suicide )
                {
                    Main.previousDeathType[__instance.playerNum] = DeathType.None;
                    return false;
                }

                Main.previousDeathType[__instance.playerNum] = DeathType.None;

                if ( Main.settings.competitiveModeEnabled )
                {
                    // Disable life loss for ghosts if they have infinite lives
                    if ( Main.settings.ghostLives == 0 && __instance.playerNum != Main.currentHeroNum )
                    {
                        allowLifeLoss = false;
                        return false;
                    }
                    // Disable life loss for heros if they have infinite lives
                    else if ( Main.settings.heroLives == 0 && __instance.playerNum == Main.currentHeroNum )
                    {
                        allowLifeLoss = false;
                        return false;
                    }

                    // Disable all life loss unless specifically enabled
                    if ( allowLifeLoss )
                    {
                        allowLifeLoss = false;
                        return true;
                    }

                    return false;
                }

                return true;
            }
        }

        // Prevent HeroController from knowing our mook is dead to prevent us from respawning
        [HarmonyPatch( typeof( Player ), "IsAlive" )]
        static class Player_IsAlive_Patch
        {
            public static bool Prefix( Player __instance, ref bool __result )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( Main.everyoneDead )
                {
                    __result = false;
                    return false;
                }
                else if ( Main.countdownToRespawn[__instance.playerNum] > 0 )
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        // Prevent HeroController from knowing our mook is dead to prevent us from respawning
        [HarmonyPatch( typeof( PlayerHUD ), "SetAvatarDead" )]
        static class PlayerHUD_SetAvatarDead_Patch
        {
            public static bool Prefix( PlayerHUD __instance, int ___playerNum, ref bool ___SetToDead )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( Main.countdownToRespawn[___playerNum] > 0 )
                {
                    ___SetToDead = true;
                    return false;
                }

                return true;
            }
        }

        // Ensure heavy units are allowed on the helicopter
        [HarmonyPatch( typeof( TestVanDammeAnim ), "IsOverFinish" )]
        static class TestVanDammeAnim_IsOverFinish_Patch
        {
            public static LayerMask victoryLayer = 1 << LayerMask.NameToLayer( "Finish" );

            public static float AttachToHelicopter( TestVanDammeAnim __instance, float ladderXPos, Helicopter helicopter )
            {
                helicopter.attachedHeroes.Add( __instance );
                ladderXPos = helicopter.transform.position.x + 13f;
                helicopter.Leave();
                __instance.transform.parent = helicopter.ladderHolder;
                float x = helicopter.transform.position.x;
                if ( __instance.X > x )
                {
                    __instance.transform.localScale = new Vector3( -1f, 1f, 1f );
                    __instance.X = x + 5f;
                }
                else
                {
                    __instance.transform.localScale = new Vector3( 1f, 1f, 1f );
                    __instance.X = x - 5f;
                }
                __instance.transform.position = new Vector3( Mathf.Round( __instance.X ), Mathf.Round( __instance.Y ), -50f );
                return ladderXPos;
            }

            public static void IsOverFinish( TestVanDammeAnim character, ref float ladderXPos, ref bool __result, bool canTakePortal = true )
            {
                Collider[] array = Physics.OverlapSphere( new Vector3( character.X, character.Y, 0f ), 4f, victoryLayer );
                if ( array.Length > 0 )
                {
                    // Don't let character take the portal
                    HeroLevelExitPortal component4 = array[0].GetComponent<HeroLevelExitPortal>();
                    if ( component4 != null && !canTakePortal )
                    {
                        __result = false;
                        return;
                    }

                    character.invulnerable = true;
                    if ( character.GetComponent<AudioSource>() != null )
                    {
                        character.GetComponent<AudioSource>().Stop();
                    }
                    character.enabled = false;
                    if ( array[0].transform.parent != null )
                    {
                        HelicopterFake component = array[0].transform.parent.GetComponent<HelicopterFake>();
                        if ( component != null || Map.MapData.onlyTriggersCanWinLevel )
                        {
                            Helicopter component2 = array[0].transform.parent.GetComponent<Helicopter>();
                            ladderXPos = AttachToHelicopter( character, ladderXPos, component2 );
                            Net.RPC<Vector3, float, TestVanDammeAnim, Helicopter, bool>( PID.TargetAll, new RpcSignature<Vector3, float, TestVanDammeAnim, Helicopter, bool>( HeroController.Instance.AttachHeroToHelicopter ), character.transform.localPosition, character.transform.localScale.x, character, component2, false, false );
                            __result = true;
                        }
                        Helicopter component3 = array[0].transform.parent.GetComponent<Helicopter>();
                        if ( component3 != null )
                        {
                            ladderXPos = AttachToHelicopter( character, ladderXPos, component3 );
                            Net.RPC<Vector3, float, TestVanDammeAnim, Helicopter, bool>( PID.TargetAll, new RpcSignature<Vector3, float, TestVanDammeAnim, Helicopter, bool>( HeroController.Instance.AttachHeroToHelicopter ), character.transform.localPosition, character.transform.localScale.x, character, component3, true, false );
                        }
                    }
                    Traverse trav = Traverse.Create( character );
                    trav.Field( "jumpTime" ).SetValue( 0.07f );
                    trav.Field( "doubleJumpsLeft" ).SetValue( 0 );
                    if ( component4 != null )
                    {
                        typeof( TestVanDammeAnim ).GetMethod( "SuckIntoPortal", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( character, null );
                    }
                    GameModeController.LevelFinish( LevelResult.Success );
                    Map.StartLevelEndExplosionsOverNetwork();
                    character.isOnHelicopter = true;
                    character.playerNum = 5;
                    __result = true;
                }
            }

            public static bool Prefix( TestVanDammeAnim __instance, ref float ladderXPos, ref bool __result )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( !Main.settings.competitiveModeEnabled )
                {
                    if ( __instance.name == "c" && __instance.IsHeavy() )
                    {
                        __result = false;
                        if ( __instance.playerNum >= 0 && __instance.playerNum < 4 )
                        {
                            IsOverFinish( __instance, ref ladderXPos, ref __result );
                        }
                        return false;
                    }
                }
                else
                {
                    // Don't allow non hero players or inactive players to finish the level
                    if ( __instance.playerNum != Main.currentHeroNum || !__instance.gameObject.activeSelf )
                    {
                        return false;
                    }
                    // Don't allow player into portal if they don't have the required score
                    else if ( Main.openedPortal && __instance.playerNum == Main.currentHeroNum && !ScoreManager.CanWin( __instance.playerNum ) )
                    {
                        IsOverFinish( __instance, ref ladderXPos, ref __result, false );
                        return false;
                    }
                }

                return true;
            }
        }

        // Clear previous character when rescuing a bro
        [HarmonyPatch( typeof( TestVanDammeAnim ), "RecallBro" )]
        static class TestVanDammeAnim_RecallBro_Patch
        {
            public static void Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 )
                {
                    Main.previousCharacter[__instance.playerNum] = null;
                }
            }
        }

        // Prevent enemies from trying to show the start bubble
        [HarmonyPatch( typeof( TestVanDammeAnim ), "ShowStartBubble" )]
        static class TestVanDammeAnim_ShowStartBubble_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // Don't let method execute if this is a controlled enemy
                return __instance.name != "c";
            }
        }

        // Prevent enemies from trying to show the start bubble
        [HarmonyPatch( typeof( TestVanDammeAnim ), "RestartBubble", new Type[] { typeof( float ) } )]
        static class TestVanDammeAnim_RestartBubble_float_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // Don't let method execute if this is a controlled enemy
                return __instance.name != "c";
            }
        }

        // Prevent enemies from trying to show the start bubble
        [HarmonyPatch( typeof( TestVanDammeAnim ), "RestartBubble", new Type[] { } )]
        static class TestVanDammeAnim_RestartBubble_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // Don't let method execute if this is a controlled enemy
                return __instance.name != "c";
            }
        }

        // Use different avatar for players who are controlling enemies
        [HarmonyPatch( typeof( HeroController ), "SwitchAvatarMaterial" )]
        static class HeroController_SwitchAvatarMaterial_Patch
        {
            public static bool Prefix( ref SpriteSM sprite, ref bool __result )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // If a player won, make sure all the avatars display correctly on the score screen
                if ( Main.settings.competitiveModeEnabled && Main.playerWon )
                {
                    int playerNum = -1;
                    PlayerScoreDisplay currentDisplay = sprite.transform.parent.parent.parent.gameObject.GetComponent<PlayerScoreDisplay>();
                    if ( currentDisplay != null && LevelOverScreen_Show_Patch.scoreDisplays.Length > 0 )
                    {
                        for ( int i = 0; i < LevelOverScreen_Show_Patch.scoreDisplays.Length; ++i )
                        {
                            if ( LevelOverScreen_Show_Patch.scoreDisplays[i] == currentDisplay )
                            {
                                playerNum = i;
                                break;
                            }
                        }
                    }

                    // Unable to determine player
                    if ( playerNum == -1 )
                    {
                        return true;
                    }

                    // Don't replace avatar with ghost if the current player is the one who one
                    if ( playerNum == Main.attemptingWin )
                    {
                        if ( Main.isBroMakerInstalled )
                        {
                            if ( Main.SetAvatarToSwitch( HeroController.players[playerNum].character, playerNum ) )
                            {
                                HeroController.SwitchAvatarMaterial( sprite, HeroType.None );
                                return false;
                            }
                        }
                        return true;
                    }
                    // Replace all other avatars with ghosts
                    else
                    {
                        sprite.GetComponent<Renderer>().material = Main.ghostAvatarMat;
                        return false;
                    }
                }

                PlayerHUD hud = sprite.gameObject.GetComponent<PlayerHUD>();
                if ( hud != null )
                {
                    int playerNum = (int)Traverse.Create( hud ).Field( "playerNum" ).GetValue();

                    // If we're assigning an avatar to a player who is an enemy, use a different avatar instead of the bro one
                    if ( Main.currentlyEnemy[playerNum] )
                    {
                        sprite.GetComponent<Renderer>().material = Main.defaultAvatarMat;
                        return false;
                    }
                }

                return true;
            }
        }

        // Refresh all settings
        [HarmonyPatch( typeof( Map ), "Start" )]
        static class Map_Start_Patch
        {
            public static void Prefix( Map __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                Main.ClearVariables();

                // Randomize hero num among starting players
                if ( Main.settings.competitiveModeEnabled && Main.currentHeroNum == -1 )
                {
                    Main.currentHeroNum = UnityEngine.Random.Range( 0, HeroController.NumberOfPlayers() );
                }
            }
        }

        // Remove buggy animation when wall climbing
        [HarmonyPatch( typeof( TestVanDammeAnim ), "AnimateWallAnticipation" )]
        static class TestVanDammeAnim_AnimateWallAnticipation_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    __instance.GetComponent<SpriteSM>().SetLowerLeftPixel( 0f, Main.currentSpriteHeight[__instance.playerNum] );
                    return false;
                }

                return true;
            }
        }

        // Remove buggy animation when wall climbing
        [HarmonyPatch( typeof( TestVanDammeAnim ), "AnimateWallClimb" )]
        static class TestVanDammeAnim_AnimateWallClimb_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    __instance.GetComponent<SpriteSM>().SetLowerLeftPixel( 0f, Main.currentSpriteHeight[__instance.playerNum] );
                    return false;
                }

                return true;
            }
        }

        // Remove buggy animation when wall climbing
        [HarmonyPatch( typeof( TestVanDammeAnim ), "AnimateWallDrag" )]
        static class TestVanDammeAnim_AnimateWallDrag_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    __instance.GetComponent<SpriteSM>().SetLowerLeftPixel( 0f, Main.currentSpriteHeight[__instance.playerNum] );
                    return false;
                }

                return true;
            }
        }

        // Make sure enemies don't play confused noise when dancing
        [HarmonyPatch( typeof( TestVanDammeAnim ), "PlayStunnedSound" )]
        static class TestVanDammeAnim_PlayStunnedSound_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance, ref float ___stunVocalDelay )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // Play laugh instead of confused sound
                if ( __instance.name == "c" && Main.holdingGesture[__instance.playerNum] )
                {
                    __instance.PlayLaughterSound();
                    ___stunVocalDelay = 0.4f + UnityEngine.Random.Range( 0.3f, 0.9f );
                    return false;
                }

                return true;
            }
        }

        // Disable grenadier mook throwing back grenade crashing the game
        [HarmonyPatch( typeof( TestVanDammeAnim ), "ThrowBackGrenade" )]
        static class TestVanDammeAnim_ThrowBackGrenade_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" && Main.currentUnitType[__instance.playerNum] == UnitType.GrenadierMook )
                {
                    return false;
                }

                return true;
            }
        }
        #endregion

        #region Enemy Specific Patches
        // Fix controlled enemies taking fall damage
        #region FallDamagePatches
        // Disable fall damage for mooks
        [HarmonyPatch( typeof( Mook ), "FallDamage" )]
        static class Mook_FallDamage_Patch
        {
            public static bool Prefix( Mook __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for bruisers
        [HarmonyPatch( typeof( MookBigGuy ), "FallDamage" )]
        static class MookBigGuy_FallDamage_Patch
        {
            public static bool Prefix( MookBigGuy __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for aliens
        [HarmonyPatch( typeof( Alien ), "FallDamage" )]
        static class Alien_FallDamage_Patch
        {
            public static bool Prefix( Alien __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for AlienFaceHugger
        [HarmonyPatch( typeof( AlienFaceHugger ), "FallDamage" )]
        static class AlienFaceHugger_FallDamage_Patch
        {
            public static bool Prefix( AlienFaceHugger __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for AlienMelter
        [HarmonyPatch( typeof( AlienMelter ), "FallDamage" )]
        static class AlienMelter_FallDamage_Patch
        {
            public static bool Prefix( AlienMelter __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for Animal
        [HarmonyPatch( typeof( Animal ), "FallDamage" )]
        static class Animal_FallDamage_Patch
        {
            public static bool Prefix( Animal __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookArmouredGuy
        [HarmonyPatch( typeof( MookArmouredGuy ), "FallDamage" )]
        static class MookArmouredGuy_FallDamage_Patch
        {
            public static bool Prefix( MookArmouredGuy __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookDog
        [HarmonyPatch( typeof( MookDog ), "FallDamage" )]
        static class MookDog_FallDamage_Patch
        {
            public static bool Prefix( MookDog __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookHellBigGuy
        [HarmonyPatch( typeof( MookHellBigGuy ), "FallDamage" )]
        static class MookHellBigGuy_FallDamage_Patch
        {
            public static bool Prefix( MookHellBigGuy __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookHellBoomer
        [HarmonyPatch( typeof( MookHellBoomer ), "FallDamage" )]
        static class MookHellBoomer_FallDamage_Patch
        {
            public static bool Prefix( MookHellBoomer __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookSuicide
        [HarmonyPatch( typeof( MookSuicide ), "FallDamage" )]
        static class MookSuicide_FallDamage_Patch
        {
            public static bool Prefix( MookSuicide __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for SkinnedMook
        [HarmonyPatch( typeof( SkinnedMook ), "FallDamage" )]
        static class MSkinnedMook_FallDamage_Patch
        {
            public static bool Prefix( SkinnedMook __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for Villager
        [HarmonyPatch( typeof( Villager ), "FallDamage" )]
        static class Villager_FallDamage_Patch
        {
            public static bool Prefix( Villager __instance, ref float yI )
            {
                if ( !Main.enabled || !Main.settings.disableFallDamage )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }
        #endregion

        // Allow controlled enemies to fire offscreen
        #region UseFirePatches
        public static bool overrideNextVisibilityCheck = false;

        [HarmonyPatch( typeof( SetResolutionCamera ), "IsItVisible" )]
        static class SetResolutionCamera_IsItVisible_Patch
        {
            public static bool Prefix( SetResolutionCamera __instance, ref bool __result )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( overrideNextVisibilityCheck )
                {
                    __result = true;
                    overrideNextVisibilityCheck = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( SortOfFollow ), "IsItSortOfVisible", new Type[] { typeof( float ), typeof( float ), typeof( float ), typeof( float ) } )]
        static class SortOfFollow_IsItSortOfVisible_Patch
        {
            public static bool Prefix( SortOfFollow __instance, ref bool __result )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( overrideNextVisibilityCheck )
                {
                    __result = true;
                    overrideNextVisibilityCheck = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( Mook ), "UseFire" )]
        static class Mook_UseFire_Patch
        {
            public static void Prefix( Mook __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    overrideNextVisibilityCheck = true;
                }
            }
        }

        [HarmonyPatch( typeof( MookArmouredGuy ), "UseFire" )]
        static class MookArmouredGuy_UseFire_Patch
        {
            public static void Prefix( MookArmouredGuy __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    overrideNextVisibilityCheck = true;
                }
            }
        }

        [HarmonyPatch( typeof( MookBigGuy ), "UseFire" )]
        static class MookBigGuy_UseFire_Patch
        {
            public static void Prefix( MookBigGuy __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    overrideNextVisibilityCheck = true;
                }
            }
        }

        [HarmonyPatch( typeof( MookHellBoomer ), "UseFire" )]
        static class MookHellBoomer_UseFire_Patch
        {
            public static void Prefix( MookHellBoomer __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    overrideNextVisibilityCheck = true;
                }
            }
        }

        [HarmonyPatch( typeof( MookJetpackBazooka ), "UseFire" )]
        static class MookJetpackBazooka_UseFire_Patch
        {
            public static void Prefix( MookJetpackBazooka __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    overrideNextVisibilityCheck = true;
                }
            }
        }

        [HarmonyPatch( typeof( MookBigGuyElite ), "UseFire" )]
        static class MookBigGuyElite_UseFire_Patch
        {
            public static void Prefix( MookBigGuyElite __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    overrideNextVisibilityCheck = true;
                }
            }
        }

        [HarmonyPatch( typeof( MookBazooka ), "UseFire" )]
        static class MookBazooka_UseFire_Patch
        {
            public static void Prefix( MookBazooka __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    overrideNextVisibilityCheck = true;
                }
            }
        }
        #endregion

        // Allow enemies to use doors
        #region CanPassThroughBarriers
        [HarmonyPatch( typeof( Mook ), "CanPassThroughBarriers" )]
        static class Mook_CanPassThroughBarriers_Patch
        {
            public static bool Prefix( Mook __instance, ref bool __result )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( MookDog ), "CanPassThroughBarriers" )]
        static class MookDog_CanPassThroughBarriers_Patch
        {
            public static bool Prefix( MookDog __instance, ref bool __result )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }
        #endregion

        // Fix various death functions
        #region Death Fixes
        // Fix suicide mook death being ignored
        [HarmonyPatch( typeof( MookSuicide ), "Gib" )]
        static class MookSuicide_Gib_Patch
        {
            public static void Prefix( MookSuicide __instance, ref DamageType damageType )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && HeroController.players[__instance.playerNum].character == __instance )
                {
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        Main.PlayerDiedInCompetitiveMode( __instance );
                    }
                    // Ensure controlled enemies cause life loss on death
                    else if ( __instance.name == "c" )
                    {
                        if ( !TestVanDammeAnim_Death_Patch.outOfBounds )
                        {
                            TestVanDammeAnim_Death_Patch.outOfBounds = damageType == DamageType.OutOfBounds;
                        }
                        typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                    }
                }
            }
        }

        // Fix jetpack mook deaths being ignored
        [HarmonyPatch( typeof( MookJetpack ), "Gib" )]
        static class MookJetpack_Gib_Patch
        {
            public static void Prefix( MookJetpack __instance, ref DamageType damageType )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && HeroController.players[__instance.playerNum].character == __instance )
                {
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        Main.PlayerDiedInCompetitiveMode( __instance );
                    }
                    // Ensure controlled enemies cause life loss on death
                    else if ( __instance.name == "c" )
                    {
                        if ( !TestVanDammeAnim_Death_Patch.outOfBounds )
                        {
                            TestVanDammeAnim_Death_Patch.outOfBounds = damageType == DamageType.OutOfBounds;
                        }
                        typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                    }
                }
            }
        }

        // Fix alien melter deaths being ignored
        [HarmonyPatch( typeof( AlienMelter ), "Gib" )]
        static class AlienMelter_Gib_Patch
        {
            public static void Prefix( AlienMelter __instance, ref DamageType damageType )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && HeroController.players[__instance.playerNum].character == __instance )
                {
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        Main.PlayerDiedInCompetitiveMode( __instance );
                    }
                    // Ensure controlled enemies cause life loss on death
                    else if ( __instance.name == "c" )
                    {
                        if ( !TestVanDammeAnim_Death_Patch.outOfBounds )
                        {
                            TestVanDammeAnim_Death_Patch.outOfBounds = damageType == DamageType.OutOfBounds;
                        }
                        typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                    }
                }
            }
        }

        // Fix alien mosquito deaths being ignored
        [HarmonyPatch( typeof( AlienMosquito ), "Gib" )]
        static class AlienMosquito_Gib_Patch
        {
            public static void Prefix( AlienMosquito __instance, ref DamageType damageType )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && HeroController.players[__instance.playerNum].character == __instance )
                {
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        Main.PlayerDiedInCompetitiveMode( __instance );
                    }
                    // Ensure controlled enemies cause life loss on death
                    else if ( __instance.name == "c" )
                    {
                        if ( !TestVanDammeAnim_Death_Patch.outOfBounds )
                        {
                            TestVanDammeAnim_Death_Patch.outOfBounds = damageType == DamageType.OutOfBounds;
                        }
                        typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                    }
                }
            }
        }

        // Fix undead suicide mook deaths being ignored
        [HarmonyPatch( typeof( MookSuicideUndead ), "Gib" )]
        static class MookSuicideUndead_Gib_Patch
        {
            public static void Prefix( MookSuicideUndead __instance, ref DamageType damageType )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && HeroController.players[__instance.playerNum].character == __instance )
                {
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        Main.PlayerDiedInCompetitiveMode( __instance );
                    }
                    // Ensure controlled enemies cause life loss on death
                    else if ( __instance.name == "c" )
                    {
                        if ( !TestVanDammeAnim_Death_Patch.outOfBounds )
                        {
                            TestVanDammeAnim_Death_Patch.outOfBounds = damageType == DamageType.OutOfBounds;
                        }
                        typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                    }
                }
            }
        }

        // Fix villager deaths being ignored
        [HarmonyPatch( typeof( Villager ), "Death" )]
        static class Villager_Death_Patch
        {
            public static void Prefix( Villager __instance, ref DamageObject damage )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && HeroController.players[__instance.playerNum].character == __instance )
                {
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        Main.PlayerDiedInCompetitiveMode( __instance, damage );
                    }
                    // Ensure controlled enemies cause life loss on death
                    else if ( __instance.name == "c" )
                    {
                        if ( !TestVanDammeAnim_Death_Patch.outOfBounds && damage != null )
                        {
                            TestVanDammeAnim_Death_Patch.outOfBounds = damage.damageType == DamageType.OutOfBounds;
                        }
                        typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                    }
                }
            }
        }

        // Fix villager deaths being ignored
        [HarmonyPatch( typeof( Villager ), "Gib", new Type[] { typeof( DamageType ), typeof( float ), typeof( float ) } )]
        static class Villager_Gib_Patch
        {
            public static void Prefix( Villager __instance, ref DamageType damageType )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && HeroController.players[__instance.playerNum].character == __instance )
                {
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        Main.PlayerDiedInCompetitiveMode( __instance );
                    }
                    // Ensure controlled enemies cause life loss on death
                    else if ( __instance.name == "c" )
                    {
                        if ( !TestVanDammeAnim_Death_Patch.outOfBounds )
                        {
                            TestVanDammeAnim_Death_Patch.outOfBounds = damageType == DamageType.OutOfBounds;
                        }
                        typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                    }
                }
            }
        }

        // Fix villager deaths being ignored
        [HarmonyPatch( typeof( Villager ), "Gib", new Type[] { } )]
        static class Villager_Gib2_Patch
        {
            public static void Prefix( Villager __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && HeroController.players[__instance.playerNum].character == __instance )
                {
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        Main.PlayerDiedInCompetitiveMode( __instance );
                    }
                    // Ensure controlled enemies cause life loss on death
                    else if ( __instance.name == "c" )
                    {
                        typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                    }
                }
            }
        }

        // Make sure we leave satan when he dies
        [HarmonyPatch( typeof( SatanMiniboss ), "StartDeathRattle" )]
        static class SatanMiniboss_StartDeathRattle_Patch
        {
            public static void Prefix( SatanMiniboss __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( !Main.settings.competitiveModeEnabled )
                {
                    if ( __instance.name == "c" )
                    {
                        Main.LeaveUnit( __instance, __instance.playerNum, true );
                    }
                    else if ( __instance.name == "Enemy" )
                    {
                        for ( int i = 0; i < 4; ++i )
                        {
                            if ( HeroController.IsPlaying( i ) && HeroController.players[i].character == __instance )
                            {
                                HeroController.players[i].character = null;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        // Disable explosive deaths when recalling
        #region Explosive Deaths
        [HarmonyPatch( typeof( MookSuicide ), "MakeEffects" )]
        static class MookSuicide_MakeEffects_Patch
        {
            public static bool Prefix( MookSuicide __instance, bool ___recalling )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // Don't explode if recalling
                if ( __instance.name == "c" && ___recalling )
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( MookSuicideUndead ), "MakeEffects" )]
        static class MookSuicideUndead_MakeEffects_Patch
        {
            public static bool Prefix( MookSuicideUndead __instance, bool ___recalling )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // Don't explode if recalling
                if ( __instance.name == "c" && ___recalling )
                {
                    return false;
                }

                return true;
            }
        }
        #endregion

        // Make sure suicide deaths are counted as suicides
        #region Suicide Deaths
        [HarmonyPatch( typeof( HellLostSoul ), "MakeEffects" )]
        static class HellLostSoul_MakeEffects_Patch
        {
            public static void Prefix( HellLostSoul __instance, bool ___diving )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Deathtype is set to suicide if the game thinks you're attacking
                if ( __instance.name == "c" && ___diving )
                {
                    __instance.fire = true;
                }
            }
        }

        [HarmonyPatch( typeof( AlienMosquito ), "MakeEffects" )]
        static class AlienMosquito_MakeEffects_Patch
        {
            public static void Prefix( AlienMosquito __instance, bool ___diving )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Deathtype is set to suicide if the game thinks you're attacking
                if ( __instance.name == "c" && ___diving )
                {
                    __instance.fire = true;
                }
            }
        }
        #endregion

        // Make spawned helldogs friendly
        #region HellDog
        [HarmonyPatch( typeof( HellDogEgg ), "MakeEffects" )]
        public static class HellDogEgg_MakeEffects_Patch
        {
            public static bool nextDogFriendly = false;
            public static int playerNum = -1;

            public static void Prefix( HellDogEgg __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // If egg was fired by controlled enemy or friendly enemy, make it friendly
                if ( ( __instance.firedBy.name == "c" || ( __instance.firedBy as Mook ).playerNum >= 0 ) )
                {
                    nextDogFriendly = true;
                    playerNum = ( __instance.firedBy as Mook ).playerNum;
                }
            }
        }

        [HarmonyPatch( typeof( HellDog ), "GrowFromEgg" )]
        static class HellDog_GrowFromEgg_Patch
        {
            public static void Prefix( HellDog __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Set this dog to friendly since it was spawned by a player
                if ( HellDogEgg_MakeEffects_Patch.nextDogFriendly )
                {
                    __instance.firingPlayerNum = HellDogEgg_MakeEffects_Patch.playerNum;
                    __instance.playerNum = HellDogEgg_MakeEffects_Patch.playerNum;
                    HellDogEgg_MakeEffects_Patch.nextDogFriendly = false;
                }
            }
        }
        #endregion

        // Become xenomorph when facehugging enemy
        #region Facehugger
        [HarmonyPatch( typeof( AlienXenomorph ), "Start" )]
        public static class AlienXenomorph_Start_Patch
        {
            public static int controllerPlayerNum = -1;
            public static bool controlNextAlien = false;

            public static void Postfix( AlienXenomorph __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Automatically assume control of alien that spawned from facehugger that was controlled by a player
                if ( controlNextAlien )
                {
                    // If previous character is null, that means we are in spawn as enemy mode, so we shouldn't save the previous character again
                    if ( Main.previousCharacter[controllerPlayerNum] == null )
                    {
                        Main.StartControllingUnit( controllerPlayerNum, __instance, true, false );
                    }
                    else
                    {
                        Main.StartControllingUnit( controllerPlayerNum, __instance, true );
                    }
                    controlNextAlien = false;
                    Main.countdownToRespawn[controllerPlayerNum] = -1f;
                }
                // Make sure already controlled aliens don't have their playernum overwritten by the start function
                else if ( __instance.name == "c" )
                {
                    int playerNum = Main.currentUnit.IndexOf( __instance );
                    if ( playerNum != __instance.playerNum )
                    {
                        Main.ReaffirmControl( playerNum );
                    }
                    __instance.SpecialAmmo = 0;
                }
            }
        }
        #endregion

        // Fix issues with jetpack
        #region Jetpack Mooks
        // Prevent Jetpack mooks from automatically flying up
        [HarmonyPatch( typeof( MookJetpack ), "StartJetPacks" )]
        public static class MookJetpack_StartJetPacks_Patch
        {
            public static bool allowJetpack = false;

            public static bool Prefix( MookJetpack __instance, ref float ___jetpacksDelay )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    if ( !allowJetpack )
                    {
                        ___jetpacksDelay = 1000000f;
                        return false;
                    }
                    else
                    {
                        allowJetpack = false;
                        ___jetpacksDelay = 0f;
                        return true;
                    }
                }

                return true;
            }
        }

        // Change Jetpack Mook's jetpackHeight as you're playing them to make the jetpack more useful
        [HarmonyPatch( typeof( MookJetpack ), "Land" )]
        static class MookJetpack_Land_Patch
        {
            public static void Postfix( MookJetpack __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    __instance.jetPackHeight = __instance.groundHeight + 48f;
                }
            }
        }
        #endregion

        // Fix warlock summoning
        #region Warlock
        // Fix warlocks being unable to fire unless they've seen an enemy
        [HarmonyPatch( typeof( PolymorphicAI ), "GetSeenPlayerNum" )]
        static class PolymorphicAI_GetSeenPlayerNum_Patch
        {
            public static void Postfix( PolymorphicAI __instance, ref int __result )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" && __instance is MookWarlockAI )
                {
                    // Set playernum to hero in competitive
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        __result = Main.currentHeroNum;
                    }
                    // Set playernum to yourself in normal mode, because it can't be set to enemies
                    else
                    {
                        __result = __instance.GetComponent<TestVanDammeAnim>().playerNum;
                    }
                }
            }
        }

        // Fix warlock mooks damaging the player if they're controlling the warlock
        [HarmonyPatch( typeof( WarlockPortal ), "SpawnUnit" )]
        static class WarlockPortal_SpawnUnit_Patch
        {
            public static void Postfix( WarlockPortal __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.owner != null && __instance.owner.name == "c" )
                {
                    __instance.unit.playerNum = __instance.owner.playerNum;
                }
            }
        }
        #endregion

        // Fix issues with controlling mechs
        #region Mech
        // Take control of discharged unit if the mech is being controlled
        [HarmonyPatch( typeof( MookArmouredGuy ), "DisChargePilotRPC" )]
        static class MookArmouredGuy_DisChargePilotRPC_Patch
        {
            public static void Prefix( MookArmouredGuy __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Mech itself is being controlled
                if ( __instance.name == "c" )
                {
                    // Take control of discharged unit
                    TestVanDammeAnim pilotUnit = __instance.pilotUnit as TestVanDammeAnim;
                    if ( pilotUnit != null && Main.AvailableToPossess( pilotUnit ) )
                    {
                        int playerNum = __instance.playerNum;
                        Main.LeaveUnit( __instance, playerNum, true );
                        // Force IsAlive to be true
                        Main.countdownToRespawn[playerNum] = 1f;
                        Main.StartControllingUnit( playerNum, pilotUnit, true, false, false );
                        Main.countdownToRespawn[playerNum] = 0f;
                        // Make sure unit isn't a child of the mech anymore
                        pilotUnit.transform.parent = Map.Instance.transform;
                    }
                }
            }
        }

        // Patched to remove damage to controlled mooks who are discharged
        [HarmonyPatch( typeof( Mook ), "DischargePilotingUnit" )]
        static class Mook_DischargePilotingUnit_Patch
        {
            public static bool Prefix( Mook __instance, ref float x, ref float y, ref float xI, ref float yI, ref bool stunUnit, ref float ___ignoreHighFivePressTime, ref float ___jumpTime, ref float ___stunTime )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // Prevent mook from taking damage if it's a player controlled enemy
                if ( __instance.name == "c" )
                {
                    __instance.SetInvulnerable( 0.1f, false, false );
                    if ( __instance.gunSprite != null )
                    {
                        __instance.gunSprite.enabled = true;
                        __instance.gunSprite.GetComponent<Renderer>().enabled = true;
                    }
                    __instance.GetComponent<Renderer>().enabled = true;
                    ___ignoreHighFivePressTime = 0.1f;
                    ___jumpTime = 0.13f;
                    __instance.enabled = true;
                    __instance.health = 1;
                    __instance.pilottedUnit = null;
                    __instance.SpecialAmmo = __instance.SpecialAmmo;
                    __instance.SetXY( x, y );
                    __instance.yI = yI;
                    if ( !stunUnit )
                    {
                        __instance.xIBlast = xI;
                    }
                    else
                    {
                        __instance.xIBlast = xI * 0.5f;
                        __instance.xI = xI * 0.5f;
                    }
                    __instance.ShowStartBubble();
                    __instance.gameObject.SetActive( true );
                    if ( stunUnit )
                    {
                        __instance.Stun( 1f );
                    }
                    else
                    {
                        ___stunTime = 0f;
                    }

                    __instance.xI = xI;
                    __instance.xIBlast = 0f;
                    return false;
                }

                return true;
            }
        }
        #endregion

        // Make enemies spawned by Mook General friendly
        #region Mook General
        [HarmonyPatch( typeof( MookGeneral ), "SpawnMook" )]
        static class MookGeneral_SpawnMook_Patch
        {
            public static bool Prefix( MookGeneral __instance, ref Mook prefab, ref float x, ref float y )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    Mook mook = MapController.SpawnMook_Networked( prefab, x, y, 0f, 0f, false, false, true, false, true );
                    mook.playerNum = __instance.playerNum;
                    mook.firingPlayerNum = __instance.firingPlayerNum;
                    return false;
                }

                return true;
            }
        }
        #endregion

        // Fix suicide mooks being unable to wall climb
        #region Suicide Mook
        [HarmonyPatch( typeof( MookSuicide ), "Start" )]
        static class MookSuicide_Start_Patch
        {
            public static void Postfix( MookSuicide __instance, ref float ___halfWidth )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Fix suicide mooks being unable to wall climb
                if ( __instance.name == "c" )
                {
                    ___halfWidth = 6f;
                }
            }
        }
        #endregion

        // Fix RC Car not checking input
        #region RCCar
        // Get base method of TestVanDammeAnim CheckInput
        [HarmonyPatch]
        static class RemoteControlExplosiveCar_CheckInput_Patch
        {
            [HarmonyReversePatch]
            [HarmonyPatch( typeof( TestVanDammeAnim ), "CheckInput" )]
            [MethodImpl( MethodImplOptions.NoInlining )]
            static void CheckInput( RemoteControlExplosiveCar instance ) { }

            // Make sure we check input for controlled RC Cars
            [HarmonyPatch( typeof( RemoteControlExplosiveCar ), "CheckInput" )]
            static void Prefix( RemoteControlExplosiveCar __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    CheckInput( __instance );
                }

                // Blow up on using special
                if ( __instance.fire || __instance.special )
                {
                    RemoteControlExplosiveCar_Explode_Patch.manualActivation = true;
                    __instance.fire = __instance.special = false;
                    __instance.Explode();
                }
            }
        }

        // Make sure we lose a life on death
        [HarmonyPatch( typeof( RemoteControlExplosiveCar ), "Explode" )]
        public static class RemoteControlExplosiveCar_Explode_Patch
        {
            public static bool manualActivation = false;
            public static void Prefix( RemoteControlExplosiveCar __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    if ( manualActivation && Main.settings.noLifeLossOnSuicide )
                    {
                        manualActivation = false;
                        return;
                    }

                    typeof( TestVanDammeAnim ).GetMethod( "ReduceLives", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { false } );
                }
            }
        }
        #endregion

        // Fix skinless mook dying immediately
        #region Skinless Mook
        // Prevent SkinnedMook from dying on spawn in non-Hell levels when player-controlled
        [HarmonyPatch( typeof( SkinnedMook ), "Start" )]
        static class SkinnedMook_Start_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler( IEnumerable<CodeInstruction> instructions, ILGenerator generator )
            {
                var codeMatcher = new CodeMatcher( instructions, generator );

                MethodInfo deathRPCMethod = AccessTools.Method( typeof( Unit ), "DeathRPC", new Type[] { typeof( float ), typeof( float ), typeof( float ), typeof( float ) } );

                FieldInfo playerNumField = AccessTools.Field( typeof( NetworkedUnit ), "_playerNum" );

                codeMatcher.MatchStartForward(
                        new CodeMatch( OpCodes.Ldarg_0 ),
                        new CodeMatch( OpCodes.Ldc_R4, 0f ),
                        new CodeMatch( OpCodes.Ldc_R4, 0f ),
                        new CodeMatch( OpCodes.Ldarg_0 ),
                        new CodeMatch( OpCodes.Call ),
                        new CodeMatch( OpCodes.Ldarg_0 ),
                        new CodeMatch( OpCodes.Call ),
                        new CodeMatch( i => i.Calls( deathRPCMethod ) )
                    );

                if ( !codeMatcher.IsValid )
                {
                    Main.mod.Logger.Log( "SkinnedMook.Start transpiler: Could not find DeathRPC call" );
                    return instructions;
                }

                Label skipDeathLabel = generator.DefineLabel();

                codeMatcher.Insert(
                    new CodeInstruction( OpCodes.Ldarg_0 ),
                    new CodeInstruction( OpCodes.Ldfld, playerNumField ),
                    new CodeInstruction( OpCodes.Ldc_I4_0 ),
                    new CodeInstruction( OpCodes.Bge_S, skipDeathLabel )
                );

                codeMatcher.Advance( 12 );
                codeMatcher.Labels.Add( skipDeathLabel );

                return codeMatcher.InstructionEnumeration();
            }
        }
        #endregion
        #endregion // End Enemy Specific Patches

        #region Spawning As Enemy Patches
        // Automatic spawn
        [HarmonyPatch( typeof( Player ), "SpawnHero" )]
        static class Player_SpawnHero_Patch
        {
            static void Prefix( Player __instance, ref HeroType nextHeroType )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                Main.currentlyEnemy[__instance.playerNum] = false;

                if ( Main.settings.spawnAsEnemyEnabled )
                {
                    Main.willReplaceBro[__instance.playerNum] = ( Main.settings.spawnAsEnemyChance > 0 && Main.settings.spawnAsEnemyChance >= UnityEngine.Random.value * 100f );

                    // Disable BroMaker if it's installed
                    if ( Main.willReplaceBro[__instance.playerNum] )
                    {
                        Main.DisableBroMaker( __instance.playerNum );
                    }
                }

                return;
            }
            static void Postfix( Player __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                try
                {
                    if ( Main.willReplaceBro[__instance.playerNum] )
                    {
                        // Reenable BroMaker if it's installed
                        Main.EnableBroMaker();

                        Main.willReplaceBro[__instance.playerNum] = false;

                        if ( !Main.settings.alwaysChosen )
                        {
                            // Randomize unit
                            Main.settings.selGridInt[__instance.playerNum] = UnityEngine.Random.Range( 0, Main.currentUnitList.Length );
                        }
                        GameObject obj = Main.SpawnUnit( Main.GetSelectedUnit( __instance.playerNum ), new Vector3( 0f, 0f, 0f ) );
                        TestVanDammeAnim newUnit = obj.GetComponent<TestVanDammeAnim>();
                        Main.StartControllingUnit( __instance.playerNum, newUnit, false, false, true );
                        Main.WorkOutSpawnPosition( __instance, newUnit );
                        // Move lost soul up a bit
                        if ( Main.GetSelectedUnit( __instance.playerNum ) == UnitType.LostSoul )
                        {
                            Traverse.Create( newUnit ).SetFieldValue( "flying", true );
                            newUnit.yI += 30f;
                            newUnit.xI += 30f;
                            newUnit.Y += 10f;
                        }
                    }
                    else
                    {
                        Main.previousCharacter[__instance.playerNum] = null;
                        Main.currentlyEnemy[__instance.playerNum] = false;
                    }

                    // Hide player since they're not the current hero
                    if ( Main.settings.competitiveModeEnabled && __instance.playerNum != Main.currentHeroNum )
                    {
                        Main.HidePlayer( __instance.playerNum );
                    }
                }
                catch ( Exception e )
                {
                    Main.Log( "Exception replacing bro: " + e.ToString() );
                }
            }
        }

        // Fix vanilla bro being visible for 1 frame in certain spawning situations
        [HarmonyPatch( typeof( Player ), "InstantiateHero" )]
        static class Player_InstantiateHero_Patch
        {
            static void Postfix( Player __instance, ref TestVanDammeAnim __result )
            {
                // If mod is disabled or if we aren't loading a custom character don't disable
                if ( !Main.enabled || !Main.willReplaceBro[__instance.playerNum] )
                {
                    return;
                }

                __result.gameObject.SetActive( false );
            }
        }

        [HarmonyPatch( typeof( Map ), "AddBroToHeroTransport" )]
        static class Map_AddBroToHeroTransport_Patch
        {
            static bool Prefix( Map __instance, ref TestVanDammeAnim Bro )
            {
                // If mod is disabled or if we aren't loading a custom character don't disable
                return !Main.enabled || !( Bro.playerNum >= 0 && Bro.playerNum < 4 && Main.willReplaceBro[Bro.playerNum] );
            }
        }

        [HarmonyPatch( typeof( Player ), "WorkOutSpawnPosition" )]
        static class Player_WorkOutSpawnPosition_Patch
        {
            static void Prefix( Player __instance, ref TestVanDammeAnim bro )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( Main.willReplaceBro[__instance.playerNum] )
                {
                    Main.wasFirstDeployment[__instance.playerNum] = __instance.firstDeployment;
                }
            }
        }

        [HarmonyPatch( typeof( Player ), "WorkOutSpawnScenario" )]
        public static class Player_WorkOutSpawnScenario_Patch
        {
            public static bool forceCheckpointSpawn = false;

            static void Postfix( Player __instance, ref Player.SpawnType __result )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Set all players lives to starting value if in competitive mode
                if ( Main.settings.competitiveModeEnabled )
                {
                    if ( __instance.firstDeployment )
                    {
                        if ( __instance.playerNum == Main.currentHeroNum )
                        {
                            // Infinite lives
                            if ( Main.settings.heroLives == 0 )
                            {
                                __instance.Lives = 10;
                            }
                            else
                            {
                                // Give hero extra lives for win attempt
                                if ( Main.anyAttemptingWin )
                                {
                                    __instance.Lives = Main.settings.heroLives + Main.settings.extraLiveOnBossLevel + Main.settings.livesHandicap[__instance.playerNum];
                                }
                                else
                                {
                                    __instance.Lives = Main.settings.heroLives + Main.settings.livesHandicap[__instance.playerNum];
                                }
                            }
                        }
                        else
                        {
                            // Infinite lives
                            if ( Main.settings.ghostLives == 0 )
                            {
                                __instance.Lives = 10;
                            }
                            else
                            {
                                __instance.Lives = Main.settings.ghostLives + Main.settings.livesHandicap[__instance.playerNum];
                            }
                        }
                    }

                    if ( forceCheckpointSpawn )
                    {
                        __result = Player.SpawnType.CheckpointRespawn;
                        if ( Main.isBroMakerInstalled )
                        {
                            Main.OverrideSpawn( __instance.playerNum );
                        }
                        forceCheckpointSpawn = false;
                    }
                }

                // Store spawning info of normal character so we can pass it on to the custom character
                if ( Main.willReplaceBro[__instance.playerNum] )
                {
                    Main.previousSpawnInfo[__instance.playerNum] = __result;
                }
            }
        }

        [HarmonyPatch( typeof( HeroTransport ), "ReleaseBros" )]
        static class HeroTransport_ReleaseBros_Patch
        {
            public static void Postfix( HeroTransport __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                for ( int i = 0; i < 4; ++i )
                {
                    if ( HeroController.PlayerIsAlive( i ) && Main.currentlyEnemy[i] )
                    {
                        TestVanDammeAnim character = HeroController.players[i].character;
                        if ( character.enemyAI != null )
                        {
                            character.enemyAI.enabled = true;
                        }
                    }
                }
            }
        }

        // Check for swap button being pressed
        // Check for special 2 and 3 buttons
        [HarmonyPatch( typeof( Player ), "GetInput" )]
        static class Player_GetInput_Patch
        {
            public static void Postfix( Player __instance, ref bool up, ref bool down, ref bool left, ref bool right, ref bool fire, ref bool buttonJump, ref bool special, ref bool highFive, ref bool buttonGesture, ref bool sprint )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                int playerNum = __instance.playerNum;
                if ( Main.settings.spawnAsEnemyEnabled )
                {
                    bool leftPressed = Main.swapEnemiesLeft.IsDown( playerNum );
                    bool rightPressed = Main.swapEnemiesRight.IsDown( playerNum );

                    if ( ( ( ( leftPressed || rightPressed ) && Main.currentSpawnCooldown[playerNum] <= 0f && __instance.IsAlive() ) || ( Main.settings.clickingSwapEnabled && Main.switched[playerNum] ) ) && __instance.character.pilottedUnit == null )
                    {
                        float X, Y, XI, YI;
                        Vector3 vec = __instance.GetCharacterPosition();
                        X = vec.x;
                        Y = vec.y;
                        XI = (float)Traverse.Create( __instance.character ).Field( "xI" ).GetValue();
                        YI = (float)Traverse.Create( __instance.character ).Field( "yI" ).GetValue();

                        if ( Main.settings.clickingSwapEnabled && Main.switched[playerNum] )
                        {
                            try
                            {
                                GameObject obj = Main.SpawnUnit( Main.GetSelectedUnit( playerNum ), vec );
                                TestVanDammeAnim newUnit = obj.GetComponent<TestVanDammeAnim>();
                                Main.StartControllingUnit( playerNum, newUnit, false, false, true );

                                __instance.character.SetPositionAndVelocity( X, Y, XI, YI );
                                __instance.character.SetInvulnerable( 0f, false );
                                // Move lost soul up a bit
                                if ( Main.GetSelectedUnit( __instance.playerNum ) == UnitType.LostSoul )
                                {
                                    Traverse.Create( newUnit ).SetFieldValue( "flying", true );
                                    newUnit.yI += 30f;
                                    newUnit.xI += 30f;
                                    newUnit.Y += 10f;
                                }
                                Main.switched[playerNum] = false;
                            }
                            catch ( Exception ex )
                            {
                                Main.Log( "Exception switching unit: " + ex.ToString() );
                            }
                            return;
                        }
                        else
                        {
                            if ( leftPressed )
                            {
                                --Main.settings.selGridInt[playerNum];
                                if ( Main.settings.selGridInt[playerNum] < 0 )
                                {
                                    Main.settings.selGridInt[playerNum] = Main.currentUnitList.Length - 1;
                                }
                            }
                            else if ( rightPressed )
                            {
                                ++Main.settings.selGridInt[playerNum];
                                if ( Main.settings.selGridInt[playerNum] > Main.currentUnitList.Length - 1 )
                                {
                                    Main.settings.selGridInt[playerNum] = 0;
                                }
                            }

                            GameObject obj = Main.SpawnUnit( Main.GetSelectedUnit( playerNum ), vec );
                            TestVanDammeAnim newUnit = obj.GetComponent<TestVanDammeAnim>();
                            Main.StartControllingUnit( playerNum, newUnit, false, false, true );

                            __instance._character.SetPositionAndVelocity( X, Y, XI, YI );
                            __instance.character.SetInvulnerable( 0f, false );
                            // Move lost soul up a bit
                            if ( Main.GetSelectedUnit( __instance.playerNum ) == UnitType.LostSoul )
                            {
                                Traverse.Create( newUnit ).SetFieldValue( "flying", true );
                                newUnit.yI += 30f;
                                newUnit.xI += 30f;
                                newUnit.Y += 10f;
                            }

                            Main.currentSpawnCooldown[playerNum] = Main.settings.spawnSwapCooldown;
                        }
                    }
                }

                if ( Main.currentlyEnemy[playerNum] )
                {
                    TestVanDammeAnim character = __instance.character;

                    #region Special
                    bool special2Down = Main.special2[playerNum].IsDown();
                    bool special3Down = Main.special3[playerNum].IsDown();

                    Main.HandleSpecial( ref special, ref Main.specialWasDown[playerNum], ref Main.holdingSpecial[playerNum], character, playerNum );
                    Main.HandleButton( special2Down, ref Main.holdingSpecial2[playerNum], ref Main.special2WasDown[playerNum], Main.PressSpecial2, Main.ReleaseSpecial2, character, playerNum );
                    Main.HandleButton( special3Down, ref Main.holdingSpecial3[playerNum], ref Main.special3WasDown[playerNum], Main.PressSpecial3, Main.ReleaseSpecial3, character, playerNum );

                    // Override special with our own variable so we can disable specials that don't work
                    special = Main.holdingSpecial[playerNum];
                    #endregion

                    #region Taunting
                    // Started dancing
                    if ( buttonGesture && !Main.holdingGesture[playerNum] && Main.currentUnitType[playerNum].CanDance() )
                    {
                        Main.holdingGesture[playerNum] = true;
                        character.Dance( 10000000f );
                    }
                    #endregion

                    #region Competitive
                    // Check if enemy should be revealed if in competitive mode
                    if ( Main.settings.competitiveModeEnabled )
                    {
                        if ( fire && Main.currentHeroNum != playerNum )
                        {
                            Main.revealed[playerNum] = true;
                        }

                        // Don't allow enemies to move when stunned
                        if ( character.IsIncapacitated() || character.IsBlind() )
                        {
                            up = down = left = right = fire = buttonJump = special = highFive = buttonGesture = sprint = false;
                        }
                    }
                    #endregion
                }
                return;
            }
        }

        // Don't let enemies have their playernum overwritten
        [HarmonyPatch( typeof( TestVanDammeAnim ), "Start" )]
        static class TestVanDammeAnim_Start_Patch
        {
            public static void Postfix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( Main.settings.spawnAsEnemyEnabled )
                {
                    if ( __instance.name == "c" )
                    {
                        int playerNum = Main.currentUnit.IndexOf( __instance );
                        if ( playerNum != __instance.playerNum )
                        {
                            Main.ReaffirmControl( playerNum );
                        }
                        __instance.SpecialAmmo = 0;
                    }
                }
                else if ( Main.settings.competitiveModeEnabled )
                {
                    if ( Main.waitingToBecomeEnemy.Count > 0 )
                    {
                        int chosenPlayer = Main.waitingToBecomeEnemy[UnityEngine.Random.Range( 0, Main.waitingToBecomeEnemy.Count )];
                        if ( chosenPlayer == Main.currentHeroNum )
                        {
                            Main.waitingToBecomeEnemy.Remove( chosenPlayer );
                            return;
                        }
                        if ( !( __instance.playerNum >= 0 && __instance.playerNum < 4 ) && __instance.name != "c" && __instance.name != "Hobro" )
                        {
                            Main.StartControllingUnit( chosenPlayer, __instance, false, true, false );
                        }
                    }
                }
            }
        }

        // Don't let enemies have their playernum overwritten
        [HarmonyPatch( typeof( Alien ), "Start" )]
        static class Alien_Start_Patch
        {
            public static void Postfix( Alien __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( __instance.name == "c" )
                {
                    int playerNum = Main.currentUnit.IndexOf( __instance );
                    if ( playerNum != __instance.playerNum )
                    {
                        Main.ReaffirmControl( playerNum );
                    }
                    __instance.SpecialAmmo = 0;
                }
            }
        }
        #endregion

        #region Competitive Mode Patches
        // Allow players to kill each other
        [HarmonyPatch( typeof( GameModeController ), "DoesPlayerNumDamage" )]
        static class GameModeController_DoesPlayerNumDamage_Patch
        {
            public static void Postfix( GameModeController __instance, ref int fromNum, ref int toNum, ref bool __result )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return;
                }

                if ( fromNum != toNum )
                {
                    // Hero player was attacked
                    if ( toNum == Main.currentHeroNum )
                    {
                        // Hit player unless a playerNum greater than 3 was attacking them, which is seemingly reserved for stuff like doors and crates
                        if ( fromNum < 4 )
                        {
                            __result = true;
                        }
                    }
                    // Hero player attacking
                    else if ( fromNum == Main.currentHeroNum )
                    {
                        // Ghost player being attacked
                        if ( toNum >= 0 && toNum < 4 && Main.currentlyEnemy[toNum] )
                        {
                            __result = true;
                        }
                    }
                    // Ghost Player attacking things other than hero
                    else if ( fromNum != Main.currentHeroNum )
                    {
                        // Ghost player was attacked
                        if ( toNum >= 0 && toNum < 4 )
                        {
                            __result = false;
                        }
                        // Ghost player attacking enemies
                        else
                        {
                            __result = false;
                        }
                    }

                }
            }
        }

        // Make camera focus on hero
        [HarmonyPatch( typeof( Player ), "HasFollowPosition" )]
        static class Player_HasFollowPosition_Patch
        {
            public static bool Prefix( Player __instance, ref bool __result )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                if ( __instance != null && __instance.playerNum != Main.currentHeroNum && ( !Main.settings.ghostControlledEnemiesAffectCamera || __instance.character == null || __instance.character.destroyed || __instance.character.name != "c" || !__instance.character.IsAlive() ) )
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        // Hide ghost players from enemies
        [HarmonyPatch( typeof( TestVanDammeAnim ), "AlertNearbyMooks" )]
        static class TestVanDammeAnim_AlertNearbyMooks_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                if ( __instance.IsHero && __instance.playerNum != Main.currentHeroNum )
                {
                    return false;
                }

                return true;
            }
        }

        // Hide ghost players from enemies
        [HarmonyPatch( typeof( TestVanDammeAnim ), "IsInStealthMode" )]
        static class TestVanDammeAnim_IsInStealthMode_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance, ref bool __result )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 && __instance.playerNum != Main.currentHeroNum )
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        // Hide ghost players from enemies
        [HarmonyPatch( typeof( Map ), "DisturbWildLife" )]
        static class Map_DisturbWildLife_Patch
        {
            public static bool Prefix( Map __instance, ref int playerNum )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                if ( playerNum >= 0 && playerNum < 4 && playerNum != Main.currentHeroNum )
                {
                    return false;
                }

                return true;
            }
        }

        // Hide ghost players from enemies
        [HarmonyPatch( typeof( Map ), "BotherNearbyMooks" )]
        static class Map_BotherNearbyMooks_Patch
        {
            public static bool Prefix( Map __instance, ref int playerNum )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                if ( playerNum >= 0 && playerNum < 4 && playerNum != Main.currentHeroNum )
                {
                    return false;
                }

                return true;
            }
        }

        // Ensure players have the correct number of lives
        [HarmonyPatch( typeof( Player ), "Start" )]
        static class Player_Start_Patch
        {
            public static void Postfix( Player __instance )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( Main.settings.competitiveModeEnabled )
                {
                    // Setup score display
                    ScoreManager.SetupSprites( __instance.playerNum );

                    // Fix lives
                    if ( __instance.playerNum == Main.currentHeroNum )
                    {
                        // Infinite lives
                        if ( Main.settings.heroLives == 0 )
                        {
                            __instance.Lives = 10;
                        }
                        else
                        {
                            // Give hero extra lives for win attempt
                            if ( Main.anyAttemptingWin )
                            {
                                __instance.Lives = Main.settings.heroLives + Main.settings.extraLiveOnBossLevel + Main.settings.livesHandicap[__instance.playerNum];
                            }
                            else
                            {
                                __instance.Lives = Main.settings.heroLives + Main.settings.livesHandicap[__instance.playerNum];
                            }
                        }
                    }
                    else
                    {
                        // Infinite lives
                        if ( Main.settings.ghostLives == 0 )
                        {
                            __instance.Lives = 10;
                        }
                        else
                        {
                            __instance.Lives = Main.settings.ghostLives + Main.settings.livesHandicap[__instance.playerNum];
                        }
                    }
                }
            }
        }

        // Fix avatars flashing for ghost players
        [HarmonyPatch( typeof( HeroController ), "FlashAvatar" )]
        static class HeroController_FlashAvatar_Patch
        {
            public static bool Prefix( ref int playerNum )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                return playerNum == Main.currentHeroNum;
            }
        }

        // Fix avatars flashing for ghost players
        [HarmonyPatch( typeof( PlayerHUD ), "FlashAvatar" )]
        static class PlayerHUD_FlashAvatar_Patch
        {
            public static bool Prefix( PlayerHUD __instance, int ___playerNum )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                return ___playerNum == Main.currentHeroNum;
            }
        }

        // Fix ghosts being left over when player drops out of the game
        [HarmonyPatch( typeof( HeroController ), "DropoutRPC" )]
        static class HeroController_DropoutRPC_Patch
        {
            public static void Prefix( ref int playerNum )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Delete ghost if bro is dropping out of the game
                if ( Main.settings.competitiveModeEnabled && Main.currentGhosts[playerNum] != null )
                {
                    UnityEngine.Object.Destroy( Main.currentGhosts[playerNum].gameObject );
                    Main.previousCharacter[playerNum] = null;
                    Main.currentlyEnemy[playerNum] = false;
                    Main.currentGhosts[playerNum] = null;
                }
            }
        }

        // Disable rescuing prisoners for ghost players
        [HarmonyPatch( typeof( TestVanDammeAnim ), "CheckRescues" )]
        static class TestVanDammeAnim_CheckRescues_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                return __instance.playerNum == Main.currentHeroNum;
            }
        }

        // Disable highfives with ghost players
        [HarmonyPatch( typeof( HeroController ), "IsAnotherPlayerNearby" )]
        static class HeroController_IsAnotherPlayerNearby_Patch
        {
            public static bool Prefix( HeroController __instance, ref bool __result )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                __result = false;
                return false;
            }
        }

        // Include ghost controlled mooks as mooks
        [HarmonyPatch( typeof( Map ), "GetNearbyMook" )]
        static class Map_GetNearbyMook_Patch
        {
            public static bool Prefix( Map __instance, ref float xRange, ref float yRange, ref float x, ref float y, ref int direction, ref bool canBeDead, ref Mook __result )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                __result = null;
                float nearestDist = Mathf.Max( xRange, yRange ) + 1f;
                for ( int i = Map.units.Count - 1; i >= 0; i-- )
                {
                    Unit unit = Map.units[i];
                    if ( !( unit == null ) && ( unit.playerNum < 0 || unit.name == "c" ) && ( canBeDead || unit.health > 0 ) )
                    {
                        float num = unit.X - x;
                        if ( Tools.FastAbsWithinRange( num, xRange ) && Mathf.Sign( num ) == (float)direction )
                        {
                            float num2 = unit.Y - y;
                            if ( Tools.FastAbsWithinRange( num2, yRange ) )
                            {
                                Mook component = unit.GetComponent<Mook>();
                                if ( component != null )
                                {
                                    float num3 = Mathf.Abs( num ) + Mathf.Abs( num2 );
                                    if ( num3 < nearestDist )
                                    {
                                        nearestDist = num3;
                                        __result = component;
                                    }
                                }
                            }
                        }
                    }
                }
                return false;
            }
        }

        // Include ghost controlled mooks as mooks
        [HarmonyPatch( typeof( Map ), "GetNearbyMookVertical" )]
        static class Map_GetNearbyMookVertical_Patch
        {
            public static bool Prefix( Map __instance, ref float xRange, ref float yRange, ref float x, ref float y, ref int direction, ref bool canBeDead, ref Mook __result )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                if ( Map.units == null )
                {
                    __result = null;
                    return false;
                }
                float nearestDist = Mathf.Max( xRange, yRange ) + 1f;
                for ( int i = Map.units.Count - 1; i >= 0; i-- )
                {
                    Unit unit = Map.units[i];
                    if ( !( unit == null ) && unit.playerNum < 0 && ( canBeDead || unit.health > 0 ) )
                    {
                        float num = unit.X - x;
                        if ( Tools.FastAbsWithinRange( num, xRange ) )
                        {
                            float num2 = unit.Y - y;
                            if ( Tools.FastAbsWithinRange( num2, yRange ) && Mathf.Sign( num2 ) == (float)direction )
                            {
                                Mook component = unit.GetComponent<Mook>();
                                if ( component != null )
                                {
                                    float num3 = Mathf.Abs( num ) + Mathf.Abs( num2 );
                                    if ( num3 < nearestDist )
                                    {
                                        nearestDist = num3;
                                        __result = component;
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }
        }

        // Prevent rescues from going to ghost players that have died
        [HarmonyPatch( typeof( HeroController ), "MayIRescueThisBro" )]
        static class HeroController_MayIRescueThisBro_Patch
        {
            public static bool Prefix( HeroController __instance, ref int playerNum, ref RescueBro rescueBro, ref Ack ackRequest )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                // Don't bother checking anything, always accept rescue requests from hero player
                if ( playerNum == Main.currentHeroNum )
                {
                    typeof( HeroController ).GetMethod( "SwapBro", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( __instance, new object[] { playerNum, rescueBro, HeroController.Instance.playerDeathOrder.ToArray(), ackRequest } );
                    return false;
                }

                return true;
            }
        }

        // Keep score
        [HarmonyPatch( typeof( GameModeController ), "LevelFinish" )]
        public static class GameModeController_LevelFinish_Patch
        {
            public static bool finishedThisLevel = false;
            public static void Prefix( GameModeController __instance, ref LevelResult result )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return;
                }

                if ( result == LevelResult.Success && !finishedThisLevel )
                {
                    // Make sure this level isn't finishing multiple times
                    finishedThisLevel = true;
                    ++ScoreManager.currentScore[Main.currentHeroNum];
                    ScoreManager.UpdateScore( Main.currentHeroNum );
                }
            }
        }

        // Make sure ghost controlled pigs can hit the player
        [HarmonyPatch( typeof( Animal ), "RunFalling" )]
        static class Animal_RunFalling_Patch
        {
            public static bool Prefix( Animal __instance )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    if ( __instance.fatAnimal )
                    {
                        __instance.invulnerable = true;
                        // Make sure we can hit players for competitive mode
                        if ( Map.HitLivingUnits( __instance, __instance.playerNum, 3, DamageType.Crush, __instance.squashRange, __instance.X, __instance.Y + 2f, __instance.transform.localScale.x * 100f, 30f, false, true, true, false ) )
                        {
                            __instance.PlaySpecialAttackSound( 0.25f );
                            __instance.yI = 160f;
                        }
                        __instance.invulnerable = false;
                        return false;
                    }
                }

                return true;
            }
        }

        // Don't allow ghost players to activate checkpoints
        [HarmonyPatch( typeof( TestVanDammeAnim ), "CheckForCheckPoints" )]
        static class TestVanDammeAnim_CheckForCheckPoints_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                if ( __instance.name == "c" )
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( Map ), "CallInTransport_RPC" )]
        static class Map_CallInTransport_RPC_Patch
        {
            public static void Prefix( Map __instance, ref bool ArriveByHelicopter )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return;
                }

                Helicopter_Enter_Patch.helicopterDroppingOff = ArriveByHelicopter;
            }
        }

        // Create exit portal on level finish if this is a victory helicopter
        [HarmonyPatch( typeof( Helicopter ), "Enter" )]
        public static class Helicopter_Enter_Patch
        {
            public static bool helicopterDroppingOff = false;
            public static void Postfix( Helicopter __instance, ref Vector2 Target )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return;
                }

                // Make sure this isn't a dropoff helicopter
                if ( !helicopterDroppingOff && !Main.openedPortal )
                {
                    // Check if any players are able to win
                    for ( int i = 0; i < 4; ++i )
                    {
                        if ( ScoreManager.CanWin( i ) )
                        {
                            Main.openedPortal = true;
                            Vector2 portalLocation = Target;
                            portalLocation.x += 70;
                            Map.CreateExitPortal( portalLocation );
                            return;
                        }
                    }
                }

                helicopterDroppingOff = false;
            }
        }

        // Create exit portal on level finish for alien levels that use elevators
        [HarmonyPatch( typeof( GiantElevator ), "StartMoving" )]
        static class GiantElevator_StartMoving_Patch
        {
            public static bool Prefix( GiantElevator __instance, float ___floorY, bool ___hasDoneVictory )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                // Prevent the elevator from being hit for a few seconds to give the portal time to spawn
                if ( Main.disableElevatorCounter > 0f )
                {
                    return false;
                }

                if ( !Main.openedPortal && ___floorY < 0f && !___hasDoneVictory )
                {
                    // Check if any players are able to win
                    for ( int i = 0; i < 4; ++i )
                    {
                        if ( ScoreManager.CanWin( i ) )
                        {
                            Main.openedPortal = true;
                            Main.disableElevatorCounter = 5f;
                            Vector2 portalLocation = new Vector2( __instance.transform.position.x, __instance.transform.position.y + 20f );
                            Map.CreateExitPortal( portalLocation );
                            return false;
                        }
                    }
                }


                return true;
            }
        }

        // Check if player that can win enter the portal
        [HarmonyPatch( typeof( TestVanDammeAnim ), "SuckIntoPortal" )]
        static class TestVanDammeAnim_SuckIntoPortal_Patch
        {
            public static void Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return;
                }

                if ( ScoreManager.CanWin( __instance.playerNum ) )
                {
                    Main.anyAttemptingWin = true;
                    Main.attemptingWin = __instance.playerNum;
                }
            }
        }

        // Go to boss level for win attempt or return to level we were previously on after a failed attempt
        [HarmonyPatch( typeof( GameModeController ), "LoadNextScene" )]
        static class GameModeController_LoadNextScene_Patch
        {
            public static void Prefix( GameModeController __instance )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return;
                }

                if ( Main.anyAttemptingWin && !Main.onWinAttempt )
                {
                    Main.previousSceneName = GameState.Instance.sceneToLoad;
                    Main.previousCampaignName = GameState.Instance.campaignName;
                    Main.previousLevel = LevelSelectionController.CurrentLevelNum;

                    LevelSelectionController.ResetLevelAndGameModeToDefault();
                    GameState.Instance.ResetToDefault();

                    int chosenBoss = UnityEngine.Random.Range( 0, 6 );

                    switch ( chosenBoss )
                    {
                        // CR666 (Campaign 6 Level 4)
                        case 0:
                            GameState.Instance.campaignName = "WM_Village1(mouse)";
                            LevelSelectionController.CurrentLevelNum = 3;
                            break;
                        // Sky Fortress (Campaign 8 Level 4)
                        case 1:
                            GameState.Instance.campaignName = "WM_Bombardment2(mouse)";
                            LevelSelectionController.CurrentLevelNum = 3;
                            break;
                        // Terrorbot (Campaign 10 Level 4)
                        case 2:
                            GameState.Instance.campaignName = "WM_KazakhstanRainy(mouse)";
                            LevelSelectionController.CurrentLevelNum = 3;
                            break;
                        // Humongocrawler (Campaign 12 Level 5)
                        case 3:
                            GameState.Instance.campaignName = "WM_AlienMission2(mouse)";
                            LevelSelectionController.CurrentLevelNum = 4;
                            break;
                        // Double Bone Wurms (Campaign 15 Level 11)
                        case 4:
                            GameState.Instance.campaignName = "WM_Hell";
                            LevelSelectionController.CurrentLevelNum = 10;
                            break;
                        // Satan (Campaign 15 Level 12)
                        case 5:
                            GameState.Instance.campaignName = "WM_Hell";
                            LevelSelectionController.CurrentLevelNum = 11;
                            break;
                    }

                    GameState.Instance.loadMode = MapLoadMode.Campaign;
                    GameState.Instance.gameMode = GameMode.Campaign;
                    GameState.Instance.returnToWorldMap = true;
                    GameState.Instance.sceneToLoad = LevelSelectionController.CampaignScene;
                    GameState.Instance.sessionID = Connect.GetIncrementedSessionID().AsByte;
                    Main.onWinAttempt = true;
                }
                else if ( Main.returnToPreviousLevel )
                {
                    LevelSelectionController.ResetLevelAndGameModeToDefault();
                    GameState.Instance.ResetToDefault();

                    GameState.Instance.sceneToLoad = Main.previousSceneName;
                    GameState.Instance.campaignName = Main.previousCampaignName;
                    LevelSelectionController.CurrentLevelNum = Main.previousLevel;

                    GameState.Instance.loadMode = MapLoadMode.Campaign;
                    GameState.Instance.gameMode = GameMode.Campaign;
                    GameState.Instance.returnToWorldMap = true;
                    GameState.Instance.sessionID = Connect.GetIncrementedSessionID().AsByte;

                    Main.returnToPreviousLevel = false;
                    // Reset mod
                    if ( Main.playerWon )
                    {
                        Main.ResetAll();
                    }
                }
            }
        }

        // Handle winning / losing boss level
        [HarmonyPatch( typeof( GameModeController ), "DetermineLevelOutcome" )]
        static class GameModeController_DetermineLevelOutcome_Patch
        {
            public static void Prefix( GameModeController __instance, LevelResult ___levelResult )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return;
                }

                if ( Main.onWinAttempt )
                {
                    // Win and show success screen
                    if ( ___levelResult == LevelResult.Success )
                    {
                        Main.onWinAttempt = false;
                        Main.anyAttemptingWin = false;
                        Main.playerWon = true;
                        // Return to main menu
                        Main.previousSceneName = GameState.Instance.sceneToLoad = LevelSelectionController.MainMenuScene;
                        Main.returnToPreviousLevel = true;

                    }
                    // Fail and return to previous level
                    else
                    {
                        Main.onWinAttempt = false;
                        Main.anyAttemptingWin = false;
                        Main.returnToPreviousLevel = true;
                        ScoreManager.requiredScore[Main.attemptingWin] += Main.settings.scoreIncrement + Main.settings.scoreIncreaseHandicap[Main.attemptingWin];
                        Main.attemptingWin = -1;
                    }
                }
            }
        }

        // Show win screen if player won boss level
        [HarmonyPatch( typeof( LevelOverScreen ), "Show" )]
        static class LevelOverScreen_Show_Patch
        {
            public static PlayerScoreDisplay[] scoreDisplays;

            public static bool Prefix( LevelOverScreen __instance )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return true;
                }

                if ( Main.playerWon )
                {
                    ScoreScreen scoreScreen = Traverse.Create( typeof( ScoreScreen ) ).GetFieldValue( "instance" ) as ScoreScreen;
                    scoreDisplays = Traverse.Create( scoreScreen ).GetFieldValue( "scoredisplays" ) as PlayerScoreDisplay[];
                    string text = string.Format( BitCode.Singleton<LanguageManager>.Instance.GetLocalisedString( "RESULT_PLAYER_WINS" ), HeroController.GetHeroColorName( Main.attemptingWin ) );
                    // Set herotype of winning hero so their avatar can be displayed
                    GameModeController.deathmatchHero[Main.attemptingWin] = HeroController.players[Main.attemptingWin].heroType;
                    ScoreScreen.Appear( 20f, text, true, true, Main.attemptingWin, false );
                    for ( int i = 0; i < 4; ++i )
                    {
                        if ( HeroController.IsPlaying( i ) )
                        {
                            ScoreManager.SetupFinalScoreSprites( i, scoreDisplays[i].deathObjectBase );
                        }
                    }
                    return false;
                }
                return true;
            }
        }

        // Check if weather is burning to set red player to different color
        [HarmonyPatch( typeof( WeatherController ), "SwitchWeather", new Type[] { typeof( WeatherType ), typeof( bool ) } )]
        static class WeatherController_SwitchWeather_Patch
        {
            public static void Prefix( WeatherController __instance, ref WeatherType newWeatherType )
            {
                if ( !Main.enabled || !Main.settings.competitiveModeEnabled )
                {
                    return;
                }

                Main.isBurning = newWeatherType == WeatherType.Burning;

                // Update red player's color
                if ( Main.isBurning && Main.currentGhosts[1] != null )
                {
                    Main.currentGhosts[1].playerColor = GhostPlayer.burningColor;
                    Main.currentGhosts[1].sprite.SetColor( new Color( GhostPlayer.burningColor.r, GhostPlayer.burningColor.g, GhostPlayer.burningColor.b, Main.currentGhosts[1].currentTransparency ) );
                }
            }
        }

        // Handle creating and loading saves
        [HarmonyPatch( typeof( SaveSlotsMenu ), "SelectSlot" )]
        static class SaveSlotsMenu_SelectSlot_Patch
        {
            public static void Prefix( SaveSlotsMenu __instance, ref int slot )
            {
                if ( SaveSlotsMenu.createNewGame )
                {
                    try
                    {
                        // If a new save is being created, clear previous save data
                        Main.settings.saveGames[slot] = new SaveGame();

                        // Randomize player
                        if ( Main.settings.startingHeroPlayer == 0 )
                        {
                            Main.settings.saveGames[slot].currentHeroNum = -1;
                        }
                        else
                        {
                            Main.settings.saveGames[slot].currentHeroNum = Main.settings.startingHeroPlayer - 1;
                        }

                        Main.settings.saveGames[slot].requiredScore = new int[] { Main.settings.scoreToWin + Main.settings.scoreHandicap[0], Main.settings.scoreToWin + Main.settings.scoreHandicap[1], Main.settings.scoreToWin + Main.settings.scoreHandicap[2], Main.settings.scoreToWin + Main.settings.scoreHandicap[3] };
                    }
                    catch ( Exception ex )
                    {
                        Main.Log( "Exception creating save game: " + ex.ToString() );
                    }
                }
            }
        }
        #endregion
    }
}
