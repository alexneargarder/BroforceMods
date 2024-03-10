using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using BSett = BroMakerLib.Settings;
using BroMakerLib.Storages;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loaders;

namespace Swap_Bros_Mod
{
    public enum DPAD
    {
        LEFT,
        RIGHT,
        UP,
        DOWN,
        NONE
    }
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static float cooldown = 0;
        public static Settings settings;
        public static int currentCharIndex = 0;
        public static bool[] switched = { false, false, false, false };
        public static bool manualSpawn = false;
        public static bool creatingBroList = false;

        public static List<string> allNormal = new List<string> { "Rambro", "Brommando", "B. A. Broracus", "Brodell Walker", "Bro Hard", "MacBrover", "Brade", "Bro Dredd", "Bro In Black", "Snake Broskin", "Brominator",
                "Brobocop", "Indiana Brones", "Ash Brolliams", "Mr. Anderbro", "The Boondock Bros", "Brochete", "Bronan the Brobarian", "Ellen Ripbro", "Time Bro", "Broniversal Soldier",
                "Colonel James Broddock", "Cherry Broling", "Bro Max", "The Brode", "Double Bro Seven", "The Brodator", "The Brocketeer", "Broheart", "The Brofessional", "Broden",
                "The Brolander", "Dirty Brory", "Tank Bro", "Bro Lee", "Seth Brondle", "Xebro", "Desperabro", "Broffy the Vampire Slayer", "Burt Brommer", "Demolition Bro" };
        public static List<string> allExpendabros = new List<string> {"Broney Ross", "Lee Broxmas", "Bronnar Jensen", "Bro Caesar", "Trent Broser", "Broctor Death", "Toll Broad"};
        public static List<string> allCustomBros = new List<string>();
        public static int numCustomBros = 0;

        public static List<string> currentBroList;
        public static string[] broList;
        public static string[] previousSelection = new string[] { "", "", "", "" };
        public static int maxBroNum = 40;
        public static int maxBroNumWithoutCustom = 40;
        public static bool isHardcore = false;

    static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = Settings.Load<Settings>(modEntry);
            // Update selGridInt to an array
            if ( settings.selGridInt.Length < 4 )
            {
                settings.selGridInt = new int[] { 0, 0, 0, 0 };
            }
            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            mod = modEntry;
            try
            {
                harmony.PatchAll(assembly);
            }
            catch (Exception e)
            {
                mod.Logger.Error(e.ToString());
            }

            if ( Main.settings.enableBromaker )
            {
                try
                {
                    LoadCustomBros();
                }
                catch (Exception ex)
                {
                    Main.Log("BroMaker is not installed.");
                    Main.settings.enableBromaker = false;
                }
            }

            int[] previousSelGridInts = new int[4];
            settings.selGridInt.CopyTo(previousSelGridInts, 0);
            CreateBroList();
            settings.selGridInt = previousSelGridInts;
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (isHardcore != GameModeController.IsHardcoreMode)
            {
                isHardcore = GameModeController.IsHardcoreMode;
                if (!settings.ignoreCurrentUnlocked)
                {
                    CreateBroList();
                }
            }

            if (settings.enableBromaker && CustomCountChanged() && !(GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked))
            {
                LoadCustomBros();
                CreateBroList();
            }

            GUILayout.BeginHorizontal();


            GUILayout.BeginVertical();

            settings.clickingEnabled = GUILayout.Toggle(settings.clickingEnabled, new GUIContent("Clicking a new bro causes swap",
                "Clicking a bro in the menu below will swap you to that bro immediately"), GUILayout.ExpandWidth(false));

            settings.disableConfirm = GUILayout.Toggle(settings.disableConfirm, new GUIContent("Fix mod window disappearing",
                "Disables confirmation screen when restarting or returning to map/menu"), GUILayout.ExpandWidth(false));

            // Display the tooltip from the element that has mouseover or keyboard focus
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 300;
            GUI.Label(lastRect, GUI.tooltip);

            GUILayout.EndVertical();


            GUILayout.BeginVertical();

            settings.alwaysChosen = GUILayout.Toggle(settings.alwaysChosen, new GUIContent("Always spawn as chosen bro",
                "Prevents restarting a level or collecting a new bro from swapping to a new character"), GUILayout.ExpandWidth(false));

            bool previousSetting = settings.ignoreCurrentUnlocked;
            if (settings.ignoreCurrentUnlocked)
            {
                settings.ignoreCurrentUnlocked = GUILayout.Toggle(settings.ignoreCurrentUnlocked, new GUIContent("Ignore currently unlocked bros in IronBro",
                    "Enabled: In IronBro you can switch to any bro regardless of who you have collected"), GUILayout.ExpandWidth(false));
            }
            else
            {
                settings.ignoreCurrentUnlocked = GUILayout.Toggle(settings.ignoreCurrentUnlocked, new GUIContent("Ignore currently unlocked bros in IronBro",
                    "Disabled: In IronBro you can only switch to bros you have collected"), GUILayout.ExpandWidth(false));
            }
            if (previousSetting != settings.ignoreCurrentUnlocked && isHardcore)
            {
                CreateBroList();
            }

            GUI.Label(lastRect, GUI.tooltip);

            GUILayout.EndVertical();


            GUILayout.BeginVertical();

            if (settings.includeUnfinishedCharacters != (settings.includeUnfinishedCharacters = GUILayout.Toggle(settings.includeUnfinishedCharacters, new GUIContent("Include Exependabro bros",
                "Include bros from Expendabros"), GUILayout.ExpandWidth(false))))
            {
                CreateBroList();
            }

            if (settings.enableBromaker != (settings.enableBromaker = GUILayout.Toggle(settings.enableBromaker, new GUIContent("Include BroMaker Bros",
                "Shows custom bros installed with BroMaker"), GUILayout.ExpandWidth(false))))
            {
                if (settings.enableBromaker)
                {
                    try
                    {
                        LoadCustomBros();
                        CreateBroList();
                    }
                    catch (Exception ex)
                    {
                        Main.Log("BroMaker is not installed.");
                        Main.settings.enableBromaker = false;
                    }
                }
                else
                {
                    CreateBroList();
                }
            }

            GUI.Label(lastRect, GUI.tooltip);
            string previousToolTip = GUI.tooltip;

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(30);

            GUILayout.BeginHorizontal();

            GUILayout.Label(String.Format("Cooldown between swaps: {0:0.00}s", Main.settings.swapCoolDown), GUILayout.Width(225), GUILayout.ExpandWidth(false));
            Main.settings.swapCoolDown = GUILayout.HorizontalSlider(Main.settings.swapCoolDown, 0, 2);

            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            for (int i = 0; i < 4; ++i)
            {
                if (GUILayout.Button("Player " + (i + 1) + " Options - " + (settings.showSettings[i] ? "Hide" : "Show")))
                {
                    settings.showSettings[i] = !settings.showSettings[i];
                }
                if (settings.showSettings[i])
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(
                        new GUIContent("Swap Bro Left: " + (settings.swapLeftKeys[i].waitingForInput ? "Press Any Key/Button" : (settings.swapLeftKeys[i].DPADKey == DPAD.NONE ? settings.swapLeftKeys[i].kc.ToString() : "DPAD " + settings.swapLeftKeys[i].DPADString)),
                        "Set a key for swapping bros, or press Esc to unbind")
                        ) && !InputReader.IsBlocked)
                    {
                        settings.swapLeftKeys[i].waitingForInput = true;
                        UnityModManager.UI.Instance.StartCoroutine(BindKey(settings.swapLeftKeys[i], i));
                    }
                    lastRect = GUILayoutUtility.GetLastRect();
                    lastRect.y += 20;
                    lastRect.width += 300;
                    if (!GUI.tooltip.Equals(previousToolTip))
                    {
                        GUI.Label(lastRect, GUI.tooltip);
                    }
                    previousToolTip = GUI.tooltip;

                    if (GUILayout.Button(
                        new GUIContent("Swap Bro Right: " + (settings.swapRightKeys[i].waitingForInput ? "Press Any Key/Button" : (settings.swapRightKeys[i].DPADKey == DPAD.NONE ? settings.swapRightKeys[i].kc.ToString() : "DPAD " + settings.swapRightKeys[i].DPADString)),
                        "Set a key for swapping bros, or press Esc to unbind")
                        ) && !InputReader.IsBlocked)
                    {
                        settings.swapRightKeys[i].waitingForInput = true;
                        UnityModManager.UI.Instance.StartCoroutine(BindKey(settings.swapRightKeys[i], i));
                    }
                    if (!GUI.tooltip.Equals(previousToolTip))
                    {
                        GUI.Label(lastRect, GUI.tooltip);
                    }
                    previousToolTip = GUI.tooltip;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(25);
                    GUILayout.BeginHorizontal();

                    if (!creatingBroList)
                    {
                        if (settings.selGridInt[i] < 0 || settings.selGridInt[i] >= broList.Length)
                        {
                            settings.selGridInt[i] = 0;
                        }

                        if (settings.selGridInt[i] <= maxBroNum)
                        {
                            if (settings.clickingEnabled)
                            {
                                if (settings.selGridInt[i] != (settings.selGridInt[i] = GUILayout.SelectionGrid(settings.selGridInt[i], broList, 5, GUILayout.Height(30 * Mathf.Ceil(broList.Length / 5.0f)))))
                                {
                                    switched[i] = true;
                                }
                            }
                            else
                            {
                                settings.selGridInt[i] = GUILayout.SelectionGrid(settings.selGridInt[i], broList, 5, GUILayout.Height(30 * Mathf.Ceil(broList.Length / 5.0f)));
                            }
                        }
                        else
                        {
                            CreateBroList();
                        }
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        public static string[] axis6 = { "Joy1 Axis 6", "Joy2 Axis 6", "Joy3 Axis 6", "Joy4 Axis 6" };
        public static string[] axis7 = { "Joy1 Axis 7", "Joy2 Axis 7", "Joy3 Axis 7", "Joy4 Axis 7" };
        public static void CheckDPADDirection( ref KeyBind kb )
        {
            for ( int i = 0; i < 4; ++i )
            {
                float x = Input.GetAxis( axis6[i] );
                float y = Input.GetAxis( axis7[i] );

                if (x < -0.8)
                {
                    kb.DPADString = "Joy" + (i + 1) + " LEFT";
                    kb.DPADKey = DPAD.LEFT;
                    kb.joystick = "Joy" + (i + 1) + " Axis 6";
                    return;
                }
                else if (x > 0.8)
                {
                    kb.DPADString = "Joy" + (i + 1) + " RIGHT";
                    kb.DPADKey = DPAD.RIGHT;
                    kb.joystick = "Joy" + (i + 1) + " Axis 6";
                    return;
                }
                else if (y < -0.8)
                {
                    kb.DPADString = "Joy" + (i + 1) + " DOWN";
                    kb.DPADKey = DPAD.DOWN;
                    kb.joystick = "Joy" + (i + 1) + " Axis 7";
                    return;
                }
                else if (y > 0.8)
                {
                    kb.DPADString = "Joy" + (i + 1) + " UP";
                    kb.DPADKey = DPAD.UP;
                    kb.joystick = "Joy" + (i + 1) + " Axis 7";
                    return;
                }
            }
            kb.DPADString = "NONE";
            kb.DPADKey = DPAD.NONE;
            kb.joystick = "NONE";
        }

        private static IEnumerator BindKey(KeyBind kb, int player)
        {
            InputReader.IsBlocked = true;
            yield return new WaitForSeconds(0.2f);
            KeyCode[] keyCodes = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().ToArray();
            bool exit = false;
            while (!exit)
            {
                foreach (KeyCode keyCode in keyCodes)
                {
                    if (Input.GetKeyUp(keyCode))
                    {
                        if (keyCode == KeyCode.Delete)
                        {
                            kb.kc = KeyCode.None; 
                        }
                        else
                        {
                            kb.kc = keyCode;
                        }
                        kb.waitingForInput = false;
                        kb.DPADKey = DPAD.NONE;
                        exit = true;
                    }
                }
                CheckDPADDirection(ref kb);
                if ( kb.DPADKey != DPAD.NONE )
                {
                    kb.kc = KeyCode.None;
                    kb.waitingForInput = false;
                    exit = true;
                }
                yield return null;
            }
            InputReader.IsBlocked = false;
        }

        public static bool wasKeyPressed( KeyBind kb )
        {
            if ( kb.kc != KeyCode.None )
            {
                return Input.GetKeyUp(kb.kc);
            }
            else if ( kb.DPADKey != DPAD.NONE )
            {
                float x = Input.GetAxis( kb.joystick );

                return (x < -0.8 && (kb.DPADKey == DPAD.LEFT || kb.DPADKey == DPAD.DOWN)) || (x > 0.8 && (kb.DPADKey == DPAD.RIGHT || kb.DPADKey == DPAD.DOWN));
            }
            return false;
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

        public static void CreateBroList()
        {
            creatingBroList = true;
            if ( currentBroList != null )
            {
                for ( int i = 0; i < 4; ++i )
                {
                    if ( settings.selGridInt[i] > 0 && settings.selGridInt[i] < currentBroList.Count() )
                    {
                        previousSelection[i] = currentBroList[settings.selGridInt[i]];
                    }
                }
            }
            // If in IronBro and not ignoring unlocked characters, only show unlocked ones
            if ( GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked )
            {
                currentBroList = new List<string>();
                for ( int i = 0; i < GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count(); ++i )
                {
                    currentBroList.Add(allNormal[HeroTypeToInt(GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros[i])]);
                }
                maxBroNumWithoutCustom = currentBroList.Count() - 1;

                if ( settings.enableBromaker )
                {
                    LoadCustomBros();
                    currentBroList.AddRange(allCustomBros);
                }
            }
            else
            {
                currentBroList = new List<string>();
                currentBroList.AddRange(allNormal);

                if (settings.includeUnfinishedCharacters)
                {
                    currentBroList.AddRange(allExpendabros);
                }
                maxBroNumWithoutCustom = currentBroList.Count() - 1;
                if (settings.enableBromaker)
                {
                    currentBroList.AddRange(allCustomBros);
                }
            }

            maxBroNum = currentBroList.Count() - 1;
            broList = currentBroList.ToArray();
            for (int i = 0; i < 4; ++i)
            {
                settings.selGridInt[i] = currentBroList.IndexOf(previousSelection[i]);
                if ( settings.selGridInt[i] == -1 )
                {
                    settings.selGridInt[i] = 0;
                }
            }
            creatingBroList = false;
        }

        public static bool CustomCountChanged()
        {
            return MakerObjectStorage.Bros.Length != allCustomBros.Count();
        }

        public static void CheckBroMakerAvailable()
        {
            BSett.instance.countEnabledBros();
        }

        public static void LoadCustomBros()
        {
            if ( GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked )
            {
                allCustomBros = BSett.instance.availableBros;
            }
            else
            {
                allCustomBros = new List<string>();
                for (int i = 0; i < MakerObjectStorage.Bros.Length; ++i)
                {
                    allCustomBros.Add(MakerObjectStorage.Bros[i].name);
                }
            }
        }

        public static string GetSelectedBro(int playerNum)
        {
            if (settings.selGridInt[playerNum] > Main.maxBroNumWithoutCustom)
            {
                return allCustomBros[settings.selGridInt[playerNum] - Main.maxBroNumWithoutCustom - 1];
            }
            return "";
        }

        public static bool CheckIfCustomBro(TestVanDammeAnim character, ref string name)
        {
            if ( character is CustomHero )
            {
                name = (character as CustomHero).info.name;
                return true;
            }

            return false;
        }

        public static void MakeCustomBroSpawn(int curPlayer, string name)
        {
            LoadHero.willReplaceBro[curPlayer] = true;
            BSett.instance.overrideNextBroSpawn = true;
            BSett.instance.nextBroSpawn = name;
        }

        public static void EnableCustomBroSpawning()
        {
            BSett.instance.disableSpawning = false;
        }

        public static void DisableCustomBroSpawning(int curPlayer)
        {
            BSett.instance.disableSpawning = true;
            LoadHero.willReplaceBro[curPlayer] = false;
        }

        public static int GetHardcoreCount()
        {
            if ( settings.enableBromaker )
            {
                return GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count() + GetBromakerHardcoreAmount();
            }
            else
            {
                return GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count();
            }
        }

        public static int GetBromakerHardcoreAmount()
        {
            return BSett.instance.availableBros.Count();
        }

        public static int HeroTypeToInt(HeroType nextHero)
        {
            int chosen = 0;
            switch (nextHero)
            {
                case HeroType.Rambro: chosen = 0; break;
                case HeroType.Brommando: chosen = 1; break;
                case HeroType.BaBroracus: chosen = 2; break;
                case HeroType.BrodellWalker: chosen = 3; break;
                case HeroType.BroHard: chosen = 4; break;
                case HeroType.McBrover: chosen = 5; break;
                case HeroType.Blade: chosen = 6; break;
                case HeroType.BroDredd: chosen = 7; break;
                case HeroType.Brononymous: chosen = 8; break;
                case HeroType.SnakeBroSkin: chosen = 9; break;
                case HeroType.Brominator: chosen = 10; break;
                case HeroType.Brobocop: chosen = 11; break;
                case HeroType.IndianaBrones: chosen = 12; break;
                case HeroType.AshBrolliams: chosen = 13; break;
                case HeroType.Nebro: chosen = 14; break;
                case HeroType.BoondockBros: chosen = 15; break;
                case HeroType.Brochete: chosen = 16; break;
                case HeroType.BronanTheBrobarian: chosen = 17; break;
                case HeroType.EllenRipbro: chosen = 18; break;
                case HeroType.TimeBroVanDamme: chosen = 19; break;
                case HeroType.BroniversalSoldier: chosen = 20; break;
                case HeroType.ColJamesBroddock: chosen = 21; break;
                case HeroType.CherryBroling: chosen = 22; break;
                case HeroType.BroMax: chosen = 23; break;
                case HeroType.TheBrode: chosen = 24; break;
                case HeroType.DoubleBroSeven: chosen = 25; break;
                case HeroType.Predabro: chosen = 26; break;
                case HeroType.TheBrocketeer: chosen = 27; break;
                case HeroType.BroveHeart: chosen = 28; break;
                case HeroType.TheBrofessional: chosen = 29; break;
                case HeroType.Broden: chosen = 30; break;
                case HeroType.TheBrolander: chosen = 31; break;
                case HeroType.DirtyHarry: chosen = 32; break;
                case HeroType.TankBro: chosen = 33; break;
                case HeroType.BroLee: chosen = 34; break;
                case HeroType.BrondleFly: chosen = 35; break;
                case HeroType.Xebro: chosen = 36; break;
                case HeroType.Desperabro: chosen = 37; break;
                case HeroType.Broffy: chosen = 38; break;
                case HeroType.BroGummer: chosen = 39; break;
                case HeroType.DemolitionBro: chosen = 40; break;

                // extra characters
                case HeroType.BroneyRoss: chosen = 41; break;
                case HeroType.LeeBroxmas: chosen = 42; break;
                case HeroType.BronnarJensen: chosen = 43; break;
                case HeroType.HaleTheBro: chosen = 44; break;
                case HeroType.TrentBroser: chosen = 45; break;
                case HeroType.Broc: chosen = 46; break;
                case HeroType.TollBroad: chosen = 47; break;
            }

            return chosen;
        }

        public static HeroType IntToHeroType(int hero)
        {
            switch (hero)
            {
                case 0: return HeroType.Rambro;
                case 1: return HeroType.Brommando;
                case 2: return HeroType.BaBroracus;
                case 3: return HeroType.BrodellWalker;
                case 4: return HeroType.BroHard;
                case 5: return HeroType.McBrover;
                case 6: return HeroType.Blade;
                case 7: return HeroType.BroDredd;
                case 8: return HeroType.Brononymous; // bro in black
                case 9: return HeroType.SnakeBroSkin;
                case 10: return HeroType.Brominator;
                case 11: return HeroType.Brobocop;
                case 12: return HeroType.IndianaBrones;
                case 13: return HeroType.AshBrolliams;
                case 14: return HeroType.Nebro;
                case 15: return HeroType.BoondockBros;
                case 16: return HeroType.Brochete;
                case 17: return HeroType.BronanTheBrobarian;
                case 18: return HeroType.EllenRipbro;
                case 19: return HeroType.TimeBroVanDamme;
                case 20: return HeroType.BroniversalSoldier;
                case 21: return HeroType.ColJamesBroddock;
                case 22: return HeroType.CherryBroling;
                case 23: return HeroType.BroMax;
                case 24: return HeroType.TheBrode;
                case 25: return HeroType.DoubleBroSeven;
                case 26: return HeroType.Predabro;
                case 27: return HeroType.TheBrocketeer;
                case 28: return HeroType.BroveHeart;
                case 29: return HeroType.TheBrofessional;
                case 30: return HeroType.Broden;
                case 31: return HeroType.TheBrolander;
                case 32: return HeroType.DirtyHarry;
                case 33: return HeroType.TankBro;
                case 34: return HeroType.BroLee;
                case 35: return HeroType.BrondleFly;
                case 36: return HeroType.Xebro;
                case 37: return HeroType.Desperabro;
                case 38: return HeroType.Broffy;
                case 39: return HeroType.BroGummer;
                case 40: return HeroType.DemolitionBro;

                // extra characters
                case 41: return HeroType.BroneyRoss;
                case 42: return HeroType.LeeBroxmas;
                case 43: return HeroType.BronnarJensen;
                case 44: return HeroType.HaleTheBro;
                case 45: return HeroType.TrentBroser;
                case 46: return HeroType.Broc;
                case 47: return HeroType.TollBroad;
            }
            return HeroType.None;
        }

        public static string HeroTypeToString(HeroType hero)
        {
            switch (hero)
            {
                case HeroType.Rambro: return "Rambro";
                case HeroType.Brommando: return "Brommando";
                case HeroType.BaBroracus: return "B. A. Broracus";
                case HeroType.BrodellWalker: return "Brodell Walker";
                case HeroType.BroHard: return "Bro Hard";
                case HeroType.McBrover: return "MacBrover";
                case HeroType.Blade: return "Brade";
                case HeroType.BroDredd: return "Bro Dredd";
                case HeroType.Brononymous: return "Bro In Black";
                case HeroType.SnakeBroSkin: return "Snake Broskin";
                case HeroType.Brominator: return "Brominator";
                case HeroType.Brobocop: return "Brobocop";
                case HeroType.IndianaBrones: return "Indiana Brones";
                case HeroType.AshBrolliams: return "Ash Brolliams";
                case HeroType.Nebro: return "Mr. Anderbro";
                case HeroType.BoondockBros: return "The Boondock Bros";
                case HeroType.Brochete: return "Brochete";
                case HeroType.BronanTheBrobarian: return "Bronan the Brobarian";
                case HeroType.EllenRipbro: return "Ellen Ripbro";
                case HeroType.TimeBroVanDamme: return "Time Bro";
                case HeroType.BroniversalSoldier: return "Broniversal Soldier";
                case HeroType.ColJamesBroddock: return "Colonel James Broddock";
                case HeroType.CherryBroling: return "Cherry Broling";
                case HeroType.BroMax: return "Bro Max";
                case HeroType.TheBrode: return "The Brode";
                case HeroType.DoubleBroSeven: return "Double Bro Seven";
                case HeroType.Predabro: return "The Brodator";
                case HeroType.TheBrocketeer: return "The Brocketeer";
                case HeroType.BroveHeart: return "Broheart";
                case HeroType.TheBrofessional: return "The Brofessional";
                case HeroType.Broden: return "Broden";
                case HeroType.TheBrolander: return "The Brolander";
                case HeroType.DirtyHarry: return "Dirty Brory";
                case HeroType.TankBro: return "Tank Bro";
                case HeroType.BroLee: return "Bro Lee";
                case HeroType.BrondleFly: return "Seth Brondle";
                case HeroType.Xebro: return "Xebro";
                case HeroType.Desperabro: return "Desperabro";
                case HeroType.Broffy: return "Broffy the Vampire Slayer";
                case HeroType.BroGummer: return "Burt Brommer";
                case HeroType.DemolitionBro: return "Demolition Bro";

                // extra characters
                case HeroType.BroneyRoss: return "Broney Ross";
                case HeroType.LeeBroxmas: return "Lee Broxmas";
                case HeroType.BronnarJensen: return "Bronnar Jensen";
                case HeroType.HaleTheBro: return "Bro Caesar";
                case HeroType.TrentBroser: return "Trent Broser";
                case HeroType.Broc: return "Broctor Death";
                case HeroType.TollBroad: return "Toll Broad";
            }
            return "";
        }

        public static HeroType StringToHeroType(string hero)
        {
            switch (hero)
            {
                case "Rambro": return HeroType.Rambro;
                case "Brommando": return HeroType.Brommando;
                case "B. A. Broracus": return HeroType.BaBroracus;
                case "Brodell Walker": return HeroType.BrodellWalker;
                case "Bro Hard": return HeroType.BroHard;
                case "MacBrover": return HeroType.McBrover;
                case "Brade": return HeroType.Blade;
                case "Bro Dredd": return HeroType.BroDredd;
                case "Bro In Black": return HeroType.Brononymous;
                case "Snake Broskin": return HeroType.SnakeBroSkin;
                case "Brominator": return HeroType.Brominator;
                case "Brobocop": return HeroType.Brobocop;
                case "Indiana Brones": return HeroType.IndianaBrones;
                case "Ash Brolliams": return HeroType.AshBrolliams;
                case "Mr. Anderbro": return HeroType.Nebro;
                case "The Boondock Bros": return HeroType.BoondockBros;
                case "Brochete": return HeroType.Brochete;
                case "Bronan the Brobarian": return HeroType.BronanTheBrobarian;
                case "Ellen Ripbro": return HeroType.EllenRipbro;
                case "Time Bro": return HeroType.TimeBroVanDamme;
                case "Broniversal Soldier": return HeroType.BroniversalSoldier;
                case "Colonel James Broddock": return HeroType.ColJamesBroddock;
                case "Cherry Broling": return HeroType.CherryBroling;
                case "Bro Max": return HeroType.BroMax;
                case "The Brode": return HeroType.TheBrode;
                case "Double Bro Seven": return HeroType.DoubleBroSeven;
                case "The Brodator": return HeroType.Predabro;
                case "The Brocketeer": return HeroType.TheBrocketeer;
                case "Broheart": return HeroType.BroveHeart;
                case "The Brofessional": return HeroType.TheBrofessional;
                case "Broden": return HeroType.Broden;
                case "The Brolander": return HeroType.TheBrolander;
                case "Dirty Brory": return HeroType.DirtyHarry;
                case "Tank Bro": return HeroType.TankBro;
                case "Bro Lee": return HeroType.BroLee;
                case "Seth Brondle": return HeroType.BrondleFly;
                case "Xebro": return HeroType.Xebro;
                case "Desperabro": return HeroType.Desperabro;
                case "Broffy the Vampire Slayer": return HeroType.Broffy;
                case "Burt Brommer": return HeroType.BroGummer;
                case "Demolition Bro": return HeroType.DemolitionBro;
                
                // extra characters
                case "Broney Ross": return HeroType.BroneyRoss;
                case "Lee Broxmas": return HeroType.LeeBroxmas;
                case "Bronnar Jensen": return HeroType.BronnarJensen;
                case "Bro Caesar": return HeroType.HaleTheBro;
                case "Trent Broser": return HeroType.TrentBroser;
                case "Broctor Death": return HeroType.Broc;
                case "Toll Broad": return HeroType.TollBroad;
            }
            return HeroType.None;
        }
    }

    public class KeyBind
    {
        public KeyCode kc;
        public bool waitingForInput = false;
        public string DPADString = "NONE";
        public DPAD DPADKey = DPAD.NONE;
        public string joystick = "NONE";
    }

    public class Settings : UnityModManager.ModSettings
    { 
        public bool alwaysChosen = false;
        public bool ignoreCurrentUnlocked = false;
        public bool includeUnfinishedCharacters = false;
        public bool clickingEnabled = true;
        public bool disableConfirm = true;
        public bool enableBromaker = false;
        public float swapCoolDown = 0.5f;

        public int[] selGridInt = { 0, 0, 0, 0 };
        public bool[] showSettings = { true, false, false, false };
        public KeyBind[] swapLeftKeys = { new KeyBind(), new KeyBind(), new KeyBind(), new KeyBind() };
        public KeyBind[] swapRightKeys = { new KeyBind(), new KeyBind(), new KeyBind(), new KeyBind() };

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public HeroType getSelectedHero( int playerNum )
        {
            if ( GameModeController.IsHardcoreMode && !this.ignoreCurrentUnlocked )
            {
                return Main.StringToHeroType(Main.currentBroList[this.selGridInt[playerNum]]);
            }
            else
            {
                return Main.IntToHeroType(this.selGridInt[playerNum]);
            }
        }

        public void setSelectedHero( int playerNum, HeroType nextHero )
        {
            if (GameModeController.IsHardcoreMode && !this.ignoreCurrentUnlocked)
            {
                selGridInt[playerNum] = Main.currentBroList.IndexOf(Main.HeroTypeToString(nextHero));
                if ( selGridInt[playerNum] == -1 )
                {
                    selGridInt[playerNum] = 0;
                }
            }
            else
            {
                selGridInt[playerNum] = Main.HeroTypeToInt(nextHero);
            }
            
        }
    }

    [HarmonyPatch(typeof(Player), "SpawnHero")]
    static class Player_SpawnHero_Patch
    {
        static void Prefix(Player __instance, ref HeroType nextHeroType)
        {
            if (!Main.enabled)
                return;

            if (Main.manualSpawn)
            {
                Main.manualSpawn = false;
                return;
            }

            if (!Main.settings.alwaysChosen)
            {
                if (GameState.Instance.hardCoreMode && !Main.settings.ignoreCurrentUnlocked)
                {
                    Main.CreateBroList();
                }
                Main.settings.setSelectedHero( __instance.playerNum, nextHeroType);
                return;
            }

            int curPlayer = __instance.playerNum;

            // If we're in IronBro and don't want to force spawn a bro we haven't unlocked
            if (GameState.Instance.hardCoreMode && !Main.settings.ignoreCurrentUnlocked)
            {
                // Make sure list of available hardcore bros is up-to-date
                Main.CreateBroList();
                // If Bromaker is enabled and selected character is custom
                if (Main.settings.enableBromaker && (Main.settings.selGridInt[curPlayer] > Main.maxBroNumWithoutCustom))
                {
                    Main.MakeCustomBroSpawn(curPlayer, Main.GetSelectedBro(curPlayer));
                    // Ensure we don't spawn boondock bros because one gets left over
                    nextHeroType = HeroType.Rambro;
                }
                else
                {
                    if (Main.settings.enableBromaker)
                        Main.DisableCustomBroSpawning(curPlayer);
                    nextHeroType = Main.settings.getSelectedHero(curPlayer);
                }
            }
            // If bro spawning is a custom bro
            else if ( Main.settings.enableBromaker && Main.settings.selGridInt[curPlayer] > Main.maxBroNumWithoutCustom)
            {
                Main.MakeCustomBroSpawn(curPlayer, Main.GetSelectedBro(curPlayer));
                // Ensure we don't spawn boondock bros because one gets left over
                nextHeroType = HeroType.Rambro;
            }
            // If we're just spawning a normal character
            else
            {
                if (Main.settings.enableBromaker)
                    Main.DisableCustomBroSpawning(curPlayer);
                nextHeroType = Main.settings.getSelectedHero(curPlayer);
            }

        }
        static void Postfix(Player __instance, ref HeroType nextHeroType)
        {
            if (!Main.enabled)
                return;

            if (Main.settings.enableBromaker)
            {
                Main.EnableCustomBroSpawning();
                string name = "";
                if (Main.CheckIfCustomBro(__instance.character, ref name) && name != Main.GetSelectedBro(__instance.playerNum) )
                {
                    Main.settings.selGridInt[__instance.playerNum] = Main.currentBroList.IndexOf(name);
                    if ( Main.settings.selGridInt[__instance.playerNum] == -1 )
                    {
                        Main.CreateBroList();
                        Main.settings.selGridInt[__instance.playerNum] = Main.currentBroList.IndexOf(name);
                    }
                }
            }
        }
    }


    [HarmonyPatch(typeof(Player), "GetInput")]
    static class Player_GetInput_Patch
    {
        public static void Postfix(Player __instance)
        {
            if (!Main.enabled)
            {
                return;
            }

            int curPlayer = __instance.playerNum;
            bool leftPressed = Main.wasKeyPressed(Main.settings.swapLeftKeys[__instance.playerNum]);
            bool rightPressed = Main.wasKeyPressed(Main.settings.swapRightKeys[__instance.playerNum]);

            if (((leftPressed || rightPressed) && Main.cooldown == 0f && __instance.IsAlive()) || (Main.settings.clickingEnabled && Main.switched[curPlayer]))
            {
                float X, Y, XI, YI;
                Vector3 vec = __instance.GetCharacterPosition();
                X = vec.x;
                Y = vec.y;
                XI = (float)Traverse.Create(__instance.character).Field("xI").GetValue();
                YI = (float)Traverse.Create(__instance.character).Field("yI").GetValue();
                Main.manualSpawn = true;

                if (Main.settings.clickingEnabled && Main.switched[curPlayer])
                {
                    if (Main.settings.selGridInt[curPlayer] > Main.maxBroNumWithoutCustom)
                    {
                        Main.MakeCustomBroSpawn(curPlayer, Main.GetSelectedBro(curPlayer));

                        __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());
                        __instance.SpawnHero(HeroType.Rambro);

                        __instance._character.SetPositionAndVelocity(X, Y, XI, YI);
                        __instance.character.SetInvulnerable(0f, false);
                    }
                    else
                    {
                        __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());
                        if (Main.settings.enableBromaker)
                            Main.DisableCustomBroSpawning(curPlayer);
                        __instance.SpawnHero(Main.settings.getSelectedHero(curPlayer));
                        if (Main.settings.enableBromaker)
                            Main.EnableCustomBroSpawning();

                        __instance.character.SetPositionAndVelocity(X, Y, XI, YI);
                        __instance.character.SetInvulnerable(0f, false);
                    }
                    Main.switched[curPlayer] = false;
                    return;
                }

                // If our list of IronBro characters is out of date, update it
                if (GameState.Instance.hardCoreMode && !Main.settings.ignoreCurrentUnlocked && Main.currentBroList.Count() != Main.GetHardcoreCount())
                {
                    Main.CreateBroList();
                }

                if ( leftPressed )
                {
                    --Main.settings.selGridInt[curPlayer];
                    if ( Main.settings.selGridInt[curPlayer] < 0 )
                    {
                        Main.settings.selGridInt[curPlayer] = Main.maxBroNum;
                    }
                }
                else if ( rightPressed )
                {
                    ++Main.settings.selGridInt[curPlayer];
                    if (Main.settings.selGridInt[curPlayer] > Main.maxBroNum)
                    {
                        Main.settings.selGridInt[curPlayer] = 0;
                    }
                }

                // If character spawning is custom 
                if (Main.settings.enableBromaker && Main.settings.selGridInt[curPlayer] > Main.maxBroNumWithoutCustom)
                {
                    Main.MakeCustomBroSpawn(curPlayer, Main.GetSelectedBro(curPlayer));

                    __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());
                    __instance.SpawnHero(HeroType.Rambro);

                    __instance._character.SetPositionAndVelocity(X, Y, XI, YI);
                    __instance.character.SetInvulnerable(0f, false);

                    Main.cooldown = Main.settings.swapCoolDown;
                }
                else 
                {
                    __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());
                    if (Main.settings.enableBromaker)
                    {
                        Main.DisableCustomBroSpawning(curPlayer);
                    }
                        
                    __instance.SpawnHero(Main.settings.getSelectedHero(curPlayer));
                    if (Main.settings.enableBromaker)
                        Main.EnableCustomBroSpawning();

                    __instance._character.SetPositionAndVelocity(X, Y, XI, YI);
                    __instance.character.SetInvulnerable(0f, false);

                    Main.cooldown = Main.settings.swapCoolDown;

                }
            }
            return;
        }
    }

    [HarmonyPatch(typeof(Player), "Update")]
    static class Player_Update_Patch
    {
        static void Prefix(Player __instance)
        {
            if (!Main.enabled)
            {
                return;
            }
            if (Main.cooldown > 0f)
            {
                __instance.character.SetInvulnerable(0f, false);
                Main.cooldown -= Time.unscaledDeltaTime;
                if (Main.cooldown < 0f)
                {
                    Main.cooldown = 0f;
                }
            }


        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "SetInvulnerable")]
    static class TestVanDammeAnim_SetInvulnerable_Patch
    {
        static bool Prefix(TestVanDammeAnim __instance, float time, bool restartBubble = true)
        {
            if (!Main.enabled)
            {
                return true;
            }
            if (time == 0f && !restartBubble)
            {
                Traverse.Create(typeof(TestVanDammeAnim)).Field("invulnerableTime").SetValue(0);
                __instance.invulnerable = false;
                return false;
            }

            return true;


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
        static bool Prefix(PauseMenu __instance)
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

}

    
