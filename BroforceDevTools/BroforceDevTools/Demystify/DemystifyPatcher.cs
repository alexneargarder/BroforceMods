using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace BroforceDevTools.Demystify
{
    internal static class DemystifyPatcher
    {
        internal static void Apply(Harmony harmony)
        {
            var stackTraceGetter = typeof(Exception).GetProperty("StackTrace").GetGetMethod();
            var toStringMethod = typeof(Exception).GetMethod("ToString", new Type[0]);

            var prefixStackTrace = typeof(DemystifyPatcher).GetMethod("ExceptionGetStackTrace",
                BindingFlags.Static | BindingFlags.Public);
            var prefixToString = typeof(DemystifyPatcher).GetMethod("ExceptionToStringHook",
                BindingFlags.Static | BindingFlags.Public);

            harmony.Patch(stackTraceGetter, prefix: new HarmonyMethod(prefixStackTrace));
            harmony.Patch(toStringMethod, prefix: new HarmonyMethod(prefixToString));
        }

        public static bool ExceptionGetStackTrace(Exception __instance, ref string __result)
        {
            try
            {
                var exStackTrace = new EnhancedStackTrace(__instance);
                __result = exStackTrace.ToString();
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        [HarmonyPatch(typeof(Exception), "ToString", new Type[0])]
        [HarmonyPrefix]
        public static bool ExceptionToStringHook(Exception __instance, ref string __result)
        {
            try
            {
                __result = __instance.ToStringDemystified();
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
