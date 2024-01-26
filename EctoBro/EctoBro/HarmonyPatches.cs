using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using HarmonyLib;
using BroMakerLib.Loggers;

namespace EctoBro
{
    class HarmonyPatches
    {
        [HarmonyPatch(typeof(TestVanDammeAnim), "RunBlindStars")]
        static class TestVanDammeAnim_RunBlindStars_Patch
        {
            public static bool Prefix(Mook __instance)
            {
                if ( GhostTrap.grabbedUnits.Count > 0 && GhostTrap.grabbedUnits.Contains(__instance) )
                {
                    return false;
                }
                return true;
            }
        }

/*        [HarmonyPatch(typeof(Mook), "IsOnGround")]
        static class Mook_IsOnGround_Patch
        {
            public static void Postfix(Mook __instance, ref bool __result)
            {
                if (GhostTrap.grabbedUnits.Count > 0 && GhostTrap.grabbedUnits.Contains(__instance))
                {
                    //BMLogger.Log("overrode ground");
                    __result = true;
                }
            }
        }*/

        [HarmonyPatch(typeof(TestVanDammeAnim), "EvaluateIsJumping")]
        static class TestVanDammeAnim_EvaluateIsJumping_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (GhostTrap.grabbedUnits.Count > 0 && GhostTrap.grabbedUnits.Contains(__instance))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Mook), "Land")]
        static class Mook_Land_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (GhostTrap.grabbedUnits.Count > 0 && GhostTrap.grabbedUnits.Contains(__instance))
                {
                    return false;
                }
                return true;
            }
        }
    }
}
