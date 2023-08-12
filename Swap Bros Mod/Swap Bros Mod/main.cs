/*
 * TODO
 * 
 * FIX not respawning on hell levels (this might actually be multiplayer mod)
 * 
 * Somehow preserve special count between swaps and add toggle for it on options
 * 
 * Allow swapping forward and back with rebindable keys
 * 
 */
/*
* FIXED
* 
* 
* Make the order you swap in the same as the order shown on the mod options screen for normal and ironbro
* 
* Enable switching to unfinished characters if they exist
* 
* Bug in IronBro where BroLee is skipped over even if you have him unlocked
* 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;

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

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            string[] texts = { "Rambro", "Brommando", "B. A. Broracus", "Brodell Walker", "Bro Hard", "MacBrover", "Brade", "Bro Dredd", "Bro In Black", "Snake Broskin", "Brominator",
                "Brobocop", "Indiana Brones", "Ash Brolliams", "Mr. Anderbro", "The Boondock Bros", "Brochete", "Bronan the Brobarian", "Ellen Ripbro", "Time Bro", "Broniversal Soldier",
                "Colonel James Broddock", "Cherry Broling", "Bro Max", "The Brode", "Double Bro Seven", "The Brodator", "The Brocketeer", "Broheart", "The Brofessional", "Broden",
                "The Brolander", "Dirty Brory", "Tank Bro", "Bro Lee", "Seth Brondle", "Xebro", "Desperabro", "Broffy the Vampire Slayer", "Burt Brommer", "Demolition Bro" };
            if (settings.includeUnfinishedCharacters)
            {
                texts = new string[] { "Rambro", "Brommando", "B. A. Broracus", "Brodell Walker", "Bro Hard", "MacBrover", "Brade", "Bro Dredd", "Bro In Black", "Snake Broskin", "Brominator",
                "Brobocop", "Indiana Brones", "Ash Brolliams", "Mr. Anderbro", "The Boondock Bros", "Brochete", "Bronan the Brobarian", "Ellen Ripbro", "Time Bro", "Broniversal Soldier",
                "Colonel James Broddock", "Cherry Broling", "Bro Max", "The Brode", "Double Bro Seven", "The Brodator", "The Brocketeer", "Broheart", "The Brofessional", "Broden",
                "The Brolander", "Dirty Brory", "Tank Bro", "Bro Lee", "Seth Brondle", "Xebro", "Desperabro", "Broffy the Vampire Slayer", "Burt Brommer","Demolition Bro", "Broney Ross", "Lee Broxmas", "Bronnar Jensen", "Bro Caesar", "Trent Broser", "Broctor Death",
                "Toll Broad", "Final", "SuicideBro", "Random", "TankBroTank"};
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

            GUI.Label(lastRect, GUI.tooltip);

            GUILayout.EndVertical();


            GUILayout.BeginVertical();

            settings.includeUnfinishedCharacters = GUILayout.Toggle(settings.includeUnfinishedCharacters, new GUIContent("Include unfinished bros",
                "Include bros from Expendabros, BrondleFly, and some weird other ones"), GUILayout.ExpandWidth(false));

            GUI.Label(lastRect, GUI.tooltip);

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(30);

            GUILayout.BeginHorizontal();

            GUILayout.Label( String.Format("Cooldown between swaps: {0:0.00}s", Main.settings.swapCoolDown), GUILayout.Width(225), GUILayout.ExpandWidth(false) );
            Main.settings.swapCoolDown = GUILayout.HorizontalSlider(Main.settings.swapCoolDown, 0, 2);

            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            for ( int i = 0; i < 4; ++i )
            {
                GUILayout.Label("Player " + (i + 1));
                if ( GUILayout.Button(settings.showSettings[i] ? "Hide" : "Show") )
                {
                    settings.showSettings[i] = !settings.showSettings[i];
                }
                if ( settings.showSettings[i] )
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Swap Bro Left: " + (settings.swapLeftKeys[i].waitingForInput ? "Press Any Key/Button" : (settings.swapLeftKeys[i].DPADKey == DPAD.NONE ? settings.swapLeftKeys[i].kc.ToString() : "DPAD " + settings.swapLeftKeys[i].DPADString))) && !InputReader.IsBlocked)
                    {
                        settings.swapLeftKeys[i].waitingForInput = true;
                        UnityModManager.UI.Instance.StartCoroutine(BindKey(settings.swapLeftKeys[i], i));
                    }
                    if (GUILayout.Button("Swap Bro Right: " + (settings.swapRightKeys[i].waitingForInput ? "Press Any Key/Button" : (settings.swapRightKeys[i].DPADKey == DPAD.NONE ? settings.swapRightKeys[i].kc.ToString() : "DPAD " + settings.swapRightKeys[i].DPADString))) && !InputReader.IsBlocked)
                    {
                        settings.swapRightKeys[i].waitingForInput = true;
                        UnityModManager.UI.Instance.StartCoroutine(BindKey(settings.swapRightKeys[i], i));
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(25);
                    GUILayout.BeginHorizontal();

                    if ( settings.selGridInt[i] < 0 || settings.selGridInt[i] >= texts.Length )
                    {
                        settings.selGridInt[i] = 0;
                    }

                    if (settings.clickingEnabled)
                    {
                        if (settings.selGridInt[i] != (settings.selGridInt[i] = GUILayout.SelectionGrid(settings.selGridInt[i], texts, 5, GUILayout.Height(270))))
                        {
                            switched[i] = true;
                        }
                    }
                    else
                    {
                        settings.selGridInt[i] = GUILayout.SelectionGrid(settings.selGridInt[i], texts, 5, GUILayout.Height(230));
                    }

                    GUILayout.EndHorizontal();
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
            switch (selGridInt[playerNum]) {
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

                case 48: return HeroType.Final;
                case 49: return HeroType.SuicideBro;
                case 50: return HeroType.Random;
                case 51: return HeroType.TankBroTank;
            }
            return HeroType.None;
        }

        public void setSelectedHero( int playerNum, HeroType nextHero )
        {
            int chosen = 0;
            switch (nextHero)
            {
                case HeroType.Rambro: chosen = 0;break;
                case HeroType.Brommando: chosen = 1;break;
                case HeroType.BaBroracus: chosen = 2;break;
                case HeroType.BrodellWalker: chosen = 3;break;
                case HeroType.BroHard: chosen = 4;break;
                case HeroType.McBrover: chosen = 5;break;
                case HeroType.Blade: chosen = 6;break;
                case HeroType.BroDredd: chosen = 7;break;
                case HeroType.Brononymous: chosen = 8;break; 
                case HeroType.SnakeBroSkin: chosen = 9;break;
                case HeroType.Brominator: chosen = 10;break;
                case HeroType.Brobocop: chosen = 11;break;
                case HeroType.IndianaBrones: chosen = 12;break;
                case HeroType.AshBrolliams: chosen = 13;break;
                case HeroType.Nebro: chosen = 14;break;
                case HeroType.BoondockBros: chosen = 15;break;
                case HeroType.Brochete: chosen = 16;break;
                case HeroType.BronanTheBrobarian: chosen = 17;break;
                case HeroType.EllenRipbro: chosen = 18;break;
                case HeroType.TimeBroVanDamme: chosen = 19;break;
                case HeroType.BroniversalSoldier: chosen = 20;break;
                case HeroType.ColJamesBroddock: chosen = 21;break;
                case HeroType.CherryBroling: chosen = 22;break;
                case HeroType.BroMax: chosen = 23;break;
                case HeroType.TheBrode: chosen = 24;break;
                case HeroType.DoubleBroSeven: chosen = 25;break;
                case HeroType.Predabro: chosen = 26;break;
                case HeroType.TheBrocketeer: chosen = 27;break;
                case HeroType.BroveHeart: chosen = 28;break;
                case HeroType.TheBrofessional: chosen = 29;break;
                case HeroType.Broden: chosen = 30;break;
                case HeroType.TheBrolander: chosen = 31;break;
                case HeroType.DirtyHarry: chosen = 32;break;
                case HeroType.TankBro: chosen = 33;break;
                case HeroType.BroLee: chosen = 34;break;
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

                case HeroType.Final: chosen = 48; break;
                case HeroType.SuicideBro: chosen = 49; break;
                case HeroType.Random: chosen = 50; break;
                case HeroType.TankBroTank: chosen = 51; break;
            }
            selGridInt[playerNum] = chosen;
        }

    }

    [HarmonyPatch(typeof(Player), "SpawnHero")]
    static class Player_SpawnHero_Patch
    {
        static void Prefix(Player __instance, ref HeroType nextHeroType)
        {
            if (!Main.enabled)
                return;

            if (!Main.settings.alwaysChosen)
            {
                Main.settings.setSelectedHero( __instance.playerNum, nextHeroType);
                return;
            }

            int curPlayer = __instance.playerNum;

            if (GameState.Instance.hardCoreMode)
            {
                if (GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero(curPlayer)) == -1 && !Main.settings.ignoreCurrentUnlocked)
                {
                    
                    if (HeroController.GetTotalLives() == 1)
                    {
                        nextHeroType = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros[0];
                        Main.settings.setSelectedHero(curPlayer, GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros[0]);
                        return;
                    }
                    else
                    {
                        int max = 40;
                        if (Main.settings.includeUnfinishedCharacters)
                        {
                            max = 51;
                        }
                        int num = -1;
                        
                        for (; Main.settings.selGridInt[curPlayer] <= max; Main.settings.selGridInt[curPlayer]++)
                        {
                            //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                            num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero(curPlayer));
                            if (num != -1)
                                break;
                        }
                        if (num == -1)
                        {
                            for (Main.settings.selGridInt[curPlayer] = 0; Main.settings.selGridInt[curPlayer] <= max; Main.settings.selGridInt[curPlayer]++)
                            {
                                //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                                num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero(curPlayer));
                                if (num != -1)
                                    break;
                            }
                        }

                        nextHeroType = Main.settings.getSelectedHero(curPlayer);
                        return;
                    }

                }
                else
                {
                    nextHeroType = Main.settings.getSelectedHero(curPlayer);
                }
            }
            else
            {
                nextHeroType = Main.settings.getSelectedHero(curPlayer);
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
            
            if (((leftPressed || rightPressed) && Main.cooldown == 0f) || (Main.settings.clickingEnabled && Main.switched[curPlayer]))
            {
                float X, Y, XI, YI;
                Vector3 vec = __instance.GetCharacterPosition();
                X = vec.x;
                Y = vec.y;
                XI = (float) Traverse.Create(__instance.character).Field("xI").GetValue();
                YI = (float) Traverse.Create(__instance.character).Field("yI").GetValue();

                if (Main.settings.clickingEnabled && Main.switched[curPlayer])
                {
                    Main.switched[curPlayer] = false;
                    Vector3 characterPosition = __instance.GetCharacterPosition();
                    __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());

                    __instance.SpawnHero(Main.settings.getSelectedHero(curPlayer));

                    __instance.character.SetPositionAndVelocity(X, Y, XI, YI);
                    Main.cooldown = Main.settings.swapCoolDown;
                    __instance.character.SetInvulnerable(0f, false);
                    return;
                }
                
                if (GameState.Instance.hardCoreMode && !Main.settings.ignoreCurrentUnlocked)
                {
                    Vector3 characterPosition = __instance.GetCharacterPosition();
                    __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());
                    

                    if ( rightPressed )
                    {
                        int max = 40;
                        if (Main.settings.includeUnfinishedCharacters)
                        {
                            max = 51;
                        }
                        int num = -1;
                        for (Main.settings.selGridInt[curPlayer]++; Main.settings.selGridInt[curPlayer] <= max; Main.settings.selGridInt[curPlayer]++)
                        {
                            //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                            num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero(curPlayer));

                            if (num != -1)
                            {
                                // Main.mod.Logger.Log("found hero " + num + " " + Main.settings.getSelectedHero());
                                break;
                            }
                        }
                        if (num == -1)
                        {
                            for (Main.settings.selGridInt[curPlayer] = 0; Main.settings.selGridInt[curPlayer] <= max; Main.settings.selGridInt[curPlayer]++)
                            {
                                //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                                num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero(curPlayer));
                                if (num != -1)
                                    break;
                            }
                        }
                    }
                    else
                    {
                        int max = 40;
                        if (Main.settings.includeUnfinishedCharacters)
                        {
                            max = 51;
                        }
                        int num = -1;
                        for (Main.settings.selGridInt[curPlayer]--; Main.settings.selGridInt[curPlayer] >= 0; Main.settings.selGridInt[curPlayer]--)
                        {
                            //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                            num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero(curPlayer));

                            if (num != -1)
                            {
                                // Main.mod.Logger.Log("found hero " + num + " " + Main.settings.getSelectedHero());
                                break;
                            }
                        }
                        if (num == -1)
                        {
                            for (Main.settings.selGridInt[curPlayer] = max; Main.settings.selGridInt[curPlayer] >= 0; Main.settings.selGridInt[curPlayer]--)
                            {
                                //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                                num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero(curPlayer));
                                if (num != -1)
                                    break;
                            }
                        }
                    }

                    __instance.SpawnHero(Main.settings.getSelectedHero(curPlayer));

                    __instance._character.SetPositionAndVelocity(X, Y, XI, YI);
                    __instance.character.SetInvulnerable(0f, false);

                    Main.cooldown = Main.settings.swapCoolDown;
                }
                else
                {
                    __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());

                    int num2 = Main.settings.selGridInt[curPlayer];
                    int max = 40;
                    if (Main.settings.includeUnfinishedCharacters)
                    {
                        max = 51;
                    }

                    if ( rightPressed )
                    {
                        ++num2;
                    }
                    else
                    {
                        --num2;
                    }

                    if (num2 > max)
                    {
                        Main.settings.selGridInt[curPlayer] = 0;

                    }
                    else if ( num2 < 0 )
                    {
                        Main.settings.selGridInt[curPlayer] = max;
                    }
                    else
                    {
                        Main.settings.selGridInt[curPlayer] = num2;
                    }

                    __instance.SpawnHero(Main.settings.getSelectedHero(curPlayer));

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

    
