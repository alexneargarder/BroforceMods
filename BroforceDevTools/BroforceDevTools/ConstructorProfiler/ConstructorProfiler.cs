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
        internal static volatile bool IsRunning;

        private static Harmony harmony;
        private static readonly object sync = new object();
        private static Dictionary<string, StackData> callCounter = new Dictionary<string, StackData>();
        private static Dictionary<string, StackData> results = new Dictionary<string, StackData>();
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
            lock (sync)
            {
                callCounter = new Dictionary<string, StackData>();
            }
            IsRunning = true;
            Main.Log("ConstructorProfiler: Started recording");
        }

        internal static void StopRecording()
        {
            var wasRunning = IsRunning;
            IsRunning = false;
            if (!wasRunning) return;

            lock (sync)
            {
                results = callCounter;
                callCounter = new Dictionary<string, StackData>();
            }
            Main.Log("ConstructorProfiler: Stopped recording (" + results.Count + " unique stacks)");
        }

        internal static List<ConstructorCallData> GetResults()
        {
            List<StackData> snapshot;
            lock (sync)
            {
                snapshot = new List<StackData>((IsRunning ? callCounter : results).Values);
            }

            var list = new List<ConstructorCallData>();
            foreach (var data in snapshot)
            {
                list.Add(new ConstructorCallData
                {
                    TypeName = data.createdType,
                    TotalCallCount = data.count,
                    CallStacks = new List<StackCallData>
                    {
                        new StackCallData { StackTrace = data.cleanStack, CallCount = data.count }
                    }
                });
            }
            list.Sort((a, b) => b.TotalCallCount.CompareTo(a.TotalCallCount));
            return list;
        }

        internal static void ClearResults()
        {
            lock (sync)
            {
                results = new Dictionary<string, StackData>();
                if (IsRunning)
                    callCounter = new Dictionary<string, StackData>();
            }
        }

        internal static void ExportCSV(string path)
        {
            List<StackData> snapshot;
            lock (sync)
            {
                snapshot = new List<StackData>((IsRunning ? callCounter : results).Values);
            }

            var lines = new List<string>();
            lines.Add("\"Stack\",\"Created object\",\"Count\"");
            foreach (var item in snapshot.OrderByDescending(x => x.count))
            {
                lines.Add("\"" + item.cleanStack + "\",\"" + item.createdType + "\",\"" + item.count + "\"");
            }
            File.WriteAllLines(path, lines.ToArray());
            Main.Log("ConstructorProfiler: Exported " + (lines.Count - 1) + " entries to " + path);
        }

        internal static void ToggleRecording()
        {
            if (!IsRunning)
                StartRecording();
            else
                StopRecordingAndExport();
        }

        internal static void StopRecordingAndExport()
        {
            StopRecording();
            var path = Path.Combine(Main.mod.Path, "ConstructorProfiler_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv");
            ExportCSV(path);
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
            }).Where(x => x.IsClass && !x.IsGenericType && !typeof(Delegate).IsAssignableFrom(x)).ToList();

            var constructors = types.SelectMany(GetConstructorsSafe).Where(HasILBody).ToList();
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

        private static ConstructorInfo[] GetConstructorsSafe(Type type)
        {
            try { return type.GetConstructors(); }
            catch (Exception) { return new ConstructorInfo[0]; }
        }

        // Runtime-implemented constructors (delegates especially) have no IL body; patching one detours
        // Mono's shared native delegate constructor and breaks every delegate creation in the process.
        private static bool HasILBody(ConstructorInfo constructor)
        {
            try
            {
                if (constructor.IsAbstract)
                    return false;
                var flags = constructor.GetMethodImplementationFlags();
                if ((flags & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL)
                    return false;
                if ((flags & MethodImplAttributes.InternalCall) != 0)
                    return false;
                return constructor.GetMethodBody() != null;
            }
            catch (Exception) { return false; }
        }

        private static void AddCall(MethodBase __originalMethod)
        {
            if (!IsRunning) return;

            var key = new System.Diagnostics.StackTrace().ToString();
            var createdType = __originalMethod != null && __originalMethod.DeclaringType != null
                ? __originalMethod.DeclaringType.FullName : "Unknown";

            lock (sync)
            {
                StackData data;
                if (callCounter.TryGetValue(key, out data))
                {
                    data.count++;
                }
                else
                {
                    callCounter.Add(key, new StackData(key, createdType));
                }
            }
        }

        internal static void Cleanup()
        {
            if (IsRunning)
            {
                try
                {
                    StopRecordingAndExport();
                }
                catch (Exception ex)
                {
                    Main.Log("ConstructorProfiler: Export failed: " + ex.Message);
                }
            }

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

            public StackData(string stackTrace, string createdType)
            {
                count = 1;
                this.createdType = createdType;

                try
                {
                    var frames = stackTrace
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    cleanStack = string.Join("\n", frames.Skip(2).Take(5).ToArray());
                }
                catch { cleanStack = ""; }
            }
        }
    }
}
