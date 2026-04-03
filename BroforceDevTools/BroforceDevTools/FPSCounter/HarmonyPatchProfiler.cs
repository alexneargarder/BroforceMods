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
    internal static class HarmonyPatchProfiler
    {
        private class PatchEntry
        {
            public string ModName;
            public string TargetMethod;
            public string PatchType;
            public Stopwatch Timer;
            public MovingAverage Average;
            public int FrameCallCount;
        }

        private static readonly Dictionary<MethodBase, PatchEntry> patchTimers =
            new Dictionary<MethodBase, PatchEntry>();

        private static Harmony harmonyInstance;
        private static bool running;
        private static FixedString fString;
        private static Action stopAction;

        public static string StringOutput { get; private set; }
        public static bool IsRunning { get { return running; } }

        public static void Start(MonoBehaviour mb)
        {
            if (running) return;
            running = true;

            if (harmonyInstance == null)
                harmonyInstance = new Harmony("BroforceDevTools.PatchProfiler");
            if (fString == null)
                fString = new FixedString(1000);

            ScanAndHookPatches();

            var co = mb.StartCoroutine(CollectLoop());
            stopAction = () => mb.StopCoroutine(co);
        }

        public static void Stop()
        {
            if (!running) return;
            running = false;

            if (harmonyInstance != null)
                harmonyInstance.UnpatchAll("BroforceDevTools.PatchProfiler");

            patchTimers.Clear();

            if (stopAction != null)
                stopAction();

            StringOutput = null;
        }

        public static void Refresh()
        {
            if (!running) return;

            harmonyInstance.UnpatchAll("BroforceDevTools.PatchProfiler");
            patchTimers.Clear();
            ScanAndHookPatches();
        }

        public static List<PatchTimingData> GetPatchTimings()
        {
            var nanosecPerTick = 1000L * 1000L * 100L / Stopwatch.Frequency;
            var msScale = 1f / (nanosecPerTick * 1000f);

            return patchTimers.Values
                .Where(p => p.Average.GetAverage() > 0)
                .Select(p => new PatchTimingData
                {
                    ModName = p.ModName,
                    TargetMethod = p.TargetMethod,
                    PatchType = p.PatchType,
                    AverageMs = p.Average.GetAverage() * msScale,
                    CallsPerFrame = p.FrameCallCount
                })
                .OrderByDescending(p => p.AverageMs)
                .ToList();
        }

        private static void ScanAndHookPatches()
        {
            var modAssemblyMap = new Dictionary<Assembly, string>();
            foreach (var mod in UnityModManager.modEntries)
            {
                if (mod.Assembly != null && mod.Info.Id != "BroforceDevTools")
                    modAssemblyMap[mod.Assembly] = mod.Info.DisplayName;
            }

            var pre = new HarmonyMethod(AccessTools.Method(typeof(HarmonyPatchProfiler), "TimerPre"));
            var post = new HarmonyMethod(AccessTools.Method(typeof(HarmonyPatchProfiler), "TimerPost"));

            int hooked = 0;
            foreach (var method in Harmony.GetAllPatchedMethods().ToList())
            {
                var patches = Harmony.GetPatchInfo(method);
                if (patches == null) continue;

                var targetName = method.DeclaringType != null
                    ? method.DeclaringType.Name + "." + method.Name
                    : method.Name;

                HookPatches(patches.Prefixes, method, targetName, "Prefix", modAssemblyMap, pre, post, ref hooked);
                HookPatches(patches.Postfixes, method, targetName, "Postfix", modAssemblyMap, pre, post, ref hooked);
                HookPatches(patches.Finalizers, method, targetName, "Finalizer", modAssemblyMap, pre, post, ref hooked);
            }

            Main.Log("PatchProfiler: Hooked " + hooked + " patches");
        }

        private static void HookPatches(IList<Patch> patches, MethodBase targetMethod, string targetName,
            string patchType, Dictionary<Assembly, string> modAssemblyMap,
            HarmonyMethod pre, HarmonyMethod post, ref int hooked)
        {
            if (patches == null) return;

            foreach (var patch in patches)
            {
                var patchMethod = patch.PatchMethod;
                if (patchMethod == null) continue;

                if (patch.owner == "BroforceDevTools.PatchProfiler" ||
                    patch.owner == "BroforceDevTools.ModCounter") continue;

                var assembly = patchMethod.DeclaringType != null ? patchMethod.DeclaringType.Assembly : null;
                string modName;
                if (assembly == null || !modAssemblyMap.TryGetValue(assembly, out modName))
                    modName = patch.owner;

                if (patchTimers.ContainsKey(patchMethod)) continue;

                var entry = new PatchEntry
                {
                    ModName = modName,
                    TargetMethod = targetName,
                    PatchType = patchType,
                    Timer = new Stopwatch(),
                    Average = new MovingAverage(60),
                    FrameCallCount = 0
                };
                patchTimers[patchMethod] = entry;

                try
                {
                    harmonyInstance.Patch(patchMethod, pre, post);
                    hooked++;
                }
                catch (Exception) { }
            }
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

                // Per-frame recording capture (before stopwatch reset)
                if (ProfilingRecorder.IsPerFrame)
                {
                    var rawList = new List<PatchTimingData>();
                    foreach (var entry in patchTimers.Values)
                    {
                        var ticks = entry.Timer.ElapsedTicks;
                        if (ticks > 0)
                        {
                            rawList.Add(new PatchTimingData
                            {
                                ModName = entry.ModName,
                                TargetMethod = entry.TargetMethod,
                                PatchType = entry.PatchType,
                                AverageMs = ticks * msScale,
                                CallsPerFrame = entry.FrameCallCount
                            });
                        }
                    }
                    if (rawList.Count > 0)
                        ProfilingRecorder.RecordPatchTimings(rawList);
                }

                // Sample and reset all patch timers
                var sortable = new List<KeyValuePair<PatchEntry, long>>();
                foreach (var entry in patchTimers.Values)
                {
                    var ticks = entry.Timer.ElapsedTicks;
                    entry.Average.Sample(ticks);
                    entry.Timer.Reset();

                    var avg = entry.Average.GetAverage();
                    if (avg > cutoffTicks)
                        sortable.Add(new KeyValuePair<PatchEntry, long>(entry, avg));

                    entry.FrameCallCount = 0;
                }

                if (sortable.Count > 0)
                {
                    sortable.Sort((a, b) => b.Value.CompareTo(a.Value));
                    builder.Concat("Patches:");
                    int c = 0;
                    foreach (var item in sortable)
                    {
                        builder.Concat("\n  ");
                        builder.Concat(item.Key.ModName);
                        builder.Concat(" ");
                        builder.Concat(item.Key.TargetMethod);
                        builder.Concat("(");
                        builder.Concat(item.Key.PatchType);
                        builder.Concat("):");
                        builder.Concat(item.Value * msScale, 2, 3);
                        builder.Concat("ms");
                        if (c++ >= 8) break;
                    }
                }

                StringOutput = sortable.Count > 0 ? fString.PopValue() : null;
                if (sortable.Count == 0)
                    fString.builder.Length = 0;
            }
        }

        private static void TimerPre(MethodBase __originalMethod)
        {
            PatchEntry entry;
            if (patchTimers.TryGetValue(__originalMethod, out entry))
            {
                entry.Timer.Start();
                entry.FrameCallCount++;
            }
        }

        private static void TimerPost(MethodBase __originalMethod)
        {
            PatchEntry entry;
            if (patchTimers.TryGetValue(__originalMethod, out entry))
                entry.Timer.Stop();
        }
    }

}
