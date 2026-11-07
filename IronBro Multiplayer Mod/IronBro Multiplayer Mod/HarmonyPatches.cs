using HarmonyLib;

namespace IronBro_Multiplayer_Mod
{
    [HarmonyPatch( typeof( GameModeController ), "AllowPlayerDropIn", MethodType.Getter )]
    static class GameModeController_AllowPlayerDropIn_Patch
    {
        public static void Postfix( GameModeController __instance, ref bool __result )
        {
            if ( !Main.enabled )
                return;
            __result = HeroController.InstanceExists && ( GameModeController.GameMode == GameMode.DeathMatch || GameModeController.GameMode == GameMode.Campaign || GameModeController.GameMode == GameMode.ExplosionRun ||
                GameModeController.IsHardcoreMode || ( GameModeController.GameMode == GameMode.Race && false ) );
        }
    }

    [HarmonyPatch( typeof( GameModeController ), "LevelFinish" )]
    static class GameModeController_LevelFinish_Patch
    {
        public static bool Prefix( GameModeController __instance, LevelResult result )
        {
            if ( !Main.enabled || !Main.settings.helicopterWait )
                return true;

            if ( result != LevelResult.Success || ( HeroController.GetPlayersOnHelicopterAmount() == 0 ) )
                return true;

            if ( HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount() )
            {
                Helicopter_Leave_Patch.attachCalled = false;
                // Null on portal levels
                if ( Main.heli != null )
                    Main.heli.Leave();
                return true;
            }
            else
            {
                Main.control = __instance;
                return false;
            }


        }
    }

    [HarmonyPatch( typeof( HeroController ), "AttachHeroToHelicopter" )]
    static class HeroController_AttachHeroToHelicopter_Patch
    {
        // The RPC runs heli.Leave() before the hero is aboard, and AttachToHelicopter only runs on the boarding player's machine
        public static void Prefix( TestVanDammeAnim hero, Helicopter heli )
        {
            if ( !Main.enabled )
                return;

            if ( hero != null && heli != null )
                Helicopter_Leave_Patch.attachCalled = true;
        }

        public static void Postfix( TestVanDammeAnim hero, Helicopter heli, bool startExplosions )
        {
            // startExplosions is false on HelicopterFake and trigger-won levels, where boarding doesn't finish the level
            if ( !Main.enabled || !Main.settings.helicopterWait || hero == null || heli == null || !startExplosions )
                return;

            Main.FinishLevelIfAllAboard();
        }
    }

    [HarmonyPatch( typeof( TestVanDammeAnim ), "AttachToHelicopter" )]
    static class TestVanDammeAnim_AttachToHelicopter_Patch
    {
        public static void Prefix( TestVanDammeAnim __instance )
        {
            Helicopter_Leave_Patch.attachCalled = true;
        }
    }

    [HarmonyPatch( typeof( Helicopter ), "Leave" )]
    static class Helicopter_Leave_Patch
    {
        public static bool attachCalled = false;
        public static bool Prefix( Helicopter __instance )
        {
            if ( !Main.enabled || !Main.settings.helicopterWait )
                return true;

            if ( HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount() || ( HeroController.GetPlayersOnHelicopterAmount() == 0 && !attachCalled ) )
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

    [HarmonyPatch( typeof( Map ), "Awake" )]
    static class Map_Awake_Patch
    {
        public static void Postfix()
        {
            Helicopter_Leave_Patch.attachCalled = false;
            Main.heli = null;
        }
    }

    [HarmonyPatch( typeof( TestVanDammeAnim ), "SuckIntoPortal" )]
    static class TestVanDammeAnim_SuckIntoPortal_Patch
    {
        public static void Postfix( TestVanDammeAnim __instance )
        {
            if ( !Main.enabled || !Main.settings.helicopterWait )
                return;

            // IsOverFinish only sets this after calling LevelFinish and starting the level end explosions
            __instance.isOnHelicopter = true;
            Main.FinishLevelIfAllAboard();
        }
    }

    [HarmonyPatch( typeof( Map ), "StartLevelEndExplosions" )]
    static class Map_StartLevelEndExplosions_Patch
    {
        public static bool Prefix( Map __instance )
        {
            if ( !Main.enabled || !Main.settings.helicopterWait )
                return true;


            if ( HeroController.GetPlayersOnHelicopterAmount() == HeroController.GetPlayersAliveCount() )
            {
                return true;
            }
            else
            {
                Main.map = __instance;
                return false;
            }
        }
    }

    [HarmonyPatch( typeof( Player ), "RemoveLife" )]
    static class Player_RemoveLife_Patch
    {
        public static void Postfix( Player __instance )
        {
            if ( !Main.enabled )
                return;

            if ( GameModeController.IsHardcoreMode && ( Main.AllPlayersAboard() || ( HeroController.GetTotalLives() == 0 ) ) )
            {
                GameModeController.LevelFinish( LevelResult.ForcedFail );
            }
            if ( !GameModeController.IsHardcoreMode && Main.AllPlayersAboard() )
            {
                GameModeController.LevelFinish( LevelResult.Success );
            }

        }
    }

    // Fix other players live counters not updating
    [HarmonyPatch( typeof( Player ), "SetLivesRPC" )]
    static class Player_SetLivesRPC_Patch
    {
        public static void Prefix( Player __instance, ref int _lives )
        {
            if ( !Main.enabled )
            {
                return;
            }

            if ( GameModeController.IsHardcoreMode && HeroController.GetPlayersPlayingCount() > 1 )
            {
                for ( int i = 0; i < 4; ++i )
                {
                    if ( i != __instance.playerNum && HeroController.IsPlayerPlaying( i ) )
                    {
                        HeroController.players[i].hud.SetLives( _lives );
                    }
                }
            }
        }
    }

    [HarmonyPatch( typeof( GameModeController ), "IsHardcoreMode", MethodType.Getter )]
    static class GameModeController_IsHardcoreMode_Patch
    {
        public static bool disableHardcoreCheck = false;
        public static void Postfix( GameModeController __instance, ref bool __result )
        {
            if ( disableHardcoreCheck )
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch( typeof( PauseMenu ), "HandlegamePausedChangedEvent" )]
    static class PauseMenu_ReturnToMenu_Patch
    {
        public static void Prefix()
        {
            GameModeController_IsHardcoreMode_Patch.disableHardcoreCheck = true;
        }
        public static void Postfix()
        {
            GameModeController_IsHardcoreMode_Patch.disableHardcoreCheck = false;
        }
    }

    // Allow join screen to show multiple players in ironbro mode
    [HarmonyPatch( typeof( JoinScreen ), "Start" )]
    static class JoinScreen_Start_Patch
    {
        public static void Prefix()
        {
            if ( !Main.enabled )
            {
                return;
            }

            GameModeController_IsHardcoreMode_Patch.disableHardcoreCheck = true;
        }

        public static void Postfix()
        {
            if ( !Main.enabled )
            {
                return;
            }

            GameModeController_IsHardcoreMode_Patch.disableHardcoreCheck = false;
        }
    }

    // Allow multiple players to join on join screen in ironbro mode
    [HarmonyPatch( typeof( JoinScreen ), "Update" )]
    static class JoinScreen_Update_Patch
    {
        public static void Prefix()
        {
            if ( !Main.enabled )
            {
                return;
            }

            GameModeController_IsHardcoreMode_Patch.disableHardcoreCheck = true;
        }

        public static void Postfix()
        {
            if ( !Main.enabled )
            {
                return;
            }

            GameModeController_IsHardcoreMode_Patch.disableHardcoreCheck = false;
        }
    }

    // Allow multiple players to join on join screen in ironbro mode
    [HarmonyPatch( typeof( JoinScreen ), "Join" )]
    static class JoinScreen_Join_Patch
    {
        public static void Prefix( JoinScreen __instance )
        {
            if ( !Main.enabled )
            {
                return;
            }

            GameModeController_IsHardcoreMode_Patch.disableHardcoreCheck = true;
        }

        public static void Postfix( JoinScreen __instance )
        {
            if ( !Main.enabled )
            {
                return;
            }

            GameModeController_IsHardcoreMode_Patch.disableHardcoreCheck = false;
        }
    }

    // Fix rejoining after dropping out causing you to lose all lives
    [HarmonyPatch( typeof( HeroController ), "DropoutRPC" )]
    static class HeroController_DropoutRPC_Patch
    {
        public static void Postfix( HeroController __instance )
        {
            if ( !Main.enabled )
            {
                return;
            }

            if ( GameModeController.IsHardcoreMode )
            {
                HeroController.Instance.IDroppedOutThisRound = false;
            }
        }
    }
}
