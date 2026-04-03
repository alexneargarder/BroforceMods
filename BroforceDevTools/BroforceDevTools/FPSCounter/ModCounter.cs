using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace BroforceDevTools.FPSCounter
{
    internal static class ModCounter
    {
        private static readonly Dictionary<Type, KeyValuePair<string, Dictionary<string, Stopwatch>>> timers =
            new Dictionary<Type, KeyValuePair<string, Dictionary<string, Stopwatch>>>();

        private static readonly Dictionary<string, Dictionary<string, MovingAverage>> averages =
            new Dictionary<string, Dictionary<string, MovingAverage>>();

        private static Harmony harmonyInstance;
        private static bool running;
        private static FixedString fString;
        private static Action stopAction;

        public static string StringOutput { get; private set; }

        public static void Start(MonoBehaviour mb)
        {
            if (running) return;
            running = true;

            if (harmonyInstance == null)
                harmonyInstance = new Harmony("BroforceDevTools.ModCounter");

            if (fString == null)
                fString = new FixedString(800);

            var hookCount = 0;
            var modCount = 0;

            var baseType = typeof(MonoBehaviour);
            var unityMethods = new[] { "FixedUpdate", "Update", "LateUpdate", "OnGUI" };

            foreach (var modEntry in UnityModManager.modEntries)
            {
                if (modEntry.Assembly == null) continue;
                if (modEntry.Info.Id == "BroforceDevTools") continue;

                var modName = modEntry.Info.DisplayName;
                modCount++;

                var modTypes = SafeGetTypes(modEntry.Assembly)
                    .Where(x => baseType.IsAssignableFrom(x) && !x.IsAbstract)
                    .ToList();

                foreach (var modType in modTypes)
                {
                    foreach (var unityMethod in unityMethods)
                    {
                        var methodInfo = modType.GetMethod(unityMethod,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (methodInfo == null) continue;

                        if (!timers.ContainsKey(modType))
                        {
                            timers[modType] = new KeyValuePair<string, Dictionary<string, Stopwatch>>(
                                modName, new Dictionary<string, Stopwatch>());
                        }

                        var methodTimers = timers[modType].Value;
                        if (!methodTimers.ContainsKey(unityMethod))
                        {
                            methodTimers[unityMethod] = new Stopwatch();
                        }

                        if (!averages.ContainsKey(modName))
                        {
                            averages[modName] = new Dictionary<string, MovingAverage>();
                        }
                        if (!averages[modName].ContainsKey(unityMethod))
                        {
                            averages[modName][unityMethod] = new MovingAverage(60);
                        }

                        try
                        {
                            harmonyInstance.Patch(methodInfo,
                                new HarmonyMethod(AccessTools.Method(typeof(ModCounter), "Pre")),
                                new HarmonyMethod(AccessTools.Method(typeof(ModCounter), "Post")));
                            hookCount++;
                        }
                        catch (Exception) { }
                    }
                }
            }

            var co = mb.StartCoroutine(CollectLoop());
            stopAction = () => mb.StopCoroutine(co);

            Main.Log("ModCounter: Attached " + hookCount + " hooks across " + modCount + " mods");
        }

        public static void Stop()
        {
            if (!running) return;

            if (harmonyInstance != null)
                harmonyInstance.UnpatchAll("BroforceDevTools.ModCounter");

            running = false;

            foreach (var t in timers)
                foreach (var sw in t.Value.Value.Values)
                    sw.Reset();
            timers.Clear();
            averages.Clear();

            if (stopAction != null)
                stopAction();

            StringOutput = null;
        }

        public static List<ModTimingData> GetModTimings()
        {
            var result = new List<ModTimingData>();
            var nanosecPerTick = 1000L * 1000L * 100L / Stopwatch.Frequency;
            var msScale = 1f / (nanosecPerTick * 1000f);

            foreach (var modAvgs in averages)
            {
                var data = new ModTimingData();
                data.ModName = modAvgs.Key;

                MovingAverage ma;
                if (modAvgs.Value.TryGetValue("Update", out ma))
                    data.UpdateMs = ma.GetAverage() * msScale;
                if (modAvgs.Value.TryGetValue("FixedUpdate", out ma))
                    data.FixedUpdateMs = ma.GetAverage() * msScale;
                if (modAvgs.Value.TryGetValue("LateUpdate", out ma))
                    data.LateUpdateMs = ma.GetAverage() * msScale;
                if (modAvgs.Value.TryGetValue("OnGUI", out ma))
                    data.OnGUIMs = ma.GetAverage() * msScale;
                data.TotalMs = data.UpdateMs + data.FixedUpdateMs + data.LateUpdateMs + data.OnGUIMs;

                if (data.TotalMs > 0.01f)
                    result.Add(data);
            }

            result.Sort((a, b) => b.TotalMs.CompareTo(a.TotalMs));
            return result;
        }

        private static readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
        private static IEnumerator CollectLoop()
        {
            var nanosecPerTick = 1000L * 1000L * 100L / Stopwatch.Frequency;
            var msScale = 1f / (nanosecPerTick * 1000f);
            var cutoffTicks = nanosecPerTick * 100;
            var builder = fString.builder;

            while (true)
            {
                yield return waitForEndOfFrame;

                // Aggregate per-type stopwatches into per-mod per-callback averages
                foreach (var typeEntry in timers)
                {
                    var modName = typeEntry.Value.Key;
                    var methodTimers = typeEntry.Value.Value;

                    Dictionary<string, MovingAverage> modAvgs;
                    if (!averages.TryGetValue(modName, out modAvgs)) continue;

                    foreach (var methodTimer in methodTimers)
                    {
                        MovingAverage ma;
                        if (modAvgs.TryGetValue(methodTimer.Key, out ma))
                        {
                            ma.Sample(methodTimer.Value.ElapsedTicks);
                        }
                        methodTimer.Value.Reset();
                    }
                }

                // Build display string
                var modList = new List<KeyValuePair<string, float>>();
                foreach (var modAvgs in averages)
                {
                    long totalTicks = 0;
                    foreach (var ma in modAvgs.Value.Values)
                        totalTicks += ma.GetAverage();

                    if (totalTicks > cutoffTicks)
                        modList.Add(new KeyValuePair<string, float>(modAvgs.Key, totalTicks * msScale));
                }

                if (modList.Count > 0)
                {
                    modList.Sort((a, b) => b.Value.CompareTo(a.Value));
                    int c = 0;
                    foreach (var mod in modList)
                    {
                        if (c > 0) builder.Concat(", ");
                        builder.Concat(mod.Key);
                        builder.Concat(":");
                        builder.Concat(mod.Value, 1, 2);
                        builder.Concat("ms");
                        if (c++ >= 5) break;
                    }
                }
                else
                {
                    builder.Concat("No slow mods");
                }

                StringOutput = fString.PopValue();
            }
        }

        private static void Post(MonoBehaviour __instance, MethodInfo __originalMethod)
        {
            if (!FrameCounterHelper.CanProcessOnGui && __originalMethod.Name == "OnGUI") return;

            KeyValuePair<string, Dictionary<string, Stopwatch>> entry;
            if (timers.TryGetValue(__instance.GetType(), out entry))
            {
                Stopwatch sw;
                if (entry.Value.TryGetValue(__originalMethod.Name, out sw))
                    sw.Stop();
            }
        }

        private static void Pre(MonoBehaviour __instance, MethodInfo __originalMethod)
        {
            if (!FrameCounterHelper.CanProcessOnGui && __originalMethod.Name == "OnGUI") return;

            KeyValuePair<string, Dictionary<string, Stopwatch>> entry;
            if (timers.TryGetValue(__instance.GetType(), out entry))
            {
                Stopwatch sw;
                if (entry.Value.TryGetValue(__originalMethod.Name, out sw))
                    sw.Start();
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly ass)
        {
            try { return ass.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null); }
        }
    }

}
