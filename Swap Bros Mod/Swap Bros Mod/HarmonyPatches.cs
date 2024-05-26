using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Swap_Bros_Mod
{
    public class HarmonyPatches
    {
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

                int curPlayer = __instance.playerNum;

                if (!Main.settings.alwaysChosen)
                {
                    if (GameState.Instance.hardCoreMode && !Main.settings.ignoreCurrentUnlocked)
                    {
                        Main.CreateBroList();
                    }

                    // Set next hero to one of the enabled ones to ensure we don't spawn as a disabled character
                    if (Main.settings.filterBros && Main.brosRemoved && !GameModeController.IsHardcoreMode)
                    {
                        // Check if map has a forced bro
                        if (!Main.settings.ignoreForcedBros && Map.MapData.forcedBro != HeroType.Random)
                        {
                            nextHeroType = Map.MapData.forcedBro;

                            int nextHero = Main.currentBroList.IndexOf(Main.HeroTypeToString(nextHeroType));

                            if ( nextHero == -1 )
                            {
                                nextHero = 0;
                            }

                            if ( Main.settings.enableBromaker )
                            {
                                Main.DisableCustomBroSpawning(curPlayer);
                            }

                            Main.settings.selGridInt[curPlayer] = nextHero;
                        }
                        // Check if map has multiple forced bros
                        else if (!Main.settings.ignoreForcedBros && Map.MapData.forcedBros != null && Map.MapData.forcedBros.Count() > 0)
                        {
                            string nextHeroName = Main.currentBroListUnseen[UnityEngine.Random.Range(0, Main.currentBroListUnseen.Count())];

                            nextHeroType = Main.StringToHeroType(nextHeroName);

                            int nextHero = Main.currentBroList.IndexOf(nextHeroName);

                            if (nextHero == -1)
                            {
                                nextHero = 0;
                            }

                            if ( Main.settings.enableBromaker )
                            {
                                Main.DisableCustomBroSpawning(curPlayer);
                            }

                            Main.settings.selGridInt[curPlayer] = nextHero;
                        }
                        else
                        {
                            int nextHero = 0;

                            // If we're using vanilla bro selection and there are still bros that we haven't spawned as, prioritize those first
                            if (Main.settings.useVanillaBroSelection && Main.currentBroListUnseen.Count() > 0)
                            {
                                // Check if a previous character exists and ensure we don't spawn as them if possible
                                string previousCharacter = string.Empty;
                                if (__instance.character != null)
                                {
                                    if (!(Main.settings.enableBromaker && Main.CheckIfCustomBro(__instance.character, ref previousCharacter)))
                                    {
                                        previousCharacter = Main.HeroTypeToString(__instance.character.heroType);
                                    }

                                    // Don't remove bro unless this is a new list
                                    if (Main.currentBroListUnseen.Contains(previousCharacter) && Main.currentBroListUnseen.Count() > 1)
                                    {
                                        Main.currentBroListUnseen.Remove(previousCharacter);
                                    }
                                    else
                                    {
                                        previousCharacter = string.Empty;
                                    }
                                }
                                nextHero = Main.currentBroList.IndexOf(Main.currentBroListUnseen[UnityEngine.Random.Range(0, Main.currentBroListUnseen.Count())]);
                                if (previousCharacter != string.Empty)
                                {
                                    Main.currentBroListUnseen.Add(previousCharacter);
                                }

                                if (nextHero == -1)
                                {
                                    nextHero = 0;
                                }
                            }
                            else
                            {
                                nextHero = UnityEngine.Random.Range(0, Main.currentBroList.Count());
                            }

                            // Check if bro is custom or not
                            if (Main.IsBroCustom(nextHero))
                            {
                                Main.MakeCustomBroSpawn(curPlayer, Main.currentBroList[nextHero]);
                                nextHeroType = HeroType.Rambro;
                            }
                            else
                            {
                                if (Main.settings.enableBromaker)
                                    Main.DisableCustomBroSpawning(curPlayer);

                                nextHeroType = Main.StringToHeroType(Main.currentBroList[nextHero]);
                            }

                            Main.settings.selGridInt[curPlayer] = nextHero;
                        }
                    }
                    else
                    {
                        Main.SetSelectedBro(__instance.playerNum, nextHeroType);
                    }
                    return;
                }

                // If we're in IronBro and don't want to force spawn a bro we haven't unlocked
                if (GameState.Instance.hardCoreMode && !Main.settings.ignoreCurrentUnlocked)
                {
                    // Make sure list of available hardcore bros is up-to-date
                    Main.CreateBroList();
                    // If Bromaker is enabled and selected character is custom
                    if (Main.settings.enableBromaker && Main.IsBroCustom(Main.settings.selGridInt[curPlayer]))
                    {
                        Main.MakeCustomBroSpawn(curPlayer, Main.GetSelectedBroName(curPlayer));
                        // Ensure we don't spawn boondock bros because one gets left over
                        nextHeroType = HeroType.Rambro;
                    }
                    else
                    {
                        if (Main.settings.enableBromaker)
                            Main.DisableCustomBroSpawning(curPlayer);
                        nextHeroType = Main.GetSelectedBroHeroType(curPlayer);
                    }
                }
                // If bro spawning is a custom bro
                else if (Main.settings.enableBromaker && Main.IsBroCustom(Main.settings.selGridInt[curPlayer]) )
                {
                    Main.MakeCustomBroSpawn(curPlayer, Main.GetSelectedBroName(curPlayer));
                    // Ensure we don't spawn boondock bros because one gets left over
                    nextHeroType = HeroType.Rambro;
                }
                // If we're just spawning a normal character
                else
                {
                    if (Main.settings.enableBromaker)
                        Main.DisableCustomBroSpawning(curPlayer);
                    nextHeroType = Main.GetSelectedBroHeroType(curPlayer);
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
                    if ( Main.CheckIfCustomBro(__instance.character, ref name) )
                    {
                        if (name != Main.GetSelectedBroName(__instance.playerNum))
                        {
                            Main.settings.selGridInt[__instance.playerNum] = Main.currentBroList.IndexOf(name);
                            if (Main.settings.selGridInt[__instance.playerNum] == -1)
                            {
                                Main.CreateBroList();
                                Main.settings.selGridInt[__instance.playerNum] = Main.currentBroList.IndexOf(name);
                            }
                        }

                        Main.currentBroListUnseen.Remove(name);
                    }
                    else
                    {
                        Main.currentBroListUnseen.Remove(Main.HeroTypeToString(nextHeroType));
                    }
                }
                else
                {
                    Main.currentBroListUnseen.Remove(Main.HeroTypeToString(nextHeroType));
                }

                if (Main.currentBroListUnseen.Count() == 0)
                {
                    if (!Main.settings.ignoreForcedBros && Map.MapData != null && Map.MapData.forcedBros != null && Map.MapData.forcedBros.Count > 0)
                    {
                        Main.currentBroListUnseen.Clear();
                        for (int i = 0; i < Map.MapData.forcedBros.Count(); ++i)
                        {
                            Main.currentBroListUnseen.Add(Main.HeroTypeToString(Map.MapData.forcedBros[i]));
                        }
                    }
                    else
                    {
                        Main.currentBroListUnseen.AddRange(Main.currentBroList);
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

                if ((((leftPressed || rightPressed) && Main.cooldown == 0f && __instance.IsAlive()) || (Main.settings.clickingEnabled && Main.switched[curPlayer])) && __instance.character.pilottedUnit == null)
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
                        if (Main.IsBroCustom(Main.settings.selGridInt[curPlayer]))
                        {
                            Main.MakeCustomBroSpawn(curPlayer, Main.GetSelectedBroName(curPlayer));

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
                            __instance.SpawnHero(Main.GetSelectedBroHeroType(curPlayer));
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

                    if (leftPressed)
                    {
                        --Main.settings.selGridInt[curPlayer];
                        if (Main.settings.selGridInt[curPlayer] < 0)
                        {
                            Main.settings.selGridInt[curPlayer] = Main.maxBroNum;
                        }
                    }
                    else if (rightPressed)
                    {
                        ++Main.settings.selGridInt[curPlayer];
                        if (Main.settings.selGridInt[curPlayer] > Main.maxBroNum)
                        {
                            Main.settings.selGridInt[curPlayer] = 0;
                        }
                    }

                    // If character spawning is custom 
                    if (Main.settings.enableBromaker && Main.IsBroCustom(Main.settings.selGridInt[curPlayer]))
                    {
                        Main.MakeCustomBroSpawn(curPlayer, Main.GetSelectedBroName(curPlayer));

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

                        __instance.SpawnHero(Main.GetSelectedBroHeroType(curPlayer));

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

        [HarmonyPatch(typeof(Map), "Awake")]
        static class Map_Awake_Patch
        {
            static void Postfix()
            {
                if (!Main.enabled)
                {
                    return;
                }

                // Clear bro list
                Main.currentBroListUnseen.Clear();

                if ( !Main.settings.ignoreForcedBros && Map.MapData != null && Map.MapData.forcedBros != null && Map.MapData.forcedBros.Count > 0 )
                {
                    Main.currentBroListUnseen.Clear();
                    for (int i = 0; i < Map.MapData.forcedBros.Count(); ++i)
                    {
                        Main.currentBroListUnseen.Add(Main.HeroTypeToString(Map.MapData.forcedBros[i]));
                    }
                }
                else
                {
                    Main.currentBroListUnseen.AddRange(Main.currentBroList);
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
}
