/**
 * TODO
 * 
 * Fix weird crashes which seem to be more frequent in the Hell levels
 * 
 * Sync lives correctly in iron bro multiplayer
 * 
 * drop out works but is sort of weird if player 1 leaves and rejoins
 * 
 * Fix other player dying when one player finishes level via zip line in hell
 * 
 * Helicopter stays and level doesn't end on special challenge levels
 * 
**/
/**
 * FIXED
 * 
 * dropout doesn't appear
 * 
 * Doesn't work well in Normal mode multiplayer (if both players die, the level finishes and you win)
 * 
 * If in singleplayer, dying resets the level
 * 
 * helicopter doesn't leave on levels where helicopter drops you off (not a huge deal)
 * 
 * 
 * 
**/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;

namespace IronBro_Multiplayer_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        public static int playersFinished = 0;
        public static GameModeController control;
        public static Map map;
        public static Helicopter heli;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = Settings.Load<Settings>(modEntry);


            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);
            mod = modEntry;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.helicopterWait = GUILayout.Toggle(settings.helicopterWait, "Helicopter waits for all players", GUILayout.Width(100f));
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


    }

    [HarmonyPatch(typeof(GameModeController), "AllowPlayerDropIn", MethodType.Getter)]
    static class GameModeController_AllowPlayerDropIn_Patch
    {
        static void Postfix(GameModeController __instance, ref bool __result)
        {
            if (!Main.enabled)
                return;
            __result = HeroController.InstanceExists && //!(__instance == null) && 
                (GameModeController.GameMode == GameMode.DeathMatch || GameModeController.GameMode == GameMode.Campaign || GameModeController.GameMode == GameMode.ExplosionRun || 
                GameModeController.IsHardcoreMode || (GameModeController.GameMode == GameMode.Race && false));
        }
    }

    [HarmonyPatch(typeof(GameModeController), "LevelFinish")]
    static class GameModeController_LevelFinish_Patch
    {
        static bool Prefix(GameModeController __instance, LevelResult result)
        {
            //Main.mod.Logger.Log("level finsih called");
            if (!Main.enabled || !Main.settings.helicopterWait)
                return true;

            if (result != LevelResult.Success || (HeroController.GetPlayersOnHelicopterAmount() == 0)) 
                return true;

            //Main.mod.Logger.Log(HeroController.GetPlayersOnHelicopterAmount() + " == " + HeroController.GetPlayersAliveCount());
            if (HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount())
            {
                Helicopter_Leave_Patch.attachCalled = false;
                Main.heli.Leave();
                //Main.mod.Logger.Log("after helicopter leave");
                return true;
            }
            else
            {
                Main.control = __instance;
                return false;
            }

            
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "AttachToHelicopter")]
    static class TestVanDammeAnim_AttachToHelicopter_Patch
    {
        static void Prefix(TestVanDammeAnim __instance)
        {
            Helicopter_Leave_Patch.attachCalled = true;
        }
    }

    [HarmonyPatch(typeof(Helicopter), "Leave")]
    static class Helicopter_Leave_Patch
    {
        public static bool attachCalled = false;
        static bool Prefix(Helicopter __instance)
        {
            //Main.mod.Logger.Log("leave called");
            if (!Main.enabled || !Main.settings.helicopterWait)
                return true;

            if (HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount() || (HeroController.GetPlayersOnHelicopterAmount() == 0 && !attachCalled))
            {
                return true;
            }
            else
            {
                Main.heli = __instance;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(Map), "StartLevelEndExplosions")]
    static class Map_StartLevelEndExplosions_Patch
    {
        static bool Prefix(Map __instance)
        {
            //Main.mod.Logger.Log("end level explosions called");
            if (!Main.enabled || !Main.settings.helicopterWait)
                return true;

           
            if (HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount())
            {
                //Main.mod.Logger.Log("Helicopter: " + HeroController.GetPlayersOnHelicopterAmount() + "   alive:   " + HeroController.GetPlayersAliveCount());
                return true;
            }
            else
            {
                Main.map = __instance;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(Player), "RemoveLife")]
    static class Player_RemoveLife_Patch
    {
        static void Postfix(Player __instance)
        {
            if (!Main.enabled)
                return;

            if (GameModeController.IsHardcoreMode && (((HeroController.GetPlayersOnHelicopterAmount() == (HeroController.GetPlayersAliveCount()) && HeroController.GetPlayersOnHelicopterAmount() > 0))|| (HeroController.GetTotalLives() == 0)) )
            {
                //Main.mod.Logger.Log("you have failed");
                //Main.mod.Logger.Log("Players on helicopter amount = " + HeroController.GetPlayersOnHelicopterAmount() + " players alive count = " + HeroController.GetPlayersAliveCount()
                //    + " total lives = " + HeroController.GetTotalLives());
                GameModeController.LevelFinish(LevelResult.ForcedFail);
            }
            if (!GameModeController.IsHardcoreMode && HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount() && HeroController.GetPlayersOnHelicopterAmount() > 0)
            {
                GameModeController.LevelFinish(LevelResult.Success);
            }

        }
    }

    [HarmonyPatch(typeof(GameModeController), "IsHardcoreMode", MethodType.Getter)]
    static class GameModeController_IsHardcoreMode_Patch
    {
        public static bool paused = false;
        static void Postfix(GameModeController __instance, ref bool __result)
        {
            if (paused)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(PauseMenu), "HandlegamePausedChangedEvent")]
    static class PauseMenu_ReturnToMenu_Patch
    {
        static void Prefix(PauseMenu __instance)
        {
            GameModeController_IsHardcoreMode_Patch.paused = true;
        }
        static void Postfix(PauseMenu __instance)
        {
            GameModeController_IsHardcoreMode_Patch.paused = false;
        }
    }

/*    [HarmonyPatch(typeof(HeroController), "AddLife")]
    static class HeroController_AddLife_Patch
    {
        static void Postfix(HeroController __instance, int playerNum)
        {
            Main.mod.Logger.Log("collect by: " + playerNum);
            for (int i = 0; i < HeroController.players.Length; i++)
            {
                
                if (i != playerNum)
                {
                    Main.mod.Logger.Log("player: " + i + " added");
                    HeroController.players[playerNum].AddLife();
                }
            }
        }
    }*/

    public class Settings : UnityModManager.ModSettings
    {
        public bool helicopterWait;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }


}
