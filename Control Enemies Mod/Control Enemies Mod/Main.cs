using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using RocketLib;
using static NeckBeard.OceanEngine.Server.Config;
using JetBrains.Annotations;
using UnityEngine.Experimental.Rendering;
using static UnityEngine.UI.CanvasScaler;

namespace Control_Enemies_Mod
{
    static class Main
    {
        #region UMM
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;

        public static GUIStyle headerStyle = null;
        public static KeyBindingForPlayers possessEnemy;
        public static KeyBindingForPlayers leaveEnemy;
        public static KeyBindingForPlayers swapEnemiesLeft;
        public static KeyBindingForPlayers swapEnemiesRight;
        public static string[] swapBehaviorList = new string[] { "Kill Enemy", "Stun Enemy", "Delete Enemy", "Do Nothing" };

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
            swapEnemiesLeft.OnGUI(out _, true);
            GUILayout.Space(10);
            swapEnemiesRight.OnGUI(out _, true);

            GUILayout.Space(20);
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

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
/*            try
            {
                if (Input.GetMouseButtonDown(1))
                {
                    Camera camera = (Traverse.Create(typeof(SetResolutionCamera)).Field("mainCamera").GetValue() as Camera);
                    Vector3 newPos = camera.ScreenToWorldPoint(Input.mousePosition);

                    FindUnitToControl(newPos, 0);
                }
            }
            catch { }*/

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
                }
            }
            catch ( Exception ex )
            {
                Main.Log("Exception in update: " + ex.ToString());
            }
        }

        public static void Log(String str)
        {
            mod.Logger.Log(str);
        }
        #endregion

        #region Modding
        public static MindControlBullet bulletPrefab = null;

        public static Unit[] currentUnit = new Unit[] { null, null, null, null };
        public static int[] previousPlayerNum = new int[] { -1, -1, -1, -1 };
        public static float[] fireDelay = new float[] { 0f, 0f, 0f, 0f };
        public static TestVanDammeAnim[] previousCharacter = new TestVanDammeAnim[] { null, null, null, null };
        public static float[] countdownToRespawn = new float[] { 0f, 0f, 0f, 0f };

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
                if (unit != null && unit.playerNum < 0 && unit.health > 0 && !currentUnit.Contains(unit) && !unit.IsHero)
                {
                    // Check unit is in rectangle around trap
                    if (Tools.FastAbsWithinRange(unit.X - center.x, 10f) && Tools.FastAbsWithinRange(unit.Y - center.y, 10f))
                    {
                        StartControllingUnit(playerNum, unit);
                        return;
                    }
                }
            }
        }

        public static void StartControllingUnit( int playerNum, Unit unit, bool gentleLeave = false )
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
                    if (unit.gameObject.HasComponent<DisableWhenOffCamera>())
                    {
                        unit.gameObject.GetComponent<DisableWhenOffCamera>().enabled = false;
                    }
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
                        SwitchUnit(HeroController.players[playerNum].character as TestVanDammeAnim, playerNum, gentleLeave );
                    }
                    // Hide previous character
                    else
                    {
                        HeroController.players[playerNum].character.gameObject.SetActive(false);
                        previousCharacter[playerNum] = HeroController.players[playerNum].character;
                    }

                    HeroController.players[playerNum].character = unit as TestVanDammeAnim;
                }
            }
            catch ( Exception ex )
            {
                Log("Exception controlling unit: " + ex.ToString());
            }
        }

        public static void SwitchUnit(TestVanDammeAnim previous, int playerNum, bool gentleLeave )
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
            if (previous.gameObject.HasComponent<DisableWhenOffCamera>())
            {
                previous.gameObject.GetComponent<DisableWhenOffCamera>().enabled = true;
            }

            if ( !gentleLeave )
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
                if (previous.gameObject.HasComponent<DisableWhenOffCamera>())
                {
                    previous.gameObject.GetComponent<DisableWhenOffCamera>().enabled = true;
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
            }
            else
            {
                previousCharacter[playerNum] = null;
            }
        }

        #endregion
    }
}