using BroMakerLib.CustomObjects;
using BroMakerLib.Loaders;
using HarmonyLib;
using RocketLib;
using RocketLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static RocketLib.Utils.TestVanDammeAnimTypes;
using BSett = BroMakerLib.Settings;

namespace Control_Enemies_Mod
{
    static class Main
    {
        #region UMM
        // General
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        public static KeyBindingForPlayers possessEnemy;
        public static KeyBindingForPlayers leaveEnemy;
        public static KeyBindingForPlayers swapEnemiesLeft;
        public static KeyBindingForPlayers swapEnemiesRight;
        public static KeyBindingForPlayers special2;
        public static KeyBindingForPlayers special3;
        public static bool isBroMakerInstalled = false;
        public static bool isSwapBrosModInstalled = false;

        // UI
        public static GUIStyle headerStyle, buttonStyle, warningStyle, centeredTextStyle;
        public static bool changingEnabledUnits = false;
        public static bool creatingUnitList = false;
        public static float displayWarningTime = 0f;
        public static string[] swapBehaviorList = new string[] { "Kill Enemy", "Stun Enemy", "Delete Enemy", "Do Nothing" };
        public static string[] spawnBehaviorList = new string[] { "Spawn As Ghost", "Automatically Spawn as Enemies" };
        public static string[] fullUnitList = TestVanDammeAnimTypes.allUnitNames;
        public static bool[] filteredUnitList;
        public static string[] currentUnitList;
        public static string[] previousSelection = { "", "", "", "" };

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUpdate = OnUpdate;
            mod = modEntry;
            try
            {
                settings = Settings.Load<Settings>(modEntry);
            }
            catch
            {
                // Settings format changed
                settings = new Settings();
            }
            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            try
            {
                harmony.PatchAll(assembly);
            }
            catch ( Exception ex )
            {
                Main.Log("Exception patching: " + ex.ToString());
            }

            // Load keybinding
            LoadKeyBinding();

            // Initialize Unit List
            int[] previousSelGridInts = new int[4];
            settings.selGridInt.CopyTo(previousSelGridInts, 0);
            CreateUnitList();
            settings.selGridInt = previousSelGridInts;

            // Initialize enabled bro list
            if (settings.enabledUnits == null || settings.enabledUnits.Count() == 0)
            {
                settings.enabledUnits = new List<string>();
                settings.enabledUnits.AddRange(fullUnitList);
                filteredUnitList = Enumerable.Repeat(true, fullUnitList.Length).ToArray();
            }

            // Check if BroMaker is installed
            CheckBroMakerAvailable();

            // Load sprites for avatars
            LoadSprites();

            // Load sprites for score
            ScoreManager.LoadSprites();
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if ( headerStyle == null )
            {
                headerStyle = new GUIStyle(GUI.skin.button);
                headerStyle.fontStyle = FontStyle.Bold;
                headerStyle.normal.textColor = new Color(0.639216f, 0.909804f, 1f);
            }
            string previousToolTip = String.Empty;

            if (GUILayout.Button("General Options", headerStyle))
            {
                settings.showGeneralOptions = !settings.showGeneralOptions;
            }
            if (settings.showGeneralOptions)
            {
                ShowGeneralOptions(modEntry, ref previousToolTip);
            } // End General Options

            if (GUILayout.Button("Possession Options", headerStyle))
            {
                settings.showPossessionOptions = !settings.showPossessionOptions;
            }
            if (settings.showPossessionOptions)
            {
                ShowPossessionModeOptions(modEntry, ref previousToolTip);
            } // End Possession Options

            if (GUILayout.Button("Spawn As Enemy Options", headerStyle))
            {
                settings.showSpawnAsEnemyOptions = !settings.showSpawnAsEnemyOptions;
            }
            if (settings.showSpawnAsEnemyOptions)
            {
                ShowSpawnAsEnemyOptions(modEntry, ref previousToolTip);
            } // End Spawn As Enemy Options

            if (GUILayout.Button("Competitive Mode Options", headerStyle))
            {
                settings.showCompetitiveOptions = !settings.showCompetitiveOptions;
            }
            if (settings.showCompetitiveOptions)
            {
                ShowCompetitiveModeOptions(modEntry, ref previousToolTip);
            } // End Competitive Mode Options
        }

        static void ShowGeneralOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            GUILayout.BeginHorizontal();
            settings.allowWallClimbing = GUILayout.Toggle(settings.allowWallClimbing, new GUIContent("Enable Wall Climbing", "By default, enemies can't fully wall climb. If you enable this they will be able to, but they won't have animations."));

            settings.disableFallDamage = GUILayout.Toggle(settings.disableFallDamage, new GUIContent("Disable Fall Damage", "Disables fall damage for controlled enemies."));

            settings.disableTaunting = GUILayout.Toggle(settings.disableTaunting, new GUIContent("Disable Taunting", "Disables taunting for controlled enemies. They don't have animations so most enemies go invisible if you taunt."));

            settings.enableSprinting = GUILayout.Toggle(settings.enableSprinting, new GUIContent("Enable Sprinting", "Allows controlled enemies to sprint."));

            settings.extraControlsToggle = GUILayout.Toggle(settings.extraControlsToggle, new GUIContent("Keybindings Toggleable", "When enabled, makes the Special 2 and Special 3 buttons function as toggles rather than requiring that you hold them."));

            settings.noLifeLossOnSuicide = GUILayout.Toggle(settings.noLifeLossOnSuicide, new GUIContent("No Life Loss On Suicide", "When enabled, makes suicide enemies not cost lives when you die as them."));

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            settings.buffJumpForce = GUILayout.Toggle(settings.buffJumpForce, new GUIContent("Buff Jump Force", "Gives certain heavier enemies higher jumps so they can climb walls better."));

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 22;
            lastRect.width += 600;

            if ( GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;    
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(30);

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Used for certain additional abilities some enemies have."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Space(0f);
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 42;
            lastRect.width += 800;
            // Ensure keybinding gui function doesn't display the tooltip for the box
            bool showAfter = (previousToolTip != GUI.tooltip);
            previousToolTip = GUI.tooltip;
            special2.OnGUI(out _, true, true, ref previousToolTip);
            GUILayout.EndHorizontal();

            if (showAfter)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(20);

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Used for certain additional abilities some enemies have."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Space(0f);
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 42;
            lastRect.width += 800;
            // Ensure keybinding gui function doesn't display the tooltip for the box
            showAfter = (previousToolTip != GUI.tooltip);
            previousToolTip = GUI.tooltip;
            special3.OnGUI(out _, true, true, ref previousToolTip);
            GUILayout.EndHorizontal();

            if (showAfter)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(20);

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Press to leave the enemy you are currently controlling. This doesn't work if you spawned as an enemy however."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Space(0f);
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 42;
            lastRect.width += 800;
            // Ensure keybinding gui function doesn't display the tooltip for the box
            showAfter = (previousToolTip != GUI.tooltip);
            previousToolTip = GUI.tooltip;
            leaveEnemy.OnGUI(out _, true, true, ref previousToolTip);
            GUILayout.EndHorizontal();

            if (showAfter)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(20);
        }

        static void ShowPossessionModeOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            GUILayout.BeginHorizontal();
            if ( settings.possessionModeEnabled != (settings.possessionModeEnabled = GUILayout.Toggle(settings.possessionModeEnabled, new GUIContent("Enable Possessing Enemies", "Allows players to press a button to fire a bullet that can take control of enemies. This cannot be enabled while playing the competitive mode."))) )
            {
                if ( settings.possessionModeEnabled )
                {
                    settings.competitiveModeEnabled = false;
                }
            }
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 700;

            settings.loseLifeOnDeath = GUILayout.Toggle(settings.loseLifeOnDeath, new GUIContent("Lose Life On Death", "Removes a life when you die while controlling an enemy"));

            settings.loseLifeOnSwitch = GUILayout.Toggle(settings.loseLifeOnSwitch, new GUIContent("Lose Life On Switch", "Removes a life when you switch from one enemy to another"));

            settings.respawnFromCorpse = GUILayout.Toggle(settings.respawnFromCorpse, new GUIContent("Respawn from Corpse", "Respawn at the corpse of the enemy you were controlling when you die as them"));

            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            // Display keybinding options
            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Press to fire a bullet that takes control of enemies it hits."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Space(0f);
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 42;
            lastRect.width += 800;
            // Ensure keybinding gui function doesn't display the tooltip for the box
            bool showAfter = (previousToolTip != GUI.tooltip);
            previousToolTip = GUI.tooltip;
            possessEnemy.OnGUI(out _, true, true, ref previousToolTip);
            GUILayout.EndHorizontal();

            if ( showAfter )
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(25);

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Decides what happens to the enemy you were controlling when you press the Leave Enemy key"), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Label("When Leaving a Controlled Enemy:", GUILayout.Width(225));
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 700;
            settings.leavingEnemy = (SwapBehavior)GUILayout.SelectionGrid((int)settings.leavingEnemy, swapBehaviorList, 4);
            GUILayout.EndHorizontal();

            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(25);

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Decides what happens to the enemy you were controlling when you start controlling a new enemy."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Label("When Swapping to a New Enemy:", GUILayout.Width(225));
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 700;
            settings.swappingEnemies = (SwapBehavior)GUILayout.SelectionGrid((int)settings.swappingEnemies, swapBehaviorList, 4);
            GUILayout.EndHorizontal();

            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(35);

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Cooldown after you start controlling a new enemy before you can start firing bullets again."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Label(String.Format("Cooldown Between Swaps: {0:0.00}s", settings.swapCooldown), GUILayout.Width(225), GUILayout.ExpandWidth(false));
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 700;
            settings.swapCooldown = GUILayout.HorizontalSlider(settings.swapCooldown, 0, 2);
            GUILayout.EndHorizontal();

            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(25);

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Cooldown between firing each bullet."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Label(String.Format("Mind Control Bullet Firerate: {0:0.00}s", settings.fireRate), GUILayout.Width(225), GUILayout.ExpandWidth(false));
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 700;
            settings.swapCooldown = GUILayout.HorizontalSlider(settings.swapCooldown, 0, 2);
            settings.fireRate = GUILayout.HorizontalSlider(settings.fireRate, 0, 2);
            GUILayout.EndHorizontal();

            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(20);
        }

        static void ShowSpawnAsEnemyOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip)
        {
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.normal.textColor = Color.red;
                buttonStyle.hover.textColor = Color.red;
                buttonStyle.active.textColor = Color.red;

                buttonStyle.onHover.textColor = Color.green;
                buttonStyle.onNormal.textColor = Color.green;
                buttonStyle.onActive.textColor = Color.green;
            }

            if (warningStyle == null)
            {
                warningStyle = new GUIStyle(GUI.skin.label);

                warningStyle.normal.textColor = Color.red;
                warningStyle.hover.textColor = Color.red;
                warningStyle.active.textColor = Color.red;
                warningStyle.onHover.textColor = Color.red;
                warningStyle.onNormal.textColor = Color.red;
                warningStyle.onActive.textColor = Color.red;
            }

            GUILayout.BeginHorizontal();
            if ( settings.spawnAsEnemyEnabled != (settings.spawnAsEnemyEnabled = GUILayout.Toggle(settings.spawnAsEnemyEnabled, new GUIContent("Enable Spawning as Enemies", "Allows players to automatically spawn as enemies. This cannot be enabled while playing the competitive mode."))) )
            {
                if ( settings.spawnAsEnemyEnabled )
                {
                    settings.competitiveModeEnabled = false;
                }
            }
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 500;

            if ( settings.filterEnemies != (settings.filterEnemies = GUILayout.Toggle(settings.filterEnemies, new GUIContent("Filter Enemies", "Limits the enemies you can spawn as to the enabled ones."))))
            {
                // Remake filtered list
                CreateUnitList();
            }

            settings.alwaysChosen = GUILayout.Toggle(settings.alwaysChosen, new GUIContent("Always Spawn as Chosen Enemy", "Always spawn as the selected enemy rather than a random one from the current list."));

            settings.clickingSwapEnabled = GUILayout.Toggle(settings.clickingSwapEnabled, new GUIContent("Swap On Click", "Swaps to a new enemy when you click one in the menu."));

            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Chance to Spawn as an Enemy: " + settings.spawnAsEnemyChance.ToString("0.00") + "%", GUILayout.Width(275), GUILayout.ExpandWidth(false));
            settings.spawnAsEnemyChance = GUILayout.HorizontalSlider(settings.spawnAsEnemyChance, 0, 100);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Cooldown between when the player is allowed to swap to a new enemy by pressing the Swap Left or Swap Right keys."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Label(String.Format("Cooldown Between Swaps: {0:0.00}s", settings.spawnSwapCooldown), GUILayout.Width(275), GUILayout.ExpandWidth(false));
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 700;
            settings.spawnSwapCooldown = GUILayout.HorizontalSlider(settings.spawnSwapCooldown, 0, 2);
            GUILayout.EndHorizontal();

            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(25);

            GUILayout.BeginVertical();
            for (int i = 0; i < 4; ++i)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Player " + (i + 1) + " Options - " + (settings.showSettings[i] ? "Hide" : "Show"), GUILayout.Width(900)) )
                {
                    settings.showSettings[i] = !settings.showSettings[i];
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (settings.showSettings[i])
                {
                    GUILayout.BeginHorizontal();
                    swapEnemiesLeft.OnGUI(out _, false, true, ref previousToolTip, i, true, false, false);
                    swapEnemiesRight.OnGUI(out _, false, true, ref previousToolTip, i, true, false, false);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button(changingEnabledUnits ? new GUIContent("Save Changes", "") : new GUIContent( "Enter Filtering Mode",
                                               "Enable or disable enemies for all players"), GUILayout.ExpandWidth(false), GUILayout.Width(300)))
                    {
                        changingEnabledUnits = !changingEnabledUnits;
                        if (changingEnabledUnits)
                        {
                            displayWarningTime = 0f;
                            settings.filterEnemies = true;
                            CreateFilteredUnitList();
                        }
                        else
                        {
                            // Check that at least one enemy is enabled
                            bool atleastOne = false;
                            for (int x = 0; x < filteredUnitList.Length; ++x)
                            {
                                if (filteredUnitList[x])
                                {
                                    atleastOne = true;
                                    break;
                                }
                            }
                            if (atleastOne)
                            {
                                displayWarningTime = 0f;
                                UpdateFilteredUnitList();
                                CreateUnitList();
                            }
                            else
                            {
                                displayWarningTime = 10f;
                                changingEnabledUnits = !changingEnabledUnits;
                            }
                        }
                    }

                    lastRect = GUILayoutUtility.GetLastRect();
                    lastRect.y += 20;
                    lastRect.width += 300;

                    if (displayWarningTime <= 0f)
                    {
                        if (!GUI.tooltip.Equals(previousToolTip))
                        {
                            GUI.Label(lastRect, GUI.tooltip);
                        }
                        previousToolTip = GUI.tooltip;
                    }
                    else
                    {
                        displayWarningTime -= Time.unscaledDeltaTime;
                        GUI.Label(lastRect, "Must have at least one enemy enabled", warningStyle);

                        previousToolTip = GUI.tooltip;
                    }

                    GUILayout.EndHorizontal();

                    GUILayout.Space(25);

                    GUILayout.BeginHorizontal();

                    if (!creatingUnitList)
                    {
                        // Display filtering menu
                        if (changingEnabledUnits)
                        {
                            GUILayout.BeginVertical();
                            GUILayout.BeginHorizontal();
                            for (int j = 0; j < fullUnitList.Count(); ++j)
                            {
                                if (j % 5 == 0)
                                {
                                    GUILayout.EndHorizontal();
                                    GUILayout.BeginHorizontal();
                                }

                                filteredUnitList[j] = GUILayout.Toggle(filteredUnitList[j], fullUnitList[j], buttonStyle, GUILayout.Height(26), GUILayout.Width(180));
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(20);
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button("Select All", GUILayout.Width(200)))
                            {
                                for (int j = 0; j < filteredUnitList.Length; ++j)
                                {
                                    filteredUnitList[j] = true;
                                }
                            }
                            if (GUILayout.Button("Unselect All", GUILayout.Width(200)))
                            {
                                for (int j = 0; j < filteredUnitList.Length; ++j)
                                {
                                    filteredUnitList[j] = false;
                                }
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.EndVertical();
                        }
                        // Display bro selection menu
                        else
                        {
                            if (settings.selGridInt[i] < 0 || settings.selGridInt[i] >= currentUnitList.Length)
                            {
                                settings.selGridInt[i] = 0;
                            }

                            if (settings.selGridInt[i] <= currentUnitList.Length)
                            {
                                if (settings.clickingSwapEnabled)
                                {
                                    if (settings.selGridInt[i] != (settings.selGridInt[i] = GUILayout.SelectionGrid(settings.selGridInt[i], currentUnitList, 5, GUILayout.Height(30 * Mathf.Ceil(currentUnitList.Length / 5.0f))))
                                        && (Map.Instance != null))
                                    {
                                        switched[i] = true;
                                    }
                                }
                                else
                                {
                                    settings.selGridInt[i] = GUILayout.SelectionGrid(settings.selGridInt[i], currentUnitList, 5, GUILayout.Height(30 * Mathf.Ceil(currentUnitList.Length / 5.0f)));
                                }
                            }
                            else
                            {
                                CreateUnitList();
                            }
                        }
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(15);
        }

        static void ShowCompetitiveModeOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip)
        {
            if ( centeredTextStyle == null )
            {
                centeredTextStyle = new GUIStyle(GUI.skin.label);
                centeredTextStyle.alignment = TextAnchor.MiddleCenter;
            }

            GUILayout.BeginHorizontal();
            if ( settings.competitiveModeEnabled != (settings.competitiveModeEnabled = GUILayout.Toggle(settings.competitiveModeEnabled, new GUIContent("Enable Competitive Mode", "Allows players to control enemies and fight against another player")) ) )
            {
                if ( settings.competitiveModeEnabled )
                {
                    settings.spawnAsEnemyEnabled = false;
                    settings.possessionModeEnabled = false;
                }
            }
            
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 700;

            settings.ghostControlledEnemiesAffectCamera = GUILayout.Toggle(settings.ghostControlledEnemiesAffectCamera, new GUIContent("Ghost Enemies Affect Camera", "Allows enemies controlled by ghosts to affect the camera, so they can't be left behind."));

            if (previousToolTip != GUI.tooltip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(25);

            GUILayout.BeginHorizontal(new GUIContent("", "Controls whether you spawn as a ghost and fly towards enemies to possess them, or if you are automatically given enemies to control."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Label("Spawn Mode:", GUILayout.Width(225));

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 600;

            settings.spawnMode = (SpawnMode)GUILayout.SelectionGrid((int)settings.spawnMode, spawnBehaviorList, 2);

            if ( previousToolTip != GUI.tooltip )
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(30);

            settings.scoreToWin = RGUI.HorizontalSliderInt("Score Required to Attempt a Win (0 = unlimited): ", "Sets the initial score players have to reach to be able to spawn a portal and go to the boss level in order to try and win.", settings.scoreToWin, 0, 10, 500);

            GUILayout.Space(15);

            settings.scoreIncrement = RGUI.HorizontalSliderInt("Required Score Increase on Failed Win Attempt: ", "Sets the increase in score required to win when a player goes to the boss level and fails.", settings.scoreIncrement, 0, 10, 500);

            GUILayout.Space(15);

            settings.extraLiveOnBossLevel = RGUI.HorizontalSliderInt("Extra Hero Lives for Win Attempts: ", "Gives the hero player extra lives when they go to the boss level and attempt to win.", settings.extraLiveOnBossLevel, 0, 10, 500);

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 800;

            if (previousToolTip != GUI.tooltip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(40);

            settings.heroLives = RGUI.HorizontalSliderInt("Hero Lives at Level Start (0 = unlimited):   ", "Sets the lives the current hero player will have when they start the level.", settings.heroLives, 0, 10, 500);

            GUILayout.Space(15);

            settings.ghostLives = RGUI.HorizontalSliderInt("Ghost Lives at Level Start (0 = unlimited): ", "Sets the lives the ghost players will have when they start the level.", settings.ghostLives, 0, 10, 500);

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 800;

            if (previousToolTip != GUI.tooltip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(40);

            settings.startingHeroPlayer = RGUI.HorizontalSliderInt("Starting Hero Player (0 = Random): ", "Sets the starting hero player to the specified player number.", settings.startingHeroPlayer, 0, 4, 500);

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 800;

            if (previousToolTip != GUI.tooltip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(40);

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Sets the cooldown for players to automatically take control of a new enemy when the Spawn Mode is set to Automatically Spawn as Enemies"), GUIStyle.none, new GUILayoutOption[] { } );
            GUILayout.Label(String.Format("Cooldown Between Spawns for Automatic Mode: {0:0.00}s", settings.automaticallyFindEnemyCooldown), GUILayout.Width(225), GUILayout.ExpandWidth(false));

            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 35;
            lastRect.width += 800;

            if (previousToolTip != GUI.tooltip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            settings.automaticallyFindEnemyCooldown = GUILayout.HorizontalSlider(settings.automaticallyFindEnemyCooldown, 0.1f, 10f);
            GUILayout.EndHorizontal();

            GUILayout.Space(30f);

            // Lives handicap
            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Increase or decrease the starting lives for each player."), GUIStyle.none, new GUILayoutOption[] { } );
            GUILayout.Space(195f);
            GUILayout.Label("Player 1", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.Space(129f);
            GUILayout.Label("Player 2", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.Space(129f);
            GUILayout.Label("Player 3", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.Space(129f);
            GUILayout.Label("Player 4", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Increase or decrease the starting lives for each player."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Label("Lives Handicap: ", GUILayout.Width(135f));
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 800;
            for ( int i = 0; i < 4; ++i )
            {
                if (i == 0)
                    GUILayout.Space(15f);
                else
                    GUILayout.Space(50f);
                if (GUILayout.Button("-", GUILayout.Width(50f)))
                    --settings.livesHandicap[i];
                
                GUILayout.Label(settings.livesHandicap[i].ToString(), centeredTextStyle, GUILayout.Width(20f));

                if (GUILayout.Button("+", GUILayout.Width(50f)))
                    ++settings.livesHandicap[i];
            }

            GUILayout.Space(25f);
            if ( GUILayout.Button(new GUIContent("Reset", "Resets the lives handicap for all players."), GUILayout.ExpandWidth(false)) )
            {
                settings.livesHandicap = new int[] { 0, 0, 0, 0 };
            }
            GUILayout.EndHorizontal();

            if (previousToolTip != GUI.tooltip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(25f);

            // Score handicap
            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Increase or decrease the initial required score to win for each player."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Space(195f);
            GUILayout.Label("Player 1", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.Space(129f);
            GUILayout.Label("Player 2", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.Space(129f);
            GUILayout.Label("Player 3", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.Space(129f);
            GUILayout.Label("Player 4", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Increase or decrease the initial required score to win for each player."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Label("Required Score Handicap: ", GUILayout.Width(135f));
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 32;
            lastRect.width += 800;
            for (int i = 0; i < 4; ++i)
            {
                if (i == 0)
                    GUILayout.Space(15f);
                else
                    GUILayout.Space(50f);
                if (GUILayout.Button("-", GUILayout.Width(50f)))
                    --settings.scoreHandicap[i];

                GUILayout.Label(settings.scoreHandicap[i].ToString(), centeredTextStyle, GUILayout.Width(20f));

                if (GUILayout.Button("+", GUILayout.Width(50f)))
                    ++settings.scoreHandicap[i];
            }

            GUILayout.Space(25f);
            if (GUILayout.Button(new GUIContent("Reset", "Resets the score handicap for all players."), GUILayout.ExpandWidth(false)))
            {
                settings.scoreHandicap = new int[] { 0, 0, 0, 0 };
            }
            GUILayout.EndHorizontal();

            if (previousToolTip != GUI.tooltip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(25);

            // Score Increase handicap
            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Increase or decrease the required score to win increase after a failed win attempt for each player."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Space(195f);
            GUILayout.Label("Player 1", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.Space(129f);
            GUILayout.Label("Player 2", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.Space(129f);
            GUILayout.Label("Player 3", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.Space(129f);
            GUILayout.Label("Player 4", centeredTextStyle, GUILayout.Width(50f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(new GUIContent(string.Empty, "Increase or decrease the required score to win increase after a failed win attempt for each player."), GUIStyle.none, new GUILayoutOption[] { });
            GUILayout.Label("Required Score Increase Handicap: ", GUILayout.Width(135f));
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 32;
            lastRect.width += 800;
            for (int i = 0; i < 4; ++i)
            {
                if (i == 0)
                    GUILayout.Space(15f);
                else
                    GUILayout.Space(50f);
                if (GUILayout.Button("-", GUILayout.Width(50f)))
                    --settings.scoreIncreaseHandicap[i];

                GUILayout.Label(settings.scoreIncreaseHandicap[i].ToString(), centeredTextStyle, GUILayout.Width(20f));

                if (GUILayout.Button("+", GUILayout.Width(50f)))
                    ++settings.scoreIncreaseHandicap[i];
            }

            GUILayout.Space(25f);
            if (GUILayout.Button(new GUIContent("Reset", "Resets the score increase handicap for all players."), GUILayout.ExpandWidth(false)))
            {
                settings.scoreIncreaseHandicap = new int[] { 0, 0, 0, 0 };
            }
            GUILayout.EndHorizontal();

            if (previousToolTip != GUI.tooltip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(25);

            if (GUILayout.Button(new GUIContent("Reset Score", "Resets the score for the current game.")))
            {
                ResetAll();
                for (int i = 0; i < 4; ++i)
                {
                    if (ScoreManager.spriteSetup[i])
                    {
                        ScoreManager.UpdateScore(i);
                    }
                }
            }
            lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 800;

            if (previousToolTip != GUI.tooltip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.Space(20);
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            return true;
        }

        public static void Log(String str)
        {
            mod.Logger.Log(str);
        }
        #endregion

        #region Modding
        // General options
        public static List<Unit> currentUnit = new List<Unit>() { null, null, null, null };
        public static UnitType[] currentUnitType = new UnitType[] { UnitType.None, UnitType.None, UnitType.None, UnitType.None };
        public static float[] currentSpriteWidth = new float[] { 0f, 0f, 0f, 0f };
        public static float[] currentSpriteHeight = new float[] { 0f, 0f, 0f, 0f };
        public static bool[] currentlyEnemy = { false, false, false, false };
        public static int[] previousPlayerNum = new int[] { -1, -1, -1, -1 };
        public static TestVanDammeAnim[] previousCharacter = new TestVanDammeAnim[] { null, null, null, null };
        public static float[] countdownToRespawn = new float[] { 0f, 0f, 0f, 0f };
        public static Material defaultAvatarMat, ghostAvatarMat, mookAvatarMat, cr666AvatarMat, pigAvatarMat, bruiserAvatarMat;
        public static bool[] specialWasDown = { false, false, false, false };
        public static bool[] holdingSpecial = { false, false, false, false };
        public static bool[] holdingSpecial2 = { false, false, false, false };
        public static bool[] holdingSpecial3 = { false, false, false, false };
        public static bool[] holdingGesture = { false, false, false, false };
        public static bool up, left, down, right, fire, buttonJump, special, highfive, buttonGesture, sprint;
        public static bool createdSandworms = false;
        public static DeathType[] previousDeathType = new DeathType[] { DeathType.None, DeathType.None, DeathType.None, DeathType.None };
        public static float[] previousJumpForce = new float[] { 0f, 0f, 0f, 0f };

        // Possessing Enemy
        public static MindControlBullet bulletPrefab = null;
        public static float[] fireDelay = new float[] { 0f, 0f, 0f, 0f };

        // Spawning as Enemy
        public static bool[] switched = { false, false, false, false };
        public static float[] currentSpawnCooldown = { 0f, 0f, 0f, 0f };
        public static Player.SpawnType[] previousSpawnInfo = { Player.SpawnType.Unknown, Player.SpawnType.Unknown, Player.SpawnType.Unknown, Player.SpawnType.Unknown };
        public static bool[] willReplaceBro = { false, false, false, false };
        public static bool[] wasFirstDeployment = { false, false, false, false };

        // Competitive Mode
        public static int currentHeroNum
        {
            get => Main.settings.saveGames[PlayerProgress.currentWorldMapSaveSlot].currentHeroNum;
            set => Main.settings.saveGames[PlayerProgress.currentWorldMapSaveSlot].currentHeroNum = value;
        }
        public static bool[] revealed = { false, false, false, false };
        public static float[] findNewEnemyCooldown = { 0f, 0f, 0f, 0f };
        public static List<int> waitingToBecomeEnemy = new List<int>();
        public static GhostPlayer[] currentGhosts = new GhostPlayer[] { null, null, null, null };
        public static Vector3[] ghostSpawnPoint = new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
        public static GhostPlayer ghostPrefab;
        public static bool everyoneDead = false;
        public static bool openedPortal = false;
        public static bool anyAttemptingWin = false;
        public static bool onWinAttempt = false;
        public static int attemptingWin = -1;
        public static bool returnToPreviousLevel = false;
        public static bool playerWon = false;
        public static string previousSceneName;
        public static string previousCampaignName;
        public static int previousLevel;
        public static float switchCounter = 0f;
        public static float disableElevatorCounter = 0f;
        public static bool isBurning = false;
        public static float brokenCheckTimer = 0.25f;

        #region General
        // Update Everything
        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            try
            {
                for (int i = 0; i < 4; ++i)
                {
                    // Disable possession bullets and normal leaving enemy
                    if ( !settings.competitiveModeEnabled )
                    {
                        fireDelay[i] -= dt;
                        if (possessEnemy.IsDown(i) && HeroController.PlayerIsAlive(i) && fireDelay[i] <= 0f)
                        {
                            FireBullet(i);
                        }

                        if (leaveEnemy.IsDown(i) && HeroController.PlayerIsAlive(i) && previousCharacter[i] != null && !previousCharacter[i].destroyed)
                        {
                            LeaveUnit(HeroController.players[i].character, i, false);
                            FixAvatar(i);
                        }
                    }
                    // Check for leaving enemy in competitive mode
                    else
                    {
                        if (leaveEnemy.IsDown(i) && HeroController.PlayerIsAlive(i) && previousCharacter[i] != null && currentlyEnemy[i])
                        {
                            if (settings.spawnMode == SpawnMode.Automatic)
                            {
                                LeaveUnit(HeroController.players[i].character, i, true);
                                // Restore previous character
                                HeroController.players[i].character = previousCharacter[i];
                                HidePlayer(i, true);
                                findNewEnemyCooldown[i] = settings.automaticallyFindEnemyCooldown;
                            }
                            else
                            {
                                // Set ghost spawn to dead player
                                ghostSpawnPoint[i] = HeroController.players[i].character.transform.position + new Vector3(0f, GhostPlayer.ghostSpawnOffset);
                                LeaveUnit(HeroController.players[i].character, i, true);
                                // Restore previous character
                                HeroController.players[i].character = previousCharacter[i];
                                HidePlayer(i, true);
                            }
                        }
                    }

                    // Check if any characters are in the process of respawning
                    if (countdownToRespawn[i] > 0f)
                    {
                        countdownToRespawn[i] -= dt;

                        if (countdownToRespawn[i] <= 0f)
                        {
                            // Respawn as normal player
                            if ( settings.competitiveModeEnabled && HeroController.players[i] != null )
                            {
                                HarmonyPatches.Player_WorkOutSpawnScenario_Patch.forceCheckpointSpawn = true;
                                HeroController.players[i].RespawnBro(false);
                            }
                            // Respawn from enemy corpse
                            else if (HeroController.players[i].character != null && !HeroController.players[i].character.destroyed)
                            {
                                LeaveUnit(HeroController.players[i].character, i, false, true);
                            }
                        }
                    }

                    // Check for dancing
                    if (holdingGesture[i])
                    {
                        // Manually check for input because dancing disables GetInput
                        HeroController.players[i].GetInput(ref up, ref down, ref left, ref right, ref fire, ref buttonJump, ref special, ref highfive, ref buttonGesture, ref sprint);
                        if ( !buttonGesture )
                        {
                            holdingGesture[i] = false;
                            HeroController.players[i].character.Dance(0f);
                        }
                    }

                    // Countdown to respawn
                    if (findNewEnemyCooldown[i] > 0f)
                    {
                        findNewEnemyCooldown[i] -= dt;
                        if (findNewEnemyCooldown[i] <= 0f)
                        {
                            FindNewEnemyOnMap(i);
                        }
                    }
                    currentSpawnCooldown[i] -= dt;
                }

                if ( settings.competitiveModeEnabled )
                {
                    // Check for player winning
                    if (playerWon)
                    {
                        switchCounter += dt;
                        if (switchCounter > 8f)
                        {
                            GameModeController.SetSwitchDelay(1f);
                            switchCounter = 0;
                        }
                    }

                    // Countdown to reenable elevators
                    if (disableElevatorCounter > 0f)
                    {
                        disableElevatorCounter -= dt;
                    }

                    brokenCheckTimer -= dt;
                    if (brokenCheckTimer <= 0f)
                    {
                        try
                        {
                            brokenCheckTimer = 0.25f;
                            for (int i = 0; i < 4; ++i)
                            {
                                if (i != currentHeroNum && HeroController.IsPlayerPlaying(i))
                                {
                                    // If player is not respawning, isn't alive, and isn't a ghost, respawn them
                                    if (countdownToRespawn[i] <= 0f && !(HeroController.players[i].character != null && HeroController.players[i].character.health > 0 && HeroController.players[i].character.gameObject.activeSelf) && !(Main.currentGhosts[i] != null && Main.currentGhosts[i].gameObject.activeSelf))
                                    {
                                        HarmonyPatches.Player_WorkOutSpawnScenario_Patch.forceCheckpointSpawn = true;
                                        HeroController.players[i].RespawnBro(false);
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Log("Exception in update: " + ex.ToString());
            }
        }

        // Loading
        public static void LoadKeyBinding()
        {
            possessEnemy = AllModKeyBindings.LoadKeyBinding("Control Enemies Mod", "Possess Enemy Key");
            leaveEnemy = AllModKeyBindings.LoadKeyBinding("Control Enemies Mod", "Leave Enemy Key");
            swapEnemiesLeft = AllModKeyBindings.LoadKeyBinding("Control Enemies Mod", "Swap Enemies Left Key");
            swapEnemiesRight = AllModKeyBindings.LoadKeyBinding("Control Enemies Mod", "Swap Enemies Right Key");
            special2 = AllModKeyBindings.LoadKeyBinding("Control Enemies Mod", "Special 2 Key");
            special3 = AllModKeyBindings.LoadKeyBinding("Control Enemies Mod", "Special 3 Key");
        }
        public static void LoadSprites()
        {
            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            directoryPath = Path.Combine(directoryPath, "sprites");

            defaultAvatarMat = ResourcesController.GetMaterial(directoryPath, "avatar_default.png");
            ghostAvatarMat = ResourcesController.GetMaterial(directoryPath, "avatar_ghost.png");
            mookAvatarMat = ResourcesController.GetMaterial(directoryPath, "avatar_mook.png");
            cr666AvatarMat = ResourcesController.GetMaterial(directoryPath, "avatar_CR666.png");
            pigAvatarMat = ResourcesController.GetMaterial(directoryPath, "avatar_pig.png");
            bruiserAvatarMat = ResourcesController.GetMaterial(directoryPath, "avatar_bruiser.png");

            // Create ghost prefab
            if ( ghostPrefab == null )
            {
                ghostPrefab = new GameObject("GhostPlayer", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(GhostPlayer) }).GetComponent<GhostPlayer>();
                ghostPrefab.gameObject.SetActive(false);
                ghostPrefab.Setup();
                UnityEngine.Object.DontDestroyOnLoad(ghostPrefab);
            }
        }
        public static void ClearVariables()
        {
            // General options
            currentUnit = new List<Unit>() { null, null, null, null };
            currentUnitType = new UnitType[] { UnitType.None, UnitType.None, UnitType.None, UnitType.None };
            currentlyEnemy = new bool[] { false, false, false, false };
            previousPlayerNum = new int[] { -1, -1, -1, -1 };
            previousCharacter = new TestVanDammeAnim[] { null, null, null, null };
            countdownToRespawn = new float[] { 0f, 0f, 0f, 0f };
            specialWasDown = new bool[] { false, false, false, false };
            holdingSpecial = new bool[] { false, false, false, false };
            holdingSpecial2 = new bool[] { false, false, false, false };
            holdingSpecial3 = new bool[] { false, false, false, false };
            holdingGesture = new bool[] { false, false, false, false };
            HarmonyPatches.overrideNextVisibilityCheck = false;
            HarmonyPatches.MookJetpack_StartJetPacks_Patch.allowJetpack = false;
            createdSandworms = false;
            currentSpriteWidth = new float[] { 0f, 0f, 0f, 0f };
            currentSpriteHeight = new float[] { 0f, 0f, 0f, 0f };
            previousDeathType = new DeathType[] { DeathType.None, DeathType.None, DeathType.None, DeathType.None };
            previousJumpForce = new float[] { 0f, 0f, 0f, 0f };

            // Possessing Enemy
            fireDelay = new float[] { 0f, 0f, 0f, 0f };

            // Spawning as Enemy
            switched = new bool[] { false, false, false, false };
            currentSpawnCooldown = new float[] { 0f, 0f, 0f, 0f };
            previousSpawnInfo = new Player.SpawnType[] { Player.SpawnType.Unknown, Player.SpawnType.Unknown, Player.SpawnType.Unknown, Player.SpawnType.Unknown };
            willReplaceBro = new bool[] { false, false, false, false };
            wasFirstDeployment = new bool[] { false, false, false, false };
            HarmonyPatches.TestVanDammeAnim_Death_Patch.outOfBounds = false;
            HarmonyPatches.TestVanDammeAnim_ReduceLives_Patch.ignoreNextLifeLoss = false;
            HarmonyPatches.HellDogEgg_MakeEffects_Patch.playerNum = -1;
            HarmonyPatches.HellDogEgg_MakeEffects_Patch.nextDogFriendly = false;
            HarmonyPatches.AlienXenomorph_Start_Patch.controllerPlayerNum = -1;
            HarmonyPatches.AlienXenomorph_Start_Patch.controlNextAlien = false;

            // Competitive Mode
            revealed = new bool[] { false, false, false, false };
            findNewEnemyCooldown = new float[] { 0f, 0f, 0f, 0f };
            waitingToBecomeEnemy = new List<int>();
            currentGhosts = new GhostPlayer[] { null, null, null, null };
            ghostSpawnPoint = new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
            HarmonyPatches.Player_WorkOutSpawnScenario_Patch.forceCheckpointSpawn = false;
            ScoreManager.scoreSprites = new List<List<SpriteSM>>() { new List<SpriteSM>() { null, null, null, null } };
            ScoreManager.spriteSetup = new bool[] { false, false, false, false };
            everyoneDead = false;
            openedPortal = false;
            HarmonyPatches.Player_RemoveLife_Patch.allowLifeLoss = false;
            disableElevatorCounter = 0f;
            isBurning = false;
            HarmonyPatches.GameModeController_LevelFinish_Patch.finishedThisLevel = false;
            HarmonyPatches.Helicopter_Enter_Patch.helicopterDroppingOff = false;
        }

        // Controlling Units
        public static void StartControllingUnit(int playerNum, TestVanDammeAnim unit, bool gentleLeave = false, bool savePreviousCharacter = true, bool erasePreviousCharacter = false)
        {
            try
            {
                TestVanDammeAnim currentCharacter = HeroController.players[playerNum].character;
                if (HeroController.players[playerNum].IsAlive() && unit != null && unit is TestVanDammeAnim && unit.IsAlive())
                {
                    // Don't allow swap while player is piloting another unit, unless they are a mech, in which case pilotted unit refers to the mook inside the mech
                    if (currentCharacter != null && currentCharacter.pilottedUnit != null && !(currentCharacter is MookArmouredGuy) )
                    {
                        return;
                    }

                    // Store previous info
                    fireDelay[playerNum] = settings.swapCooldown;
                    currentUnit[playerNum] = unit;
                    currentUnitType[playerNum] = unit.GetUnitType();
                    currentSpriteWidth[playerNum] = currentUnitType[playerNum].GetSpriteWidth();
                    currentSpriteHeight[playerNum] = currentUnitType[playerNum].GetSpriteHeight();
                    previousDeathType[playerNum] = DeathType.None;

                    // Overwrite unit's info
                    unit.playerNum = playerNum;
                    Traverse trav = Traverse.Create(unit);
                    trav.SetFieldValue("isHero", true);
                    unit.name = "c";
                    DisableWhenOffCamera disableWhenOffCamera = unit.gameObject.GetComponent<DisableWhenOffCamera>();
                    if (disableWhenOffCamera != null)
                    {
                        disableWhenOffCamera.enabled = false;
                    }
                    unit.enabled = true;
                    if (unit.enemyAI != null)
                    {
                        unit.enemyAI.enabled = true;
                    }
                    unit.SpecialAmmo = 0;
                    unit.canWallClimb = settings.allowWallClimbing;
                    unit.canDash = settings.enableSprinting;
                    unit.canPushBlocks = true;
                    Mook mook = unit as Mook;
                    if (mook != null)
                    {
                        mook.firingPlayerNum = playerNum;
                        MookArmouredGuy mech = unit as MookArmouredGuy;
                        // Fix mechs shooting themselves
                        if ( mech != null )
                        {
                            mech.GetComponent<Collider>().gameObject.layer = LayerMask.NameToLayer("FriendlyBarriers");
                        }
                    }
                    // Fix heavy units having too low of a jump
                    if ( settings.buffJumpForce )
                    {
                        switch (currentUnitType[playerNum])
                        {
                            case UnitType.Bruiser:
                            case UnitType.SuicideBruiser:
                            case UnitType.StrongBruiser:
                            case UnitType.Boomer:
                            case UnitType.SoulCatcher:
                                previousJumpForce[playerNum] = unit.jumpForce;
                                unit._jumpForce = 215;
                                break;
                        }
                    }
                    // Fix suicide mooks being unable to wall climb
                    switch (currentUnitType[playerNum])
                    {
                        case UnitType.SuicideMook:
                            unit.SetFieldValue("halfWidth", 6f);
                            break;
                        case UnitType.RCCar:
                            // Set life to infinite and fix soundholder
                            trav.SetFieldValue("maxLife", 10000000);
                            unit.soundHolder = (HeroController.GetHeroPrefab(HeroType.Rambro) as Rambro).soundHolder;
                            break;
                    }
                    // Make sure held buttons aren't carried over
                    specialWasDown[playerNum] = false;
                    holdingSpecial[playerNum] = false;
                    holdingSpecial2[playerNum] = false;
                    holdingSpecial3[playerNum] = false;
                    holdingGesture[playerNum] = false;

                    waitingToBecomeEnemy.Remove(playerNum);

                    if ( currentCharacter != null )
                    {
                        // Release currently controlled unit, previousCharacter not being null indicates that we have a bro in storage
                        if (previousCharacter[playerNum] != null && !previousCharacter[playerNum].destroyed && previousCharacter[playerNum].IsAlive() && !(currentCharacter is BroBase))
                        {
                            SwitchUnit(currentCharacter, playerNum, gentleLeave, erasePreviousCharacter);
                        }
                        else
                        {
                            // Hide previous character
                            if (!gentleLeave)
                            {
                                currentCharacter.gameObject.SetActive(false);
                            }
                            if (savePreviousCharacter)
                            {
                                previousCharacter[playerNum] = currentCharacter;
                            }
                            else
                            {
                                previousCharacter[playerNum] = null;
                            }
                        }
                    }

                    // Hide player if in competitive mode
                    revealed[playerNum] = false;

                    // Set avatar to enemy one
                    SwitchToEnemyAvatar(playerNum, currentUnitType[playerNum]);

                    HeroController.players[playerNum].character = unit;
                    currentlyEnemy[playerNum] = true;
                }
            }
            catch (Exception ex)
            {
                Log("Exception playerNum " + playerNum + " controlling unit " + unit.name + ": " + ex.ToString());
            }
        }
        public static void ReaffirmControl(int playerNum)
        {
            TestVanDammeAnim unit = HeroController.players[playerNum].character;
            unit.playerNum = playerNum;
            Traverse.Create(unit).Field("isHero").SetValue(true);
            DisableWhenOffCamera disableWhenOffCamera = unit.gameObject.GetComponent<DisableWhenOffCamera>();
            if (disableWhenOffCamera != null)
            {
                disableWhenOffCamera.enabled = false;
            }
            if (unit is Mook)
            {
                Mook mook = unit as Mook;
                mook.firingPlayerNum = playerNum;
            }
            unit.canWallClimb = settings.allowWallClimbing;
            unit.canDash = settings.enableSprinting;
        }
        public static void SwitchUnit(TestVanDammeAnim previous, int playerNum, bool gentleLeave, bool erasePreviousCharacter)
        {
            previous.playerNum = previousPlayerNum[playerNum];
            Mook previousMook = previous as Mook;
            if (previousMook != null)
            {
                previousMook.firingPlayerNum = previousPlayerNum[playerNum];
            }
            Traverse previousTrav = Traverse.Create(previous);
            previousTrav.Field("isHero").SetValue(false);
            previous.name = "Enemy";
            previous.canWallClimb = false;
            previous.canDash = false;
            previous.canPushBlocks = false;
            DisableWhenOffCamera disableWhenOffCamera = previous.gameObject.GetComponent<DisableWhenOffCamera>();
            if (disableWhenOffCamera != null)
            {
                disableWhenOffCamera.enabled = true;
            }
            if ( previous.GetUnitType() == UnitType.SuicideMook )
            {
                previousTrav.SetFieldValue("halfWidth", 4f);
            }
            // Fix heavy units having too low of a jump
            if (settings.buffJumpForce)
            {
                switch (currentUnitType[playerNum])
                {
                    case UnitType.Bruiser:
                    case UnitType.SuicideBruiser:
                    case UnitType.StrongBruiser:
                    case UnitType.Boomer:
                    case UnitType.SoulCatcher:
                        previous.jumpForce = previousJumpForce[playerNum];
                        break;
                }
            }

            if (erasePreviousCharacter)
            {
                UnityEngine.Object.Destroy(previous.gameObject);
            }
            else if (!gentleLeave)
            {
                switch (settings.swappingEnemies)
                {
                    case SwapBehavior.KillEnemy:
                        if (previous is Mook)
                        {
                            (previous as Mook).Gib();
                        }
                        else
                        {
                            typeof(TestVanDammeAnim).GetMethod("Gib", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(previous, new object[] { DamageType.InstaGib, 0f, 0f });
                        }
                        EffectsController.CreateSlimeExplosion(previous.X, previous.Y + 5f, 10f, 10f, 140f, 0f, 0f, 0f, 0.5f, 0, 20, 120f, 0f, Vector3.up, previous.bloodColor);
                        break;
                    case SwapBehavior.StunEnemy:
                        previous.Stun(2f);
                        break;
                    case SwapBehavior.DeleteEnemy:
                        UnityEngine.Object.Destroy(previous.gameObject);
                        break;
                    case SwapBehavior.Nothing:
                        break;
                }
            }
        }
        public static void LeaveUnit(TestVanDammeAnim previous, int playerNum, bool onlyLeaveUnit, bool respawning = false)
        {
            try
            {
                if (!(previous is BroBase))
                {
                    previous.playerNum = previousPlayerNum[playerNum];
                    Mook previousMook = previous as Mook;
                    if (previousMook != null)
                    {
                        previousMook.firingPlayerNum = previousPlayerNum[playerNum];
                    }
                    Traverse previousTrav = Traverse.Create(previous);
                    previousTrav.Field("isHero").SetValue(false);
                    previous.name = "Enemy";
                    previous.canWallClimb = false;
                    previous.canDash = false;
                    previous.canPushBlocks = false;
                    DisableWhenOffCamera disableWhenOffCamera = previous.gameObject.GetComponent<DisableWhenOffCamera>();
                    if (disableWhenOffCamera != null)
                    {
                        disableWhenOffCamera.enabled = true;
                    }
                    if (previous.GetUnitType() == UnitType.SuicideMook)
                    {
                        previousTrav.SetFieldValue("halfWidth", 4f);
                    }
                    // Fix heavy units having too low of a jump
                    if (settings.buffJumpForce)
                    {
                        switch (currentUnitType[playerNum])
                        {
                            case UnitType.Bruiser:
                            case UnitType.SuicideBruiser:
                            case UnitType.StrongBruiser:
                            case UnitType.Boomer:
                            case UnitType.SoulCatcher:
                                previous.jumpForce = previousJumpForce[playerNum];
                                break;
                        }
                    }

                    if (previousCharacter[playerNum] != null && !previousCharacter[playerNum].destroyed && previousCharacter[playerNum].IsAlive())
                    {
                        if (!onlyLeaveUnit)
                        {
                            TestVanDammeAnim originalCharacter = previousCharacter[playerNum];
                            HeroController.players[playerNum].character = originalCharacter;
                            // If originalCharacter is also an enemy, set the current unit type back
                            if ( originalCharacter.name == "c" )
                            {
                                currentUnitType[playerNum] = originalCharacter.GetUnitType();
                            }
                            else
                            {
                                currentUnitType[playerNum] = UnitType.Bro;
                            }
                            originalCharacter.X = previous.X;
                            originalCharacter.Y = previous.Y;
                            originalCharacter.transform.localScale = new Vector3(Mathf.Sign(previous.transform.localScale.x) * originalCharacter.transform.localScale.x, originalCharacter.transform.localScale.y, originalCharacter.transform.localScale.z);
                            originalCharacter.xI = previous.xI;
                            originalCharacter.yI = previous.yI;

                            if (!respawning)
                            {
                                if (settings.loseLifeOnSwitch)
                                {
                                    HeroController.players[playerNum].RemoveLife();
                                }

                                switch (settings.leavingEnemy)
                                {
                                    case SwapBehavior.KillEnemy:
                                        if (previousMook != null)
                                        {
                                            (previous as Mook).Gib();
                                        }
                                        else
                                        {
                                            typeof(TestVanDammeAnim).GetMethod("Gib", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(previous, new object[] { DamageType.InstaGib, 0f, 0f });
                                        }
                                        EffectsController.CreateSlimeExplosion(previous.X, previous.Y + 5f, 10f, 10f, 140f, 0f, 0f, 0f, 0.5f, 0, 20, 120f, 0f, Vector3.up, previous.bloodColor);
                                        break;
                                    case SwapBehavior.StunEnemy:
                                        previous.Stun(2f);
                                        break;
                                    case SwapBehavior.DeleteEnemy:
                                        UnityEngine.Object.Destroy(previous.gameObject);
                                        break;
                                    case SwapBehavior.Nothing:
                                        break;
                                }
                            }
                            else
                            {
                                if (previousMook != null)
                                {
                                    (previous as Mook).Gib();
                                }
                                else
                                {
                                    typeof(TestVanDammeAnim).GetMethod("Gib", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(previous, new object[] { DamageType.InstaGib, 0f, 0f });
                                }
                                EffectsController.CreateSlimeExplosion(previous.X, previous.Y + 5f, 10f, 10f, 140f, 0f, 0f, 0f, 0.5f, 0, 20, 120f, 0f, Vector3.up, previous.bloodColor);
                            }

                            originalCharacter.gameObject.SetActive(true);
                            previousCharacter[playerNum] = null;
                        }
                        
                        currentlyEnemy[playerNum] = false;
                    }
                    else
                    {
                        previousCharacter[playerNum] = null;
                    }
                }
            }
            catch ( Exception ex )
            {
                Log("Exception leaving unit: " + ex.ToString());
            }
        }
        public static void SwitchToEnemyAvatar(int playerNum, UnitType type)
        {
            switch (type)
            {
                case UnitType.CR666:
                    HeroController.players[playerNum].hud.SetAvatar(cr666AvatarMat);
                    break;
                case UnitType.Bruiser:
                case UnitType.SuicideBruiser:
                case UnitType.EliteBruiser:
                    HeroController.players[playerNum].hud.SetAvatar(bruiserAvatarMat);
                    break;
                case UnitType.Pig:
                case UnitType.RottenPig:
                    HeroController.players[playerNum].hud.SetAvatar(pigAvatarMat);
                    break;
                default:
                    HeroController.players[playerNum].hud.SetAvatar(mookAvatarMat);
                    break;
            }
        }
        public static void SwitchToHeroAvatar(int playerNum)
        {
            if (isBroMakerInstalled && SetAvatarToSwitch(HeroController.players[playerNum].character))
            {
                HeroController.SwitchAvatarMaterial(HeroController.players[playerNum].hud.avatar, HeroType.None);
            }
            else
            {
                HeroController.players[playerNum].hud.SwitchAvatarAndGrenadeMaterial(HeroController.players[playerNum].heroType);
            }
        }
        public static void FixAvatar(int playerNum)
        {
            if (HeroController.players[playerNum].character.name == "c")
            {
                SwitchToEnemyAvatar(playerNum, currentUnitType[playerNum]);
            }
            else
            {
                SwitchToHeroAvatar(playerNum);
            }
        }

        // Enemy Specific Inputs
        public static void HandleSpecial(ref bool down, ref bool wasDown, ref bool holding, TestVanDammeAnim character, int playerNum)
        {
            bool actuallyDown = down;

            // Pressed button
            if (!wasDown && down)
            {
                PressSpecial(character, playerNum, ref down);
                holding = down;
            }
            // Released button
            else if (wasDown && !down)
            {
                ReleaseSpecial(character, playerNum, ref down);
                holding = false;
            }

            wasDown = actuallyDown;
        }
        public static void PressSpecial(TestVanDammeAnim character, int playerNum, ref bool down)
        {
            Traverse trav = Traverse.Create(character);
            switch (currentUnitType[character.playerNum])
            {
                // Don't use special because it requires special ammo which we're removing from all enemies
                case UnitType.Villager:
                    down = false;
                    break;

                // Manually set usingSpecial to true since satan ignores it from input
                case UnitType.SatanMiniboss:
                    trav.SetFieldValue("usingSpecial", true);
                    break;

                // Don't use special if we don't have one
                default:
                    if (!currentUnitType[playerNum].HasSpecial())
                    {
                        down = false;
                    }
                    break;
            }
        }
        public static void ReleaseSpecial(TestVanDammeAnim character, int playerNum, ref bool down)
        {
            Traverse trav = Traverse.Create(character);
            switch (currentUnitType[character.playerNum])
            {
                // Stop using special so you don't get trapped into using it forever
                case UnitType.AttackDog:
                case UnitType.Hellhound:
                case UnitType.MookGeneral:
                case UnitType.Alarmist:
                    trav.SetFieldValue("usingSpecial", false);
                    break;

                // Manually set usingSpecial since satan ignores it from input
                case UnitType.SatanMiniboss:
                    trav.SetFieldValue("usingSpecial", false);
                    break;
            }
        }
        public static void HandleButton(bool down, ref bool wasDown, ref bool holding, Action<TestVanDammeAnim> press, Action<TestVanDammeAnim> release, TestVanDammeAnim character, int playerNum)
        {
            // Pressed button
            if (!wasDown && down)
            {
                // Press and hold
                if (!Main.settings.extraControlsToggle)
                {
                    press(character);
                }
                // Press to toggle
                else
                {
                    // Wasn't held so start holding
                    if (!holding)
                    {
                        holding = true;
                        press(character);
                    }
                    // Release
                    else
                    {
                        holding = false;
                        release(character);
                    }
                }
            }
            // Released button
            else if (wasDown && !down)
            {
                // Releasing hold
                if (!Main.settings.extraControlsToggle)
                {
                    release(character);
                }
            }

            wasDown = down;
        }
        public static void PressSpecial2(TestVanDammeAnim character)
        {
            Traverse trav = Traverse.Create(character);

            switch (currentUnitType[character.playerNum])
            {
                // Make climbers climb
                case UnitType.Snake:
                case UnitType.Facehugger:
                case UnitType.Xenomorph:
                case UnitType.Screecher:
                case UnitType.XenomorphBrainbox:
                    trav.SetFieldValue("climbButton", true);
                    break;

                // Make jetpack enemies start their jetpack
                case UnitType.JetpackMook:
                case UnitType.JetpackBazookaMook:
                    // if using toggle controls and mosquito is already flying, stop flying
                    if (settings.extraControlsToggle && (bool)trav.GetFieldValue("jetpacksOn"))
                    {
                        holdingSpecial2[character.playerNum] = false;
                        typeof(MookJetpack).GetMethod("StopJetpacks", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(character, new object[] {});
                    }
                    else
                    {
                        HarmonyPatches.MookJetpack_StartJetPacks_Patch.allowJetpack = true;
                        typeof(MookJetpack).GetMethod("StartJetPacks", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(character, new object[] { });
                    }
                    break;

                // Make flying enemies fly
                case UnitType.Baneling:
                case UnitType.LostSoul:
                    // if using toggle controls and mosquito is already flying, stop flying
                    if (settings.extraControlsToggle && (bool)trav.GetFieldValue("flying"))
                    {
                        holdingSpecial2[character.playerNum] = false;
                        trav.SetFieldValue("flying", false);
                    }
                    else
                    {
                        trav.SetFieldValue("flying", true);
                    }
                    break;

                // Give DolphLundren his super jump special manually
                case UnitType.CR666:
                    character.jumpForce = 1000;
                    Traverse.Create(character).SetFieldValue("usingSpecial2", true);
                    break;

                // Spawn in sandworms for satan's special
                case UnitType.SatanMiniboss:
                    try
                    {
                        // Setup more sandworms if there aren't some available
                        if (!createdSandworms)
                        {
                            for (int i = 0; i < 4; ++i)
                            {
                                AlienSandWorm worm = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[7], new Vector3(-200f, -200f, 0f), Quaternion.identity).GetComponent<AlienSandWorm>();
                            }
                            createdSandworms = true;
                        }
                    }
                    catch ( Exception ex )
                    {
                        Main.Log("Exception creating sandowrms: " + ex.ToString());
                    }
                    Traverse.Create(character).SetFieldValue("usingSpecial2", true);
                    break;

                default:
                    if (currentUnitType[character.playerNum].HasSpecial2())
                    {
                        Traverse.Create(character).SetFieldValue("usingSpecial2", true);
                    }
                    break;
            }
        }
        public static void ReleaseSpecial2(TestVanDammeAnim character)
        {
            Traverse trav = Traverse.Create(character);

            switch (currentUnitType[character.playerNum])
            {
                // Make climbers climb
                case UnitType.Snake:
                case UnitType.Facehugger:
                case UnitType.Xenomorph:
                case UnitType.Screecher:
                case UnitType.XenomorphBrainbox:
                    trav.SetFieldValue("climbButton", false);
                    break;

                // Make jetpack enemies start their jetpack
                case UnitType.JetpackMook:
                case UnitType.JetpackBazookaMook:
                    typeof(MookJetpack).GetMethod("StopJetpacks", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(character, new object[] { });
                    break;

                // Make flying enemies fly
                case UnitType.Baneling:
                case UnitType.LostSoul:
                    trav.SetFieldValue("flying", false);
                    break;

                // Give DolphLundren his super jump special manually
                case UnitType.CR666:
                    character.jumpForce = 260;
                    Traverse.Create(character).SetFieldValue("usingSpecial2", false);
                    break;

                default:
                    if (currentUnitType[character.playerNum].HasSpecial2())
                    {
                        Traverse.Create(character).SetFieldValue("usingSpecial2", false);
                    }
                    break;
            }
        }
        public static void PressSpecial3(TestVanDammeAnim character)
        {
            Traverse trav = Traverse.Create(character);

            switch (currentUnitType[character.playerNum])
            {
                // Special 4 for alien melters to allow rolling attack
                case UnitType.Screecher:
                    trav.SetFieldValue("usingSpecial4", true);
                    break;

                // Make flying enemies dive
                case UnitType.Baneling:
                case UnitType.LostSoul:
                    Vector3 diveTarget = character.transform.position;
                    Vector3 diveDirection = new Vector3(0f, 0f, 0f);
                    if ( character.left )
                    {
                        diveTarget += Vector3.left * 500f;
                        diveDirection = Vector3.left;
                    }
                    else if ( character.right )
                    {
                        diveTarget += Vector3.right * 500f;
                        diveDirection = Vector3.right;
                    }
                    
                    if ( character.up )
                    {
                        diveTarget += Vector3.up * 500f;
                        diveDirection += Vector3.up;
                    }
                    else if ( character.down )
                    {
                        diveTarget += Vector3.down * 500f;
                        diveDirection += Vector3.down;
                    }
                    trav.SetFieldValue("diveDelay", 0.65f);
                    trav.SetFieldValue("diveDirection", diveDirection);
                    trav.SetFieldValue("divingTarget", diveTarget);
                    trav.SetFieldValue("diving", true);
                    break;

                case UnitType.CR666:
                    // Make sure dolph has seen the player to allow his special to activate
                    Traverse.Create(character.enemyAI).SetFieldValue("seenEnemyNum", 0);
                    trav.SetFieldValue("usingSpecial3", true);
                    break;

                default:
                    if (currentUnitType[character.playerNum].HasSpecial3())
                    {
                        trav.SetFieldValue("usingSpecial3", true);
                    }
                    break;
            }
        }
        public static void ReleaseSpecial3(TestVanDammeAnim character)
        {
            Traverse trav = Traverse.Create(character);

            switch (currentUnitType[character.playerNum])
            {
                // Special 4 for alien melters to allow rolling attack
                case UnitType.Screecher:
                    trav.SetFieldValue("usingSpecial4", false);
                    break;

                // Make flying enemies dive
                case UnitType.Baneling:
                case UnitType.LostSoul:
                    trav.SetFieldValue("diving", false);
                    trav.SetFieldValue("deathCounter", 0f);
                    break;

                case UnitType.CR666:
                    // Make sure dolph has seen the player to allow his special to activate
                    Traverse.Create(character.enemyAI).SetFieldValue("seenEnemyNum", 0);
                    trav.SetFieldValue("usingSpecial3", false);
                    break;

                default:
                    if (currentUnitType[character.playerNum].HasSpecial3())
                    {
                        trav.SetFieldValue("usingSpecial3", false);
                    }
                    break;
            }
        }

        // BroMaker
        private static string TryToUseBroMaker()
        {
            return BroMakerLib.Info.NAME;
        }
        public static void CheckBroMakerAvailable()
        {
            try
            {
                TryToUseBroMaker();
                isBroMakerInstalled = true;
            }
            catch
            {
                isBroMakerInstalled = false;
            }
        }
        public static void TryDisableBroMaker(int playerNum)
        {
            BSett.instance.disableSpawning = true;
            LoadHero.willReplaceBro[playerNum] = false;
        }
        public static void DisableBroMaker(int playerNum)
        {
            if (isBroMakerInstalled)
            {
                try
                {
                    TryDisableBroMaker(playerNum);
                }
                catch
                {
                    isBroMakerInstalled = false;
                }
            }
        }
        public static void TryEnableBroMaker()
        {
            BSett.instance.disableSpawning = false;
        }
        public static void EnableBroMaker()
        {
            if (isBroMakerInstalled)
            {
                try
                {
                    TryEnableBroMaker();
                }
                catch
                {
                    isBroMakerInstalled = false;
                }
            }
        }
        public static bool TrySetAvatarToSwitch(TestVanDammeAnim character, int playerNum)
        {
            if ( character is ICustomHero )
            {
                LoadHero.tryReplaceAvatar = true;
                if ( playerNum != -1 )
                {
                    LoadHero.playerNum = playerNum;
                }
                else
                {
                    LoadHero.playerNum = character.playerNum;
                }
                return true;
            }
            return false;
        }
        public static bool SetAvatarToSwitch(TestVanDammeAnim character, int playerNum = -1)
        {
            try
            {
                return TrySetAvatarToSwitch(character, playerNum);
            }
            catch
            {
                isBroMakerInstalled = false;
                return false;
            }
        }
        public static bool TryOverrideSpawn(int playerNum)
        {
            if (LoadHero.willReplaceBro[playerNum])
            {
                LoadHero.previousSpawnInfo[playerNum] = Player.SpawnType.CheckpointRespawn;
                return true;
            }
            return false;
        }
        public static bool OverrideSpawn(int playerNum)
        {
            try
            {
                return TryOverrideSpawn(playerNum);
            }
            catch
            {
                isBroMakerInstalled = false;
                return false;
            }
        }
        #endregion

        #region Possessing Enemies
        public static void SetupBullet()
        {
            try
            {
                bulletPrefab = new GameObject("MindControlBullet", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(MindControlBullet) }).GetComponent<MindControlBullet>();
                bulletPrefab.gameObject.SetActive(false);
                EllenRipbro ellenRipbro = (HeroController.GetHeroPrefab(HeroType.EllenRipbro) as EllenRipbro);
                bulletPrefab.Setup(ellenRipbro);
                UnityEngine.Object.DontDestroyOnLoad(bulletPrefab);
            }
            catch (Exception ex)
            {
                Main.Log("Exception creating bullet: " + ex.ToString());
            }
        }

        public static void FireBullet(int playerNum)
        {
            // Don't allow dying characters to fire bullets
            if (countdownToRespawn[playerNum] > 0)
            {
                return;
            }
            if (bulletPrefab == null)
            {
                // Create Mind Control Bullet
                SetupBullet();
            }
            fireDelay[playerNum] = settings.fireRate;
            TestVanDammeAnim firingChar = HeroController.players[playerNum].character;
            float x = firingChar.X + 3f;
            float y = firingChar.Y + firingChar.height + 1.5f;
            float xSpeed = firingChar.transform.localScale.x * 700f;
            float ySpeed = 0f;
            MindControlBullet firedBullet = ProjectileController.SpawnProjectileLocally(bulletPrefab, firingChar, x, y, xSpeed, ySpeed, firingChar.playerNum) as MindControlBullet;
        }

        public static void FindUnitToControl(Vector3 center, int playerNum)
        {
            for (int i = 0; i < Map.units.Count; ++i)
            {
                Unit unit = Map.units[i];
                // Check that unit is not null, is not a player, is not dead, and is not already grabbed by this trap or another
                if (unit != null && unit is TestVanDammeAnim && unit.playerNum < 0 && unit.health > 0 && unit.name != "c" && unit.name != "p" && !unit.IsHero)
                {
                    // Check unit is in rectangle around trap
                    if (Tools.FastAbsWithinRange(unit.X - center.x, 10f) && Tools.FastAbsWithinRange(unit.Y - center.y, 10f))
                    {
                        StartControllingUnit(playerNum, unit as TestVanDammeAnim);
                        return;
                    }
                }
            }
        }

        public static bool AvailableToPossess(TestVanDammeAnim character)
        {
            return character.name != "c" && character.name != "p" && character.name != "Hobro" && character.health > 0 && !character.IsHero;
        }
        #endregion

        #region Spawning As Enemy
        public static void CreateUnitList()
        {
            Main.creatingUnitList = true;

            if (currentUnitList != null)
            {
                for (int i = 0; i < 4; ++i)
                {
                    if (settings.selGridInt[i] > 0 && settings.selGridInt[i] < currentUnitList.Length)
                    {
                        previousSelection[i] = currentUnitList[settings.selGridInt[i]];
                    }
                }
            }
            if (settings.filterEnemies)
            {
                currentUnitList = settings.enabledUnits.ToArray();
            }
            else
            {
                currentUnitList = fullUnitList;
            }

            for (int i = 0; i < 4; ++i)
            {
                settings.selGridInt[i] = Array.IndexOf(currentUnitList, previousSelection[i]);
                if (settings.selGridInt[i] == -1)
                {
                    settings.selGridInt[i] = 0;
                }
            }
            Main.creatingUnitList = false;
        }

        public static void CreateFilteredUnitList()
        {
            filteredUnitList = Enumerable.Repeat(false, fullUnitList.Length).ToArray();
            // Find index of the enabled bro in fullUnitList and set the corresponding index in filteredBroList to true
            for (int i = 0; i < settings.enabledUnits.Count(); ++i)
            {
                int index = Array.IndexOf(fullUnitList, settings.enabledUnits[i]);
                if (index != -1)
                {
                    filteredUnitList[index] = true;
                }
            }
        }

        public static void UpdateFilteredUnitList()
        {
            settings.enabledUnits.Clear();
            for (int i = 0; i < filteredUnitList.Length; ++i)
            {
                if (filteredUnitList[i])
                {
                    settings.enabledUnits.Add(fullUnitList[i]);
                }
            }
        }

        public static UnitType GetSelectedUnit(int playerNum)
        {
            if (settings.selGridInt[playerNum] >= 0 && settings.selGridInt[playerNum] < currentUnitList.Length)
            {
                return ToUnitType(currentUnitList[settings.selGridInt[playerNum]]);
            }
            else
            {
                return UnitType.Mook;
            }
        }

        public static GameObject SpawnUnit(UnitType type, Vector3 vector)
        {
            GameObject __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(type.GetUnitPrefab(), vector, Quaternion.identity).gameObject;

            if (__result != null)
            {
                __result.gameObject.transform.parent = Map.Instance.transform;
                Registry.RegisterDeterminsiticGameObject(__result.gameObject);
            }

            return __result;
        }

        public static void WorkOutSpawnPosition(Player player, TestVanDammeAnim bro)
        {
            player.firstDeployment = wasFirstDeployment[player.playerNum];
            Vector3 arg = new Vector3(100f, 100f);
            Player.SpawnType arg2 = previousSpawnInfo[player.playerNum];
            bool flag = false;
            bool flag2 = false;
            switch (arg2)
            {
                case Player.SpawnType.Unknown:
                    flag = true;
                    goto IL_1E7;
                case Player.SpawnType.AddBroToTransport:
                    {
                        Map.AddBroToHeroTransport(bro);
                        arg = bro.transform.position;
                        goto IL_1E7;
                    }
                case Player.SpawnType.CheckpointRespawn:
                    flag2 = Map.IsCheckPointAnAirdrop(HeroController.GetCurrentCheckPointID());
                    arg = HeroController.GetCheckPointPosition(player.playerNum, flag2);
                    goto IL_1E7;
                case Player.SpawnType.RespawnAtRescueBro:
                    if (player.rescuingThisBro == null)
                    {
                        flag = true;
                    }
                    else
                    {
                        arg = player.rescuingThisBro.transform.position;
                    }
                    goto IL_1E7;
                case Player.SpawnType.DropInDuringGame:
                    flag = true;
                    goto IL_1E7;
                case Player.SpawnType.SpawnInCage:
                    {
                        SpawnPoint spawnPoint = Map.GetSpawnPoint(player.playerNum);
                        if (spawnPoint == null)
                        {
                            flag = true;
                        }
                        else
                        {
                            arg = spawnPoint.transform.position;
                            if (spawnPoint.cage != null)
                            {
                                spawnPoint.cage.SetPlayerColor(player.playerNum);
                                arg.x -= 8f;
                            }
                        }
                        goto IL_1E7;
                    }
                case Player.SpawnType.LevelEditorReload:
                    arg = LevelEditorGUI.lastPayerPos;
                    LevelEditorGUI.lastPayerPos = -Vector3.one;
                    Map.CallInHeroTransportAnyway();
                    goto IL_1E7;
                case Player.SpawnType.TriggerSwapBro:
                    arg = player.playerFollowPos;
                    goto IL_1E7;
                case Player.SpawnType.CustomSpawnPoint:
                    {
                        SpawnPoint spawnPoint2 = Map.GetSpawnPoint(player.playerNum);
                        arg = spawnPoint2.transform.position;
                        if (spawnPoint2 != null && spawnPoint2.cage != null)
                        {
                            arg.x -= 8f;
                        }
                        goto IL_1E7;
                    }
                case Player.SpawnType.AirDropRespawn:
                    arg = HeroController.GetCheckPointPosition(player.playerNum, true);
                    flag2 = true;
                    goto IL_1E7;
            }
            flag = true;
            IL_1E7:
            if (flag)
            {
                arg = HeroController.GetFirstPlayerPosition(player.playerNum);
            }
            player.SetSpawnPositon(bro, arg2, flag2, arg);
        }
        #endregion

        #region Competitive
        // Called whenever a player character (ghost or hero) dies in competitive mode
        public static void PlayerDiedInCompetitiveMode(TestVanDammeAnim character, DamageObject damage = null )
        {
            // Character has already died so ignore this repeat death
            if ( character == null || character.name == "dead" )
            {
                return;
            }

            // Character is not actually a player, which usually means they're a character controlled entity like a boondock bro or vehicle
            if (HeroController.players[character.playerNum].character != character)
            {
                return;
            }

            int playerNum = character.playerNum;

            try
            {
                HarmonyPatches.Player_RemoveLife_Patch.allowLifeLoss = true;
                // Manually handle removing lifes since Broforce is terrible at it
                HeroController.players[playerNum].RemoveLife();

                int remainingLives = HeroController.players[playerNum].Lives;

                // Check if all players are dead
                bool livingPlayer = false;
                for (int i = 0; i < 4; ++i)
                {
                    if (i != playerNum)
                    {
                        if (HeroController.PlayerIsAlive(i) && HeroController.players[i].Lives > 0)
                        {
                            livingPlayer = true;
                            break;
                        }
                    }
                    else
                    {
                        if ( remainingLives > 0 )
                        {
                            livingPlayer = true;
                            break;
                        }
                    }
                }
                if ( !livingPlayer)
                {
                    everyoneDead = true;
                    return;
                }

                // Ghost controlled enemy died
                if (character.name == "c")
                {
                    if ( settings.spawnMode == SpawnMode.Automatic)
                    {
                        // Respawn player and allow them to look for a new character to possess
                        findNewEnemyCooldown[playerNum] = settings.automaticallyFindEnemyCooldown;
                        HarmonyPatches.Player_WorkOutSpawnScenario_Patch.forceCheckpointSpawn = true;
                        HeroController.players[playerNum].RespawnBro(false);
                    }
                    else
                    {
                        ghostSpawnPoint[playerNum] = character.transform.position + new Vector3(0f, GhostPlayer.ghostSpawnOffset, 0f);

                        // Leave previous unit
                        LeaveUnit(character, playerNum, true);

                        // Restore previous character if we still have remaining lives, otherwise stay as dead mook
                        HeroController.players[playerNum].character = previousCharacter[playerNum];
                        HidePlayer(playerNum);
                    }
                }
                // Hero player died
                else if (playerNum == currentHeroNum)
                {
                    if ( !anyAttemptingWin )
                    {
                        // Killed by ghost player
                        if (damage != null && damage.damageSender != null && damage.damageSender is TestVanDammeAnim && damage.damageSender.name == "c")
                        {
                            TestVanDammeAnim killer = damage.damageSender as TestVanDammeAnim;
                            ResurrectGhost(killer.playerNum);

                            if (remainingLives > 0)
                            {
                                if (settings.spawnMode == SpawnMode.Automatic)
                                {
                                    // Respawn player and allow them to look for a new character to possess
                                    findNewEnemyCooldown[playerNum] = settings.automaticallyFindEnemyCooldown;
                                    HarmonyPatches.Player_WorkOutSpawnScenario_Patch.forceCheckpointSpawn = true;
                                    HeroController.players[playerNum].RespawnBro(false);
                                }
                                else
                                {
                                    // Only adjust spawn point upwards if the ghost has already spawned
                                    if (currentGhosts[playerNum] != null)
                                    {
                                        ghostSpawnPoint[playerNum] = character.transform.position + new Vector3(0f, GhostPlayer.ghostSpawnOffset);
                                    }
                                    else
                                    {
                                        ghostSpawnPoint[playerNum] = character.transform.position;
                                    }
                                    HeroController.players[playerNum].RespawnBro(false);
                                }
                            }
                        }
                        // Died from other stuff
                        else
                        {
                            List<int> players = new List<int>();
                            for (int i = 0; i < 4; ++i)
                            {
                                if (i != Main.currentHeroNum && HeroController.PlayerIsAlive(i) && HeroController.players[i].Lives > 0)
                                {
                                    players.Add(i);
                                }
                            }

                            // If other ghost players have lives, respawn one of them
                            if (players.Count > 0)
                            {
                                int chosenPlayer = players[UnityEngine.Random.Range(0, players.Count)];

                                ResurrectGhost(chosenPlayer);

                                if (settings.spawnMode == SpawnMode.Automatic)
                                {
                                    // Respawn player and allow them to look for a new character to possess
                                    findNewEnemyCooldown[playerNum] = settings.automaticallyFindEnemyCooldown;
                                    HarmonyPatches.Player_WorkOutSpawnScenario_Patch.forceCheckpointSpawn = true;
                                    HeroController.players[playerNum].RespawnBro(false);
                                }
                                else
                                {
                                    // Only adjust spawn point upwards if the ghost has already spawned
                                    if (currentGhosts[playerNum] != null)
                                    {
                                        ghostSpawnPoint[playerNum] = character.transform.position + new Vector3(0f, GhostPlayer.ghostSpawnOffset);
                                    }
                                    else
                                    {
                                        ghostSpawnPoint[playerNum] = character.transform.position;
                                    }
                                    HeroController.players[playerNum].RespawnBro(false);
                                }
                            }
                            // Otherwise respawn the hero player
                            else
                            {
                                if (remainingLives > 0)
                                {
                                    countdownToRespawn[playerNum] = 1.25f;
                                }
                                else
                                {
                                    GameModeController.LevelFinish(LevelResult.ForcedFail);
                                }
                            }
                        }
                    }
                    // On win attempts, don't let ghosts take control from hero player
                    else
                    {
                        if (remainingLives > 0)
                        {
                            countdownToRespawn[playerNum] = 1.25f;
                        }
                        else
                        {
                            GameModeController.LevelFinish(LevelResult.ForcedFail);
                        }
                    }
                    
                }

                if (character != null)
                {
                    character.name = "dead";
                }
            }
            catch (Exception ex)
            {
                Main.Log("Exception on player death: " + ex.ToString());
            }
        }

        // Turns a character into a ghost, reusing the ghost if it already exists
        public static void HidePlayer(int playerNum, bool fastResurrect = false)
        {
            if (HeroController.players[playerNum].character != null)
            {
                HeroController.players[playerNum].character.gameObject.SetActive(false);
            }

            if ( settings.spawnMode == SpawnMode.Automatic )
            {
                // Set avatar to blank one
                HeroController.players[playerNum].hud.SetAvatar(defaultAvatarMat);

                waitingToBecomeEnemy.Add(playerNum);
            }
            else
            {
                HeroController.players[playerNum].hud.SetAvatar(ghostAvatarMat);

                CreateGhost(playerNum, fastResurrect);
            }
        }

        // Creates a ghost for the player if none exist, or reuses it if it does
        public static void CreateGhost(int playerNum, bool fastResurrect)
        {
            if (currentGhosts[playerNum] == null)
            {
                currentGhosts[playerNum] = UnityEngine.Object.Instantiate<GhostPlayer>(ghostPrefab, Vector3.zero, Quaternion.identity).GetComponent<GhostPlayer>();
                currentGhosts[playerNum].playerNum = playerNum;
                // Ghost isn't spawning at level start so play resurrection animation instead
                if (ghostSpawnPoint[playerNum] != Vector3.zero)
                {
                    currentGhosts[playerNum].overrideSpawnPoint = ghostSpawnPoint[playerNum];
                    ghostSpawnPoint[playerNum] = Vector3.zero;
                    currentGhosts[playerNum].StartResurrecting();
                }
                currentGhosts[playerNum].ReActivate();
            }
            // Update ghost position
            else
            {
                if (ghostSpawnPoint[playerNum] != Vector3.zero)
                {
                    currentGhosts[playerNum].transform.position = ghostSpawnPoint[playerNum];
                    ghostSpawnPoint[playerNum] = Vector3.zero;
                }
                if ( !fastResurrect )
                {
                    currentGhosts[playerNum].StartResurrecting();    
                }
                else
                {
                    currentGhosts[playerNum].frame = 0;
                    currentGhosts[playerNum].SetFrame();
                }
                currentGhosts[playerNum].ReActivate();
            }
        }

        // Turns a ghost into a player, if they are inside an enemy it kills the enemy, if they are in ghost mode it sets their ghost to begin the resurrection process
        public static void ResurrectGhost(int playerNum)
        {
            try
            {
                // Resurrect ghost inside enemy
                if (currentlyEnemy[playerNum])
                {
                    currentHeroNum = playerNum;
                    LeaveUnit(HeroController.players[playerNum].character, playerNum, false, true);
                    SwitchToHeroAvatar(playerNum);
                }
                // Resurrect ghost
                else
                {
                    if ( settings.spawnMode == SpawnMode.Automatic )
                    {
                        currentHeroNum = playerNum;
                        HarmonyPatches.Player_WorkOutSpawnScenario_Patch.forceCheckpointSpawn = true;
                        HeroController.players[playerNum].RespawnBro(false);
                    }
                    else
                    {
                        HeroController.players[playerNum].character.SetXY(currentGhosts[playerNum].transform.position.x, currentGhosts[playerNum].transform.position.y);
                        currentGhosts[playerNum].SetCanReviveCharacter();
                        currentHeroNum = playerNum;
                    }   
                }
            }
            catch ( Exception ex )
            {
                Log("Exception resurrecting ghost: " + ex.ToString());
            }
        }

        // Finds an enemy on the map that is visible and starts controlling it
        public static void FindNewEnemyOnMap(int playerNum)
        {
            try
            {
                if ( playerNum == currentHeroNum )
                {
                    findNewEnemyCooldown[playerNum] = 0f;
                    return;
                }

                for (int i = 0; i < Map.units.Count; ++i)
                {
                    // Find a valid unit to control
                    TestVanDammeAnim character = Map.units[i] as TestVanDammeAnim;
                    if ( AvailableToPossess(character) && SortOfFollow.IsItSortOfVisible(character.transform.position, 5f, 8f))
                    {
                        StartControllingUnit(playerNum, character, false, true, false);
                        return;
                    }
                }

                // Failed to find enemy, waiting for a bit to try again
                findNewEnemyCooldown[playerNum] = 0.5f;
            }
            catch ( Exception e)
            {
                Log("Exception finding new unit: " + e.ToString());
            }
        }

        // Resets the game
        public static void ResetAll()
        {
            ClearVariables();
            ScoreManager.currentScore = new int[] { 0, 0, 0, 0 };
            attemptingWin = -1;
            playerWon = false;
            onWinAttempt = false;
            ScoreManager.requiredScore = new int[] { settings.scoreToWin, settings.scoreToWin, settings.scoreToWin, settings.scoreToWin };
            switchCounter = 0f;
        }
        #endregion
        #endregion
    }
}