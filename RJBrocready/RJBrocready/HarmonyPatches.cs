using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using BroMakerLib.Loggers;

namespace RJBrocready
{
    class HarmonyPatches
    {
        [HarmonyPatch(typeof(ZipLine), "ShouldUnitAttach")]
        static class ZipLine_ShouldUnitAttach_Patch
        {
            public static bool Prefix(ref Unit unit, ref bool __result)
            {
                if ( unit is RJBrocready )
                {
                    RJBrocready rjbrocready = unit as RJBrocready;
                    // Don't attach brocready to zipline if he's in monster form
                    if ( rjbrocready.currentState > RJBrocready.ThingState.HumanForm )
                    {
                        __result = false;
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
