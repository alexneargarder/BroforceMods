using System;
using System.Reflection;
using BroforceDevTools.ConstructorProfiler;
using BroforceDevTools.Demystify;
using BroforceDevTools.FPSCounter;
using HarmonyLib;
using RocketLib;
using RocketLib.UMM;
using RocketLib.Utils;
using UnityEngine;
using UnityModManagerNet;

namespace BroforceDevTools
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        private static Harmony harmony;

        private static KeyBindingForPlayers fpsCounterKey;
        private static KeyBindingForPlayers constructorProfilerKey;
        private static KeyBindingForPlayers recorderKey;

        private static string previousToolTip = "";

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnUpdate = OnUpdate;

            try
            {
                settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            }
            catch
            {
                settings = new Settings();
            }

            harmony = new Harmony(modEntry.Info.Id);
            DemystifyPatcher.Apply(harmony);
            ConstructorProfilerInternal.Initialize(harmony);

            fpsCounterKey = AllModKeyBindings.LoadKeyBinding("BroforceDevTools", "Toggle FPS Counter");
            constructorProfilerKey = AllModKeyBindings.LoadKeyBinding("BroforceDevTools", "Toggle Constructor Profiler");
            recorderKey = AllModKeyBindings.LoadKeyBinding("BroforceDevTools", "Toggle Profiling Recorder");

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            if (!value)
            {
                ProfilingRecorder.Stop();
                FrameCounter.Cleanup();
                ConstructorProfilerInternal.Cleanup();
                HarmonyPatchProfiler.Stop();
            }
            return true;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            if (!enabled) return;

            if (fpsCounterKey.PressedDown(0))
            {
                if (FrameCounter.IsRunning)
                    FrameCounter.Stop();
                else
                    FrameCounter.Start();
            }

            if (constructorProfilerKey.PressedDown(0))
            {
                ConstructorProfilerInternal.ToggleRecording();
            }

            if (recorderKey.PressedDown(0))
            {
                if (ProfilingRecorder.IsRecording)
                {
                    ProfilingRecorder.Stop();
                }
                else
                {
                    if (!FrameCounter.IsRunning)
                        FrameCounter.Start();
                    ProfilingRecorder.Start(FrameCounter.Helper, settings.perFrameRecording);
                }
            }
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (!WindowScaling.TryCaptureWidth()) return;

            Rect tooltipRect;

            GUILayout.Label("<b>FPS Counter</b>");
            GUILayout.BeginHorizontal();
            {
                var fpsLabel = FrameCounter.IsRunning ? "Stop" : "Start";
                var fpsTooltip = FrameCounter.IsRunning
                    ? "Stop FPS overlay and all active profiling."
                    : "Show FPS overlay with frame timing breakdown.";
                if (GUILayout.Button(new GUIContent(fpsLabel, fpsTooltip), WindowScaling.ScaledWidth(60)))
                {
                    if (FrameCounter.IsRunning) FrameCounter.Stop();
                    else FrameCounter.Start();
                }

                tooltipRect = GUILayoutUtility.GetLastRect();
                tooltipRect.y += 20;
                tooltipRect.width += 800;

                settings.showDetailedStats = GUILayout.Toggle(settings.showDetailedStats,
                    new GUIContent(" Detailed Stats", "Per-phase frame timing (Update, FixedUpdate, Render, etc.)"),
                    WindowScaling.ScaledWidth(120));
                settings.showMemory = GUILayout.Toggle(settings.showMemory,
                    new GUIContent(" Memory", "Process and system memory usage"),
                    WindowScaling.ScaledWidth(80));
                settings.showGC = GUILayout.Toggle(settings.showGC,
                    new GUIContent(" GC Stats", "GC heap size, allocation rate, and collection counts"),
                    WindowScaling.ScaledWidth(80));
                settings.showModStats = GUILayout.Toggle(settings.showModStats,
                    new GUIContent(" Mod Stats", "Per-mod callback timing breakdown"),
                    WindowScaling.ScaledWidth(100));

                settings.showPatchProfiler = GUILayout.Toggle(settings.showPatchProfiler,
                    new GUIContent(" Patch Profiler", "Profile time spent in each Harmony patch. Activates when FPS Counter is enabled."),
                    WindowScaling.ScaledWidth(120));

                if (HarmonyPatchProfiler.IsRunning && GUILayout.Button("Refresh Patches", WindowScaling.ScaledWidth(120)))
                    HarmonyPatchProfiler.Refresh();

                GUI.Label(tooltipRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();

            FrameCounter.ShowDetailedStats = settings.showDetailedStats;
            FrameCounter.MeasureMemory = settings.showMemory;
            FrameCounter.MeasureGC = settings.showGC;
            FrameCounter.ShowPluginStats = settings.showModStats;

            if (settings.showPatchProfiler != FrameCounter.ShowPatchProfiler)
            {
                FrameCounter.ShowPatchProfiler = settings.showPatchProfiler;
                if (FrameCounter.IsRunning)
                {
                    if (settings.showPatchProfiler)
                        HarmonyPatchProfiler.Start(FrameCounter.Helper);
                    else
                        HarmonyPatchProfiler.Stop();
                }
            }

            GUILayout.Space(25);
            GUILayout.Label("<b>Profiling Recorder</b>");
            GUILayout.BeginHorizontal();
            {
                var recordLabel = ProfilingRecorder.IsRecording ? "Stop Recording" : "Start Recording";
                var recordTooltip = ProfilingRecorder.IsRecording
                    ? "Stop recording and export CSV + summary to mod directory."
                    : "Record profiling data over time. Requires FPS Counter.";
                if (GUILayout.Button(new GUIContent(recordLabel, recordTooltip), WindowScaling.ScaledWidth(120)))
                {
                    if (ProfilingRecorder.IsRecording)
                    {
                        ProfilingRecorder.Stop();
                    }
                    else
                    {
                        if (!FrameCounter.IsRunning)
                            FrameCounter.Start();
                        ProfilingRecorder.Start(FrameCounter.Helper, settings.perFrameRecording);
                    }
                }

                tooltipRect = GUILayoutUtility.GetLastRect();
                tooltipRect.y += 20;
                tooltipRect.width += 800;

                GUILayout.Label("Mode:", WindowScaling.ScaledWidth(40));
                var modeLabel = settings.perFrameRecording ? "Per-frame" : "Per-second";
                var modeTooltip = settings.perFrameRecording
                    ? "Per-frame: raw values every frame (more data, detects spikes). Click to switch to per-second."
                    : "Per-second: smoothed values every second (less data, good for trends). Click to switch to per-frame.";
                GUI.enabled = !ProfilingRecorder.IsRecording;
                if (GUILayout.Button(new GUIContent(modeLabel, modeTooltip), WindowScaling.ScaledWidth(100)))
                    settings.perFrameRecording = !settings.perFrameRecording;
                GUI.enabled = true;

                if (GUILayout.Button(new GUIContent("Open Folder", "Open the results folder in file manager."), WindowScaling.ScaledWidth(100)))
                {
                    var resultsDir = System.IO.Path.Combine(mod.Path, "Results");
                    System.IO.Directory.CreateDirectory(resultsDir);
                    System.Diagnostics.Process.Start(resultsDir);
                }

                if (GUI.tooltip != previousToolTip)
                {
                    GUI.Label(tooltipRect, GUI.tooltip);
                    previousToolTip = GUI.tooltip;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(25);
            GUILayout.Label("<b>Constructor Profiler</b>");
            GUILayout.BeginHorizontal();
            {
                var ctorLabel = ConstructorProfilerInternal.IsRunning ? "Stop Recording" : "Start Recording";
                var ctorTooltip = ConstructorProfilerInternal.IsRunning
                    ? "Stop recording and export constructor call data to CSV."
                    : "Profile constructor calls in game and mod assemblies.";
                if (GUILayout.Button(new GUIContent(ctorLabel, ctorTooltip), WindowScaling.ScaledWidth(120)))
                {
                    if (ConstructorProfilerInternal.IsRunning)
                    {
                        ConstructorProfilerInternal.StopRecording();
                        var path = System.IO.Path.Combine(mod.Path,
                            "ConstructorProfiler_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv");
                        ConstructorProfilerInternal.ExportCSV(path);
                    }
                    else
                    {
                        ConstructorProfilerInternal.StartRecording();
                    }
                }

                tooltipRect = GUILayoutUtility.GetLastRect();
                tooltipRect.y += 20;
                tooltipRect.width += 800;

                if (GUI.tooltip != previousToolTip)
                {
                    GUI.Label(tooltipRect, GUI.tooltip);
                    previousToolTip = GUI.tooltip;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(25);
            GUILayout.Label("<b>Keybindings</b>");
            DrawKeybinding(fpsCounterKey, ref previousToolTip);
            GUILayout.Space(30);
            DrawKeybinding(constructorProfilerKey, ref previousToolTip);
            GUILayout.Space(30);
            DrawKeybinding(recorderKey, ref previousToolTip);
        }

        static void DrawKeybinding(KeyBindingForPlayers key, ref string prevTooltip)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            GUILayout.Label(key.name, WindowScaling.ScaledWidth(250));
            GUILayout.BeginVertical(WindowScaling.ScaledWidth(200));
            key[0].OnGUI(true, false, false);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        public static void Log(string str)
        {
            mod.Logger.Log(str);
        }
    }

    public class Settings : UnityModManager.ModSettings
    {
        public bool showDetailedStats = true;
        public bool showMemory = true;
        public bool showGC = true;
        public bool showModStats = true;
        public bool showPatchProfiler = false;
        public bool perFrameRecording = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
