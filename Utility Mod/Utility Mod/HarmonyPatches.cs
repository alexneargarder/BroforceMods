using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Utility_Mod
{
    public class HarmonyPatches
    {
        // Track if we've already replayed edits for this level
        static string lastReplayedLevelKey = "";

        static IEnumerator ReplayLevelEditsDelayed()
        {
            // Wait a bit to ensure level is fully loaded
            yield return new WaitForSeconds( 0.5f );

            // Replay the edits
            ContextMenuManager.ReplayLevelEdits();
        }

        [HarmonyPatch( typeof( WorldMapController ), "ProcessNextAction" )]
        static class WorldMapController_ProcessNextAction_Patch
        {
            public static int nextCampaign = 0;
            static bool Prefix( WorldMapController __instance, List<WorldMapController.QueuedAction> ___actionQueue, WorldTerritory3D[] ___territories3D, ref float ___queueCounter )
            {
                if ( !Main.enabled )
                    return true;
                if ( !Main.settings.cameraShake )
                {
                    PlayerOptions.Instance.cameraShakeAmount = 0f;
                }
                if ( !Main.settings.enableSkip )
                {
                    return true;
                }



                if ( ___actionQueue.Count > 0 )
                {
                    WorldMapController.QueuedAction queuedAction = ___actionQueue[0];
                    switch ( queuedAction.actionType )
                    {
                        case WorldMapController.QueuedActions.Helicopter:
                            WorldCamera.instance.MoveToHelicopter( 0f );
                            break;
                        case WorldMapController.QueuedActions.Terrorist:
                            queuedAction.territory.BecomeEnemyBase();
                            break;
                        case WorldMapController.QueuedActions.Alien:
                            queuedAction.territory.SetState( TerritoryState.Infested );
                            break;
                        case WorldMapController.QueuedActions.Burning:
                            queuedAction.territory.SetState( TerritoryState.TerroristBurning );
                            break;
                        case WorldMapController.QueuedActions.Liberated:
                            queuedAction.territory.SetState( TerritoryState.Liberated );
                            break;
                        case WorldMapController.QueuedActions.Hell:
                            queuedAction.territory.SetState( TerritoryState.Hell );
                            break;
                        case WorldMapController.QueuedActions.Secret:
                            break;
                    }
                    ___actionQueue.RemoveAt( 0 );
                    if ( queuedAction.actionType == WorldMapController.QueuedActions.Hell )
                    {
                        //queueCounter = 5.2f;
                        ___queueCounter = 0;
                    }
                    else if ( queuedAction.actionType == WorldMapController.QueuedActions.Liberated )
                    {
                        //queueCounter = 2f;
                        ___queueCounter = 0;
                    }
                    else if ( queuedAction.actionType == WorldMapController.QueuedActions.Secret )
                    {
                        //queueCounter = 0f;
                        ___queueCounter = 0;
                    }
                    else
                    {
                        //queueCounter = 1.6f;
                        ___queueCounter = 0;
                    }
                }
                else if ( WorldCamera.instance.CamState != WorldCamera.CameraState.FollowHelicopter && WorldCamera.instance.CamState != WorldCamera.CameraState.MoveToHelicopter )
                {
                    WorldCamera.instance.MoveToHelicopter( 0f );
                }

                nextCampaign = -1;
                foreach ( WorldTerritory3D ter in ___territories3D )
                {
                    if ( ter.properties.state == TerritoryState.Liberated )
                    {
                        for ( int i = 0; i < Main.campaignList.Length; ++i )
                        {
                            if ( ter.properties.territoryName == Main.campaignList[i] )
                            {
                                if ( i > nextCampaign )
                                {
                                    nextCampaign = i;
                                }
                                break;
                            }
                        }
                    }
                }
                ++nextCampaign;

                foreach ( WorldTerritory3D ter in ___territories3D )
                {
                    if ( ter.properties.state == TerritoryState.TerroristBase || ter.properties.state == TerritoryState.Hell
                        || ter.properties.state == TerritoryState.Infested || ter.properties.state == TerritoryState.TerroristBurning
                        || ter.properties.state == TerritoryState.TerroristAirBase )
                    {
                        if ( ter.properties.territoryName == Main.campaignList[nextCampaign] && GameState.Instance.campaignName != ter.GetCampaignName() )
                        {
                            WorldMapController.RestTransport( ter );
                            WorldMapController.EnterMission( ter.GetCampaignName(), ter.properties.loadingText, ter.properties );
                        }
                    }
                }

                return false;
            }
        }

        [HarmonyPatch( typeof( WorldMapController ), "Update" )]
        public static class WorldMapController_Update_Patch
        {
            public static WorldMapController instance;
            static void Prefix( WorldMapController __instance )
            {
                if ( !Main.enabled )
                    return;

                instance = __instance;
            }
        }

        [HarmonyPatch( typeof( GameModeController ), "LevelFinish" )]
        static class GameModeController_LevelFinish_Patch
        {
            static bool Prefix( GameModeController __instance, LevelResult result )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                // Check if helicopter should wait for ohter players
                if ( Main.settings.helicopterWait )
                {
                    if ( result != LevelResult.Success || ( HeroController.GetPlayersOnHelicopterAmount() == 0 ) )
                    {
                        return true;
                    }

                    if ( HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount() )
                    {
                        Helicopter_Leave_Patch.attachCalled = false;
                        Main.helicopter.Leave();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                if ( Main.settings.loopCurrent && result == LevelResult.Success )
                {
                    bool temp = Main.settings.disableConfirm;
                    Main.settings.disableConfirm = true;
                    PauseMenu_RestartLevel_Patch.Prefix( null );
                    Main.settings.disableConfirm = temp;
                }

                return true;
            }

            static void Postfix( GameModeController __instance, LevelResult result )
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( Main.settings.endingSkip && ( result == LevelResult.Success ) && !( ( GameState.Instance.campaignName == "WM_City2(mouse)" && GameState.Instance.levelNumber == 4 ) || ( GameState.Instance.campaignName == "WM_City2(mouse)" && GameState.Instance.levelNumber == 5 ) ) )
                {
                    GameModeController.MakeFinishInstant();
                }
            }
        }

        [HarmonyPatch( typeof( PauseMenu ), "ReturnToMenu" )]
        static class PauseMenu_ReturnToMenu_Patch
        {
            static bool Prefix( PauseMenu __instance, PauseGameConfirmationPopup ___m_ConfirmationPopup )
            {
                if ( !Main.enabled || !Main.settings.disableConfirm )
                {
                    return true;
                }

                MethodInfo dynMethod = ___m_ConfirmationPopup.GetType().GetMethod( "ConfirmReturnToMenu", BindingFlags.NonPublic | BindingFlags.Instance );
                dynMethod.Invoke( ___m_ConfirmationPopup, null );

                return false;
            }

        }

        [HarmonyPatch( typeof( PauseMenu ), "ReturnToMap" )]
        static class PauseMenu_ReturnToMap_Patch
        {
            static bool Prefix( PauseMenu __instance )
            {
                if ( !Main.enabled || !Main.settings.disableConfirm )
                {
                    return true;
                }

                __instance.CloseMenu();
                GameModeController.Instance.ReturnToWorldMap();
                return false;
            }

        }

        [HarmonyPatch( typeof( GameModeController ), "RestartLevel" )]
        static class GameModeController_RestartLevel_Patch
        {
            public static void Prefix()
            {
                // Reset level replay tracking when restarting
                lastReplayedLevelKey = "";
            }
        }

        [HarmonyPatch( typeof( PauseMenu ), "RestartLevel" )]
        static class PauseMenu_RestartLevel_Patch
        {
            public static bool Prefix( PauseMenu __instance )
            {
                if ( !Main.enabled || !Main.settings.disableConfirm )
                {
                    return true;
                }

                Map.ClearSuperCheckpointStatus();

                ( Traverse.Create( typeof( TriggerManager ) ).Field( "alreadyTriggeredTriggerOnceTriggers" ).GetValue() as List<string> ).Clear();

                if ( GameModeController.publishRun )
                {
                    GameModeController.publishRun = false;
                    LevelEditorGUI.levelEditorActive = true;
                }
                PauseController.SetPause( PauseStatus.UnPaused );
                GameModeController.RestartLevel();

                return false;
            }
        }

        [HarmonyPatch( typeof( MapController ), "SpawnMook_Networked" )]
        static class MapController_SpawnMook_Networked
        {
            public static bool Prefix()
            {
                if ( !Main.enabled || !Main.settings.disableEnemySpawn )
                {
                    return true;
                }

                return false;
            }
        }

        [HarmonyPatch( typeof( MapController ), "SpawnMook_Local" )]
        static class MapController_SpawnMook_Local
        {
            public static bool Prefix()
            {
                if ( !Main.enabled || !Main.settings.disableEnemySpawn )
                {
                    return true;
                }

                return false;
            }
        }

        [HarmonyPatch( typeof( Map ), "PlaceDoodad" )]
        static class Map_PlaceDoodad
        {
            public static bool Prefix( Map __instance, ref DoodadInfo doodad, ref GameObject __result )
            {
                if ( !Main.enabled || !Main.settings.disableEnemySpawn )
                {
                    return true;
                }

                if ( doodad.type == DoodadType.Mook || doodad.type == DoodadType.Alien || doodad.type == DoodadType.HellEnemy || doodad.type == DoodadType.AlienBoss || doodad.type == DoodadType.HellBoss )
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( Player ), "WorkOutSpawnScenario" )]
        static class Player_WorkOutSpawnScenario_Patch
        {
            public static void Prefix( Player __instance )
            {
                if ( !Main.enabled || ( !( Main.settings.changeSpawn && Main.HasCustomSpawnForCurrentLevel() ) && !Main.settings.changeSpawnFinal ) )
                {
                    return;
                }

                __instance.firstDeployment = false;
            }
            public static void Postfix( ref Player.SpawnType __result )
            {
                if ( !Main.enabled || ( !( Main.settings.changeSpawn && Main.HasCustomSpawnForCurrentLevel() ) && !Main.settings.changeSpawnFinal ) )
                {
                    return;
                }

                if ( __result != Player.SpawnType.RespawnAtRescueBro )
                {
                    __result = Player.SpawnType.Unknown;
                }
            }
        }

        [HarmonyPatch( typeof( Player ), "SetSpawnPositon" )]
        static class Player_SetSpawnPositon_Patch
        {
            public static void Prefix( Player __instance, ref TestVanDammeAnim bro, ref Player.SpawnType spawnType, ref bool spawnViaAirDrop, ref Vector3 pos )
            {
                // Check if we need to replay level edits
                if ( Main.enabled && Main.settings.enableLevelEditReplay )
                {
                    string currentLevelKey = Main.GetCurrentLevelKey();
                    if ( lastReplayedLevelKey != currentLevelKey )
                    {
                        lastReplayedLevelKey = currentLevelKey;

                        // If we have a custom spawn, apply edits immediately without delay
                        if ( Main.settings.changeSpawn && Main.HasCustomSpawnForCurrentLevel() )
                        {
                            // Apply level edits synchronously to ensure blocks are in place before spawn
                            ContextMenuManager.ReplayLevelEdits();
                        }
                        else
                        {
                            // Otherwise use the delayed replay for normal spawns
                            if ( ContextMenuManager.Instance != null )
                            {
                                ContextMenuManager.Instance.StartCoroutine( ReplayLevelEditsDelayed() );
                            }
                        }
                    }
                }

                if ( !Main.enabled || ( !( Main.settings.changeSpawn && Main.HasCustomSpawnForCurrentLevel() ) && !Main.settings.changeSpawnFinal ) )
                {
                    return;
                }

                if ( spawnType != Player.SpawnType.RespawnAtRescueBro )
                {
                    spawnType = Player.SpawnType.CustomSpawnPoint;
                    spawnViaAirDrop = false;

                    if ( Main.settings.changeSpawn && Main.HasCustomSpawnForCurrentLevel() )
                    {
                        Vector2 customSpawn = Main.GetCustomSpawnForCurrentLevel();
                        pos.x = customSpawn.x;
                        pos.y = customSpawn.y;
                    }
                    else if ( Main.settings.changeSpawnFinal )
                    {
                        pos = Main.GetFinalCheckpointPos();
                    }
                }
            }
        }

        [HarmonyPatch( typeof( MainMenu ), "Start" )]
        static class MainMenu_Start_Patch
        {
            public static void Prefix( MainMenu __instance )
            {
                if ( !Main.enabled || !Main.settings.quickMainMenu )
                {
                    return;
                }

                Main.skipNextMenu = false;

                Traverse.Create( __instance ).Method( "InitializeMenu" ).GetValue();

                Main.skipNextMenu = true;
            }

            public static void Postfix()
            {
                if ( !Main.enabled )
                {
                    return;
                }

                if ( !Main.loadedLevel && Main.settings.goToLevelOnStartup )
                {
                    Main.loadedLevel = true;
                    if ( !Main.settings.cameraShake )
                    {
                        PlayerOptions.Instance.cameraShakeAmount = 0f;
                    }
                    Main.GoToLevel( Main.campaignNum.indexNumber, Main.levelNum.indexNumber );
                }

                Main.loadedLevel = true;
            }
        }

        [HarmonyPatch( typeof( MainMenu ), "InitializeMenu" )]
        static class MainMenu_InitializeMenu_Patch
        {
            public static bool Prefix( MainMenu __instance )
            {
                if ( !Main.enabled || !Main.settings.quickMainMenu )
                {
                    return true;
                }

                if ( Main.skipNextMenu )
                {
                    Main.skipNextMenu = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( TestVanDammeAnim ), "SetSpecialAmmoRPC" )]
        static class TestVanDammeAnim_SetSpecialAmmoRPC_Patch
        {
            public static void Prefix( TestVanDammeAnim __instance, ref int ammo )
            {
                if ( !Main.enabled || !Main.settings.infiniteSpecials )
                {
                    return;
                }

                if ( ammo == 0 )
                {
                    if ( __instance.originalSpecialAmmo > 0 )
                    {
                        ammo = __instance.originalSpecialAmmo;
                    }
                    else
                    {
                        ammo = int.MaxValue;
                    }
                }
            }
        }

        [HarmonyPatch( typeof( TestVanDammeAnim ), "SetInvulnerable" )]
        static class TestVanDammeAnim_SetInvulnerable_Patch
        {
            public static void Prefix( ref float time )
            {
                if ( !Main.enabled || !Main.settings.invulnerable )
                {
                    return;
                }

                time = float.MaxValue;
            }
        }

        [HarmonyPatch( typeof( NetworkedUnit ), "Awake" )]
        static class NetworkedUnit_Awake_Patch
        {
            public static void Postfix( NetworkedUnit __instance )
            {
                if ( !Main.enabled || !Main.settings.oneHitEnemies )
                {
                    return;
                }

                __instance.health = 1;
                __instance.maxHealth = 1;
            }
        }

        [HarmonyPatch( typeof( Player ), "SpawnHero" )]
        static class Player_InstantiateHero_Patch
        {
            public static void Postfix()
            {
                if ( !Main.enabled || !Main.settings.slowTime )
                {
                    return;
                }

                Main.StartTimeSlow();
            }
        }

        [HarmonyPatch( typeof( Player ), "SetLivesRPC" )]
        static class Player_SetLivesRPC_Patch
        {
            public static void Prefix( ref int _lives )
            {
                if ( !Main.enabled || !Main.settings.infiniteLives )
                {
                    return;
                }

                _lives = int.MaxValue;
            }
        }

        [HarmonyPatch( typeof( TestVanDammeAnim ), "ApplyFallingGravity" )]
        static class TestVanDammeAnim_ApplyFallingGravity_Patch
        {
            public static bool Prefix( TestVanDammeAnim __instance )
            {
                if ( !Main.enabled || !( Main.settings.disableGravity || Main.settings.enableFlight ) )
                {
                    return true;
                }

                if ( __instance.yI <= 0 && !__instance.down )
                {
                    __instance.yI = 0;
                    return false;
                }

                return true;
            }
        }
        [HarmonyPatch( typeof( Sound ), "PlayAudioClip" )]
        static class Sound_PlayAudioClip_Patch
        {
            public static void Prefix( ref AudioClip clip )
            {
                if ( !Main.enabled || !Main.settings.printAudioPlayed )
                {
                    return;
                }

                Main.Log( "Audio clip played: " + clip.name );
            }
        }

        [HarmonyPatch( typeof( GameModeController ), "ResetForNextLevel" )]
        static class GameModeController_ResetForNextLevel_Patch
        {
            public static void Prefix()
            {
                if ( !Main.enabled || !Main.settings.setZoom )
                {
                    return;
                }

                Main.levelStartedCounter = 0f;
                SortOfFollow.zoomLevel = 1;
            }
        }

        [HarmonyPatch( typeof( HeroController ), "DoCountDown" )]
        static class HeroController_DoCountDown_Patch
        {
            public static void Prefix()
            {
                if ( !Main.enabled || !Main.settings.suppressAnnouncer )
                {
                    return;
                }

                Map.MapData.suppressAnnouncer = true;
            }
        }

        [HarmonyPatch( typeof( Map ), "PlaceDoodad" )]
        static class Map_PlaceDoodad_Patch
        {
            public static bool Prefix( Map __instance, ref DoodadInfo doodad, GameObject __result )
            {
                if ( !Main.enabled || !Main.settings.maxCageSpawns )
                {
                    return true;
                }

                if ( doodad.type == DoodadType.Cage )
                {
                    GridPoint gridPoint = new GridPoint( doodad.position.collumn, doodad.position.row );
                    gridPoint.collumn -= Map.lastXLoadOffset;
                    gridPoint.row -= Map.lastYLoadOffset;

                    Vector3 vector = new Vector3( (float)( gridPoint.c * 16 ), (float)( gridPoint.r * 16 ), 5f );

                    if ( GameModeController.IsHardcoreMode )
                    {
                        Map.havePlacedCageForHardcore = true;
                        Map.cagesSinceLastHardcoreCage = 0;
                    }

                    __result = ( UnityEngine.Object.Instantiate<Block>( __instance.activeTheme.blockPrefabCage, vector, Quaternion.identity ) as Cage ).gameObject;
                    __result.GetComponent<Cage>().row = gridPoint.row;
                    __result.GetComponent<Cage>().collumn = gridPoint.collumn;

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

                return true;
            }
        }

        // Make helicopter wait for players
        [HarmonyPatch( typeof( TestVanDammeAnim ), "AttachToHelicopter" )]
        static class TestVanDammeAnim_AttachToHelicopter_Patch
        {
            static void Prefix( TestVanDammeAnim __instance )
            {
                Helicopter_Leave_Patch.attachCalled = true;
            }
        }

        // Make helicopter wait for players
        [HarmonyPatch( typeof( Helicopter ), "Leave" )]
        static class Helicopter_Leave_Patch
        {
            public static bool attachCalled = false;
            static bool Prefix( Helicopter __instance )
            {
                if ( !Main.enabled || !Main.settings.helicopterWait )
                {
                    return true;
                }

                if ( HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount() || ( HeroController.GetPlayersOnHelicopterAmount() == 0 && !attachCalled ) )
                {
                    return true;
                }
                else
                {
                    Main.helicopter = __instance;
                    return false;
                }
            }
        }

        // Make helicopter wait for players
        [HarmonyPatch( typeof( Map ), "StartLevelEndExplosions" )]
        static class Map_StartLevelEndExplosions_Patch
        {
            static bool Prefix( Map __instance )
            {
                if ( !Main.enabled || !Main.settings.helicopterWait )
                {
                    return true;
                }

                if ( HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount() )
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        // Make helicopter wait for players
        [HarmonyPatch( typeof( Player ), "RemoveLife" )]
        static class Player_RemoveLife_Patch
        {
            static void Postfix( Player __instance )
            {
                if ( !Main.enabled || !Main.settings.helicopterWait )
                    return;

                if ( GameModeController.IsHardcoreMode && ( ( ( HeroController.GetPlayersOnHelicopterAmount() == ( HeroController.GetPlayersAliveCount() ) && HeroController.GetPlayersOnHelicopterAmount() > 0 ) ) || ( HeroController.GetTotalLives() == 0 ) ) )
                {
                    GameModeController.LevelFinish( LevelResult.ForcedFail );
                }
                if ( !GameModeController.IsHardcoreMode && HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount() && HeroController.GetPlayersOnHelicopterAmount() > 0 )
                {
                    GameModeController.LevelFinish( LevelResult.Success );
                }

            }
        }

        [HarmonyPatch( typeof( CutsceneController ), "LoadCutScene" )]
        static class CutsceneController_LoadCutScene_Patch
        {
            public static bool Prefix( CutsceneController __instance, ref CutsceneName name )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                if ( Main.settings.skipBreakingCutscenes && ( name == CutsceneName.FlexAir || name == CutsceneName.FlexGolden || name == CutsceneName.FlexInvincible || name == CutsceneName.FlexTeleport ) )
                {
                    return false;
                }
                else if ( Main.settings.skipBreakingCutscenes && ( name == CutsceneName.AmmoAirstrike || name == CutsceneName.AmmoMechDrop || name == CutsceneName.AmmoPheromones || name == CutsceneName.AmmoRCCar || name == CutsceneName.AmmoStandard || name == CutsceneName.AmmoSteroids || name == CutsceneName.AmmoTimeSlow ) )
                {
                    string sceneToLoad = GameModeController.FinishCampaignFromCutscene( true );
                    GameState.Instance.sceneToLoad = sceneToLoad;
                    GameModeController.LoadNextScene( GameState.Instance );
                    return false;
                }
                else if ( Main.settings.skipAllCutscenes )
                {
                    return false;
                }

                return true;
            }
        }

        // Disable checkpoint flag audio
        [HarmonyPatch( typeof( CheckPoint ), "ActivateInternal" )]
        static class CheckPoint_ActivateInternal_Patch
        {
            static void Postfix( CheckPoint __instance )
            {
                if ( !Main.enabled || !Main.settings.disableFlagNoise )
                    return;

                // Stop and disable the AudioSource after activation
                AudioSource audioSource = __instance.GetComponent<AudioSource>();
                if ( audioSource != null )
                {
                    audioSource.Stop();
                    audioSource.enabled = false;
                }
            }
        }

        // Disable helicopter audio
        [HarmonyPatch( typeof( Helicopter ), "Start" )]
        static class Helicopter_Start_Patch
        {
            static void Postfix( Helicopter __instance )
            {
                if ( !Main.enabled || !Main.settings.disableHelicopterNoise )
                    return;

                // Disable the helicopter's AudioSource
                AudioSource audioSource = __instance.GetComponent<AudioSource>();
                if ( audioSource != null )
                    audioSource.enabled = false;
            }
        }
        
        [HarmonyPatch( typeof( Startup ), "Update" )]
        static class Startup_Update_Patch
        {
            static bool Prefix( Startup __instance )
            {
                if ( !Main.enabled || !Main.settings.skipIntro )
                    return true;
                    
                // Skip intro and go straight to main menu
                GameState.LoadLevel( "MainMenu" );
                return false;
            }
        }
    }
}
