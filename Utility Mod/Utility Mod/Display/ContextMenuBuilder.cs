using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RocketLib.Utils;

namespace Utility_Mod
{
    /// <summary>
    /// Handles building all menu content for the context menu system
    /// </summary>
    public class ContextMenuBuilder
    {
        #region Fields and Properties
        
        private ContextMenuManager manager;
        
        #endregion
        
        #region Constructor
        
        public ContextMenuBuilder(ContextMenuManager manager)
        {
            this.manager = manager;
        }
        
        #endregion
        
        #region Menu Building
        
        public void BuildMenuItems()
        {
            if (manager.CurrentMenu == null)
                return;

            // Check what's under the cursor for context-sensitive actions
            GameObject targetObject = manager.GetObjectUnderCursor();
            bool hasContextActions = false;
            if (targetObject != null)
            {
                hasContextActions = BuildContextActions(targetObject);
            }

            // Add General Actions header if we have context actions
            if (hasContextActions)
            {
                manager.CurrentMenu.AddItem(MenuItem.CreateHeader("General Actions"));
                manager.CurrentMenu.AddSeparator();
            }

            // Add recently used items at the top (if enabled or if quick action is set)
            if (Main.settings.enableRecentItems || Main.settings.selectedQuickAction != null)
            {
                BuildRecentlyUsedSection();
            }

            var teleportAction = MenuAction.CreateTeleport();
            var teleportItem = new MenuItem(teleportAction.DisplayName, () => {
                manager.ExecuteAction(teleportAction);
            })
            {
                ActionId = teleportAction.Id,
                MenuAction = teleportAction
            };
            manager.CurrentMenu.AddItem(teleportItem);

            manager.CurrentMenu.AddSeparator();

            // Add Spawn Enemy submenu
            MenuItem spawnEnemyMenu = new MenuItem("Spawn Enemy");
            BuildEnemySubmenu(spawnEnemyMenu);
            manager.CurrentMenu.AddItem(spawnEnemyMenu);

            // Add Spawn Object submenu
            MenuItem spawnObjectMenu = new MenuItem("Spawn Object");
            BuildObjectSubmenu(spawnObjectMenu);
            manager.CurrentMenu.AddItem(spawnObjectMenu);

            // Add Player Cheats submenu
            MenuItem playerCheatsMenu = new MenuItem("Player Cheats");
            BuildPlayerCheatsSubmenu(playerCheatsMenu);
            manager.CurrentMenu.AddItem(playerCheatsMenu);

            // Add Level Control submenu
            MenuItem levelControlMenu = new MenuItem("Level Control");
            BuildLevelControlSubmenu(levelControlMenu);
            manager.CurrentMenu.AddItem(levelControlMenu);

            // Add Game Modifiers submenu
            MenuItem gameModifiersMenu = new MenuItem("Game Modifiers");
            BuildGameModifiersSubmenu(gameModifiersMenu);
            manager.CurrentMenu.AddItem(gameModifiersMenu);

            // Add Debug Options submenu
            MenuItem debugOptionsMenu = new MenuItem("Debug Options");
            BuildDebugOptionsSubmenu(debugOptionsMenu);
            manager.CurrentMenu.AddItem(debugOptionsMenu);

            // Add Teleport submenu
            MenuItem teleportMenu = new MenuItem("Teleport");
            BuildTeleportSubmenu(teleportMenu);
            manager.CurrentMenu.AddItem(teleportMenu);
            
            // Add Level Edit Recording submenu
            MenuItem levelEditMenu = new MenuItem("Level Edit Recording");
            BuildLevelEditRecordingSubmenu(levelEditMenu);
            manager.CurrentMenu.AddItem(levelEditMenu);

            manager.CurrentMenu.AddSeparator();
            
            // Add Help option
            manager.CurrentMenu.AddItem(new MenuItem("Help", () => {
                manager.ShowHelpDialog();
            }));

            // Add Cancel option
            manager.CurrentMenu.AddItem(new MenuItem("Cancel", () => {
                manager.CloseMenu();
            }));
        }
        
        public void BuildRecentlyUsedSection()
        {
            bool hasItems = false;
            
            // If quick action is set but not in recently used, add it
            if (Main.settings.selectedQuickAction != null)
            {
                bool found = false;
                foreach (var item in Main.settings.recentlyUsedItems)
                {
                    if (item.Id == Main.settings.selectedQuickAction.Id)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Main.settings.recentlyUsedItems.Insert(0, Main.settings.selectedQuickAction);
                }
            }
            
            // Add recently used items with checkboxes
            foreach (var action in Main.settings.recentlyUsedItems.Take(Main.settings.maxRecentItems))
            {
                var item = new MenuItem(action.DisplayName, () => {
                    manager.ExecuteAction(action);
                })
                {
                    ActionId = action.Id,
                    MenuAction = action,
                    ShowCheckbox = true,
                    IsChecked = (Main.settings.selectedQuickAction != null && action.Id == Main.settings.selectedQuickAction.Id)
                };
                manager.CurrentMenu.AddItem(item);
                hasItems = true;
            }
            
            if (hasItems)
            {
                manager.CurrentMenu.AddSeparator();
            }
        }

        public void BuildEnemySubmenu(MenuItem parentMenu)
        {
            // Add normal enemies submenu
            MenuItem normalSubmenu = new MenuItem("Normal");
            foreach (var unitType in UnitTypes.NormalUnits)
            {
                var unit = unitType; // Capture for closure
                var action = MenuAction.CreateSpawnEnemy(unit);
                normalSubmenu.AddSubItem(new MenuItem(action.DisplayName.Replace("Spawn ", ""), () => {
                    manager.ExecuteAction(action);
                })
                {
                    ActionId = action.Id,
                    MenuAction = action
                });
            }
            parentMenu.AddSubItem(normalSubmenu);
            
            // Add alien enemies submenu
            MenuItem alienSubmenu = new MenuItem("Aliens");
            foreach (var unitType in UnitTypes.AlienUnits)
            {
                var unit = unitType; // Capture for closure
                var action = MenuAction.CreateSpawnEnemy(unit);
                alienSubmenu.AddSubItem(new MenuItem(action.DisplayName.Replace("Spawn ", ""), () => {
                    manager.ExecuteAction(action);
                })
                {
                    ActionId = action.Id,
                    MenuAction = action
                });
            }
            parentMenu.AddSubItem(alienSubmenu);
            
            // Add hell enemies submenu
            MenuItem hellSubmenu = new MenuItem("Hell");
            foreach (var unitType in UnitTypes.HellUnits)
            {
                var unit = unitType; // Capture for closure
                var action = MenuAction.CreateSpawnEnemy(unit);
                hellSubmenu.AddSubItem(new MenuItem(action.DisplayName.Replace("Spawn ", ""), () => {
                    manager.ExecuteAction(action);
                })
                {
                    ActionId = action.Id,
                    MenuAction = action
                });
            }
            parentMenu.AddSubItem(hellSubmenu);
            
            // Add worm enemies submenu
            MenuItem wormSubmenu = new MenuItem("Worms");
            foreach (var unitType in UnitTypes.WormUnits)
            {
                var unit = unitType; // Capture for closure
                var action = MenuAction.CreateSpawnEnemy(unit);
                wormSubmenu.AddSubItem(new MenuItem(action.DisplayName.Replace("Spawn ", ""), () => {
                    manager.ExecuteAction(action);
                })
                {
                    ActionId = action.Id,
                    MenuAction = action
                });
            }
            parentMenu.AddSubItem(wormSubmenu);
            
            // Add bosses submenu
            MenuItem bossSubmenu = new MenuItem("Bosses");
            foreach (var unitType in UnitTypes.BossUnits)
            {
                var unit = unitType; // Capture for closure
                var action = MenuAction.CreateSpawnEnemy(unit);
                bossSubmenu.AddSubItem(new MenuItem(action.DisplayName.Replace("Spawn ", ""), () => {
                    manager.ExecuteAction(action);
                })
                {
                    ActionId = action.Id,
                    MenuAction = action
                });
            }
            parentMenu.AddSubItem(bossSubmenu);
            
            // Add vehicles submenu
            MenuItem vehicleSubmenu = new MenuItem("Vehicles");
            foreach (var unitType in UnitTypes.VehicleEnemies)
            {
                var unit = unitType; // Capture for closure
                var action = MenuAction.CreateSpawnEnemy(unit);
                vehicleSubmenu.AddSubItem(new MenuItem(action.DisplayName.Replace("Spawn ", ""), () => {
                    manager.ExecuteAction(action);
                })
                {
                    ActionId = action.Id,
                    MenuAction = action
                });
            }
            parentMenu.AddSubItem(vehicleSubmenu);
            
            // Add friendly units submenu
            MenuItem friendlySubmenu = new MenuItem("Friendly");
            foreach (var unitType in UnitTypes.FriendlyUnits)
            {
                var unit = unitType; // Capture for closure
                var action = MenuAction.CreateSpawnEnemy(unit);
                friendlySubmenu.AddSubItem(new MenuItem(action.DisplayName.Replace("Spawn ", ""), () => {
                    manager.ExecuteAction(action);
                })
                {
                    ActionId = action.Id,
                    MenuAction = action
                });
            }
            parentMenu.AddSubItem(friendlySubmenu);
            
            // Add other units submenu
            MenuItem otherSubmenu = new MenuItem("Other");
            foreach (var unitType in UnitTypes.OtherUnits)
            {
                var unit = unitType; // Capture for closure
                var action = MenuAction.CreateSpawnEnemy(unit);
                otherSubmenu.AddSubItem(new MenuItem(action.DisplayName.Replace("Spawn ", ""), () => {
                    manager.ExecuteAction(action);
                })
                {
                    ActionId = action.Id,
                    MenuAction = action
                });
            }
            parentMenu.AddSubItem(otherSubmenu);
        }

        public void BuildObjectSubmenu(MenuItem parentMenu)
        {
            // Block categories
            MenuItem terrainSubmenu = new MenuItem("Terrain Blocks");
            BuildTerrainBlocksSubmenu(terrainSubmenu);
            parentMenu.AddSubItem(terrainSubmenu);
            
            MenuItem structuresSubmenu = new MenuItem("Building & Structures");
            BuildStructuresSubmenu(structuresSubmenu);
            parentMenu.AddSubItem(structuresSubmenu);
            
            MenuItem platformsSubmenu = new MenuItem("Bridges, Platforms & Ladders");
            BuildPlatformsSubmenu(platformsSubmenu);
            parentMenu.AddSubItem(platformsSubmenu);
            
            MenuItem specialBlocksSubmenu = new MenuItem("Special Blocks");
            BuildSpecialBlocksSubmenu(specialBlocksSubmenu);
            parentMenu.AddSubItem(specialBlocksSubmenu);
            
            parentMenu.AddSeparator();
            
            // Object categories
            MenuItem destructiblesSubmenu = new MenuItem("Destructibles & Organics");
            BuildDestructiblesSubmenu(destructiblesSubmenu);
            parentMenu.AddSubItem(destructiblesSubmenu);
            
            MenuItem cratesSubmenu = new MenuItem("Crates");
            BuildCratesSubmenu(cratesSubmenu);
            parentMenu.AddSubItem(cratesSubmenu);
            
            MenuItem interactiveSubmenu = new MenuItem("Interactive Objects");
            BuildInteractiveObjectsSubmenu(interactiveSubmenu);
            parentMenu.AddSubItem(interactiveSubmenu);
            
            MenuItem hazardsSubmenu = new MenuItem("Hazards");
            BuildHazardsSubmenu(hazardsSubmenu);
            parentMenu.AddSubItem(hazardsSubmenu);
            
            MenuItem environmentSubmenu = new MenuItem("Environment & Props");
            BuildEnvironmentSubmenu(environmentSubmenu);
            parentMenu.AddSubItem(environmentSubmenu);
        }

        public void BuildTerrainBlocksSubmenu(MenuItem parentMenu)
        {
            // Basic terrain
            AddBlockMenuItem(parentMenu, BlockType.Dirt, "Dirt");
            AddBlockMenuItem(parentMenu, BlockType.CaveRock, "Cave Rock");
            AddBlockMenuItem(parentMenu, BlockType.SandyEarth, "Sandy Earth");
            AddBlockMenuItem(parentMenu, BlockType.DesertSand, "Desert Sand");
            
            parentMenu.AddSeparator();
            
            // Environment-specific terrain
            AddBlockMenuItem(parentMenu, BlockType.CaveEarth, "Cave Earth");
            AddBlockMenuItem(parentMenu, BlockType.HellRock, "Hell Rock");
            AddBlockMenuItem(parentMenu, BlockType.DesertCaveRock, "Desert Cave Rock");
            AddBlockMenuItem(parentMenu, BlockType.DesertCaveEarth, "Desert Cave Earth");
            AddBlockMenuItem(parentMenu, BlockType.DesertEarth, "Desert Earth");
            AddBlockMenuItem(parentMenu, BlockType.CityEarth, "City Earth");
            AddBlockMenuItem(parentMenu, BlockType.Skulls, "Skulls");
            AddBlockMenuItem(parentMenu, BlockType.Bones, "Bones");
            
            parentMenu.AddSeparator();
            
            // Alien Terrain
            AddBlockMenuItem(parentMenu, BlockType.AlienEarth, "Alien Earth");
            AddBlockMenuItem(parentMenu, BlockType.AlienFlesh, "Alien Flesh");
            AddBlockMenuItem(parentMenu, BlockType.AlienExplodingFlesh, "Alien Exploding Flesh");
            AddBlockMenuItem(parentMenu, BlockType.AlienDirt, "Alien Dirt");
            
            parentMenu.AddSeparator();
            
            // Large terrain blocks
            AddBlockMenuItem(parentMenu, BlockType.BigBlock, "Big Block");
            AddBlockMenuItem(parentMenu, BlockType.SacredBigBlock, "Sacred Big Block");
        }

        public void BuildStructuresSubmenu(MenuItem parentMenu)
        {
            // Basic building materials
            AddBlockMenuItem(parentMenu, BlockType.Brick, "Brick");
            AddBlockMenuItem(parentMenu, BlockType.BrickTop, "Brick Top");
            AddBlockMenuItem(parentMenu, BlockType.Metal, "Metal");
            AddBlockMenuItem(parentMenu, BlockType.Steel, "Steel (Indestructible)");
            
            parentMenu.AddSeparator();
            
            // Military structures
            AddBlockMenuItem(parentMenu, BlockType.Bunker, "Urban Brick");
            AddBlockMenuItem(parentMenu, BlockType.WatchTower, "Watch Tower Floor");
            
            parentMenu.AddSeparator();
            
            // Roofs and covers
            AddBlockMenuItem(parentMenu, BlockType.Roof, "Roof");
            AddBlockMenuItem(parentMenu, BlockType.ThatchRoof, "Thatch Roof");
            AddBlockMenuItem(parentMenu, BlockType.FactoryRoof, "Factory Roof");
            AddBlockMenuItem(parentMenu, BlockType.DesertRoof, "Desert Roof");
            AddBlockMenuItem(parentMenu, BlockType.DesertRoofRed, "Desert Roof Red");
            AddBlockMenuItem(parentMenu, BlockType.TentRoof, "Tent Roof");
            
            parentMenu.AddSeparator();
            
            // City/Desert/Sacred structures
            AddBlockMenuItem(parentMenu, BlockType.CityBrick, "City Brick");
            AddBlockMenuItem(parentMenu, BlockType.DesertBrick, "Desert Brick");
            AddBlockMenuItem(parentMenu, BlockType.SacredTemple, "Sacred Temple");
            AddBlockMenuItem(parentMenu, BlockType.SacredTempleGold, "Sacred Temple Gold");
            AddBlockMenuItem(parentMenu, BlockType.CityRoad, "City Road");
            
            parentMenu.AddSeparator();
            
            // Miscellaneous structures
            AddBlockMenuItem(parentMenu, BlockType.Pipe, "Pipe");
            AddBlockMenuItem(parentMenu, BlockType.OilPipe, "Oil Pipe");
            AddBlockMenuItem(parentMenu, BlockType.Statue, "Statue");
            AddBlockMenuItem(parentMenu, BlockType.TyreBlock, "Tyre Block");
            AddBlockMenuItem(parentMenu, BlockType.CityAssMouth, "City Ass Mouth");
        }

        public void BuildPlatformsSubmenu(MenuItem parentMenu)
        {
            // Bridges
            AddBlockMenuItem(parentMenu, BlockType.Bridge, "Bridge");
            AddBlockMenuItem(parentMenu, BlockType.Bridge2, "Bridge 2");
            AddBlockMenuItem(parentMenu, BlockType.MetalBridge, "Metal Bridge");
            AddBlockMenuItem(parentMenu, BlockType.MetalBridge2, "Metal Bridge 2");
            AddBlockMenuItem(parentMenu, BlockType.SacredBridge, "Sacred Bridge");
            AddBlockMenuItem(parentMenu, BlockType.AlienBridge, "Alien Bridge");
            
            parentMenu.AddSeparator();
            
            // Ladders
            AddBlockMenuItem(parentMenu, BlockType.Ladder, "Ladder");
            AddBlockMenuItem(parentMenu, BlockType.MetalLadder, "Metal Ladder");
            AddBlockMenuItem(parentMenu, BlockType.CityLadder, "City Ladder");
            AddBlockMenuItem(parentMenu, BlockType.DesertLadder, "Desert Ladder");
            AddBlockMenuItem(parentMenu, BlockType.AlienLadder, "Alien Ladder");
        }

        public void BuildSpecialBlocksSubmenu(MenuItem parentMenu)
        {
            // Physics blocks
            AddBlockMenuItem(parentMenu, BlockType.FallingBlock, "Falling Block");
            AddBlockMenuItem(parentMenu, BlockType.Quicksand, "Quicksand");
            
            parentMenu.AddSeparator();
            
            // Large blocks
            AddBlockMenuItem(parentMenu, BlockType.Boulder, "Boulder");
            AddBlockMenuItem(parentMenu, BlockType.BoulderBig, "Boulder Big");
            
            parentMenu.AddSeparator();
            
            // Defensive blocks
            AddBlockMenuItem(parentMenu, BlockType.Sandbag, "Sandbag");
            AddBlockMenuItem(parentMenu, BlockType.Vault, "Vault");
            
            parentMenu.AddSeparator();
            
            // Cage blocks
            AddBlockMenuItem(parentMenu, BlockType.SmallCageBlock, "Small Cage Block");
            AddBlockMenuItem(parentMenu, BlockType.StandardCage, "Standard Cage");
            
        }

        public void BuildDestructiblesSubmenu(MenuItem parentMenu)
        {
            // Barrels/Explosives
            AddBlockMenuItem(parentMenu, BlockType.ExplosiveBarrel, "Explosive Barrel");
            AddBlockMenuItem(parentMenu, BlockType.RedExplosiveBarrel, "Red Explosive Barrel");
            AddBlockMenuItem(parentMenu, BlockType.PropaneTank, "Propane Tank");
            AddBlockMenuItem(parentMenu, BlockType.DesertOilBarrel, "Desert Oil Barrel");
            AddBlockMenuItem(parentMenu, BlockType.OilTank, "Oil Tank");
            
            parentMenu.AddSeparator();
            
            // Organic Destructibles
            AddBlockMenuItem(parentMenu, BlockType.BeeHive, "Beehive");
            AddBlockMenuItem(parentMenu, BlockType.AlienEgg, "Alien Egg");
            AddBlockMenuItem(parentMenu, BlockType.AlienEggExplosive, "Alien Egg (Explosive)");
        }

        public void BuildCratesSubmenu(MenuItem parentMenu)
        {
            AddBlockMenuItem(parentMenu, BlockType.Crate, "Wooden Crate");
            AddBlockMenuItem(parentMenu, BlockType.AmmoCrate, "Ammo Crate");
            AddBlockMenuItem(parentMenu, BlockType.TimeAmmoCrate, "Time Slow Crate");
            AddBlockMenuItem(parentMenu, BlockType.RCCarAmmoCrate, "RC Car Crate");
            AddBlockMenuItem(parentMenu, BlockType.AirStrikeAmmoCrate, "Airstrike Crate");
            AddBlockMenuItem(parentMenu, BlockType.MechDropAmmoCrate, "Mech Drop Crate");
            AddBlockMenuItem(parentMenu, BlockType.AlienPheromonesAmmoCrate, "Alien Pheromones Crate");
            AddBlockMenuItem(parentMenu, BlockType.SteroidsAmmoCrate, "Steroids Crate");
            AddBlockMenuItem(parentMenu, BlockType.PigAmmoCrate, "Pig Crate");
            AddBlockMenuItem(parentMenu, BlockType.FlexAmmoCrate, "Flex Crate");
            AddBlockMenuItem(parentMenu, BlockType.MoneyCrate, "Money Crate");
            AddBlockMenuItem(parentMenu, BlockType.PillsCrate, "Pills Crate");
        }

        public void BuildInteractiveObjectsSubmenu(MenuItem parentMenu)
        {
            // Checkpoint
            var checkpointAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.CheckPoint);
            parentMenu.AddSubItem(new MenuItem("Checkpoint Flag", () => {
                manager.ExecuteAction(checkpointAction);
            })
            {
                ActionId = checkpointAction.Id,
                MenuAction = checkpointAction
            });
            
            parentMenu.AddSeparator();
            
            // Doors
            var doorAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.Door);
            parentMenu.AddSubItem(new MenuItem("Door", () => {
                manager.ExecuteAction(doorAction);
            })
            {
                ActionId = doorAction.Id,
                MenuAction = doorAction
            });
            
            var mookDoorAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.MookDoor);
            parentMenu.AddSubItem(new MenuItem("Mook Door", () => {
                manager.ExecuteAction(mookDoorAction);
            })
            {
                ActionId = mookDoorAction.Id,
                MenuAction = mookDoorAction
            });
            
            parentMenu.AddSeparator();
            
            // Hanging Objects
            var hangingVinesAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.HangingVines);
            parentMenu.AddSubItem(new MenuItem("Hanging Vines", () => {
                manager.ExecuteAction(hangingVinesAction);
            })
            {
                ActionId = hangingVinesAction.Id,
                MenuAction = hangingVinesAction
            });
            
            var hangingBrazierAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.HangingBrazier);
            parentMenu.AddSubItem(new MenuItem("Hanging Brazier", () => {
                manager.ExecuteAction(hangingBrazierAction);
            })
            {
                ActionId = hangingBrazierAction.Id,
                MenuAction = hangingBrazierAction
            });
            
        }

        public void BuildHazardsSubmenu(MenuItem parentMenu)
        {
            // Spikes
            var spikesAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.Spikes);
            parentMenu.AddSubItem(new MenuItem("Spikes", () => {
                manager.ExecuteAction(spikesAction);
            })
            {
                ActionId = spikesAction.Id,
                MenuAction = spikesAction
            });
            
            // Mines
            var minesAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.Mines);
            parentMenu.AddSubItem(new MenuItem("Mine Field", () => {
                manager.ExecuteAction(minesAction);
            })
            {
                ActionId = minesAction.Id,
                MenuAction = minesAction
            });
            
            // Saw Blade
            var sawBladeAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.SawBlade);
            parentMenu.AddSubItem(new MenuItem("Saw Blade", () => {
                manager.ExecuteAction(sawBladeAction);
            })
            {
                ActionId = sawBladeAction.Id,
                MenuAction = sawBladeAction
            });
            
            // Hidden Explosives
            var hiddenExplosivesAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.HiddenExplosives);
            parentMenu.AddSubItem(new MenuItem("Hidden Explosives", () => {
                manager.ExecuteAction(hiddenExplosivesAction);
            })
            {
                ActionId = hiddenExplosivesAction.Id,
                MenuAction = hiddenExplosivesAction
            });
            
            parentMenu.AddSeparator();
            
            // Alien Hazards
            AddBlockMenuItem(parentMenu, BlockType.AlienBarbShooter, "Alien Barb Shooter");
            
            parentMenu.AddSeparator();
            
            // Explosive Hazards
            AddBlockMenuItem(parentMenu, BlockType.BuriedRocket, "Buried Rocket");
        }

        public void BuildEnvironmentSubmenu(MenuItem parentMenu)
        {
            // Destructible Props
            var scaffoldingAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.Scaffolding);
            parentMenu.AddSubItem(new MenuItem("Scaffolding", () => {
                manager.ExecuteAction(scaffoldingAction);
            })
            {
                ActionId = scaffoldingAction.Id,
                MenuAction = scaffoldingAction
            });
            
            var fenceAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.Fence);
            parentMenu.AddSubItem(new MenuItem("Fence", () => {
                manager.ExecuteAction(fenceAction);
            })
            {
                ActionId = fenceAction.Id,
                MenuAction = fenceAction
            });
            
            parentMenu.AddSeparator();
            
            // Zipline - Two-click placement for connected ziplines
            var ziplineAction = MenuAction.CreateZiplinePlacement();
            parentMenu.AddSubItem(new MenuItem(manager.CurrentMode == ContextMenuManager.ContextMenuMode.ZiplinePlacement ? "Zipline (Place second point)" : "Zipline", () => {
                manager.ExecuteAction(ziplineAction);
            })
            {
                ShowCheckbox = manager.CurrentMode == ContextMenuManager.ContextMenuMode.ZiplinePlacement,
                IsChecked = manager.CurrentMode == ContextMenuManager.ContextMenuMode.ZiplinePlacement,
                ActionId = ziplineAction.Id,
                MenuAction = ziplineAction
            });
            
            parentMenu.AddSeparator();
            
            // Trees/Foliage
            var treeAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.Tree);
            parentMenu.AddSubItem(new MenuItem("Tree", () => {
                manager.ExecuteAction(treeAction);
            })
            {
                ActionId = treeAction.Id,
                MenuAction = treeAction
            });
            
            var bushAction = MenuAction.CreateSpawnDoodad(SpawnableDoodadType.Bush);
            parentMenu.AddSubItem(new MenuItem("Bush", () => {
                manager.ExecuteAction(bushAction);
            })
            {
                ActionId = bushAction.Id,
                MenuAction = bushAction
            });
        }

        public void BuildPlayerCheatsSubmenu(MenuItem parentMenu)
        {
            // Invincibility
            parentMenu.AddSubItem(new MenuItem("Invincibility", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.invulnerable,
                OnToggle = (isChecked) => {
                    Main.settings.invulnerable = isChecked;
                    // Apply invincibility immediately like the mod UI does
                    if (Main.currentCharacter != null)
                    {
                        if (isChecked)
                        {
                            Main.currentCharacter.SetInvulnerable(float.MaxValue, false);
                        }
                        else
                        {
                            Main.currentCharacter.SetInvulnerable(0, false);
                        }
                    }
                }
            });
            
            // Infinite Lives
            parentMenu.AddSubItem(new MenuItem("Infinite Lives", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.infiniteLives,
                OnToggle = (isChecked) => {
                    Main.settings.infiniteLives = isChecked;
                    // Apply infinite lives immediately like the mod UI does
                    if (Main.currentCharacter != null)
                    {
                        if (isChecked)
                        {
                            for (int i = 0; i < 4; ++i)
                            {
                                HeroController.SetLives(i, int.MaxValue);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < 4; ++i)
                            {
                                HeroController.SetLives(i, 1);
                            }
                        }
                    }
                }
            });
            
            // Infinite Specials
            parentMenu.AddSubItem(new MenuItem("Infinite Specials", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.infiniteSpecials,
                OnToggle = (isChecked) => {
                    Main.settings.infiniteSpecials = isChecked;
                }
            });
            
            // Flight Mode
            parentMenu.AddSubItem(new MenuItem("Flight Mode", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.enableFlight,
                OnToggle = (isChecked) => {
                    Main.settings.enableFlight = isChecked;
                }
            });
            
            // Disable Gravity
            parentMenu.AddSubItem(new MenuItem("Disable Gravity", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.disableGravity,
                OnToggle = (isChecked) => {
                    Main.settings.disableGravity = isChecked;
                }
            });
        }

        public void BuildLevelControlSubmenu(MenuItem parentMenu)
        {
            // Go to Level submenu
            MenuItem goToLevelMenu = new MenuItem("Go to Level");
            BuildGoToLevelSubmenu(goToLevelMenu);
            parentMenu.AddSubItem(goToLevelMenu);
            
            // Starting Level submenu
            MenuItem startingLevelMenu = new MenuItem("Starting Level");
            BuildStartingLevelSubmenu(startingLevelMenu);
            parentMenu.AddSubItem(startingLevelMenu);
            
            // Level navigation controls on one line
            MenuItem levelNav = new MenuItem("", (Action)null);
            levelNav.MultiButtons = new List<MenuItem.ButtonInfo>
            {
                new MenuItem.ButtonInfo { 
                    Text = "◄", 
                    Tooltip = "Previous Level",
                    OnClick = () => {
                        var action = MenuAction.CreatePreviousLevel();
                        manager.ExecuteAction(action, false);
                    }
                },
                new MenuItem.ButtonInfo { 
                    Text = "↻", 
                    Tooltip = "Restart Current Level",
                    OnClick = () => {
                        var action = MenuAction.CreateRestartLevel();
                        manager.ExecuteAction(action, false);
                    }
                },
                new MenuItem.ButtonInfo { 
                    Text = "►", 
                    Tooltip = "Next Level",
                    OnClick = () => {
                        var action = MenuAction.CreateNextLevel();
                        manager.ExecuteAction(action, false);
                    }
                }
            };
            parentMenu.AddSubItem(levelNav);
            
            // Return to World Map
            parentMenu.AddSubItem(new MenuItem("Return to World Map", () => {
                manager.CloseMenu();
                GameModeController.Instance.ReturnToWorldMap();
            }));
            
            // Loop Current Level
            parentMenu.AddSubItem(new MenuItem("Loop Current Level", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.loopCurrent,
                OnToggle = (isChecked) => {
                    Main.settings.loopCurrent = isChecked;
                }
            });
        }

        public void BuildGoToLevelSubmenu(MenuItem parentMenu)
        {
            // Add all campaigns using the same display names as the mod UI
            for (int i = 0; i < Main.campaignDisplayNames.Length; i++)
            {
                int campaignIndex = i; // Capture for closure
                MenuItem campaignMenu = new MenuItem(Main.campaignDisplayNames[i]);
                
                // Add levels for this campaign
                int levelCount = manager.GetLevelCountForCampaign(i);
                for (int j = 0; j < levelCount; j++)
                {
                    int levelIndex = j; // Capture for closure
                    var action = MenuAction.CreateGoToLevel(campaignIndex, levelIndex);
                    campaignMenu.AddSubItem(new MenuItem($"Level {j + 1}", () => {
                        manager.ExecuteAction(action);
                    })
                    {
                        MenuAction = action
                    });
                }
                
                parentMenu.AddSubItem(campaignMenu);
            }
        }

        public void BuildStartingLevelSubmenu(MenuItem parentMenu)
        {
            // Toggle for go to level on startup
            parentMenu.AddSubItem(new MenuItem("Go to Level on Startup", () => { })
            {
                IsToggleAction = true,
                ShowCheckbox = true,
                IsChecked = Main.settings.goToLevelOnStartup,
                OnToggle = (isChecked) => {
                    Main.settings.goToLevelOnStartup = isChecked;
                }
            });
            
            parentMenu.AddSeparator();
            
            // Current starting level with dropdown selector
            string currentCampaignName = Main.settings.campaignNum < Main.campaignDisplayNames.Length 
                ? Main.campaignDisplayNames[Main.settings.campaignNum] 
                : $"Campaign {Main.settings.campaignNum + 1}";
            
            MenuItem setStartingLevelMenu = new MenuItem($"Starting Level: {currentCampaignName} - Level {Main.settings.levelNum + 1}");
            
            // Build dropdown-style submenu for all campaigns
            for (int i = 0; i < Main.campaignDisplayNames.Length; i++)
            {
                int campaignIndex = i; // Capture for closure
                MenuItem campaignMenu = new MenuItem(Main.campaignDisplayNames[i]);
                
                // Add levels for this campaign
                int levelCount = manager.GetLevelCountForCampaign(i);
                for (int j = 0; j < levelCount; j++)
                {
                    int levelIndex = j; // Capture for closure
                    var action = MenuAction.CreateSetSpecificLevelAsStarting(campaignIndex, levelIndex);
                    
                    // Add checkmark if this is the current starting level
                    bool isCurrentStartingLevel = (campaignIndex == Main.settings.campaignNum && levelIndex == Main.settings.levelNum);
                    
                    campaignMenu.AddSubItem(new MenuItem((isCurrentStartingLevel ? "✓ " : "") + $"Level {j + 1}", () => {
                        manager.ExecuteAction(action);
                        manager.CloseMenu();
                    })
                    {
                        MenuAction = action
                    });
                }
                
                setStartingLevelMenu.AddSubItem(campaignMenu);
            }
            
            parentMenu.AddSubItem(setStartingLevelMenu);
            
            parentMenu.AddSeparator();
            
            // Go to starting level
            var goToStartingAction = MenuAction.CreateGoToStartingLevel();
            parentMenu.AddSubItem(new MenuItem(goToStartingAction.DisplayName, () => {
                manager.ExecuteAction(goToStartingAction);
            })
            {
                MenuAction = goToStartingAction
            });
            
            // Set current level as starting level
            var setCurrentAction = MenuAction.CreateSetCurrentLevelAsStarting();
            parentMenu.AddSubItem(new MenuItem(setCurrentAction.DisplayName, () => {
                manager.ExecuteAction(setCurrentAction);
                manager.CloseMenu();
            })
            {
                MenuAction = setCurrentAction
            });
            
            // Clear starting level (reset to defaults)
            var clearAction = MenuAction.CreateClearStartingLevel();
            parentMenu.AddSubItem(new MenuItem(clearAction.DisplayName, () => {
                manager.ExecuteAction(clearAction);
                manager.CloseMenu();
            })
            {
                MenuAction = clearAction
            });
        }

        public void BuildGameModifiersSubmenu(MenuItem parentMenu)
        {
            // Slow Time
            parentMenu.AddSubItem(new MenuItem("Slow Time", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.slowTime,
                OnToggle = (isChecked) => {
                    Main.settings.slowTime = isChecked;
                    if (isChecked)
                        Main.StartTimeSlow();
                    else
                        Main.StopTimeSlow();
                }
            });
            
            // One Hit Enemies
            parentMenu.AddSubItem(new MenuItem("One Hit Enemies", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.oneHitEnemies,
                OnToggle = (isChecked) => {
                    Main.settings.oneHitEnemies = isChecked;
                }
            });
            
            // Disable Enemy Spawns
            parentMenu.AddSubItem(new MenuItem("Disable Enemy Spawns", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.disableEnemySpawn,
                OnToggle = (isChecked) => {
                    Main.settings.disableEnemySpawn = isChecked;
                }
            });
            
            // Set Zoom submenu
            MenuItem setZoomMenu = new MenuItem("Set Zoom");
            setZoomMenu.AddSubItem(new MenuItem("0.5x", () => {
                SortOfFollow.zoomLevel = 0.5f;
                Main.settings.zoomLevel = 0.5f;
                Main.settings.setZoom = true;
            }));
            setZoomMenu.AddSubItem(new MenuItem("1.0x (Default)", () => {
                SortOfFollow.zoomLevel = 1.0f;
                Main.settings.zoomLevel = 1.0f;
                Main.settings.setZoom = false;
            }));
            setZoomMenu.AddSubItem(new MenuItem("1.5x", () => {
                SortOfFollow.zoomLevel = 1.5f;
                Main.settings.zoomLevel = 1.5f;
                Main.settings.setZoom = true;
            }));
            setZoomMenu.AddSubItem(new MenuItem("2.0x", () => {
                SortOfFollow.zoomLevel = 2.0f;
                Main.settings.zoomLevel = 2.0f;
                Main.settings.setZoom = true;
            }));
            parentMenu.AddSubItem(setZoomMenu);
        }

        public void BuildDebugOptionsSubmenu(MenuItem parentMenu)
        {
            // Print Audio Played
            parentMenu.AddSubItem(new MenuItem("Print Audio Played", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.printAudioPlayed,
                OnToggle = (isChecked) => {
                    Main.settings.printAudioPlayed = isChecked;
                }
            });
            
            // Suppress Announcer
            parentMenu.AddSubItem(new MenuItem("Suppress Announcer", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.suppressAnnouncer,
                OnToggle = (isChecked) => {
                    Main.settings.suppressAnnouncer = isChecked;
                }
            });
            
            // Max Cage Spawns
            parentMenu.AddSubItem(new MenuItem("Max Cage Spawns", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.maxCageSpawns,
                OnToggle = (isChecked) => {
                    Main.settings.maxCageSpawns = isChecked;
                }
            });
            
            // Show Mouse Position
            parentMenu.AddSubItem(new MenuItem("Show Mouse Position", (Action)null)
            {
                ShowCheckbox = true,
                IsToggleAction = true,
                IsChecked = Main.settings.showMousePosition,
                OnToggle = (isChecked) => {
                    Main.settings.showMousePosition = isChecked;
                }
            });
            
            parentMenu.AddSeparator();
            
            // Save Unity Mod Manager Settings
            parentMenu.AddSubItem(new MenuItem("Save All Mod Settings", () => {
                UnityModManagerNet.UnityModManager.SaveSettingsAndParams();
                manager.CloseMenu();
            }));
            
        }

        public void BuildTeleportSubmenu(MenuItem parentMenu)
        {
            // Spawn option toggles (mutually exclusive)
            parentMenu.AddSubItem(new MenuItem("Spawn at Custom Waypoint", () => { })
            {
                IsToggleAction = true,
                ShowCheckbox = true,
                IsChecked = Main.settings.changeSpawn,
                OnToggle = (isChecked) => {
                    Main.settings.changeSpawn = isChecked;
                    // If enabling this option, disable the other
                    if (isChecked)
                    {
                        Main.settings.changeSpawnFinal = false;
                    }
                    // Update all checkbox states in the menu
                    if (manager.CurrentMenu != null)
                    {
                        manager.UpdateSpawnToggleStates(manager.CurrentMenu.GetItems());
                    }
                }
            });
            
            parentMenu.AddSubItem(new MenuItem("Spawn at Final Checkpoint", () => { })
            {
                IsToggleAction = true,
                ShowCheckbox = true,
                IsChecked = Main.settings.changeSpawnFinal,
                OnToggle = (isChecked) => {
                    Main.settings.changeSpawnFinal = isChecked;
                    // If enabling this option, disable the other
                    if (isChecked)
                    {
                        Main.settings.changeSpawn = false;
                    }
                    // Update all checkbox states in the menu
                    if (manager.CurrentMenu != null)
                    {
                        manager.UpdateSpawnToggleStates(manager.CurrentMenu.GetItems());
                    }
                }
            });
            
            parentMenu.AddSeparator();
            
            // Custom spawn options
            AddWaypointMenuItem(parentMenu, MenuAction.CreateSetCustomSpawn());
            AddWaypointMenuItem(parentMenu, MenuAction.CreateGoToCustomSpawn());
            
            parentMenu.AddSubItem(new MenuItem("Clear Custom Spawn", () => {
                Main.ClearCustomSpawnForCurrentLevel();
                manager.CloseMenu();
            }));
            
            AddWaypointMenuItem(parentMenu, MenuAction.CreateGoToFinalCheckpoint());
            
            parentMenu.AddSeparator();
            
            // Set waypoints
            AddWaypointMenuItem(parentMenu, MenuAction.CreateSetWaypoint(1));
            AddWaypointMenuItem(parentMenu, MenuAction.CreateSetWaypoint(2));
            AddWaypointMenuItem(parentMenu, MenuAction.CreateSetWaypoint(3));
            
            parentMenu.AddSeparator();
            
            // Go to waypoints
            AddWaypointMenuItem(parentMenu, MenuAction.CreateGoToWaypoint(1));
            AddWaypointMenuItem(parentMenu, MenuAction.CreateGoToWaypoint(2));
            AddWaypointMenuItem(parentMenu, MenuAction.CreateGoToWaypoint(3));
            
            // Clear waypoints
            parentMenu.AddSubItem(new MenuItem("Clear All Waypoints", () => {
                Main.ClearAllWaypoints();
                manager.CloseMenu();
            }));
        }

        public void BuildLevelEditRecordingSubmenu(MenuItem parentMenu)
        {
            // Enable automatic replay toggle
            parentMenu.AddSubItem(new MenuItem("Enable Auto-Replay", () => { })
            {
                IsToggleAction = true,
                ShowCheckbox = true,
                IsChecked = Main.settings.enableLevelEditReplay,
                OnToggle = (isChecked) => {
                    Main.settings.enableLevelEditReplay = isChecked;
                    Main.settings.Save(Main.mod);
                }
            });
            
            parentMenu.AddSeparator();
            
            // Recording controls
            if (!Main.settings.isRecordingLevelEdits)
            {
                parentMenu.AddSubItem(new MenuItem("Start Recording", () => {
                    Main.settings.isRecordingLevelEdits = true;
                    string levelKey = Main.GetCurrentLevelKey();
                    manager.CloseMenu();
                }));
            }
            else
            {
                parentMenu.AddSubItem(new MenuItem("Stop Recording", () => {
                    Main.settings.isRecordingLevelEdits = false;
                    Main.settings.Save(Main.mod);
                    string levelKey = Main.GetCurrentLevelKey();
                    manager.CloseMenu();
                }));
            }
            
            parentMenu.AddSeparator();
            
            // Mass Delete Tool
            var massDeleteAction = MenuAction.CreateMassDelete();
            parentMenu.AddSubItem(new MenuItem(massDeleteAction.DisplayName, () => {
                manager.ExecuteAction(massDeleteAction);
            })
            {
                ActionId = massDeleteAction.Id,
                MenuAction = massDeleteAction
            });
            
            parentMenu.AddSeparator();
            
            // Clear edits for current level
            string currentLevelKey = Main.GetCurrentLevelKey();
            bool hasEditsForCurrentLevel = Main.settings.levelEditRecords.ContainsKey(currentLevelKey);
            
            var clearCurrentItem = new MenuItem($"Clear Edits for Current Level{(hasEditsForCurrentLevel ? $" ({Main.settings.levelEditRecords[currentLevelKey].Actions.Count} actions)" : " (none)")}", () => {
                if (Main.settings.levelEditRecords.ContainsKey(currentLevelKey))
                {
                    Main.settings.levelEditRecords.Remove(currentLevelKey);
                    Main.settings.Save(Main.mod);
                }
                manager.CloseMenu();
            });
            clearCurrentItem.Enabled = hasEditsForCurrentLevel;
            parentMenu.AddSubItem(clearCurrentItem);
            
            // Clear all edits
            int totalEditCount = 0;
            foreach (var record in Main.settings.levelEditRecords.Values)
            {
                totalEditCount += record.Actions.Count;
            }
            
            var clearAllItem = new MenuItem($"Clear All Level Edits ({Main.settings.levelEditRecords.Count} levels, {totalEditCount} actions)", () => {
                Main.settings.levelEditRecords.Clear();
                Main.settings.Save(Main.mod);
                manager.CloseMenu();
            });
            clearAllItem.Enabled = Main.settings.levelEditRecords.Count > 0;
            parentMenu.AddSubItem(clearAllItem);
        }

        public bool BuildContextActions(GameObject targetObject)
        {
            if (targetObject == null)
                return false;
            
            // Check if it's a unit
            Unit unit = targetObject.GetComponent<Unit>();
            if (unit != null && unit.IsEnemy && unit.playerNum < 0)
            {
                manager.CurrentMenu.AddItem(MenuItem.CreateHeader("Enemy Actions"));
                manager.CurrentMenu.AddSeparator();
                
                var killAction = MenuAction.CreateKillEnemy(unit);
                var killItem = new MenuItem(killAction.DisplayName, () => {
                    manager.ExecuteAction(killAction);
                })
                {
                    ActionId = killAction.Id,
                    MenuAction = killAction,
                    ShowCheckbox = true,
                    IsChecked = (Main.settings.selectedQuickAction != null && killAction.Id == Main.settings.selectedQuickAction.Id)
                };
                manager.CurrentMenu.AddItem(killItem);
                
                var toggleAction = MenuAction.CreateToggleFriendly(unit);
                var toggleItem = new MenuItem("Make Friendly", () => {
                    unit.playerNum = 0;
                    
                    // Make the unit forget about players
                    if (unit.enemyAI != null)
                    {
                        unit.enemyAI.ForgetPlayer();
                    }
                    
                    manager.CloseMenu();
                })
                {
                    ActionId = toggleAction.Id,
                    MenuAction = toggleAction,
                    ShowCheckbox = true,
                    IsChecked = (Main.settings.selectedQuickAction != null && toggleAction.Id == Main.settings.selectedQuickAction.Id)
                };
                manager.CurrentMenu.AddItem(toggleItem);
                
                var cloneAction = MenuAction.CreateCloneUnit(unit);
                var cloneItem = new MenuItem("Clone Enemy", () => {
                    manager.ExecuteAction(cloneAction);
                })
                {
                    ActionId = cloneAction.Id,
                    MenuAction = cloneAction,
                    ShowCheckbox = true,
                    IsChecked = (Main.settings.selectedQuickAction != null && cloneAction.Id == Main.settings.selectedQuickAction.Id)
                };
                manager.CurrentMenu.AddItem(cloneItem);
                
                var grabAction = MenuAction.CreateGrabEnemy(unit);
                var grabItem = new MenuItem(grabAction.DisplayName, () => {
                    manager.ExecuteAction(grabAction);
                })
                {
                    ActionId = grabAction.Id,
                    MenuAction = grabAction,
                    ShowCheckbox = true,
                    IsChecked = (Main.settings.selectedQuickAction != null && grabAction.Id == Main.settings.selectedQuickAction.Id)
                };
                manager.CurrentMenu.AddItem(grabItem);
                
                manager.CurrentMenu.AddSeparator();
            }
            // Check if it's a friendly unit (either originally friendly or made friendly)
            else if (unit != null && (!unit.IsEnemy || (unit.IsEnemy && unit.playerNum >= 0)))
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
                
                if (isPlayer)
                {
                    manager.CurrentMenu.AddItem(MenuItem.CreateHeader("Player Actions"));
                    manager.CurrentMenu.AddSeparator();
                    
                    var giveLifeAction = MenuAction.CreateGiveExtraLife(unit);
                    var giveLifeItem = new MenuItem("Give Extra Life", () => {
                        manager.ExecuteAction(giveLifeAction);
                    })
                    {
                        ActionId = giveLifeAction.Id,
                        MenuAction = giveLifeAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && giveLifeAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    manager.CurrentMenu.AddItem(giveLifeItem);
                    
                    var refillSpecialAction = MenuAction.CreateRefillSpecial(unit);
                    var refillSpecialItem = new MenuItem("Refill Special", () => {
                        manager.ExecuteAction(refillSpecialAction);
                    })
                    {
                        ActionId = refillSpecialAction.Id,
                        MenuAction = refillSpecialAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && refillSpecialAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    manager.CurrentMenu.AddItem(refillSpecialItem);
                    
                    var grabPlayerAction = MenuAction.CreateGrabPlayer(unit);
                    var grabPlayerItem = new MenuItem("Grab Player", () => {
                        manager.ExecuteAction(grabPlayerAction);
                    })
                    {
                        ActionId = grabPlayerAction.Id,
                        MenuAction = grabPlayerAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && grabPlayerAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    manager.CurrentMenu.AddItem(grabPlayerItem);
                    
                    // Only show bro switching options if Swap Bros is available
                    if (SwapBrosIntegration.IsAvailable)
                    {
                        var availableBros = SwapBrosIntegration.GetAvailableBros();
                        if (availableBros.Count > 0)
                        {
                            var switchToMenu = new MenuItem("Switch to", () => {});
                            manager.CurrentMenu.AddItem(switchToMenu);
                            
                            // Find the player number for this unit
                            int playerNum = 0;
                            for (int i = 0; i < HeroController.players.Length; i++)
                            {
                                if (HeroController.players[i] != null && HeroController.players[i].character == unit)
                                {
                                    playerNum = i;
                                    break;
                                }
                            }
                            
                            // Get current bro index for this player
                            int currentBroIndex = SwapBrosIntegration.GetCurrentBroIndex(playerNum);
                            
                            // Add each available bro as a submenu item
                            for (int i = 0; i < availableBros.Count; i++)
                            {
                                int broIndex = i; // Capture for closure
                                string broName = availableBros[i];
                                bool isCurrentBro = (i == currentBroIndex);
                                
                                // Add checkmark prefix for current bro
                                string displayName = isCurrentBro ? "✓ " + broName : broName;
                                
                                var menuItem = new MenuItem(displayName, () => {
                                    if (SwapBrosIntegration.SwapToBro(playerNum, broIndex))
                                    {
                                        manager.CloseMenu();
                                    }
                                });
                                
                                switchToMenu.AddSubItem(menuItem);
                            }
                        }
                    }
                    
                    // Add Flex Powers submenu
                    MenuItem flexPowersMenu = new MenuItem("Flex Powers");
                    
                    // Air Jump
                    var airJumpAction = MenuAction.CreateGiveFlexAirJump(unit);
                    var airJumpItem = new MenuItem("Give Air Jump", () => {
                        manager.ExecuteAction(airJumpAction);
                    })
                    {
                        ActionId = airJumpAction.Id,
                        MenuAction = airJumpAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && airJumpAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    flexPowersMenu.AddSubItem(airJumpItem);
                    
                    // Invulnerability
                    var invulnerabilityAction = MenuAction.CreateGiveFlexInvulnerability(unit);
                    var invulnerabilityItem = new MenuItem("Give Invulnerability", () => {
                        manager.ExecuteAction(invulnerabilityAction);
                    })
                    {
                        ActionId = invulnerabilityAction.Id,
                        MenuAction = invulnerabilityAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && invulnerabilityAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    flexPowersMenu.AddSubItem(invulnerabilityItem);
                    
                    // Golden Light
                    var goldenLightAction = MenuAction.CreateGiveFlexGoldenLight(unit);
                    var goldenLightItem = new MenuItem("Give Golden Light", () => {
                        manager.ExecuteAction(goldenLightAction);
                    })
                    {
                        ActionId = goldenLightAction.Id,
                        MenuAction = goldenLightAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && goldenLightAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    flexPowersMenu.AddSubItem(goldenLightItem);
                    
                    // Teleport
                    var teleportAction = MenuAction.CreateGiveFlexTeleport(unit);
                    var teleportItem = new MenuItem("Give Teleport", () => {
                        manager.ExecuteAction(teleportAction);
                    })
                    {
                        ActionId = teleportAction.Id,
                        MenuAction = teleportAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && teleportAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    flexPowersMenu.AddSubItem(teleportItem);
                    
                    flexPowersMenu.AddSeparator();
                    
                    // Clear Flex Power
                    var clearFlexAction = MenuAction.CreateClearFlexPower(unit);
                    var clearFlexItem = new MenuItem("Clear Flex Power", () => {
                        manager.ExecuteAction(clearFlexAction);
                    })
                    {
                        ActionId = clearFlexAction.Id,
                        MenuAction = clearFlexAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && clearFlexAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    flexPowersMenu.AddSubItem(clearFlexItem);
                    
                    manager.CurrentMenu.AddItem(flexPowersMenu);
                    
                    // Add Special Ammo submenu
                    MenuItem specialAmmoMenu = new MenuItem("Special Ammo");
                    
                    // Airstrike
                    var airstrikeAction = MenuAction.CreateGiveSpecialAmmoAirstrike(unit);
                    var airstrikeItem = new MenuItem("Give Airstrike", () => {
                        manager.ExecuteAction(airstrikeAction);
                    })
                    {
                        ActionId = airstrikeAction.Id,
                        MenuAction = airstrikeAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && airstrikeAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    specialAmmoMenu.AddSubItem(airstrikeItem);
                    
                    // Time Slow
                    var timeslowAction = MenuAction.CreateGiveSpecialAmmoTimeslow(unit);
                    var timeslowItem = new MenuItem("Give Time Slow", () => {
                        manager.ExecuteAction(timeslowAction);
                    })
                    {
                        ActionId = timeslowAction.Id,
                        MenuAction = timeslowAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && timeslowAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    specialAmmoMenu.AddSubItem(timeslowItem);
                    
                    // RC Car
                    var rcCarAction = MenuAction.CreateGiveSpecialAmmoRemoteControlCar(unit);
                    var rcCarItem = new MenuItem("Give RC Car", () => {
                        manager.ExecuteAction(rcCarAction);
                    })
                    {
                        ActionId = rcCarAction.Id,
                        MenuAction = rcCarAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && rcCarAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    specialAmmoMenu.AddSubItem(rcCarItem);
                    
                    // Mech Drop
                    var mechDropAction = MenuAction.CreateGiveSpecialAmmoMechDrop(unit);
                    var mechDropItem = new MenuItem("Give Mech Drop", () => {
                        manager.ExecuteAction(mechDropAction);
                    })
                    {
                        ActionId = mechDropAction.Id,
                        MenuAction = mechDropAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && mechDropAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    specialAmmoMenu.AddSubItem(mechDropItem);
                    
                    // Alien Pheromones
                    var alienPheromonesAction = MenuAction.CreateGiveSpecialAmmoAlienPheromones(unit);
                    var alienPheromonesItem = new MenuItem("Give Alien Pheromones", () => {
                        manager.ExecuteAction(alienPheromonesAction);
                    })
                    {
                        ActionId = alienPheromonesAction.Id,
                        MenuAction = alienPheromonesAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && alienPheromonesAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    specialAmmoMenu.AddSubItem(alienPheromonesItem);
                    
                    // Steroids
                    var steroidsAction = MenuAction.CreateGiveSpecialAmmoSteroids(unit);
                    var steroidsItem = new MenuItem("Give Steroids", () => {
                        manager.ExecuteAction(steroidsAction);
                    })
                    {
                        ActionId = steroidsAction.Id,
                        MenuAction = steroidsAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && steroidsAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    specialAmmoMenu.AddSubItem(steroidsItem);
                    
                    specialAmmoMenu.AddSeparator();
                    
                    // Clear Special Ammo
                    var clearAmmoAction = MenuAction.CreateClearSpecialAmmo(unit);
                    var clearAmmoItem = new MenuItem("Clear Special Ammo", () => {
                        manager.ExecuteAction(clearAmmoAction);
                    })
                    {
                        ActionId = clearAmmoAction.Id,
                        MenuAction = clearAmmoAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && clearAmmoAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    specialAmmoMenu.AddSubItem(clearAmmoItem);
                    
                    manager.CurrentMenu.AddItem(specialAmmoMenu);
                }
                else
                {
                    // Check if this was originally an enemy that was made friendly
                    if (unit.IsEnemy && unit.playerNum >= 0)
                    {
                        manager.CurrentMenu.AddItem(MenuItem.CreateHeader("Friendly Unit Actions"));
                        manager.CurrentMenu.AddSeparator();
                        
                        var makeEnemyAction = MenuAction.CreateToggleFriendly(unit);
                        var makeEnemyItem = new MenuItem("Make Enemy", () => {
                            unit.playerNum = -1;
                            manager.CloseMenu();
                        })
                        {
                            ActionId = makeEnemyAction.Id,
                            MenuAction = makeEnemyAction,
                            ShowCheckbox = true,
                            IsChecked = (Main.settings.selectedQuickAction != null && makeEnemyAction.Id == Main.settings.selectedQuickAction.Id)
                        };
                        manager.CurrentMenu.AddItem(makeEnemyItem);
                        
                        var grabFriendlyAction = MenuAction.CreateGrabEnemy(unit);
                        var grabFriendlyItem = new MenuItem("Grab Unit", () => {
                            manager.ExecuteAction(grabFriendlyAction);
                        })
                        {
                            ActionId = grabFriendlyAction.Id,
                            MenuAction = grabFriendlyAction,
                            ShowCheckbox = true,
                            IsChecked = (Main.settings.selectedQuickAction != null && grabFriendlyAction.Id == Main.settings.selectedQuickAction.Id)
                        };
                        manager.CurrentMenu.AddItem(grabFriendlyItem);
                    }
                    else
                    {
                        // Originally friendly unit (like rescued prisoners)
                        manager.CurrentMenu.AddItem(MenuItem.CreateHeader("Friendly Unit Actions"));
                        manager.CurrentMenu.AddSeparator();
                        
                        var grabFriendlyAction = MenuAction.CreateGrabEnemy(unit);
                        var grabFriendlyItem = new MenuItem("Grab Unit", () => {
                            manager.ExecuteAction(grabFriendlyAction);
                        })
                        {
                            ActionId = grabFriendlyAction.Id,
                            MenuAction = grabFriendlyAction,
                            ShowCheckbox = true,
                            IsChecked = (Main.settings.selectedQuickAction != null && grabFriendlyAction.Id == Main.settings.selectedQuickAction.Id)
                        };
                        manager.CurrentMenu.AddItem(grabFriendlyItem);
                    }
                }
                
                manager.CurrentMenu.AddSeparator();
            }
            
            // Check if it's a block
            Block block = targetObject.GetComponent<Block>();
            if (block != null && !block.destroyed)
            {
                manager.CurrentMenu.AddItem(MenuItem.CreateHeader("Block Actions"));
                manager.CurrentMenu.AddSeparator();
                
                var destroyAction = MenuAction.CreateDestroyBlock(block);
                var destroyItem = new MenuItem(destroyAction.DisplayName, () => {
                    manager.ExecuteAction(destroyAction);
                    manager.CloseMenu();
                })
                {
                    ActionId = destroyAction.Id,
                    MenuAction = destroyAction,
                    ShowCheckbox = true,
                    IsChecked = (Main.settings.selectedQuickAction != null && destroyAction.Id == Main.settings.selectedQuickAction.Id)
                };
                manager.CurrentMenu.AddItem(destroyItem);
                
                // Only show clone option for blocks we support
                BlockType? clonableType = manager.GetBlockTypeFromGroundType(block.groundType, block);
                if (clonableType.HasValue)
                {
                    var cloneBlockAction = MenuAction.CreateCloneBlock(block);
                    var cloneBlockItem = new MenuItem("Clone Block", () => {
                        manager.ExecuteAction(cloneBlockAction);
                    })
                    {
                        ActionId = cloneBlockAction.Id,
                        MenuAction = cloneBlockAction,
                        ShowCheckbox = true,
                        IsChecked = (Main.settings.selectedQuickAction != null && cloneBlockAction.Id == Main.settings.selectedQuickAction.Id)
                    };
                    manager.CurrentMenu.AddItem(cloneBlockItem);
                }
                
                manager.CurrentMenu.AddSeparator();
            }
            
            // Return true if any context actions were added
            return (unit != null) || (block != null && !block.destroyed);
        }

        // Helper Methods

        public void AddWaypointMenuItem(MenuItem parentMenu, MenuAction action)
        {
            parentMenu.AddSubItem(new MenuItem(action.DisplayName, () => {
                manager.ExecuteAction(action);
            }));
        }

        public void AddBlockMenuItem(MenuItem parentMenu, BlockType blockType, string displayName)
        {
            var action = MenuAction.CreateSpawnBlock(blockType);
            parentMenu.AddSubItem(new MenuItem(displayName, () => {
                manager.ExecuteAction(action);
            })
            {
                ActionId = action.Id,
                MenuAction = action
            });
        }
        #endregion
    }
}