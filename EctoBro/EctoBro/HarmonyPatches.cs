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
                if ( GhostTrap.grabbedUnits.Count > 0 && GhostTrap.grabbedUnits.ContainsKey(__instance) )
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
                if (GhostTrap.grabbedUnits.Count > 0 && GhostTrap.grabbedUnits.ContainsKey(__instance))
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
                if (GhostTrap.grabbedUnits.Count > 0 && GhostTrap.grabbedUnits.ContainsKey(__instance))
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
                FloatingUnit unit;
                if (GhostTrap.grabbedUnits.Count > 0 && GhostTrap.grabbedUnits.TryGetValue(__instance, out unit))
                {
                    __result = unit.currentPosition.y;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(EffectsController), "GetBloodColor")]
        static class EffectsController_GetBloodColor_Patch
        {
            public static void Postfix(ref BloodColor color, ref Color __result)
            {
                if (color == BloodColor.Green && Slimer.overrideBloodColor > 0 )
                {
                    __result = EctoBro.SlimerColor;
                }
            }
        }

        [HarmonyPatch(typeof(Sound), "PlayAudioClip")]
        static class Sound_PlayAudioClip_Patch
        {
            public static void Prefix(ref AudioClip clip)
            {

                //BMLogger.Log("audio clip played: " + clip.name);
            }
        }
    }
}
