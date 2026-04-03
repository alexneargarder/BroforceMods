using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace BroforceDevTools.FPSCounter
{
    internal static class FrameCounter
    {
        private static GameObject counterObject;
        private static FrameCounterHelper helper;
        private static FrameCounterHelper2 helper2;
        private static bool running;

        internal static bool ShowDetailedStats = true;
        internal static bool MeasureMemory = true;
        internal static bool MeasureGC = true;
        internal static bool ShowPluginStats = true;
        internal static bool ShowPatchProfiler = false;
        internal static CounterColors ColorMode = CounterColors.Outline;
        internal static TextAnchor Position = TextAnchor.LowerRight;

        internal static bool IsRunning { get { return running; } }
        internal static MonoBehaviour Helper { get { return helper; } }

        internal static void Start()
        {
            if (running) return;
            running = true;

            if (counterObject == null)
            {
                counterObject = new GameObject("BroforceDevTools_FPSCounter");
                UnityEngine.Object.DontDestroyOnLoad(counterObject);
            }

            if (helper == null)
                helper = counterObject.AddComponent<FrameCounterHelper>();
            if (helper2 == null)
                helper2 = counterObject.AddComponent<FrameCounterHelper2>();

            if (ShowPluginStats)
                ModCounter.Start(helper);

            if (ShowPatchProfiler)
                HarmonyPatchProfiler.Start(helper);

            UpdateLooks();
        }

        internal static void Stop()
        {
            if (!running) return;
            running = false;

            ProfilingRecorder.Stop();
            HarmonyPatchProfiler.Stop();
            ModCounter.Stop();

            if (helper != null)
            {
                UnityEngine.Object.Destroy(helper);
                helper = null;
            }
            if (helper2 != null)
            {
                UnityEngine.Object.Destroy(helper2);
                helper2 = null;
            }
        }

        internal static void Cleanup()
        {
            Stop();
            if (counterObject != null)
            {
                UnityEngine.Object.Destroy(counterObject);
                counterObject = null;
            }
        }

        internal static FrameData GetFrameData()
        {
            return FrameCounterHelper.GetFrameData();
        }

        internal static void Toggle()
        {
            if (running)
                Stop();
            else
                Start();
        }

        private const int MAX_STRING_SIZE = 1400;
        private static readonly GUIStyle style = new GUIStyle();
        private static Rect screenRect;
        private const int ScreenOffset = 10;
        private static readonly FixedString fString = new FixedString(MAX_STRING_SIZE);
        internal static string FrameOutputText;

        internal static void UpdateLooks()
        {
            if (ColorMode == CounterColors.White)
                style.normal.textColor = Color.white;
            if (ColorMode == CounterColors.Black)
                style.normal.textColor = Color.black;

            int w = Screen.width, h = Screen.height;
            screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);
            style.alignment = Position;
            style.fontSize = h / 65;
        }

        internal static void DrawCounter()
        {
            if (!running || string.IsNullOrEmpty(FrameOutputText)) return;

            if (ColorMode == CounterColors.Outline)
                ShadowAndOutline.DrawOutline(screenRect, FrameOutputText, style, Color.black, Color.white, 1.5f);
            else
                GUI.Label(screenRect, FrameOutputText, style);
        }

        internal static FixedString GetFixedString() { return fString; }
    }

    [DefaultExecutionOrder(int.MinValue)]
    internal sealed class FrameCounterHelper : MonoBehaviour
    {
        private static readonly MovingAverage fixedUpdateTime = new MovingAverage(60);
        private static readonly MovingAverage updateTime = new MovingAverage(60);
        private static readonly MovingAverage yieldTime = new MovingAverage(60);
        private static readonly MovingAverage lateUpdateTime = new MovingAverage(60);
        private static readonly MovingAverage renderTime = new MovingAverage(60);
        private static readonly MovingAverage onGuiTime = new MovingAverage(60);
        private static readonly MovingAverage gcAddedSize = new MovingAverage(60);
        private static readonly MovingAverage frameTime = new MovingAverage(60);

        private static Stopwatch measurementStopwatch;
        internal static bool CanProcessOnGui;
        private static bool onGuiHit;

        internal static void ResetOnGuiHit() { onGuiHit = false; }

        internal static void SampleLateUpdateTime()
        {
            rawLateTicks = TakeMeasurement();
            lateUpdateTime.Sample(rawLateTicks);
        }
        private static float nanosecPerTick;
        private static float msScale;

        private static long cachedAvgFrame;
        private static float cachedFps;
        private static long cachedAvgFixed, cachedAvgUpdate, cachedAvgYield, cachedAvgLate, cachedAvgRender, cachedAvgGui;
        private static long rawFixedTicks, rawYieldTicks, rawLateTicks, rawRenderTicks;
        private static long cachedGcCollectionCount;
        private static float cachedGcAllocRate;
        private static long cachedGcMemBytes;
        private static long cachedProcessMemBytes = -1;
        private static long cachedSystemFreeMemBytes = -1;
        private static int[] cachedGcGenCounts;

        internal static FrameData GetFrameData()
        {
            var data = new FrameData();
            data.FPS = cachedFps;
            data.TotalMs = cachedAvgFrame * msScale;
            data.FixedUpdateMs = cachedAvgFixed * msScale;
            data.UpdateMs = cachedAvgUpdate * msScale;
            data.YieldMs = cachedAvgYield * msScale;
            data.LateUpdateMs = cachedAvgLate * msScale;
            data.RenderMs = cachedAvgRender * msScale;
            data.OnGUIMs = cachedAvgGui * msScale;

            var totalCaptured = cachedAvgFixed + cachedAvgUpdate + cachedAvgYield + cachedAvgLate + cachedAvgRender + cachedAvgGui;
            data.OtherMs = System.Math.Max(0f, (cachedAvgFrame - totalCaptured) * msScale);

            data.GCAllocKBPerSec = cachedGcAllocRate;
            data.GCCollectionCounts = cachedGcGenCounts;
            data.GCMemoryBytes = cachedGcMemBytes;
            data.ProcessMemoryBytes = cachedProcessMemBytes;
            data.SystemFreeMemBytes = cachedSystemFreeMemBytes;
            return data;
        }

        private static long TakeMeasurement()
        {
            var result = measurementStopwatch.ElapsedTicks;
            measurementStopwatch.Reset();
            measurementStopwatch.Start();
            return result;
        }

        private static readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
        private IEnumerator Start()
        {
            measurementStopwatch = new Stopwatch();
            var totalStopwatch = new Stopwatch();
            nanosecPerTick = (float)(1000 * 1000 * 100) / Stopwatch.Frequency;
            msScale = 1f / (nanosecPerTick * 1000f);
            long gcPreviousAmount = 0;
            cachedGcCollectionCount = 0;

            while (true)
            {
                yield return null;
                var rawUpdateTicks = TakeMeasurement();
                updateTime.Sample(rawUpdateTicks);

                yield return waitForEndOfFrame;

                if (!onGuiHit)
                {
                    rawRenderTicks = TakeMeasurement();
                    renderTime.Sample(rawRenderTicks);
                    onGuiHit = true;
                }

                CanProcessOnGui = false;
                var rawGuiTicks = TakeMeasurement();
                onGuiTime.Sample(rawGuiTicks);
                measurementStopwatch.Reset();

                var rawFrameTicks = totalStopwatch.ElapsedTicks;
                frameTime.Sample(rawFrameTicks);
                totalStopwatch.Reset();
                totalStopwatch.Start();

                cachedAvgFrame = frameTime.GetAverage();
                cachedFps = 1000000f / (cachedAvgFrame / nanosecPerTick);
                cachedAvgFixed = fixedUpdateTime.GetAverage();
                cachedAvgUpdate = updateTime.GetAverage();
                cachedAvgYield = yieldTime.GetAverage();
                cachedAvgLate = lateUpdateTime.GetAverage();
                cachedAvgRender = renderTime.GetAverage();
                cachedAvgGui = onGuiTime.GetAverage();

                var fStr = FrameCounter.GetFixedString();
                var sb = fStr.builder;

                sb.Concat(cachedFps, 1);
                sb.Append(" FPS");

                if (FrameCounter.ShowDetailedStats)
                {
                    var totalCapturedTicks = cachedAvgFixed + cachedAvgUpdate + cachedAvgYield + cachedAvgLate + cachedAvgRender + cachedAvgGui;
                    var otherTicks = cachedAvgFrame - totalCapturedTicks;

                    sb.Append(",");
                    sb.Concat(cachedAvgFrame * msScale, 1, 3);
                    sb.Append("ms\nFixed:");
                    sb.Concat(cachedAvgFixed * msScale, 1, 3);
                    sb.Append("ms\nUpdate:");
                    sb.Concat(cachedAvgUpdate * msScale, 1, 3);
                    sb.Append("ms\nYield/anim:");
                    sb.Concat(cachedAvgYield * msScale, 1, 3);
                    sb.Append("ms\nLate:");
                    sb.Concat(cachedAvgLate * msScale, 1, 3);
                    sb.Append("ms\nRender/VSync:");
                    sb.Concat(cachedAvgRender * msScale, 1, 3);
                    sb.Append("ms\nOnGUI:");
                    sb.Concat(cachedAvgGui * msScale, 1, 3);
                    sb.Append("ms\nOther/Frameskip:");
                    sb.Concat(otherTicks * msScale, 1, 3);
                    sb.Append("ms");
                }

                if (FrameCounter.MeasureMemory)
                {
                    try
                    {
                        var procMem = MemoryInfo.QueryProcessMemStatus();
                        cachedProcessMemBytes = (long)procMem.WorkingSetSize;
                        var currentMem = procMem.WorkingSetSize / 1024 / 1024;

                        var memorystatus = MemoryInfo.QuerySystemMemStatus();
                        cachedSystemFreeMemBytes = (long)memorystatus.ullAvailPhys;
                        var freeMem = memorystatus.ullAvailPhys / 1024 / 1024;

                        sb.Append("\nRAM: ");
                        sb.Concat(currentMem, 3);
                        sb.Append("MB used, ");
                        sb.Concat(freeMem, 3);
                        sb.Append("MB free");
                    }
                    catch
                    {
                        cachedProcessMemBytes = -1;
                        cachedSystemFreeMemBytes = -1;
                    }
                }

                if (FrameCounter.MeasureGC)
                {
                    var totalGcMemBytes = GC.GetTotalMemory(false);
                    cachedGcMemBytes = totalGcMemBytes;
                    if (totalGcMemBytes != 0)
                    {
                        var gcDelta = totalGcMemBytes - gcPreviousAmount;
                        if (gcDelta >= 0)
                            gcAddedSize.Sample(gcDelta);
                        else
                            cachedGcCollectionCount++;

                        cachedGcAllocRate = gcAddedSize.GetAverage() * cachedFps / 1024f;

                        var totalGcMem = totalGcMemBytes / 1024 / 1024;
                        sb.Append("\nGC:");
                        sb.Concat((int)totalGcMem, 4);
                        sb.Append("MB,");
                        sb.Concat(Mathf.RoundToInt(cachedGcAllocRate), 5);
                        sb.Append("KB/s, ");
                        sb.Concat(cachedGcCollectionCount, 2);
                        sb.Append(" collects");

                        gcPreviousAmount = totalGcMemBytes;
                    }

                    var gcGens = GC.MaxGeneration;
                    if (gcGens > 0)
                    {
                        cachedGcGenCounts = new int[gcGens];
                        sb.Append("\nGC hits:");
                        for (var g = 0; g < gcGens; g++)
                        {
                            var collections = GC.CollectionCount(g);
                            cachedGcGenCounts[g] = collections;
                            sb.Append(" ");
                            sb.Concat(g);
                            sb.Append(":");
                            sb.Concat(collections);
                        }
                    }
                }

                if (ProfilingRecorder.IsPerFrame)
                {
                    var rawData = new FrameData();
                    rawData.TotalMs = rawFrameTicks * msScale;
                    rawData.FPS = rawFrameTicks > 0 ? 1000000f / (rawFrameTicks / nanosecPerTick) : 0;
                    rawData.FixedUpdateMs = rawFixedTicks * msScale;
                    rawData.UpdateMs = rawUpdateTicks * msScale;
                    rawData.YieldMs = rawYieldTicks * msScale;
                    rawData.LateUpdateMs = rawLateTicks * msScale;
                    rawData.RenderMs = rawRenderTicks * msScale;
                    rawData.OnGUIMs = rawGuiTicks * msScale;
                    var captured = rawData.FixedUpdateMs + rawData.UpdateMs + rawData.YieldMs
                        + rawData.LateUpdateMs + rawData.RenderMs + rawData.OnGUIMs;
                    rawData.OtherMs = System.Math.Max(0f, rawData.TotalMs - captured);
                    rawData.GCAllocKBPerSec = cachedGcAllocRate;
                    rawData.GCMemoryBytes = cachedGcMemBytes;
                    rawData.ProcessMemoryBytes = cachedProcessMemBytes;
                    rawData.SystemFreeMemBytes = cachedSystemFreeMemBytes;
                    rawData.GCCollectionCounts = cachedGcGenCounts;
                    ProfilingRecorder.RecordFrameData(rawData);
                }

                if (ModCounter.StringOutput != null)
                {
                    sb.Append("\n");
                    sb.Append(ModCounter.StringOutput);
                }

                if (HarmonyPatchProfiler.StringOutput != null)
                {
                    sb.Append("\n");
                    sb.Append(HarmonyPatchProfiler.StringOutput);
                }

                if (ProfilingRecorder.StringOutput != null)
                {
                    sb.Append("\n");
                    sb.Append(ProfilingRecorder.StringOutput);
                }

                FrameCounter.FrameOutputText = fStr.PopValue();
                measurementStopwatch.Reset();
            }
        }

        private void FixedUpdate()
        {
            measurementStopwatch.Start();
        }

        private void Update()
        {
            rawFixedTicks = TakeMeasurement();
            fixedUpdateTime.Sample(rawFixedTicks);
        }

        private void LateUpdate()
        {
            rawYieldTicks = TakeMeasurement();
            yieldTime.Sample(rawYieldTicks);
        }

        private void OnGUI()
        {
            if (!onGuiHit)
            {
                rawRenderTicks = TakeMeasurement();
                renderTime.Sample(rawRenderTicks);
                onGuiHit = true;
            }

            FrameCounter.DrawCounter();
        }
    }

    [DefaultExecutionOrder(int.MaxValue)]
    internal sealed class FrameCounterHelper2 : MonoBehaviour
    {
        private void LateUpdate()
        {
            FrameCounterHelper.SampleLateUpdateTime();
            FrameCounterHelper.ResetOnGuiHit();
            FrameCounterHelper.CanProcessOnGui = true;
        }
    }

}
