using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace BroforceDevTools.Demystify
{
    internal static class DemystifyPatcher
    {
        private static bool applied;

        internal static void Apply(Harmony harmony)
        {
            if (applied) return;

            var prefixStackTrace = typeof(DemystifyPatcher).GetMethod("ExceptionGetStackTrace",
                BindingFlags.Static | BindingFlags.Public);
            var prefixToString = typeof(DemystifyPatcher).GetMethod("ExceptionToStringHook",
                BindingFlags.Static | BindingFlags.Public);

            harmony.Patch(GetStackTraceGetter(), prefix: new HarmonyMethod(prefixStackTrace));
            harmony.Patch(GetToStringMethod(), prefix: new HarmonyMethod(prefixToString));

            applied = true;
        }

        public static void Remove(Harmony harmony)
        {
            if (!applied) return;

            harmony.Unpatch(GetStackTraceGetter(), HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(GetToStringMethod(), HarmonyPatchType.Prefix, harmony.Id);

            applied = false;
        }

        private static MethodBase GetStackTraceGetter()
        {
            return typeof(Exception).GetProperty("StackTrace").GetGetMethod();
        }

        private static MethodBase GetToStringMethod()
        {
            return typeof(Exception).GetMethod("ToString", new Type[0]);
        }

        // string.IsNullOrWhiteSpace is .NET 4.0; blank output means fall back to the unpatched method.
        private static bool IsBlank(string text)
        {
            return string.IsNullOrEmpty(text) || text.Trim().Length == 0;
        }

        public static bool ExceptionGetStackTrace(Exception __instance, ref string __result)
        {
            try
            {
                var exStackTrace = new EnhancedStackTrace(__instance);
                var text = exStackTrace.ToString();
                if (IsBlank(text)) return true;

                __result = text;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static bool ExceptionToStringHook(Exception __instance, ref string __result)
        {
            try
            {
                var text = __instance.ToStringDemystified();
                if (IsBlank(text)) return true;

                __result = text;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
