using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Control_Enemies_Mod
{
    public class HarmonyPatches
    {
        // Disable AI of enemy we're controlling
        [HarmonyPatch(typeof(PolymorphicAI), "Update")]
        static class PolymorphicAI_Update_Patch
        {
            public static bool Prefix(PolymorphicAI __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if ( __instance.name == "controlled" )
                {
                    __instance.mentalState = MentalState.Alerted;
                    return false;
                }

                return true;
            }
        }

        // Disable AI of enemy we're controlling
        [HarmonyPatch(typeof(PolymorphicAI), "LateUpdate")]
        static class PolymorphicAI_LateUpdate_Patch
        {
            public static bool Prefix(PolymorphicAI __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    __instance.mentalState = MentalState.Alerted;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Mook), "GetEnemyMovement")]
        static class Mook_GetEnemyMovement_Patch
        {
            public static bool Prefix(Mook __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if (__instance.playerNum >= 0 && __instance.IsHero)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Mook), "FallDamage")]
        static class Mook_FallDamage_Patch
        {
            public static bool Prefix(Mook __instance, ref float yI)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if (__instance.playerNum >= 0 && __instance.IsHero)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
