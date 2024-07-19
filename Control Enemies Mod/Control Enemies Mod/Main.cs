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

namespace Control_Enemies_Mod
{
    static class Main
    {
        #region UMM
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        public static KeyBindingForPlayers fireBullet;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUpdate = OnUpdate;
            mod = modEntry;
            settings = Settings.Load<Settings>(modEntry);
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
            fireBullet.OnGUI(out _, true);
            GUILayout.Space(10);
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
            try
            {
                if (Input.GetMouseButtonDown(1))
                {
                    Camera camera = (Traverse.Create(typeof(SetResolutionCamera)).Field("mainCamera").GetValue() as Camera);
                    Vector3 newPos = camera.ScreenToWorldPoint(Input.mousePosition);

                    FindUnitToControl(newPos, 0);
                }
            }
            catch { }

            for ( int i = 0; i < 4; ++i )
            {
                fireDelay[i] -= dt;
                if ( fireBullet.IsDown(i) && HeroController.PlayerIsAlive(i) && fireDelay[i] <= 0f )
                {
                    FireBullet(i);
                }
            }
        }

        public static void Log(String str)
        {
            mod.Logger.Log(str);
        }
        #endregion

        #region Modding
        public static List<Unit> currentUnit = new List<Unit>() { null, null, null, null };
        public static MindControlBullet bulletPrefab = null;
        public static List<float> fireDelay = new List<float>() { 0f, 0f, 0f, 0f };

        public static void LoadKeyBinding()
        {
            if (!AllModKeyBindings.TryGetKeyBinding("Control Enemies Mod", "Fire Bullet", out fireBullet))
            {
                fireBullet = new KeyBindingForPlayers("Fire Bullet", "Control Enemies Mod");
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
            if ( bulletPrefab == null )
            {
                // Create Mind Control Bullet
                SetupBullet();
            }
            fireDelay[playerNum] = 0.5f;
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

        public static void StartControllingUnit( int playerNum, Unit unit )
        {
            currentUnit[playerNum] = unit;
            unit.playerNum = playerNum;
            if ( unit is Mook )
            {
                Mook mook = unit as Mook;
                mook.firingPlayerNum = playerNum;
            }
            Traverse.Create(unit).Field("isHero").SetValue(true);
            //UnityEngine.Object.Destroy(unit.enemyAI);
            unit.enemyAI.name = "controlled";
            if ( unit.enemyAIOnChildOrParent != null )
            {
                //UnityEngine.Object.Destroy(unit.enemyAIOnChildOrParent);
            }

            // Release currently controlled unit
            if (HeroController.players[playerNum].character is Mook )
            {
                Mook previous = HeroController.players[playerNum].character as Mook;
                previous.playerNum = -1;
                previous.firingPlayerNum = -1;
                Traverse.Create(unit).Field("isHero").SetValue(true);
                previous.enemyAI.name = "Enemy";
                previous.Stun(3f);
            }
            else
            {
                HeroController.players[playerNum].character.gameObject.SetActive(false);
            }
            HeroController.players[playerNum].character = unit as TestVanDammeAnim;
        }
        #endregion
    }
}