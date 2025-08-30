using Drunken_Broster.MeleeItems;
using HarmonyLib;

namespace Drunken_Broster
{
    public class HarmonyPatches
    {
        [HarmonyPatch( typeof( Doodad ), "IsPointInRange" )]
        static class Doodad_IsPointInRange_Patch
        {
            public static void Postfix( Doodad __instance, ref float x, ref float y, ref float range, ref bool __result )
            {
                if ( !( __instance is ShootableCircularDoodad shootable ) )
                {
                    return;
                }
                __result = shootable.IsPointInRange( x, y, range );
            }
        }

        [HarmonyPatch( typeof( HellLostSoul ), "CanHitEnemyInDive" )]
        static class HellLostSoul_CanHitEnemyInDive_Patch
        {
            public static bool Prefix( HellLostSoul __instance, ref bool __result )
            {
                if ( __instance.playerNum != 5 )
                {
                    return true;
                }

                __result = Map.HitUnits( __instance, __instance, __instance.playerNum, 4, DamageType.Melee, 3f, __instance.X + __instance.xI / __instance.diveSpeed * 4f, __instance.Y + __instance.yI / __instance.diveSpeed * 4f, __instance.xI, __instance.yI, false, false );
                return false;
            }
        }

        // Check if friendly facehugger has completed insemination
        [HarmonyPatch( typeof( AlienFaceHugger ), "DisconnectFaceHugger" )]
        static class AlienFaceHugger_DisconnectFaceHugger_Patch
        {
            public static void Postfix( AlienFaceHugger __instance )
            {
                if ( __instance.insemenationCompleted && __instance.layEggsInsideBros )
                {
                    AlienXenomorph_Start_Patch.nextAlienFriendly = true;
                }
            }
        }

        // Make xenomorphs spawned from thrown facehugger eggs friendly
        [HarmonyPatch( typeof( AlienXenomorph ), "Start" )]
        public static class AlienXenomorph_Start_Patch
        {
            public static bool nextAlienFriendly = false;

            public static void Postfix( AlienXenomorph __instance )
            {
                if ( nextAlienFriendly )
                {
                    __instance.playerNum = 5;
                    __instance.firingPlayerNum = 5;

                    nextAlienFriendly = false;
                }
            }
        }
    }
}
