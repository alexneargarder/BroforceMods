using System.Collections.Generic;
using BroforceDevTools.FPSCounter;

namespace BroforceDevTools
{
    public static class FPSCounterAPI
    {
        public static bool IsRunning { get { return FrameCounter.IsRunning; } }

        public static void Start() { FrameCounter.Start(); }
        public static void Stop() { FrameCounter.Stop(); }

        public static FrameData GetFrameData() { return FrameCounter.GetFrameData(); }

        public static List<ModTimingData> GetModTimings() { return ModCounter.GetModTimings(); }

        public static bool IsPatchProfilingEnabled { get { return HarmonyPatchProfiler.IsRunning; } }
        public static void StartPatchProfiling() { HarmonyPatchProfiler.Start(FrameCounter.Helper); }
        public static void StopPatchProfiling() { HarmonyPatchProfiler.Stop(); }
        public static void RefreshPatchList() { HarmonyPatchProfiler.Refresh(); }
        public static List<PatchTimingData> GetPatchTimings() { return HarmonyPatchProfiler.GetPatchTimings(); }
    }

    public struct FrameData
    {
        public float FPS;
        public float TotalMs;
        public float UpdateMs;
        public float YieldMs;
        public float FixedUpdateMs;
        public float LateUpdateMs;
        public float RenderMs;
        public float OnGUIMs;
        public float OtherMs;
        public float GCAllocKBPerSec;
        public int[] GCCollectionCounts;
        public long GCMemoryBytes;
        public long ProcessMemoryBytes;
        public long SystemFreeMemBytes;
    }

    public struct ModTimingData
    {
        public string ModName;
        public float UpdateMs;
        public float FixedUpdateMs;
        public float LateUpdateMs;
        public float OnGUIMs;
        public float TotalMs;
    }

    public struct PatchTimingData
    {
        public string ModName;
        public string TargetMethod;
        public string PatchType;
        public float AverageMs;
        public int CallsPerFrame;
    }
}
