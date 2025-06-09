using BroMakerLib.Loggers;
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
                if ( !(__instance is ShootableCircularDoodad shootable) )
                {
                    return;
                }
                __result = shootable.IsPointInRange( x, y, range );
            }
        }

    }
}
