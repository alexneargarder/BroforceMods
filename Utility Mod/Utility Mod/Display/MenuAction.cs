using System;
using RocketLib.Utils;
using UnityEngine;

namespace Utility_Mod
{
    [Serializable]
    public enum MenuActionType
    {
        // Basic Actions
        Teleport,
        
        // Spawn Actions
        SpawnEnemy,
        SpawnBlock,
        SpawnDoodad,
        
        // Level Navigation Actions
        PreviousLevel,
        NextLevel,
        RestartLevel,
        GoToLevel,
        
        // Waypoint Actions
        SetCustomSpawn,
        GoToCustomSpawn,
        SetWaypoint1,
        SetWaypoint2,
        SetWaypoint3,
        GoToWaypoint1,
        GoToWaypoint2,
        GoToWaypoint3,
        GoToFinalCheckpoint,
        
        // Starting Level Actions
        SetCurrentLevel,
        SetSpecificLevel,
        ClearStartingLevel,
        GoToStartingLevel,
        
        // Context Actions
        GrabEnemy,
        GrabPlayer,
        KillEnemy,
        ToggleFriendly,
        CloneUnit,
        CloneBlock,
        DestroyBlock,
        
        // Player Actions
        GiveExtraLife,
        RefillSpecial,
        GiveFlexAirJump,
        GiveFlexInvulnerability,
        GiveFlexGoldenLight,
        GiveFlexTeleport,
        ClearFlexPower,
        GiveSpecialAmmoStandard,
        GiveSpecialAmmoAirstrike,
        GiveSpecialAmmoTimeslow,
        GiveSpecialAmmoRemoteControlCar,
        GiveSpecialAmmoMechDrop,
        GiveSpecialAmmoAlienPheromones,
        GiveSpecialAmmoSteroids,
        GiveSpecialAmmoPiggy,
        ClearSpecialAmmo,
        
        // Special Actions
        ZiplinePlacement,
        MassDelete,
        
        // Debug Actions
        CopyMousePosition
    }
    

    [Serializable]
    public class MenuAction
    {
        #region Properties
        
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public MenuActionType Type { get; set; }
        
        // For spawn actions
        public UnitType? UnitType { get; set; }
        public BlockType? BlockType { get; set; }
        public SpawnableDoodadType? DoodadType { get; set; }
        
        // For go to level actions
        public int? CampaignIndex { get; set; }
        public int? LevelIndex { get; set; }
        
        // For context actions - store the target object
        [System.Xml.Serialization.XmlIgnore]
        public Unit TargetUnit;
        [System.Xml.Serialization.XmlIgnore]
        public Block TargetBlock;
        
        #endregion
        
        #region Factory Methods
        
        // Constructor for teleport action
        public static MenuAction CreateTeleport()
        {
            return new MenuAction
            {
                Id = "action_teleport",
                DisplayName = "Teleport Here",
                Type = MenuActionType.Teleport
            };
        }
        
        // Constructor for spawn enemy action
        public static MenuAction CreateSpawnEnemy(UnitType unitType)
        {
            return new MenuAction
            {
                Id = $"action_spawn_enemy_{unitType}",
                DisplayName = $"Spawn {unitType.ToDisplayString()}",
                Type = MenuActionType.SpawnEnemy,
                UnitType = unitType
            };
        }
        
        // Constructor for spawn block action
        public static MenuAction CreateSpawnBlock(BlockType blockType)
        {
            return new MenuAction
            {
                Id = $"action_spawn_block_{blockType}",
                DisplayName = $"Spawn {blockType.GetDisplayName()}",
                Type = MenuActionType.SpawnBlock,
                BlockType = blockType
            };
        }
        
        // Constructor for spawn doodad action
        public static MenuAction CreateSpawnDoodad(SpawnableDoodadType doodadType)
        {
            return new MenuAction
            {
                Id = $"action_spawn_doodad_{doodadType}",
                DisplayName = $"Spawn {doodadType.GetDisplayName()}",
                Type = MenuActionType.SpawnDoodad,
                DoodadType = doodadType
            };
        }
        
        // Constructor for previous level action
        public static MenuAction CreatePreviousLevel()
        {
            return new MenuAction
            {
                Id = "action_level_previous",
                DisplayName = "Previous Level",
                Type = MenuActionType.PreviousLevel
            };
        }
        
        // Constructor for next level action
        public static MenuAction CreateNextLevel()
        {
            return new MenuAction
            {
                Id = "action_level_next",
                DisplayName = "Next Level",
                Type = MenuActionType.NextLevel
            };
        }
        
        // Constructor for restart level action
        public static MenuAction CreateRestartLevel()
        {
            return new MenuAction
            {
                Id = "action_level_restart",
                DisplayName = "Restart Level",
                Type = MenuActionType.RestartLevel
            };
        }
        
        // Constructor for set custom spawn action
        public static MenuAction CreateSetCustomSpawn()
        {
            return new MenuAction
            {
                Id = "action_waypoint_setcustomspawn",
                DisplayName = "Set Custom Spawn Point",
                Type = MenuActionType.SetCustomSpawn
            };
        }
        
        // Constructor for go to custom spawn action
        public static MenuAction CreateGoToCustomSpawn()
        {
            return new MenuAction
            {
                Id = "action_waypoint_gotocustomspawn",
                DisplayName = "Go to Custom Spawn",
                Type = MenuActionType.GoToCustomSpawn
            };
        }
        
        // Constructor for set waypoint actions
        public static MenuAction CreateSetWaypoint(int waypointNumber)
        {
            return new MenuAction
            {
                Id = $"action_waypoint_set{waypointNumber}",
                DisplayName = $"Set Waypoint {waypointNumber}",
                Type = waypointNumber == 1 ? MenuActionType.SetWaypoint1 : 
                       waypointNumber == 2 ? MenuActionType.SetWaypoint2 : 
                       MenuActionType.SetWaypoint3
            };
        }
        
        // Constructor for go to waypoint actions
        public static MenuAction CreateGoToWaypoint(int waypointNumber)
        {
            return new MenuAction
            {
                Id = $"action_waypoint_goto{waypointNumber}",
                DisplayName = $"Go to Waypoint {waypointNumber}",
                Type = waypointNumber == 1 ? MenuActionType.GoToWaypoint1 : 
                       waypointNumber == 2 ? MenuActionType.GoToWaypoint2 : 
                       MenuActionType.GoToWaypoint3
            };
        }
        
        // Constructor for go to final checkpoint action
        public static MenuAction CreateGoToFinalCheckpoint()
        {
            return new MenuAction
            {
                Id = "action_goto_final_checkpoint",
                DisplayName = "Go to Final Checkpoint",
                Type = MenuActionType.GoToFinalCheckpoint
            };
        }
        
        // Constructor for go to level actions
        public static MenuAction CreateGoToLevel(int campaignIndex, int levelIndex)
        {
            string campaignName = campaignIndex < Main.campaignDisplayNames.Length 
                ? Main.campaignDisplayNames[campaignIndex] 
                : $"Campaign {campaignIndex + 1}";
                
            return new MenuAction
            {
                Id = $"action_gotolevel_{campaignIndex}_{levelIndex}",
                DisplayName = $"{campaignName} - Level {levelIndex + 1}",
                Type = MenuActionType.GoToLevel,
                CampaignIndex = campaignIndex,
                LevelIndex = levelIndex
            };
        }
        
        // Constructor for set current level as starting level action
        public static MenuAction CreateSetCurrentLevelAsStarting()
        {
            return new MenuAction
            {
                Id = "action_startinglevel_setcurrent",
                DisplayName = "Set Current Level as Starting Level",
                Type = MenuActionType.SetCurrentLevel
            };
        }
        
        // Constructor for set specific level as starting level action
        public static MenuAction CreateSetSpecificLevelAsStarting(int campaignIndex, int levelIndex)
        {
            string campaignName = campaignIndex < Main.campaignDisplayNames.Length 
                ? Main.campaignDisplayNames[campaignIndex] 
                : $"Campaign {campaignIndex + 1}";
                
            return new MenuAction
            {
                Id = $"action_startinglevel_setspecific_{campaignIndex}_{levelIndex}",
                DisplayName = $"Set Starting: {campaignName} - Level {levelIndex + 1}",
                Type = MenuActionType.SetSpecificLevel,
                CampaignIndex = campaignIndex,
                LevelIndex = levelIndex
            };
        }
        
        // Constructor for clear starting level action
        public static MenuAction CreateClearStartingLevel()
        {
            return new MenuAction
            {
                Id = "action_startinglevel_clear",
                DisplayName = "Clear Starting Level",
                Type = MenuActionType.ClearStartingLevel
            };
        }
        
        // Constructor for go to starting level action
        public static MenuAction CreateGoToStartingLevel()
        {
            return new MenuAction
            {
                Id = "action_startinglevel_goto",
                DisplayName = "Go to Starting Level",
                Type = MenuActionType.GoToStartingLevel
            };
        }
        
        // Constructor for zipline placement action
        public static MenuAction CreateZiplinePlacement()
        {
            return new MenuAction
            {
                Id = "action_zipline_placement",
                DisplayName = "Place Zipline",
                Type = MenuActionType.ZiplinePlacement
            };
        }
        
        // Constructor for mass delete action
        public static MenuAction CreateMassDelete()
        {
            return new MenuAction
            {
                Id = "action_mass_delete",
                DisplayName = "Mass Delete Tool",
                Type = MenuActionType.MassDelete
            };
        }
        
        public static MenuAction CreateCopyMousePosition()
        {
            return new MenuAction
            {
                Id = "action_copy_mouse_position",
                DisplayName = "Copy Mouse Position",
                Type = MenuActionType.CopyMousePosition
            };
        }
        
        // Constructor for grab enemy action
        public static MenuAction CreateGrabEnemy(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_grab_enemy",
                DisplayName = "Grab Enemy",
                Type = MenuActionType.GrabEnemy,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for grab player action
        public static MenuAction CreateGrabPlayer(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_grab_player",
                DisplayName = "Grab Player",
                Type = MenuActionType.GrabPlayer,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for kill enemy action
        public static MenuAction CreateKillEnemy(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_kill_enemy",
                DisplayName = "Kill Enemy",
                Type = MenuActionType.KillEnemy,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for toggle friendly action
        public static MenuAction CreateToggleFriendly(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_toggle_friendly",
                DisplayName = "Toggle Friendly/Hostile",
                Type = MenuActionType.ToggleFriendly,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for clone unit action
        public static MenuAction CreateCloneUnit(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_clone_unit",
                DisplayName = "Clone Enemy",
                Type = MenuActionType.CloneUnit,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for clone block action
        public static MenuAction CreateCloneBlock(Block targetBlock = null)
        {
            return new MenuAction
            {
                Id = "action_clone_block",
                DisplayName = "Clone Block",
                Type = MenuActionType.CloneBlock,
                TargetBlock = targetBlock
            };
        }
        
        // Constructor for destroy block action
        public static MenuAction CreateDestroyBlock(Block targetBlock = null)
        {
            return new MenuAction
            {
                Id = "action_destroy_block",
                DisplayName = "Destroy Block",
                Type = MenuActionType.DestroyBlock,
                TargetBlock = targetBlock
            };
        }
        
        // Constructor for give extra life action
        public static MenuAction CreateGiveExtraLife(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_extra_life",
                DisplayName = "Give Extra Life",
                Type = MenuActionType.GiveExtraLife,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for refill special action
        public static MenuAction CreateRefillSpecial(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_refill_special",
                DisplayName = "Refill Special",
                Type = MenuActionType.RefillSpecial,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give flex air jump action
        public static MenuAction CreateGiveFlexAirJump(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_flex_air_jump",
                DisplayName = "Give Air Jump",
                Type = MenuActionType.GiveFlexAirJump,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give flex invulnerability action
        public static MenuAction CreateGiveFlexInvulnerability(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_flex_invulnerability",
                DisplayName = "Give Invulnerability",
                Type = MenuActionType.GiveFlexInvulnerability,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give flex golden light action
        public static MenuAction CreateGiveFlexGoldenLight(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_flex_golden_light",
                DisplayName = "Give Golden Light",
                Type = MenuActionType.GiveFlexGoldenLight,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give flex teleport action
        public static MenuAction CreateGiveFlexTeleport(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_flex_teleport",
                DisplayName = "Give Teleport",
                Type = MenuActionType.GiveFlexTeleport,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for clear flex power action
        public static MenuAction CreateClearFlexPower(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_clear_flex_power",
                DisplayName = "Clear Flex Power",
                Type = MenuActionType.ClearFlexPower,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give special ammo standard action
        public static MenuAction CreateGiveSpecialAmmoStandard(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_special_ammo_standard",
                DisplayName = "Give Standard Ammo",
                Type = MenuActionType.GiveSpecialAmmoStandard,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give special ammo airstrike action
        public static MenuAction CreateGiveSpecialAmmoAirstrike(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_special_ammo_airstrike",
                DisplayName = "Give Airstrike",
                Type = MenuActionType.GiveSpecialAmmoAirstrike,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give special ammo timeslow action
        public static MenuAction CreateGiveSpecialAmmoTimeslow(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_special_ammo_timeslow",
                DisplayName = "Give Time Slow",
                Type = MenuActionType.GiveSpecialAmmoTimeslow,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give special ammo remote control car action
        public static MenuAction CreateGiveSpecialAmmoRemoteControlCar(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_special_ammo_rc_car",
                DisplayName = "Give RC Car",
                Type = MenuActionType.GiveSpecialAmmoRemoteControlCar,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give special ammo mech drop action
        public static MenuAction CreateGiveSpecialAmmoMechDrop(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_special_ammo_mech_drop",
                DisplayName = "Give Mech Drop",
                Type = MenuActionType.GiveSpecialAmmoMechDrop,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give special ammo alien pheromones action
        public static MenuAction CreateGiveSpecialAmmoAlienPheromones(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_special_ammo_alien_pheromones",
                DisplayName = "Give Alien Pheromones",
                Type = MenuActionType.GiveSpecialAmmoAlienPheromones,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give special ammo steroids action
        public static MenuAction CreateGiveSpecialAmmoSteroids(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_special_ammo_steroids",
                DisplayName = "Give Steroids",
                Type = MenuActionType.GiveSpecialAmmoSteroids,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for give special ammo piggy action
        public static MenuAction CreateGiveSpecialAmmoPiggy(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_give_special_ammo_piggy",
                DisplayName = "Give Piggy",
                Type = MenuActionType.GiveSpecialAmmoPiggy,
                TargetUnit = targetUnit
            };
        }
        
        // Constructor for clear special ammo action
        public static MenuAction CreateClearSpecialAmmo(Unit targetUnit = null)
        {
            return new MenuAction
            {
                Id = "action_clear_special_ammo",
                DisplayName = "Clear Special Ammo",
                Type = MenuActionType.ClearSpecialAmmo,
                TargetUnit = targetUnit
            };
        }
        
        #endregion
        
        #region Execution Methods
        
        // Execute the action at the given position
        public void Execute(Vector3 position, bool isQuickAction = false)
        {
            switch (Type)
            {
                // Basic Actions
                case MenuActionType.Teleport:
                    ExecuteTeleport(position);
                    break;
                    
                // Spawn Actions
                case MenuActionType.SpawnEnemy:
                    if (UnitType.HasValue)
                        Main.SpawnUnit(UnitType.Value, position);
                    break;
                    
                case MenuActionType.SpawnBlock:
                    if (BlockType.HasValue)
                        Main.SpawnBlock(position, BlockType.Value);
                    break;
                    
                case MenuActionType.SpawnDoodad:
                    if (DoodadType.HasValue)
                        Main.SpawnDoodad(position, DoodadType.Value);
                    break;
                    
                // Level Navigation Actions
                case MenuActionType.PreviousLevel:
                    Main.GoToPreviousLevel();
                    break;
                    
                case MenuActionType.NextLevel:
                    Main.GoToNextLevel();
                    break;
                    
                case MenuActionType.RestartLevel:
                    Map.ClearSuperCheckpointStatus();
                    GameModeController.RestartLevel();
                    break;
                    
                case MenuActionType.GoToLevel:
                    ExecuteGoToLevel();
                    break;
                    
                // Waypoint Actions
                case MenuActionType.SetCustomSpawn:
                    Main.SetCustomSpawnForCurrentLevel(new Vector2(position.x, position.y));
                    break;
                    
                case MenuActionType.GoToCustomSpawn:
                    Main.GoToCustomSpawn();
                    break;
                    
                case MenuActionType.SetWaypoint1:
                    Main.SetWaypoint(0, new Vector2(position.x, position.y));
                    break;
                    
                case MenuActionType.SetWaypoint2:
                    Main.SetWaypoint(1, new Vector2(position.x, position.y));
                    break;
                    
                case MenuActionType.SetWaypoint3:
                    Main.SetWaypoint(2, new Vector2(position.x, position.y));
                    break;
                    
                case MenuActionType.GoToWaypoint1:
                    Main.GoToWaypoint(0);
                    break;
                    
                case MenuActionType.GoToWaypoint2:
                    Main.GoToWaypoint(1);
                    break;
                    
                case MenuActionType.GoToWaypoint3:
                    Main.GoToWaypoint(2);
                    break;
                    
                case MenuActionType.GoToFinalCheckpoint:
                    ExecuteGoToFinalCheckpoint();
                    break;
                    
                // Starting Level Actions
                case MenuActionType.SetCurrentLevel:
                    ExecuteSetCurrentLevelAsStarting();
                    break;
                    
                case MenuActionType.SetSpecificLevel:
                    ExecuteSetSpecificLevelAsStarting();
                    break;
                    
                case MenuActionType.ClearStartingLevel:
                    Main.settings.campaignNum = 0;
                    Main.settings.levelNum = 0;
                    break;
                    
                case MenuActionType.GoToStartingLevel:
                    Main.GoToLevel(Main.settings.campaignNum, Main.settings.levelNum);
                    break;
                    
                // Context Actions
                case MenuActionType.GrabEnemy:
                    ExecuteGrabEnemy(position);
                    break;
                    
                case MenuActionType.GrabPlayer:
                    ExecuteGrabPlayer(position, isQuickAction);
                    break;
                    
                case MenuActionType.KillEnemy:
                    ExecuteKillEnemy(position);
                    break;
                    
                case MenuActionType.ToggleFriendly:
                    ExecuteToggleFriendly(position);
                    break;
                    
                case MenuActionType.CloneUnit:
                    ExecuteCloneUnit(position, isQuickAction);
                    break;
                    
                case MenuActionType.CloneBlock:
                    ExecuteCloneBlock(position, isQuickAction);
                    break;
                    
                case MenuActionType.DestroyBlock:
                    ExecuteDestroyBlock(position);
                    break;
                    
                // Player Actions
                case MenuActionType.GiveExtraLife:
                    ExecuteGiveExtraLife(position, isQuickAction);
                    break;
                    
                case MenuActionType.RefillSpecial:
                    ExecuteRefillSpecial(position, isQuickAction);
                    break;
                    
                case MenuActionType.GiveFlexAirJump:
                    ExecuteGiveFlexPower(position, isQuickAction, PickupType.FlexAirJump);
                    break;
                    
                case MenuActionType.GiveFlexInvulnerability:
                    ExecuteGiveFlexPower(position, isQuickAction, PickupType.FlexInvulnerability);
                    break;
                    
                case MenuActionType.GiveFlexGoldenLight:
                    ExecuteGiveFlexPower(position, isQuickAction, PickupType.FlexGoldenLight);
                    break;
                    
                case MenuActionType.GiveFlexTeleport:
                    ExecuteGiveFlexPower(position, isQuickAction, PickupType.FlexTeleport);
                    break;
                    
                case MenuActionType.ClearFlexPower:
                    ExecuteClearFlexPower(position, isQuickAction);
                    break;
                    
                case MenuActionType.GiveSpecialAmmoStandard:
                    ExecuteGiveSpecialAmmo(position, isQuickAction, PockettedSpecialAmmoType.Standard);
                    break;
                    
                case MenuActionType.GiveSpecialAmmoAirstrike:
                    ExecuteGiveSpecialAmmo(position, isQuickAction, PockettedSpecialAmmoType.Airstrike);
                    break;
                    
                case MenuActionType.GiveSpecialAmmoTimeslow:
                    ExecuteGiveSpecialAmmo(position, isQuickAction, PockettedSpecialAmmoType.Timeslow);
                    break;
                    
                case MenuActionType.GiveSpecialAmmoRemoteControlCar:
                    ExecuteGiveSpecialAmmo(position, isQuickAction, PockettedSpecialAmmoType.RemoteControlCar);
                    break;
                    
                case MenuActionType.GiveSpecialAmmoMechDrop:
                    ExecuteGiveSpecialAmmo(position, isQuickAction, PockettedSpecialAmmoType.MechDrop);
                    break;
                    
                case MenuActionType.GiveSpecialAmmoAlienPheromones:
                    ExecuteGiveSpecialAmmo(position, isQuickAction, PockettedSpecialAmmoType.AlienPheromones);
                    break;
                    
                case MenuActionType.GiveSpecialAmmoSteroids:
                    ExecuteGiveSpecialAmmo(position, isQuickAction, PockettedSpecialAmmoType.Steroids);
                    break;
                    
                case MenuActionType.GiveSpecialAmmoPiggy:
                    ExecuteGiveSpecialAmmo(position, isQuickAction, PockettedSpecialAmmoType.Piggy);
                    break;
                    
                case MenuActionType.ClearSpecialAmmo:
                    ExecuteClearSpecialAmmo(position, isQuickAction);
                    break;
                    
                // Special Actions
                case MenuActionType.ZiplinePlacement:
                    // This is handled specially in ContextMenuManager
                    // The position passed here is used for the placement
                    break;
                    
                case MenuActionType.MassDelete:
                    // This is handled specially in ContextMenuManager
                    // Enters mass delete mode
                    break;
                    
                case MenuActionType.CopyMousePosition:
                    // Copy the position to clipboard (format: "number, number")
                    string positionText = $"{position.x:F2}, {position.y:F2}";
                    GUIUtility.systemCopyBuffer = positionText;
                    Main.mod.Logger.Log($"Copied position to clipboard: {positionText}");
                    break;
            }
        }
        
        private void ExecuteTeleport(Vector3 position)
        {
            // Use the provided position (which will be current mouse pos for quick actions, menu pos for menu items)
            if (HeroController.players != null && HeroController.players.Length > 0 && 
                HeroController.players[0] != null && HeroController.players[0].character != null)
            {
                Main.TeleportToCoords(position.x, position.y);
            }
        }
        
        private Unit FindTargetPlayer(Vector3 position, bool isQuickAction)
        {
            Unit targetUnit = null;
            
            // Use stored target if available (clicked directly from context menu)
            if (!isQuickAction && TargetUnit != null && !TargetUnit.destroyed && TargetUnit.health > 0)
            {
                targetUnit = TargetUnit;
                TargetUnit = null; // Clear after use
            }
            
            // If no stored target or it's a quick action, find nearest player character
            if (targetUnit == null)
            {
                float closestDistance = float.MaxValue;
                
                // Use raycast from camera through mouse position for more accurate detection
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
                
                // Check raycast hits first (more precise)
                foreach (RaycastHit hit in hits)
                {
                    Unit unit = hit.collider.GetComponent<Unit>();
                    if (unit == null)
                        unit = hit.collider.GetComponentInParent<Unit>();
                        
                    // Check if this unit is a player character
                    if (unit != null && unit.health > 0 && !unit.destroyed)
                    {
                        for (int i = 0; i < HeroController.players.Length; i++)
                        {
                            if (HeroController.players[i] != null && HeroController.players[i].character == unit)
                            {
                                targetUnit = unit;
                                break;
                            }
                        }
                        if (targetUnit != null) break;
                    }
                }
                
                // If no direct hit, find closest player character to position
                if (targetUnit == null)
                {
                    foreach (Player player in HeroController.players)
                    {
                        if (player != null && player.character != null && player.character.health > 0 && !player.character.destroyed)
                        {
                            float distance = Vector3.Distance(player.character.transform.position, position);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                targetUnit = player.character;
                            }
                        }
                    }
                }
            }
            
            return targetUnit;
        }
        
        private void ExecuteGoToFinalCheckpoint()
        {
            if (HeroController.players != null && HeroController.players.Length > 0 && 
                HeroController.players[0] != null && HeroController.players[0].character != null)
            {
                Vector3 finalCheckpointPos = Main.GetFinalCheckpointPos();
                Main.TeleportToCoords(finalCheckpointPos.x, finalCheckpointPos.y);
            }
        }
        
        private void ExecuteSetCurrentLevelAsStarting()
        {
            // Get current campaign and level
            string campaignName = GameState.Instance?.campaignName;
            int levelNum = LevelSelectionController.CurrentLevelNum;
            
            if (campaignName != null)
            {
                // Find the campaign index
                int campaignIndex = -1;
                for (int i = 0; i < Main.actualCampaignNames.Length; i++)
                {
                    if (Main.actualCampaignNames[i] == campaignName)
                    {
                        campaignIndex = i;
                        break;
                    }
                }
                
                if (campaignIndex >= 0)
                {
                    Main.settings.campaignNum = campaignIndex;
                    Main.settings.levelNum = levelNum;
                }
            }
        }
        
        private void ExecuteSetSpecificLevelAsStarting()
        {
            if (CampaignIndex.HasValue && LevelIndex.HasValue)
            {
                Main.settings.campaignNum = CampaignIndex.Value;
                Main.settings.levelNum = LevelIndex.Value;
            }
        }
        
        private void ExecuteGoToLevel()
        {
            if (CampaignIndex.HasValue && LevelIndex.HasValue)
            {
                Main.GoToLevel(CampaignIndex.Value, LevelIndex.Value);
            }
        }
        
        
        private void ExecuteGrabEnemy(Vector3 position)
        {
            var manager = ContextMenuManager.Instance;
            if (manager == null)
                return;
                
            // If already grabbing, release the unit
            if (manager.CurrentMode == ContextMenuManager.ContextMenuMode.Grab && manager.grabbedUnit != null)
            {
                manager.modes.ReleaseGrabbedUnit();
                return;
            }
            
            Unit unit = null;
            
            // Use stored target if available
            if (TargetUnit != null && !TargetUnit.destroyed && TargetUnit.health > 0)
            {
                unit = TargetUnit;
                TargetUnit = null; // Clear after use
            }
            else
            {
                // Find unit at position
                Collider[] colliders = Physics.OverlapSphere(position, 8f);
                foreach (Collider col in colliders)
                {
                    Unit foundUnit = col.GetComponent<Unit>();
                    if (foundUnit == null)
                        foundUnit = col.GetComponentInParent<Unit>();
                        
                    if (foundUnit != null && foundUnit.health > 0 && !foundUnit.destroyed)
                    {
                        // Check if this unit is a player character
                        bool isPlayerCharacter = false;
                        for (int i = 0; i < HeroController.players.Length; i++)
                        {
                            if (HeroController.players[i] != null && HeroController.players[i].character == foundUnit)
                            {
                                isPlayerCharacter = true;
                                break;
                            }
                        }
                        
                        if (!isPlayerCharacter)
                        {
                            unit = foundUnit;
                            break;
                        }
                    }
                }
            }
            
            if (unit != null)
            {
                // Enter grab mode
                manager.CurrentMode = ContextMenuManager.ContextMenuMode.Grab;
                manager.grabbedUnit = unit;
                Map.units.Remove(manager.grabbedUnit);
                
                // Make unit invulnerable while grabbed
                if (unit is Mook mook)
                {
                    if (unit.playerNum < 0)
                    {
                        // Enemy mook - can use SetInvulnerable normally
                        mook.SetInvulnerable(float.MaxValue);
                    }
                    else
                    {
                        // Friendly mook - use SetFieldValue to avoid bubble restart
                        mook.invulnerable = true;
                        mook.SetFieldValue("_invulnerableTime", float.MaxValue);
                    }
                }
            }
        }
        
        private void ExecuteGrabPlayer(Vector3 position, bool isQuickAction)
        {
            var manager = ContextMenuManager.Instance;
            if (manager == null) return;
            
            // If already grabbing, release the unit
            if (manager.CurrentMode == ContextMenuManager.ContextMenuMode.Grab && manager.grabbedUnit != null)
            {
                manager.modes.ReleaseGrabbedUnit();
                return;
            }
            
            Unit targetUnit = FindTargetPlayer(position, isQuickAction);
            
            if (targetUnit != null)
            {
                // Enter grab mode
                manager.CurrentMode = ContextMenuManager.ContextMenuMode.Grab;
                manager.grabbedUnit = targetUnit;
                Map.units.Remove(manager.grabbedUnit);
                // Make player invulnerable while grabbed
                if (targetUnit is TestVanDammeAnim bro)
                {
                    bro.SetInvulnerable(float.MaxValue);
                }
            }
        }
        
        private void ExecuteKillEnemy(Vector3 position)
        {
            Unit unit = null;
            
            // Use stored target if available
            if (TargetUnit != null && !TargetUnit.destroyed && TargetUnit.health > 0)
            {
                unit = TargetUnit;
                TargetUnit = null; // Clear after use
            }
            else
            {
                // Find enemy at the provided position
                Collider[] colliders = Physics.OverlapSphere(position, 8f);
                foreach (Collider col in colliders)
                {
                    Unit foundUnit = col.GetComponent<Unit>();
                    if (foundUnit == null)
                        foundUnit = col.GetComponentInParent<Unit>();
                        
                    if (foundUnit != null && foundUnit.IsEnemy && foundUnit.playerNum < 0 && foundUnit.health > 0 && !foundUnit.destroyed)
                    {
                        unit = foundUnit;
                        break;
                    }
                }
            }
            
            if (unit != null && unit.IsEnemy && unit.playerNum < 0)
            {
                unit.Damage(1000, DamageType.Normal, 0, 0, 0, null, 0, 0);
            }
        }
        
        private void ExecuteToggleFriendly(Vector3 position)
        {
            Unit unit = null;
            
            // Use stored target if available
            if (TargetUnit != null && !TargetUnit.destroyed && TargetUnit.health > 0)
            {
                unit = TargetUnit;
                TargetUnit = null; // Clear after use
            }
            else
            {
                // Find unit at position
                Collider[] colliders = Physics.OverlapSphere(position, 8f);
                foreach (Collider col in colliders)
                {
                    Unit foundUnit = col.GetComponent<Unit>();
                    if (foundUnit == null)
                        foundUnit = col.GetComponentInParent<Unit>();
                        
                    if (foundUnit != null && foundUnit.health > 0 && !foundUnit.destroyed)
                    {
                        unit = foundUnit;
                        break;
                    }
                }
            }
            
            if (unit != null)
            {
                if (unit.IsEnemy && unit.playerNum < 0)
                {
                    // Make friendly
                    unit.playerNum = 0;
                    if (unit.enemyAI != null)
                    {
                        unit.enemyAI.ForgetPlayer();
                    }
                }
                else if (!unit.IsEnemy || (unit.IsEnemy && unit.playerNum >= 0))
                {
                    // Check if it's the player character
                    bool isPlayer = false;
                    if (HeroController.players != null)
                    {
                        foreach (var player in HeroController.players)
                        {
                            if (player != null && player.character == unit)
                            {
                                isPlayer = true;
                                break;
                            }
                        }
                    }
                    
                    if (!isPlayer)
                    {
                        // Make hostile
                        unit.playerNum = -1;
                    }
                }
            }
        }
        
        private void ExecuteCloneUnit(Vector3 position, bool isQuickAction)
        {
            var manager = ContextMenuManager.Instance;
            if (manager == null)
                return;
            
            // Only use stored target if NOT a quick action (i.e., clicked from context menu)
            if (!isQuickAction)
            {
                // Handle unit cloning - enter clone mode
                if (TargetUnit != null && !TargetUnit.destroyed && TargetUnit.health > 0)
                {
                    Unit unit = TargetUnit;
                    TargetUnit = null; // Clear after use
                    
                    // Enter clone mode
                    manager.CurrentMode = ContextMenuManager.ContextMenuMode.Clone;
                    manager.unitToClone = unit;
                    manager.blockToClone = null;
                    manager.CloseMenu();
                    return;
                }
            }
            
            // Find unit at position if no target stored
            
            // Use raycast from camera through mouse position for more accurate detection
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
            
            // Also do sphere cast for broader detection
            Collider[] colliders = Physics.OverlapSphere(position, 2f); // Reduced radius for more precision
            
            // Check raycast hits first (more precise)
            foreach (RaycastHit hit in hits)
            {
                Unit unit = hit.collider.GetComponent<Unit>();
                if (unit == null)
                    unit = hit.collider.GetComponentInParent<Unit>();
                    
                if (unit != null && unit.health > 0 && !unit.destroyed)
                {
                    // Enter clone mode
                    manager.CurrentMode = ContextMenuManager.ContextMenuMode.Clone;
                    manager.unitToClone = unit;
                    manager.blockToClone = null;
                    manager.CloseMenu();
                    return;
                }
            }
            
            // Fallback to sphere check if raycast didn't find anything
            foreach (Collider col in colliders)
            {
                Unit unit = col.GetComponent<Unit>();
                if (unit == null)
                    unit = col.GetComponentInParent<Unit>();
                    
                if (unit != null && unit.health > 0 && !unit.destroyed)
                {
                    // Enter clone mode
                    manager.CurrentMode = ContextMenuManager.ContextMenuMode.Clone;
                    manager.unitToClone = unit;
                    manager.blockToClone = null;
                    manager.CloseMenu();
                    return;
                }
            }
        }
        
        private void ExecuteCloneBlock(Vector3 position, bool isQuickAction)
        {
            var manager = ContextMenuManager.Instance;
            if (manager == null)
                return;
            
            // Only use stored target if NOT a quick action (i.e., clicked from context menu)
            if (!isQuickAction)
            {
                // Handle block cloning - enter clone mode
                if (TargetBlock != null && !TargetBlock.destroyed)
                {
                    Block block = TargetBlock;
                    TargetBlock = null; // Clear after use
                    
                    // Enter clone mode
                    manager.CurrentMode = ContextMenuManager.ContextMenuMode.Clone;
                    manager.unitToClone = null;
                    manager.blockToClone = block;
                    manager.CloseMenu();
                    return;
                }
            }
            
            // Find block at position if no target stored
            
            // Use raycast from camera through mouse position for more accurate detection
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
            
            // Also do sphere cast for broader detection
            Collider[] colliders = Physics.OverlapSphere(position, 2f); // Reduced radius for more precision
            
            // Check raycast hits first (more precise)
            foreach (RaycastHit hit in hits)
            {
                Block block = hit.collider.GetComponent<Block>();
                if (block != null && !block.destroyed)
                {
                    // Check if we support cloning this block type
                    BlockType? clonableType = manager.GetBlockTypeFromGroundType(block.groundType, block);
                    if (clonableType.HasValue)
                    {
                        // Enter clone mode
                        manager.CurrentMode = ContextMenuManager.ContextMenuMode.Clone;
                        manager.unitToClone = null;
                        manager.blockToClone = block;
                        manager.CloseMenu();
                    }
                    else
                    {
                    }
                    return;
                }
            }
            
            // Fallback to sphere check if raycast didn't find anything
            foreach (Collider col in colliders)
            {
                Block block = col.GetComponent<Block>();
                if (block != null && !block.destroyed)
                {
                    // Check if we support cloning this block type
                    BlockType? clonableType = manager.GetBlockTypeFromGroundType(block.groundType, block);
                    if (clonableType.HasValue)
                    {
                        // Enter clone mode
                        manager.CurrentMode = ContextMenuManager.ContextMenuMode.Clone;
                        manager.unitToClone = null;
                        manager.blockToClone = block;
                        manager.CloseMenu();
                    }
                    else
                    {
                    }
                    return;
                }
            }
        }
        
        private void ExecuteDestroyBlock(Vector3 position)
        {
            Vector3 blockPos = position;
            bool foundBlock = false;
            
            // Use stored target if available (from context menu)
            if (TargetBlock != null && !TargetBlock.destroyed)
            {
                blockPos = TargetBlock.transform.position;
                
                // Get the block's grid position from the stored block
                int col = Mathf.RoundToInt(blockPos.x / 16f);
                int row = Mathf.RoundToInt(blockPos.y / 16f);
                
                // Use Map's proper deletion method (handles all block types including indestructible and ladders)
                Map.ClearForegroundBlock(col, row);
                foundBlock = true;
                
                TargetBlock = null; // Clear after use
            }
            else
            {
                // For quick actions, calculate grid position from mouse position
                // Snap to grid properly (blocks are centered at 8, 24, 40, etc.)
                int col = Mathf.FloorToInt((position.x + 8f) / 16f);
                int row = Mathf.FloorToInt((position.y + 8f) / 16f);
                
                // Check if there's a block at this position
                if (col >= 0 && col < Map.MapData.Width && row >= 0 && row < Map.MapData.Height)
                {
                    Block block = Map.blocks[col, row];
                    if (block != null && !block.destroyed)
                    {
                        blockPos = new Vector3(col * 16f, row * 16f, 0);
                        Map.ClearForegroundBlock(col, row);
                        foundBlock = true;
                    }
                }
            }
            
            // Track the destruction if recording and we actually deleted a block
            if (foundBlock && Main.settings.isRecordingLevelEdits)
            {
                var editAction = LevelEditAction.CreateBlockDestroy(blockPos);
                string levelKey = Main.GetCurrentLevelKey();
                
                if (!string.IsNullOrEmpty(levelKey))
                {
                    // Get or create record for this level
                    if (!Main.settings.levelEditRecords.ContainsKey(levelKey))
                    {
                        Main.settings.levelEditRecords[levelKey] = new LevelEditRecord { LevelKey = levelKey };
                    }
                    
                    Main.settings.levelEditRecords[levelKey].Actions.Add(editAction);
                }
            }
        }
        
        private void ExecuteGiveExtraLife(Vector3 position, bool isQuickAction)
        {
            Unit targetUnit = FindTargetPlayer(position, isQuickAction);
            
            if (targetUnit != null)
            {
                // Find the player number for this unit
                int playerNum = 0;
                for (int i = 0; i < HeroController.players.Length; i++)
                {
                    if (HeroController.players[i] != null && HeroController.players[i].character == targetUnit)
                    {
                        playerNum = i;
                        break;
                    }
                }
                HeroController.AddLife(playerNum);
            }
        }
        
        private void ExecuteRefillSpecial(Vector3 position, bool isQuickAction)
        {
            Unit targetUnit = FindTargetPlayer(position, isQuickAction);
            
            if (targetUnit != null && targetUnit is TestVanDammeAnim)
            {
                var bro = targetUnit as TestVanDammeAnim;
                bro.SpecialAmmo = bro.originalSpecialAmmo;
            }
        }
        
        private void ExecuteGiveFlexPower(Vector3 position, bool isQuickAction, PickupType flexPowerType)
        {
            Unit targetUnit = FindTargetPlayer(position, isQuickAction);
            
            if (targetUnit != null)
            {
                // Find the player object for this unit
                for (int i = 0; i < HeroController.players.Length; i++)
                {
                    if (HeroController.players[i] != null && HeroController.players[i].character == targetUnit)
                    {
                        // Clear any existing flex power first
                        HeroController.players[i].ClearFlexPower();
                        // Then add the new flex power
                        HeroController.players[i].AddFlexPower(flexPowerType, false);
                        break;
                    }
                }
            }
        }
        
        private void ExecuteClearFlexPower(Vector3 position, bool isQuickAction)
        {
            Unit targetUnit = FindTargetPlayer(position, isQuickAction);
            
            if (targetUnit != null)
            {
                // Find the player object for this unit
                for (int i = 0; i < HeroController.players.Length; i++)
                {
                    if (HeroController.players[i] != null && HeroController.players[i].character == targetUnit)
                    {
                        HeroController.players[i].ClearFlexPower();
                        break;
                    }
                }
            }
        }
        
        private void ExecuteGiveSpecialAmmo(Vector3 position, bool isQuickAction, PockettedSpecialAmmoType ammoType)
        {
            Unit targetUnit = FindTargetPlayer(position, isQuickAction);
            
            if (targetUnit != null && targetUnit is TestVanDammeAnim)
            {
                var bro = targetUnit as TestVanDammeAnim;
                // Give the pocketed special ammo
                bro.PickupPockettableAmmo(ammoType);
            }
        }
        
        private void ExecuteClearSpecialAmmo(Vector3 position, bool isQuickAction)
        {
            Unit targetUnit = FindTargetPlayer(position, isQuickAction);
            
            if (targetUnit != null && targetUnit is TestVanDammeAnim)
            {
                var bro = targetUnit as TestVanDammeAnim;
                // Check if this bro is a BroBase (which has pockettedSpecialAmmo)
                if (bro is BroBase broBase)
                {
                    // Clear pocketed special ammo list
                    if (broBase.pockettedSpecialAmmo != null)
                    {
                        broBase.pockettedSpecialAmmo.Clear();
                    }
                }
                // Update HUD
                if (bro.player != null)
                {
                    bro.player.hud.SetGrenadeMaterials(bro.heroType);
                    bro.player.hud.SetGrenades(bro.SpecialAmmo);
                }
            }
        }
        
        #endregion
    }
}