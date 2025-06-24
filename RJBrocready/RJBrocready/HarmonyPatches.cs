using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using BroMakerLib.Loggers;
using BroMakerLib.CustomObjects.Bros;

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

        [HarmonyPatch(typeof(SaveSlotsMenu), "SelectSlot")]
        static class SaveSlotsMenu_SelectSlot_Patch
        {
            public static void Prefix(SaveSlotsMenu __instance, ref int slot)
            {
                if (SaveSlotsMenu.createNewGame)
                {
                    try
                    {
                        // If a new save is being created, set previously died in IronBro to false
                        CustomHero.LoadSettings<RJBrocready>();
                        if ( RJBrocready.previouslyDiedInIronBro.Count() != 5 )
                        {
                            RJBrocready.previouslyDiedInIronBro = new List<bool> { false, false, false, false, false };
                        }
                        RJBrocready.previouslyDiedInIronBro[slot] = false;
                        CustomHero.SaveSettings<RJBrocready>();
                    }
                    catch (Exception e)
                    {
                        BMLogger.ExceptionLog(e);
                    }
                }
            }
        }
    }
}
