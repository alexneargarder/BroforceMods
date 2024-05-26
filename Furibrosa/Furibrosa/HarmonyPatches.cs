using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace Furibrosa
{
    class HarmonyPatches
    {
        [HarmonyPatch(typeof(TestVanDammeAnim), "RunBlindStars")]
        static class TestVanDammeAnim_RunBlindStars_Patch
        {
            public static bool Prefix(Mook __instance)
            {
                if (Furibrosa.grabbedUnits.Contains(__instance))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(TestVanDammeAnim), "EvaluateIsJumping")]
        static class TestVanDammeAnim_EvaluateIsJumping_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (Furibrosa.grabbedUnits.Contains(__instance))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Mook), "Land")]
        static class Mook_Land_Patch
        {
            public static bool Prefix(Mook __instance)
            {
                if (Furibrosa.grabbedUnits.Contains(__instance))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Mook), "GetGroundHeightGround")]
        static class Mook_GetGroundHeightGround_Patch
        {
            public static bool Prefix(Mook __instance, ref float __result)
            {
                if (Furibrosa.grabbedUnits.Contains(__instance))
                {
                    __result = __instance.Y;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(TestVanDammeAnim), "RunAvatarRunning")]
        static class TestVanDammeAnim_RunAvatarRunning_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (Furibrosa.grabbedUnits.Contains(__instance))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Mook), "IsOnGround")]
        static class Mook_IsOnGround_Patch
        {
            public static bool Prefix(Mook __instance, ref bool __result)
            {
                if (Furibrosa.grabbedUnits.Contains(__instance))
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
    }
}
