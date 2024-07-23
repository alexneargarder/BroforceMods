using BroMakerLib.Loaders;
using HarmonyLib;
using RocketLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using BSett = BroMakerLib.Settings;
using RocketLib.Utils;
using System.IO;

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
        public static GUIStyle headerStyle, buttonStyle, warningStyle;
        public static bool changingEnabledUnits = false;
        public static bool creatingUnitList = false;
        public static float displayWarningTime = 0f;
        public static string[] swapBehaviorList = new string[] { "Kill Enemy", "Stun Enemy", "Delete Enemy", "Do Nothing" };
        public static string[] fullUnitList = new string[]
        {
            // Normal
            "Mook", "Suicide Mook", "Bruiser", "Suicide Bruiser", "Strong Bruiser", "Elite Bruiser", "Scout Mook", "Riot Shield Mook", "Mech", "Brown Mech", "Jetpack Mook", "Grenadier Mook", "Bazooka Mook", "Jetpack Bazooka Mook", "Ninja Mook",
            "Treasure Mook", "Attack Dog", "Skinned Mook", "Mook General", "Alarmist", "Strong Mook", "Scientist Mook", "Snake", "Satan", 
            // Aliens
            "Facehugger", "Xenomorph", "Brute", "Screecher", "Baneling", "Xenomorph Brainbox",
            // Hell
            "Hellhound", "Undead Mook", "Undead Mook (Start Dead)", "Warlock", "Boomer", "Undead Suicide Mook", "Executioner", "Lost Soul", "Soul Catcher",
            "Satan Miniboss", "CR666", "Pig", "Rotten Pig", "Villager"
        };
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
            settings.allowWallClimbing = GUILayout.Toggle(settings.allowWallClimbing, new GUIContent("Enable Wall Climbing", "By default, enemies can't fully wall climb. If you enable this they will be able to, but they won't have animations"));

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 500;

            settings.disableFallDamage = GUILayout.Toggle(settings.disableFallDamage, new GUIContent("Disable Fall Damage", "Disables fall damage for controlled enemies."));

            settings.disableTaunting = GUILayout.Toggle(settings.disableTaunting, new GUIContent("Disable Taunting", "Disables taunting for controlled enemies. They don't have animations so most enemies go invisible if you taunt."));

            settings.enableSprinting = GUILayout.Toggle(settings.enableSprinting, new GUIContent("Enable Sprinting", "Allows controlled enemies to sprint."));

            GUI.Label(lastRect, GUI.tooltip);
            previousToolTip = GUI.tooltip;

            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            special2.OnGUI(out _, true, true, ref previousToolTip);

            GUILayout.Space(10);

            special3.OnGUI(out _, true, true, ref previousToolTip);
        }

        static void ShowPossessionModeOptions(UnityModManager.ModEntry modEntry, ref string previousToolTip )
        {
            GUILayout.BeginHorizontal();
            settings.possessionModeEnabled = GUILayout.Toggle(settings.possessionModeEnabled, "Enable Possessing Enemies");
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 500;

            settings.loseLifeOnDeath = GUILayout.Toggle(settings.loseLifeOnDeath, new GUIContent("Lose Life On Death", "Removes a life when you die while controlling an enemy"));

            settings.loseLifeOnSwitch = GUILayout.Toggle(settings.loseLifeOnSwitch, new GUIContent("Lose Life On Switch", "Removes a life when you switch from one enemy to another"));

            settings.respawnFromCorpse = GUILayout.Toggle(settings.respawnFromCorpse, new GUIContent("Respawn from corpse", "Respawn at the corpse of the enemy you were controlling when you die as them"));

            if ( GUI.tooltip != previousToolTip )
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            // Display keybinding options
            GUI.tooltip = string.Empty;
            possessEnemy.OnGUI(out _, true);
            GUILayout.Space(10);
            GUI.tooltip = string.Empty;
            leaveEnemy.OnGUI(out _, true);
            GUI.tooltip = string.Empty;
            previousToolTip = GUI.tooltip;

            GUILayout.Space(25);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("When Leaving a Controlled Enemy:", GUILayout.Width(225));
            settings.leavingEnemy = (SwapBehavior)GUILayout.SelectionGrid((int)settings.leavingEnemy, swapBehaviorList, 3);
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            GUILayout.Label("When Swapping to a New Enemy:", GUILayout.Width(225));
            settings.swappingEnemies = (SwapBehavior)GUILayout.SelectionGrid((int)settings.swappingEnemies, swapBehaviorList, 3);
            GUILayout.EndHorizontal();

            GUILayout.Space(25);

            GUILayout.BeginHorizontal();
            GUILayout.Label(String.Format("Cooldown Between Swaps: {0:0.00}s", settings.swapCooldown), GUILayout.Width(225), GUILayout.ExpandWidth(false));
            settings.swapCooldown = GUILayout.HorizontalSlider(settings.swapCooldown, 0, 2);
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            GUILayout.Label(String.Format("Mind Control Bullet Firerate: {0:0.00}s", settings.fireRate), GUILayout.Width(225), GUILayout.ExpandWidth(false));
            settings.fireRate = GUILayout.HorizontalSlider(settings.fireRate, 0, 2);
            GUILayout.EndHorizontal();
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
            settings.spawnAsEnemyEnabled = GUILayout.Toggle(settings.spawnAsEnemyEnabled, "Enable Spawning as Enemies");
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 500;

            settings.filterEnemies = GUILayout.Toggle(settings.filterEnemies, new GUIContent("Filter Enemies", "Limits the enemies you can spawn as to the enabled ones"));

            settings.alwaysChosen = GUILayout.Toggle(settings.alwaysChosen, new GUIContent("Always Spawn as Chosen Enemy"));

            settings.clickingSwapEnabled = GUILayout.Toggle(settings.clickingSwapEnabled, new GUIContent("Swap On Click", "Swaps to a new enemy when you click one in the menu"));

            if (GUI.tooltip != previousToolTip)
            {
                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Chance to Spawn as an Enemy: " + settings.spawnAsEnemyChance.ToString("0.00") + "%", GUILayout.Width(225), GUILayout.ExpandWidth(false));
            settings.spawnAsEnemyChance = GUILayout.HorizontalSlider(settings.spawnAsEnemyChance, 0, 100);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label(String.Format("Cooldown Between Swaps: {0:0.00}s", settings.spawnSwapCooldown), GUILayout.Width(225), GUILayout.ExpandWidth(false));
            settings.spawnSwapCooldown = GUILayout.HorizontalSlider(settings.spawnSwapCooldown, 0, 2);
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

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

                    if (GUILayout.Button(new GUIContent(changingEnabledUnits ? "Save Changes" : "Enter Filtering Mode",
                                               "Enable or disable enemies for this player"), GUILayout.ExpandWidth(false), GUILayout.Width(300)))
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
                            // Check that at least one bro is enabled
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
        public static MindControlBullet bulletPrefab = null;

        public static List<Unit> currentUnit = new List<Unit>() { null, null, null, null };
        public static bool[] currentlyEnemy = { false, false, false, false };
        public static int[] previousPlayerNum = new int[] { -1, -1, -1, -1 };
        public static float[] fireDelay = new float[] { 0f, 0f, 0f, 0f };
        public static TestVanDammeAnim[] previousCharacter = new TestVanDammeAnim[] { null, null, null, null };
        public static float[] countdownToRespawn = new float[] { 0f, 0f, 0f, 0f };

        public static bool[] switched = { false, false, false, false };
        public static float[] currentSpawnCooldown = { 0f, 0f, 0f, 0f };
        public static Player.SpawnType[] previousSpawnInfo = { Player.SpawnType.Unknown, Player.SpawnType.Unknown, Player.SpawnType.Unknown, Player.SpawnType.Unknown };
        public static bool[] willReplaceBro = { false, false, false, false };
        public static bool[] wasFirstDeployment = { false, false, false, false };

        public static Material defaultAvatarMat;

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            try
            {
                for (int i = 0; i < 4; ++i)
                {
                    fireDelay[i] -= dt;
                    if (possessEnemy.IsDown(i) && HeroController.PlayerIsAlive(i) && fireDelay[i] <= 0f)
                    {
                        FireBullet(i);
                    }

                    if (leaveEnemy.IsDown(i) && HeroController.PlayerIsAlive(i) && previousCharacter[i] != null && !previousCharacter[i].destroyed)
                    {
                        LeaveUnit(HeroController.players[i].character, i, false);
                    }

                    // Check if any characters are in the process of respawning from a killed enemy
                    if (countdownToRespawn[i] > 0f)
                    {
                        countdownToRespawn[i] -= dt;

                        if (countdownToRespawn[i] <= 0f)
                        {
                            if (HeroController.players[i].character != null && !HeroController.players[i].character.destroyed)
                            {
                                LeaveUnit(HeroController.players[i].character, i, false, true);
                            }
                        }
                    }

                    currentSpawnCooldown[i] -= dt;
                }
            }
            catch (Exception ex)
            {
                Main.Log("Exception in update: " + ex.ToString());
            }
        }

        public static void LoadKeyBinding()
        {
            if (!AllModKeyBindings.TryGetKeyBinding("Control Enemies Mod", "Possess Enemy Key", out possessEnemy))
            {
                possessEnemy = new KeyBindingForPlayers("Possess Enemy Key", "Control Enemies Mod");
            }
            if (!AllModKeyBindings.TryGetKeyBinding("Control Enemies Mod", "Leave Enemy Key", out leaveEnemy))
            {
                leaveEnemy = new KeyBindingForPlayers("Leave Enemy Key", "Control Enemies Mod");
            }
            if (!AllModKeyBindings.TryGetKeyBinding("Control Enemies Mod", "Swap Enemies Left Key", out swapEnemiesLeft))
            {
                swapEnemiesLeft = new KeyBindingForPlayers("Swap Enemies Left Key", "Control Enemies Mod");
            }
            if (!AllModKeyBindings.TryGetKeyBinding("Control Enemies Mod", "Swap Enemies Right Key", out swapEnemiesRight))
            {
                swapEnemiesRight = new KeyBindingForPlayers("Swap Enemies Right Key", "Control Enemies Mod");
            }
            if (!AllModKeyBindings.TryGetKeyBinding("Control Enemies Mod", "Special 2 Key", out special2))
            {
                special2 = new KeyBindingForPlayers("Special 2 Key", "Control Enemies Mod");
            }
            if (!AllModKeyBindings.TryGetKeyBinding("Control Enemies Mod", "Special 3 Key", out special3))
            {
                special3 = new KeyBindingForPlayers("Special 3 Key", "Control Enemies Mod");
            }
        }

        public static void LoadSprites()
        {
            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            directoryPath = Path.Combine(directoryPath, "sprites");


            defaultAvatarMat = ResourcesController.GetMaterial(directoryPath, "defaultAvatar.png");
        }

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
            catch ( Exception ex )
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
            if ( bulletPrefab == null )
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

        public static void FindUnitToControl( Vector3 center, int playerNum )
        {
            for (int i = 0; i < Map.units.Count; ++i)
            {
                Unit unit = Map.units[i];
                // Check that unit is not null, is not a player, is not dead, and is not already grabbed by this trap or another
                if (unit != null && unit is TestVanDammeAnim && unit.playerNum < 0 && unit.health > 0 && !currentUnit.Contains(unit) && !unit.IsHero)
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

        public static void StartControllingUnit( int playerNum, TestVanDammeAnim unit, bool gentleLeave = false, bool savePreviousCharacter = true, bool erasePreviousCharacter = false )
        {
            try
            {
                if ( HeroController.players[playerNum].character != null && HeroController.players[playerNum].IsAlive() && unit != null && unit is TestVanDammeAnim && unit.IsAlive())
                {
                    fireDelay[playerNum] = settings.swapCooldown;
                    currentUnit[playerNum] = unit;
                    unit.playerNum = playerNum;
                    Traverse.Create(unit).Field("isHero").SetValue(true);
                    unit.name = "controlled";
                    DisableWhenOffCamera disableWhenOffCamera = unit.gameObject.GetComponent<DisableWhenOffCamera>();
                    if ( disableWhenOffCamera != null )
                    {
                        disableWhenOffCamera.enabled = false;
                    }
                    unit.enabled = true;
                    if ( unit.enemyAI != null )
                    {
                        unit.enemyAI.enabled = true;
                    }
                    unit.SpecialAmmo = 0;
                    if (unit is Mook)
                    {
                        Mook mook = unit as Mook;
                        mook.firingPlayerNum = playerNum;
                        mook.canWallClimb = settings.allowWallClimbing;
                        mook.canDash = settings.enableSprinting;
                    }

                    // Release currently controlled unit, previousCharacter not being null indicates that we have a bro in storage
                    if (previousCharacter[playerNum] != null && !previousCharacter[playerNum].destroyed && previousCharacter[playerNum].IsAlive() && !(HeroController.players[playerNum].character is BroBase) )
                    {
                        SwitchUnit(HeroController.players[playerNum].character as TestVanDammeAnim, playerNum, gentleLeave, erasePreviousCharacter );
                    }
                    // Hide previous character
                    else
                    {
                        HeroController.players[playerNum].character.gameObject.SetActive(false);
                        if ( savePreviousCharacter )
                        {
                            previousCharacter[playerNum] = HeroController.players[playerNum].character;
                        }
                        else
                        {
                            previousCharacter[playerNum] = null;
                        }
                    }

                    // Set avatar to blank one
                    HeroController.players[playerNum].hud.SetAvatar(defaultAvatarMat);

                    HeroController.players[playerNum].character = unit as TestVanDammeAnim;
                    Main.currentlyEnemy[playerNum] = true;
                }
            }
            catch ( Exception ex )
            {
                Log("Exception controlling unit: " + ex.ToString());
            }
        }

        public static void ReaffirmControl( int playerNum )
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
                mook.canWallClimb = Main.settings.allowWallClimbing;
                mook.canDash = Main.settings.enableSprinting;
            }
        }

        public static void SwitchUnit(TestVanDammeAnim previous, int playerNum, bool gentleLeave, bool erasePreviousCharacter )
        {   
            previous.playerNum = previousPlayerNum[playerNum];
            if ( previous is Mook )
            {
                Mook previousMook = previous as Mook;
                previousMook.firingPlayerNum = previousPlayerNum[playerNum];
            }
            Traverse.Create(previous).Field("isHero").SetValue(true);
            previous.name = "Enemy";
            previous.canWallClimb = false;
            previous.canDash = false;
            DisableWhenOffCamera disableWhenOffCamera = previous.gameObject.GetComponent<DisableWhenOffCamera>();
            if (disableWhenOffCamera != null)
            {
                disableWhenOffCamera.enabled = true;
            }

            if ( erasePreviousCharacter )
            {
                UnityEngine.Object.Destroy(previous.gameObject);
            }
            else if ( !gentleLeave )
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

        public static void LeaveUnit(TestVanDammeAnim previous, int playerNum, bool onlyLeaveUnit, bool respawning = false )
        {
            if (previousCharacter[playerNum] != null && !previousCharacter[playerNum].destroyed && previousCharacter[playerNum].IsAlive() && !(previous is BroBase) )
            {
                previous.playerNum = previousPlayerNum[playerNum];
                if (previous is Mook)
                {
                    Mook previousMook = previous as Mook;
                    previousMook.firingPlayerNum = previousPlayerNum[playerNum];
                }
                Traverse.Create(previous).Field("isHero").SetValue(true);
                previous.name = "Enemy";
                previous.canWallClimb = false;
                previous.canDash = false;
                DisableWhenOffCamera disableWhenOffCamera = previous.gameObject.GetComponent<DisableWhenOffCamera>();
                if (disableWhenOffCamera != null)
                {
                    disableWhenOffCamera.enabled = true;
                }

                if (!onlyLeaveUnit)
                {
                    TestVanDammeAnim originalCharacter = previousCharacter[playerNum];
                    HeroController.players[playerNum].character = originalCharacter;
                    originalCharacter.X = previous.X;
                    originalCharacter.Y = previous.Y;
                    originalCharacter.transform.localScale = new Vector3(Mathf.Sign(previous.transform.localScale.x) * originalCharacter.transform.localScale.x, originalCharacter.transform.localScale.y, originalCharacter.transform.localScale.z);
                    originalCharacter.xI = previous.xI;
                    originalCharacter.yI = previous.yI;

                    if (settings.loseLifeOnSwitch)
                    {
                        HeroController.players[playerNum].RemoveLife();
                    }

                    if (!respawning)
                    {
                        switch (settings.leavingEnemy)
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
                    else
                    {
                        if (previous is Mook)
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
                }

                previousCharacter[playerNum] = null;
                currentlyEnemy[playerNum] = false;
            }
            else
            {
                previousCharacter[playerNum] = null;
            }
        }

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

        public static string GetSelectedUnit(int playerNum)
        {
            if (settings.selGridInt[playerNum] >= 0 && settings.selGridInt[playerNum] < currentUnitList.Length)
            {
                return currentUnitList[settings.selGridInt[playerNum]];
            }
            else
            {
                return currentUnitList[0];
            }
        }

        public static GameObject SpawnUnit(string unit, Vector3 vector)
        {
            TestVanDammeAnim original = null;
            GameObject __result = null;

            switch (unit)
            {
                case "Mook":
                    original = Map.Instance.activeTheme.mook;
                    break;
                case "Suicide Mook":
                    original = Map.Instance.activeTheme.mookSuicide;
                    break;
                case "Bruiser":
                    original = Map.Instance.activeTheme.mookBigGuy;
                    break;
                case "Suicide Bruiser":
                    original = Map.Instance.activeTheme.mookSuicideBigGuy;
                    break;
                case "Strong Bruiser":
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.mookBigGuyStrong, vector, Quaternion.identity).gameObject;
                    break;
                case "Elite Bruiser":
                    original = Map.Instance.activeTheme.mookBigGuyElite;
                    break;
                case "Scout Mook":
                    original = Map.Instance.activeTheme.mookScout;
                    break;
                case "Riot Shield Mook":
                    original = Map.Instance.activeTheme.mookRiotShield;
                    break;
                case "Mech":
                    original = Map.Instance.activeTheme.mookArmoured;
                    break;
                case "Brown Mech":
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.mechBrown, vector, Quaternion.identity).gameObject;
                    break;
                case "Jetpack Mook":
                    original = Map.Instance.sharedObjectsReference.Asset.mookJetpack;
                    break;
                case "Grenadier Mook":
                    original = Map.Instance.activeTheme.mookGrenadier;
                    break;
                case "Bazooka Mook":
                    original = Map.Instance.activeTheme.mookBazooka;
                    break;
                case "Jetpack Bazooka Mook":
                    original = Map.Instance.activeTheme.mookJetpackBazooka;
                    break;
                case "Ninja Mook":
                    original = Map.Instance.activeTheme.mookNinja;
                    break;
                case "Treasure Mook":
                    __result = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.treasureMook, vector, Quaternion.identity).gameObject;
                    break;
                case "Attack Dog":
                    original = Map.Instance.activeTheme.mookDog;
                    break;
                case "Skinned Mook":
                    original = Map.Instance.activeTheme.skinnedMook;
                    break;
                case "Mook General":
                    original = Map.Instance.activeTheme.mookGeneral;
                    break;
                case "Alarmist":
                    original = Map.Instance.activeTheme.mookAlarmist;
                    break;
                case "Strong Mook":
                    original = Map.Instance.activeTheme.mookStrong;
                    break;
                case "Scientist Mook":
                    original = Map.Instance.activeTheme.mookScientist;
                    break;
                case "Snake":
                    original = Map.Instance.activeTheme.snake;
                    break;
                // Satan
                case "Satan":
                    original = Map.Instance.activeTheme.satan;
                    break;
                // Aliens
                case "Facehugger":
                    original = Map.Instance.activeTheme.alienFaceHugger;
                    break;
                case "Xenomorph":
                    original = Map.Instance.activeTheme.alienXenomorph;
                    break;
                case "Brute":
                    original = Map.Instance.activeTheme.alienBrute;
                    break;
                case "Screecher":
                    original = Map.Instance.activeTheme.alienBaneling;
                    break;
                case "Baneling":
                    original = Map.Instance.activeTheme.alienMosquito;
                    break;
                case "Xenomorph Brainbox":
                    original = Map.Instance.activeTheme.mookXenomorphBrainbox;
                    break;
                // HellDog
                case "Hellhound":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[0], vector, Quaternion.identity);
                    break;
                // ZMookUndead
                case "Undead Mook":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[1], vector, Quaternion.identity);
                    break;
                // ZMookUndeadStartDead
                case "Undead Mook (Start Dead)":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[2], vector, Quaternion.identity);
                    break;
                // ZMookWarlock
                case "Warlock":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[3], vector, Quaternion.identity);
                    break;
                // ZMookHellBoomer
                case "Boomer":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[4], vector, Quaternion.identity);
                    break;
                // ZMookUndeadSuicide
                case "Undead Suicide Mook":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[5], vector, Quaternion.identity);
                    break;
                // ZHellBigGuy
                case "Executioner":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[6], vector, Quaternion.identity);
                    break;
                // Lost Soul
                case "Lost Soul":
                    vector.y += 5;
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[8], vector, Quaternion.identity);
                    break;
                // ZMookHellSoulCatcher
                case "Soul Catcher":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.sharedObjectsReference.Asset.hellEnemies[10], vector, Quaternion.identity);
                    break;
                case "Satan Miniboss":
                    SatanMiniboss satanMiniboss = UnityEngine.Object.Instantiate<Unit>(Map.Instance.sharedObjectsReference.Asset.satanMiniboss, vector, Quaternion.identity) as SatanMiniboss;
                    if (satanMiniboss != null)
                    {
                        __result = satanMiniboss.gameObject;
                    }
                    break;
                case "CR666":
                    __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(Map.Instance.activeTheme.mookDolfLundgren, vector, Quaternion.identity).gameObject;
                    break;
                case "Pig":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.activeTheme.animals[0], vector, Quaternion.identity).gameObject;
                    break;
                case "Rotten Pig":
                    __result = UnityEngine.Object.Instantiate<GameObject>(Map.Instance.activeTheme.animals[2], vector, Quaternion.identity).gameObject;
                    break;
                case "Villager":
                    __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(Map.Instance.activeTheme.villager1[UnityEngine.Random.Range(0, 1)], vector, Quaternion.identity).gameObject;
                    break;
            }

            if (original != null)
            {
                __result = UnityEngine.Object.Instantiate<TestVanDammeAnim>(original, vector, Quaternion.identity).gameObject;
            }

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
            if ( isBroMakerInstalled )
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
        #endregion
    }
}