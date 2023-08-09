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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;

namespace Swap_Bros_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static float cooldown = 0;
        public static Settings settings;
        public static int currentCharIndex = 0;
        public static bool switched = false;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = Settings.Load<Settings>(modEntry);
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

            

            GUILayout.BeginHorizontal();


            GUILayout.BeginVertical();

            settings.swapEnabled = GUILayout.Toggle(settings.swapEnabled, "Enable swapping with taunt", GUILayout.ExpandWidth(false));

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

            settings.swapChangesChosen = GUILayout.Toggle(settings.swapChangesChosen, new GUIContent("Pressing taunt changes chosen bro",
                "Pressing taunt cycles through the list of bros below"), GUILayout.ExpandWidth(false));

            settings.includeUnfinishedCharacters = GUILayout.Toggle(settings.includeUnfinishedCharacters, new GUIContent("Include unfinished bros",
                "Include bros from Expendabros, BrondleFly, and some weird other ones"), GUILayout.ExpandWidth(false));

            GUI.Label(lastRect, GUI.tooltip);

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
           
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


            GUILayout.EndHorizontal();
            GUILayout.Space(25);
            GUILayout.BeginHorizontal();

            if (settings.clickingEnabled)
            {
                if (settings.selGridInt != (settings.selGridInt = GUILayout.SelectionGrid(settings.selGridInt, texts, 5, GUILayout.Height(270))))
                {
                    switched = true;
                }
            }
            else
            {
                settings.selGridInt = GUILayout.SelectionGrid(settings.selGridInt, texts, 5, GUILayout.Height(230));
            }

            GUILayout.EndHorizontal();
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

    public class Settings : UnityModManager.ModSettings
    {
        public int selGridInt;
        public bool alwaysChosen;
        public bool swapEnabled;
        public bool swapChangesChosen;
        public bool ignoreCurrentUnlocked;
        public bool includeUnfinishedCharacters;
        public bool clickingEnabled;
        public bool disableConfirm;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public HeroType getSelectedHero()
        {
            switch (selGridInt) {
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

        public void setSelectedHero(HeroType nextHero)
        {
            switch (nextHero)
            {
                case HeroType.Rambro: selGridInt = 0;break;
                case HeroType.Brommando: selGridInt = 1;break;
                case HeroType.BaBroracus: selGridInt = 2;break;
                case HeroType.BrodellWalker: selGridInt = 3;break;
                case HeroType.BroHard: selGridInt = 4;break;
                case HeroType.McBrover: selGridInt = 5;break;
                case HeroType.Blade: selGridInt = 6;break;
                case HeroType.BroDredd: selGridInt = 7;break;
                case HeroType.Brononymous: selGridInt = 8;break; 
                case HeroType.SnakeBroSkin: selGridInt = 9;break;
                case HeroType.Brominator: selGridInt = 10;break;
                case HeroType.Brobocop: selGridInt = 11;break;
                case HeroType.IndianaBrones: selGridInt = 12;break;
                case HeroType.AshBrolliams: selGridInt = 13;break;
                case HeroType.Nebro: selGridInt = 14;break;
                case HeroType.BoondockBros: selGridInt = 15;break;
                case HeroType.Brochete: selGridInt = 16;break;
                case HeroType.BronanTheBrobarian: selGridInt = 17;break;
                case HeroType.EllenRipbro: selGridInt = 18;break;
                case HeroType.TimeBroVanDamme: selGridInt = 19;break;
                case HeroType.BroniversalSoldier: selGridInt = 20;break;
                case HeroType.ColJamesBroddock: selGridInt = 21;break;
                case HeroType.CherryBroling: selGridInt = 22;break;
                case HeroType.BroMax: selGridInt = 23;break;
                case HeroType.TheBrode: selGridInt = 24;break;
                case HeroType.DoubleBroSeven: selGridInt = 25;break;
                case HeroType.Predabro: selGridInt = 26;break;
                case HeroType.TheBrocketeer: selGridInt = 27;break;
                case HeroType.BroveHeart: selGridInt = 28;break;
                case HeroType.TheBrofessional: selGridInt = 29;break;
                case HeroType.Broden: selGridInt = 30;break;
                case HeroType.TheBrolander: selGridInt = 31;break;
                case HeroType.DirtyHarry: selGridInt = 32;break;
                case HeroType.TankBro: selGridInt = 33;break;
                case HeroType.BroLee: selGridInt = 34;break;
                case HeroType.BrondleFly: selGridInt = 35; break;
                case HeroType.Xebro: selGridInt = 36; break;
                case HeroType.Desperabro: selGridInt = 37; break;
                case HeroType.Broffy: selGridInt = 38; break;
                case HeroType.BroGummer: selGridInt = 39; break;
                case HeroType.DemolitionBro: selGridInt = 40; break;

                // extra characters
                case HeroType.BroneyRoss: selGridInt = 41; break;
                case HeroType.LeeBroxmas: selGridInt = 42; break;
                case HeroType.BronnarJensen: selGridInt = 43; break;
                case HeroType.HaleTheBro: selGridInt = 44; break;
                case HeroType.TrentBroser: selGridInt = 45; break;
                case HeroType.Broc: selGridInt = 46; break;
                case HeroType.TollBroad: selGridInt = 47; break;

                case HeroType.Final: selGridInt = 48; break;
                case HeroType.SuicideBro: selGridInt = 49; break;
                case HeroType.Random: selGridInt = 50; break;
                case HeroType.TankBroTank: selGridInt = 51; break;
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

            if (!Main.settings.alwaysChosen)
            {
                Main.settings.setSelectedHero(nextHeroType);
                return;
            }

            if (GameState.Instance.hardCoreMode)
            {
                if (GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero()) == -1 && !Main.settings.ignoreCurrentUnlocked)
                {
                    
                    if (HeroController.GetTotalLives() == 1)
                    {
                        nextHeroType = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros[0];
                        Main.settings.setSelectedHero(GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros[0]);
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
                        
                        for (; Main.settings.selGridInt < max; Main.settings.selGridInt++)
                        {
                            //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                            num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero());
                            if (num != -1)
                                break;
                        }
                        if (num == -1)
                        {
                            for (Main.settings.selGridInt = 0; Main.settings.selGridInt < max; Main.settings.selGridInt++)
                            {
                                //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                                num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero());
                                if (num != -1)
                                    break;
                            }
                        }

                        nextHeroType = Main.settings.getSelectedHero();
                        return;
                    }

                }
                else
                {
                    nextHeroType = Main.settings.getSelectedHero();
                }
            }
            else
            {
                nextHeroType = Main.settings.getSelectedHero();
            }

        }

    }




    [HarmonyPatch(typeof(Player), "GetInput")]
    static class Player_GetInput_Patch
    {
        public static void Postfix(Player __instance, ref bool buttonGesture)
        {
            if (!Main.enabled)
            {
                return;
            }
            if ((buttonGesture && Main.cooldown == 0f) || (Main.settings.clickingEnabled && Main.switched))
            {
                if (Main.settings.clickingEnabled && Main.switched)
                {
                    Main.switched = false;
                    Vector3 characterPosition = __instance.GetCharacterPosition();
                    __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());

                    __instance.SpawnHero(Main.settings.getSelectedHero());

                    __instance._character.SetXY(characterPosition.x, characterPosition.y);
                    __instance._character.SetPosition();
                    Main.cooldown = 0.5f;
                    __instance.character.SetInvulnerable(0f, false);
                    return;
                }
                
                if (GameState.Instance.hardCoreMode && !Main.settings.ignoreCurrentUnlocked)
                {
                    Vector3 characterPosition = __instance.GetCharacterPosition();
                    __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());
                    
                    if (Main.settings.swapChangesChosen)
                    {
                        int max = 40;
                        if (Main.settings.includeUnfinishedCharacters)
                        {
                            max = 51;
                        }
                        int num = -1;
                        for (Main.settings.selGridInt++; Main.settings.selGridInt < max; Main.settings.selGridInt++)
                        {
                            //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                            num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero());

                            if (num != -1)
                            {
                                // Main.mod.Logger.Log("found hero " + num + " " + Main.settings.getSelectedHero());
                                break;
                            }
                        }
                        if (num == -1)
                        {
                            for (Main.settings.selGridInt = 0; Main.settings.selGridInt < max; Main.settings.selGridInt++)
                            {
                                //Main.Log("checking hero: " + Main.settings.getSelectedHero());
                                num = GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.IndexOf(Main.settings.getSelectedHero());
                                if (num != -1)
                                    break;
                            }
                        }
                    }

                    if (Main.settings.swapEnabled)
                    {
                        __instance.SpawnHero(Main.settings.getSelectedHero());

                        __instance._character.SetXY(characterPosition.x, characterPosition.y);
                        __instance._character.SetPosition();
                        __instance.character.SetInvulnerable(0f, false);
                    }
                    Main.cooldown = 0.5f;
                }
                else
                {
                    Vector3 characterPosition2 = __instance.GetCharacterPosition();
                    __instance.SetSpawnPositon(__instance._character, Player.SpawnType.TriggerSwapBro, false, __instance.GetCharacterPosition());

                    int num2 = Main.settings.selGridInt;
                    int max = 40;
                    if (Main.settings.includeUnfinishedCharacters)
                    {
                        max = 51;
                    }

                    if (num2 == max)
                    {
                        if (Main.settings.swapChangesChosen)
                        {

                            Main.settings.selGridInt = 0;
                        }
                        
                    }
                    else
                    {
                        if (Main.settings.swapChangesChosen)
                        {
                            Main.settings.selGridInt = num2 + 1;
                        }
                    }

                    if (Main.settings.swapEnabled)
                    {
                        __instance.SpawnHero(Main.settings.getSelectedHero());

                        __instance._character.SetXY(characterPosition2.x, characterPosition2.y);
                        __instance._character.SetPosition();
                        __instance.character.SetInvulnerable(0f, false);
                    }
                    Main.cooldown = 0.5f;

                }
            }
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

    
