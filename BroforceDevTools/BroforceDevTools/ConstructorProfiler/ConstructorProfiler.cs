using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace BroforceDevTools
{
    public static class ConstructorProfilerAPI
    {
        public static bool IsRecording { get { return ConstructorProfiler.ConstructorProfilerInternal.IsRunning; } }
        public static void StartRecording() { ConstructorProfiler.ConstructorProfilerInternal.StartRecording(); }
        public static void StopRecording() { ConstructorProfiler.ConstructorProfilerInternal.StopRecording(); }
        public static List<ConstructorCallData> GetResults() { return ConstructorProfiler.ConstructorProfilerInternal.GetResults(); }
        public static void ClearResults() { ConstructorProfiler.ConstructorProfilerInternal.ClearResults(); }
        public static void ExportCSV(string path) { ConstructorProfiler.ConstructorProfilerInternal.ExportCSV(path); }
    }

    public struct ConstructorCallData
    {
        public string TypeName;
        public int TotalCallCount;
        public List<StackCallData> CallStacks;
    }

    public struct StackCallData
    {
        public string StackTrace;
        public int CallCount;
    }
}

namespace BroforceDevTools.ConstructorProfiler
{
    internal static class ConstructorProfilerInternal
    {
        internal static bool IsRunning;

        private static Harmony harmony;
        private static Dictionary<string, StackData> callCounter = new Dictionary<string, StackData>();
        private static bool hooksInstalled;
        private static MethodInfo addCallMethod = typeof(ConstructorProfilerInternal).GetMethod("AddCall", BindingFlags.Static | BindingFlags.NonPublic);

        internal static void Initialize(Harmony mainHarmony)
        {
            harmony = new Harmony("BroforceDevTools.ConstructorProfiler");
        }

        internal static void StartRecording()
        {
            if (!hooksInstalled)
                InstallHooks();
            callCounter = new Dictionary<string, StackData>();
            IsRunning = true;
            Main.Log("ConstructorProfiler: Started recording");
        }

        internal static void StopRecording()
        {
            IsRunning = false;
            Main.Log("ConstructorProfiler: Stopped recording (" + callCounter.Count + " unique stacks)");
        }

        internal static List<ConstructorCallData> GetResults()
        {
            var results = new List<ConstructorCallData>();
            foreach (var kvp in callCounter)
            {
                results.Add(new ConstructorCallData
                {
                    TypeName = kvp.Value.createdType,
                    TotalCallCount = kvp.Value.count,
                    CallStacks = new List<StackCallData>
                    {
                        new StackCallData { StackTrace = kvp.Value.cleanStack, CallCount = kvp.Value.count }
                    }
                });
            }
            results.Sort((a, b) => b.TotalCallCount.CompareTo(a.TotalCallCount));
            return results;
        }

        internal static void ClearResults()
        {
            callCounter = new Dictionary<string, StackData>();
        }

        internal static void ExportCSV(string path)
        {
            var lines = new List<string>();
            lines.Add("\"Stack\",\"Created object\",\"Count\"");
            foreach (var item in callCounter.Values.OrderByDescending(x => x.count))
            {
                lines.Add("\"" + item.cleanStack + "\",\"" + item.createdType + "\",\"" + item.count + "\"");
            }
            File.WriteAllLines(path, lines.ToArray());
            Main.Log("ConstructorProfiler: Exported " + (lines.Count - 1) + " entries to " + path);
        }

        internal static void ToggleRecording()
        {
            if (!IsRunning)
            {
                StartRecording();
            }
            else
            {
                StopRecording();
                var path = Path.Combine(Main.mod.Path, "ConstructorProfiler_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv");
                ExportCSV(path);
            }
        }

        private static void InstallHooks()
        {
            // Patching framework constructors (mscorlib, System, UnityEngine) mid-game crashes
            // because they're actively being called during patching.
            // Scope to game code and mod assemblies only.
            var modAssemblyNames = new HashSet<string>();
            foreach (var modEntry in UnityModManagerNet.UnityModManager.modEntries)
            {
                if (modEntry.Assembly != null && modEntry.Info.Id != "BroforceDevTools")
                    modAssemblyNames.Add(modEntry.Assembly.GetName().Name);
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x =>
                {
                    var name = x.GetName().Name;
                    return name == "Assembly-CSharp" || name == "Assembly-CSharp-firstpass" ||
                           modAssemblyNames.Contains(name);
                })
                .ToList();

            var types = assemblies.SelectMany(ass =>
            {
                try { return ass.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.Where(x => x != null); }
            }).Where(x => x.IsClass && !x.IsGenericType).ToList();

            var constructors = types.SelectMany(type => type.GetConstructors()).ToList();
            int patched = 0;

            foreach (var constructor in constructors)
            {
                try
                {
                    harmony.Patch(constructor, new HarmonyMethod(addCallMethod));
                    patched++;
                }
                catch (Exception) { }
            }

            hooksInstalled = true;
            Main.Log("ConstructorProfiler: Patched " + patched + "/" + constructors.Count + " constructors");
        }

        private static void AddCall()
        {
            if (!IsRunning) return;

            var stackTrace = new System.Diagnostics.StackTrace();
            var key = stackTrace.ToString();

            StackData data;
            if (callCounter.TryGetValue(key, out data))
            {
                data.count++;
            }
            else
            {
                callCounter.Add(key, new StackData(stackTrace));
            }
        }

        internal static void Cleanup()
        {
            if (harmony != null)
                harmony.UnpatchAll("BroforceDevTools.ConstructorProfiler");
            IsRunning = false;
            hooksInstalled = false;
        }

        internal class StackData
        {
            public string createdType;
            public string cleanStack;
            public int count;

            public StackData(System.Diagnostics.StackTrace stackTrace)
            {
                count = 1;
                try
                {
                    var ctorFrame = stackTrace.GetFrame(1);
                    var method = ctorFrame != null ? ctorFrame.GetMethod() : null;
                    createdType = method != null && method.DeclaringType != null
                        ? method.DeclaringType.FullName : "Unknown";
                }
                catch { createdType = "Unknown"; }

                try
                {
                    var frames = stackTrace.ToString()
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    cleanStack = string.Join("\n", frames.Skip(2).Take(5).ToArray());
                }
                catch { cleanStack = ""; }
            }
        }
    }
}
