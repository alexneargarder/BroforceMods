﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Utility_Mod
{
    public class HarmonyPatches
    {
        [HarmonyPatch(typeof(WorldMapController), "ProcessNextAction")]
        static class WorldMapController_ProcessNextAction_Patch
        {
            public static int nextCampaign = 0;
            static bool Prefix(WorldMapController __instance)
            {
                if (!Main.enabled)
                    return true;
                if (!Main.settings.cameraShake)
                {
                    PlayerOptions.Instance.cameraShakeAmount = 0f;
                }
                if (!Main.settings.enableSkip)
                {
                    return true;
                }

                Traverse actionQueueTraverse = Traverse.Create(__instance);
                List<WorldMapController.QueuedAction> actionQueue = actionQueueTraverse.Field("actionQueue").GetValue() as List<WorldMapController.QueuedAction>;

                WorldTerritory3D[] territories = Traverse.Create(__instance).Field("territories3D").GetValue() as WorldTerritory3D[];

                if (actionQueue.Count > 0)
                {
                    WorldMapController.QueuedAction queuedAction = actionQueue[0];
                    switch (queuedAction.actionType)
                    {
                        case WorldMapController.QueuedActions.Helicopter:
                            WorldCamera.instance.MoveToHelicopter(0f);
                            break;
                        case WorldMapController.QueuedActions.Terrorist:
                            queuedAction.territory.BecomeEnemyBase();
                            break;
                        case WorldMapController.QueuedActions.Alien:
                            queuedAction.territory.SetState(TerritoryState.Infested);
                            break;
                        case WorldMapController.QueuedActions.Burning:
                            queuedAction.territory.SetState(TerritoryState.TerroristBurning);
                            break;
                        case WorldMapController.QueuedActions.Liberated:
                            queuedAction.territory.SetState(TerritoryState.Liberated);
                            break;
                        case WorldMapController.QueuedActions.Hell:
                            queuedAction.territory.SetState(TerritoryState.Hell);
                            break;
                        case WorldMapController.QueuedActions.Secret:
                            break;
                    }
                    actionQueue.RemoveAt(0);
                    Traverse queueCounter = Traverse.Create(__instance);
                    if (queuedAction.actionType == WorldMapController.QueuedActions.Hell)
                    {
                        //queueCounter = 5.2f;
                        queueCounter.Field("queueCounter").SetValue(0);
                    }
                    else if (queuedAction.actionType == WorldMapController.QueuedActions.Liberated)
                    {
                        //queueCounter = 2f;
                        queueCounter.Field("queueCounter").SetValue(0);
                    }
                    else if (queuedAction.actionType == WorldMapController.QueuedActions.Secret)
                    {
                        //queueCounter = 0f;
                        queueCounter.Field("queueCounter").SetValue(0);
                    }
                    else
                    {
                        //queueCounter = 1.6f;
                        queueCounter.Field("queueCounter").SetValue(0);
                    }
                }
                else if (WorldCamera.instance.CamState != WorldCamera.CameraState.FollowHelicopter && WorldCamera.instance.CamState != WorldCamera.CameraState.MoveToHelicopter)
                {
                    WorldCamera.instance.MoveToHelicopter(0f);
                }
                actionQueueTraverse.Field("actionQueue").SetValue(actionQueue);

                nextCampaign = -1;
                foreach (WorldTerritory3D ter in territories)
                {
                    if (ter.properties.state == TerritoryState.Liberated)
                    {
                        for (int i = 0; i < Main.campaignList.Length; ++i)
                        {
                            if (ter.properties.territoryName == Main.campaignList[i])
                            {
                                if (i > nextCampaign)
                                {
                                    nextCampaign = i;
                                }
                                break;
                            }
                        }
                    }
                }
                ++nextCampaign;

                foreach (WorldTerritory3D ter in territories)
                {
                    if (ter.properties.state == TerritoryState.TerroristBase || ter.properties.state == TerritoryState.Hell
                        || ter.properties.state == TerritoryState.Infested || ter.properties.state == TerritoryState.TerroristBurning
                        || ter.properties.state == TerritoryState.TerroristAirBase)
                    {
                        if (ter.properties.territoryName == Main.campaignList[nextCampaign] && GameState.Instance.campaignName != ter.GetCampaignName())
                        {
                            WorldMapController.RestTransport(ter);
                            WorldMapController.EnterMission(ter.GetCampaignName(), ter.properties.loadingText, ter.properties);
                        }
                    }
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(WorldMapController), "Update")]
        public static class WorldMapController_Update_Patch
        {
            public static WorldMapController instance;
            public static void GoToLevel(string campaignName, int levelNum, string terrainName)
            {
                if (instance == null || !Main.enabled)
                {
                    //Main.Log("instance null");
                    return;
                }

                WorldTerritory3D territoryObject = null; // = WorldMapController.GetTerritory(campaignName);

                WorldTerritory3D[] territories = Traverse.Create(typeof(WorldMapController)).Field("territories3D").GetValue() as WorldTerritory3D[];
                foreach (WorldTerritory3D ter in territories)
                {
                    //Main.Log(ter.properties.territoryName + " == " + campaignName + " = " + (ter.properties.territoryName == campaignName));
                    if (ter.properties.territoryName == campaignName)
                    {
                        territoryObject = ter;
                    }
                }

                if (territoryObject == null)
                {
                    //Main.Log(campaignName + " territory not found");
                    return;
                }

                WorldMapController.QueuedAction item = default(WorldMapController.QueuedAction);

                if (territoryObject.properties.isBurning)
                {
                    item.actionType = WorldMapController.QueuedActions.Burning;

                }
                else if ((Main.campaignNum.indexNumber + 1) >= 11)
                {
                    item.actionType = WorldMapController.QueuedActions.Alien;
                }
                else
                {
                    item.actionType = WorldMapController.QueuedActions.Terrorist;
                }

                item.territory = territoryObject;
                (Traverse.Create(instance).Field("actionQueue").GetValue() as List<WorldMapController.QueuedAction>).Add(item);

                //Main.mod.Logger.Log(territoryObject.properties.territoryName);
                //Main.mod.Logger.Log("isburning: " + territoryObject.properties.isBurning + " isCity: " + territoryObject.properties.isCity + " isSecret: " +
                //    territoryObject.properties.isCity + " state: " + territoryObject.properties.state);

                //return;

                foreach (TerritoryProgress territoryProgress in WorldMapProgressController.currentProgress.territoryProgress)
                {
                    //Main.mod.Logger.Log(territoryProgress.name.ToLower() + " == " + territoryObject.properties.territoryName.ToLower() + " = " + (territoryProgress.name.ToLower() == territoryObject.properties.territoryName.ToLower()));
                    if (territoryProgress.name.ToLower() == terrainName)
                    {
                        //Main.mod.Logger.Log("found territory changed level");
                        territoryProgress.startLevel = levelNum;
                    }
                }

                WorldMapController.RestTransport(territoryObject);
                WorldMapController.EnterMission(territoryObject.GetCampaignName(), territoryObject.properties.loadingText, territoryObject.properties);


            }
            static void Prefix(WorldMapController __instance)
            {
                if (!Main.enabled)
                    return;

                instance = __instance;
            }
        }

        [HarmonyPatch(typeof(GameModeController), "LevelFinish")]
        static class GameModeController_LevelFinish_Patch
        {
            static void Prefix(GameModeController __instance, LevelResult result)
            {
                if (!Main.enabled)
                {
                    return;
                }

                if (Main.settings.loopCurrent && result == LevelResult.Success)
                {
                    //Main.mod.Logger.Log("current level num after finish called: " + LevelSelectionController.CurrentLevelNum);
                    //LevelSelectionController.CurrentLevelNum -= 1;
                    //GameModeController.RestartLevel();
                    bool temp = Main.settings.disableConfirm;
                    Main.settings.disableConfirm = true;
                    PauseMenu_RestartLevel_Patch.Prefix(null);
                    Main.settings.disableConfirm = temp;
                }


            }

            static void Postfix(GameModeController __instance, LevelResult result)
            {
                if (!Main.enabled)
                {
                    return;
                }

                if (Main.settings.endingSkip && (result == LevelResult.Success))
                {
                    GameModeController.MakeFinishInstant();
                }
            }
        }

        [HarmonyPatch(typeof(PauseMenu), "ReturnToMenu")]
        static class PauseMenu_ReturnToMenu_Patch
        {
            static bool Prefix(PauseMenu __instance)
            {
                if (!Main.enabled || !Main.settings.disableConfirm)
                {
                    return true;
                }

                PauseGameConfirmationPopup m_ConfirmationPopup = (Traverse.Create(__instance).Field("m_ConfirmationPopup").GetValue() as PauseGameConfirmationPopup);

                MethodInfo dynMethod = m_ConfirmationPopup.GetType().GetMethod("ConfirmReturnToMenu", BindingFlags.NonPublic | BindingFlags.Instance);
                dynMethod.Invoke(m_ConfirmationPopup, null);

                return false;
            }

        }

        [HarmonyPatch(typeof(PauseMenu), "ReturnToMap")]
        static class PauseMenu_ReturnToMap_Patch
        {
            static bool Prefix(PauseMenu __instance)
            {
                if (!Main.enabled || !Main.settings.disableConfirm)
                {
                    return true;
                }

                __instance.CloseMenu();
                GameModeController.Instance.ReturnToWorldMap();
                return false;
            }

        }

        [HarmonyPatch(typeof(PauseMenu), "RestartLevel")]
        static class PauseMenu_RestartLevel_Patch
        {
            public static bool Prefix(PauseMenu __instance)
            {
                if (!Main.enabled || !Main.settings.disableConfirm)
                {
                    return true;
                }

                Map.ClearSuperCheckpointStatus();

                (Traverse.Create(typeof(TriggerManager)).Field("alreadyTriggeredTriggerOnceTriggers").GetValue() as List<string>).Clear();

                if (GameModeController.publishRun)
                {
                    GameModeController.publishRun = false;
                    LevelEditorGUI.levelEditorActive = true;
                }
                PauseController.SetPause(PauseStatus.UnPaused);
                GameModeController.RestartLevel();

                return false;
            }
        }

        [HarmonyPatch(typeof(MapController), "SpawnMook_Networked")]
        static class MapController_SpawnMook_Networked
        {
            public static bool Prefix()
            {
                if (!Main.enabled || !Main.settings.disableEnemySpawn)
                {
                    return true;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(MapController), "SpawnMook_Local")]
        static class MapController_SpawnMook_Local
        {
            public static bool Prefix()
            {
                if (!Main.enabled || !Main.settings.disableEnemySpawn)
                {
                    return true;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(Map), "PlaceDoodad")]
        static class Map_PlaceDoodad
        {
            public static bool Prefix(Map __instance, ref DoodadInfo doodad, ref GameObject __result)
            {
                if (!Main.enabled || !Main.settings.disableEnemySpawn)
                {
                    return true;
                }

                if (doodad.type == DoodadType.Mook || doodad.type == DoodadType.Alien || doodad.type == DoodadType.HellEnemy || doodad.type == DoodadType.AlienBoss || doodad.type == DoodadType.HellBoss)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "WorkOutSpawnScenario")]
        static class Player_WorkOutSpawnScenario_Patch
        {
            // Make the mod work with BroMaker
            public static void Prefix(Player __instance)
            {
                if (!Main.enabled || (!Main.settings.changeSpawn && !Main.settings.changeSpawnFinal))
                {
                    return;
                }

                __instance.firstDeployment = false;
            }
            public static void Postfix(ref Player.SpawnType __result)
            {
                if (!Main.enabled || (!Main.settings.changeSpawn && !Main.settings.changeSpawnFinal))
                {
                    return;
                }

                if (__result != Player.SpawnType.RespawnAtRescueBro)
                {
                    __result = Player.SpawnType.Unknown;
                }
            }
        }

        [HarmonyPatch(typeof(Player), "SetSpawnPositon")]
        static class Player_SetSpawnPositon_Patch
        {
            public static void Prefix(Player __instance, ref TestVanDammeAnim bro, ref Player.SpawnType spawnType, ref bool spawnViaAirDrop, ref Vector3 pos)
            {
                if (!Main.enabled || (!Main.settings.changeSpawn && !Main.settings.changeSpawnFinal))
                {
                    return;
                }

                if (spawnType != Player.SpawnType.RespawnAtRescueBro)
                {
                    spawnType = Player.SpawnType.CustomSpawnPoint;
                    spawnViaAirDrop = false;
                    if (Main.settings.changeSpawn)
                    {
                        pos.x = Main.settings.SpawnPositionX;
                        pos.y = Main.settings.SpawnPositionY;
                    }
                    else
                    {
                        pos = Main.GetFinalCheckpointPos();
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MainMenu), "Start")]
        static class MainMenu_Start_Patch
        {
            public static void Prefix(MainMenu __instance)
            {
                if (!Main.enabled || !Main.settings.quickMainMenu)
                {
                    return;
                }

                Main.skipNextMenu = false;

                Traverse.Create(__instance).Method("InitializeMenu").GetValue();

                Main.skipNextMenu = true;
            }
        }

        [HarmonyPatch(typeof(MainMenu), "InitializeMenu")]
        static class MainMenu_InitializeMenu_Patch
        {
            public static bool Prefix(MainMenu __instance)
            {
                if (!Main.enabled || !Main.settings.quickMainMenu)
                {
                    return true;
                }

                if (Main.skipNextMenu)
                {
                    Main.skipNextMenu = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(TestVanDammeAnim), "SetSpecialAmmoRPC")]
        static class TestVanDammeAnim_SetSpecialAmmoRPC_Patch
        {
            public static void Prefix(TestVanDammeAnim __instance, ref int ammo)
            {
                if (!Main.enabled || !Main.settings.infiniteSpecials)
                {
                    return;
                }

                if (ammo == 0)
                {
                    if (__instance.originalSpecialAmmo > 0)
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

        [HarmonyPatch(typeof(TestVanDammeAnim), "SetInvulnerable")]
        static class TestVanDammeAnim_SetInvulnerable_Patch
        {
            public static void Prefix(ref float time)
            {
                if (!Main.enabled || !Main.settings.invulnerable)
                {
                    return;
                }

                time = float.MaxValue;
            }
        }

        [HarmonyPatch(typeof(NetworkedUnit), "Awake")]
        static class NetworkedUnit_Awake_Patch
        {
            public static void Postfix(NetworkedUnit __instance)
            {
                if (!Main.enabled || !Main.settings.oneHitEnemies)
                {
                    return;
                }

                __instance.health = 1;
                __instance.maxHealth = 1;
            }
        }

        [HarmonyPatch(typeof(Player), "SpawnHero")]
        static class Player_InstantiateHero_Patch
        {
            public static void Postfix()
            {
                if (!Main.enabled || !Main.settings.slowTime)
                {
                    return;
                }

                Main.StartTimeSlow();
            }
        }

        [HarmonyPatch(typeof(Player), "SetLivesRPC")]
        static class Player_SetLivesRPC_Patch
        {
            public static void Prefix(ref int _lives)
            {
                if (!Main.enabled || !Main.settings.infiniteLives)
                {
                    return;
                }

                _lives = int.MaxValue;
            }
        }

        [HarmonyPatch(typeof(TestVanDammeAnim), "ApplyFallingGravity")]
        static class TestVanDammeAnim_ApplyFallingGravity_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (!Main.enabled || !(Main.settings.disableGravity || Main.settings.enableFlight))
                {
                    return true;
                }

                if (__instance.yI <= 0 && !__instance.down)
                {
                    __instance.yI = 0;
                    return false;
                }

                return true;
            }
        }
        [HarmonyPatch(typeof(Sound), "PlayAudioClip")]
        static class Sound_PlayAudioClip_Patch
        {
            public static void Prefix(ref AudioClip clip)
            {
                if (!Main.enabled || !Main.settings.printAudioPlayed)
                {
                    return;
                }

                Main.Log("Audio clip played: " + clip.name);
            }
        }

        [HarmonyPatch(typeof(GameModeController), "ResetForNextLevel")]
        static class GameModeController_ResetForNextLevel_Patch
        {
            public static void Prefix()
            {
                if (!Main.enabled || !Main.settings.setZoom)
                {
                    return;
                }

                Main.levelStartedCounter = 0f;
                SortOfFollow.zoomLevel = 1;
            }
        }

        [HarmonyPatch(typeof(HeroController), "DoCountDown")]
        static class HeroController_DoCountDown_Patch
        {
            public static void Prefix()
            {
                if (!Main.enabled || !Main.settings.suppressAnnouncer)
                {
                    return;
                }

                Map.MapData.suppressAnnouncer = true;
            }
        }

        [HarmonyPatch(typeof(Map), "PlaceDoodad")]
        static class Map_PlaceDoodad_Patch
        {
            public static bool Prefix(Map __instance, ref DoodadInfo doodad, GameObject __result)
            {
                if (!Main.enabled || !Main.settings.maxCageSpawns)
                {
                    return true;
                }

                if (doodad.type == DoodadType.Cage)
                {
                    GridPoint gridPoint = new GridPoint(doodad.position.collumn, doodad.position.row);
                    gridPoint.collumn -= Map.lastXLoadOffset;
                    gridPoint.row -= Map.lastYLoadOffset;

                    Vector3 vector = new Vector3((float)(gridPoint.c * 16), (float)(gridPoint.r * 16), 5f);

                    if (GameModeController.IsHardcoreMode)
                    {
                        Map.havePlacedCageForHardcore = true;
                        Map.cagesSinceLastHardcoreCage = 0;
                    }

                    __result = (UnityEngine.Object.Instantiate<Block>(__instance.activeTheme.blockPrefabCage, vector, Quaternion.identity) as Cage).gameObject;
                    __result.GetComponent<Cage>().row = gridPoint.row;
                    __result.GetComponent<Cage>().collumn = gridPoint.collumn;

                    doodad.entity = __result;
                    __result.transform.parent = __instance.transform;
                    Block component = __result.GetComponent<Block>();
                    if (component != null)
                    {
                        component.OnSpawned();
                    }
                    Registry.RegisterDeterminsiticGameObject(__result.gameObject);
                    if (component != null)
                    {
                        component.FirstFrame();
                    }

                    return false;
                }

                return true;
            }
        }


    }
}