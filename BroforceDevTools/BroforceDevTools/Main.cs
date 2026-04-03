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
        private static KeyBindingForPlayers patchProfilerKey;

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
            patchProfilerKey = AllModKeyBindings.LoadKeyBinding("BroforceDevTools", "Toggle Harmony Patch Profiler");

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            if (!value)
            {
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

            if (patchProfilerKey.PressedDown(0))
            {
                if (HarmonyPatchProfiler.IsRunning)
                    HarmonyPatchProfiler.Stop();
                else if (FrameCounter.IsRunning)
                    HarmonyPatchProfiler.Start(FrameCounter.Helper);
                else
                    Log("Start FPS Counter first before enabling Patch Profiler");
            }
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (!WindowScaling.TryCaptureWidth()) return;

            Rect tooltipRect;

            GUILayout.Label("<b>FPS Counter</b>");
            GUILayout.BeginHorizontal();
            {
                bool fpsWas = FrameCounter.IsRunning;
                bool fpsNow = GUILayout.Toggle(fpsWas,
                    new GUIContent(" Enabled", "Show FPS overlay with frame timing breakdown"),
                    WindowScaling.ScaledWidth(100));
                if (fpsNow != fpsWas)
                {
                    if (fpsNow) FrameCounter.Start();
                    else FrameCounter.Stop();
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

                GUI.Label(tooltipRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();

            FrameCounter.ShowDetailedStats = settings.showDetailedStats;
            FrameCounter.MeasureMemory = settings.showMemory;
            FrameCounter.MeasureGC = settings.showGC;
            FrameCounter.ShowPluginStats = settings.showModStats;

            GUILayout.Space(25);
            GUILayout.Label("<b>Harmony Patch Profiler</b>");
            GUILayout.BeginHorizontal();
            {
                bool patchWas = HarmonyPatchProfiler.IsRunning;
                bool patchNow = GUILayout.Toggle(patchWas,
                    new GUIContent(" Enabled", "Profile time spent in each Harmony patch. Requires FPS Counter."),
                    WindowScaling.ScaledWidth(100));
                if (patchNow != patchWas)
                {
                    if (patchNow && FrameCounter.IsRunning)
                        HarmonyPatchProfiler.Start(FrameCounter.Helper);
                    else if (patchNow)
                        Log("Start FPS Counter first");
                    else
                        HarmonyPatchProfiler.Stop();
                }

                tooltipRect = GUILayoutUtility.GetLastRect();
                tooltipRect.y += 20;
                tooltipRect.width += 800;

                if (HarmonyPatchProfiler.IsRunning && GUILayout.Button("Refresh Patches", WindowScaling.ScaledWidth(120)))
                    HarmonyPatchProfiler.Refresh();

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
                bool recWas = ConstructorProfilerInternal.IsRunning;
                bool recNow = GUILayout.Toggle(recWas,
                    new GUIContent(" Enabled", "Profile constructor calls in game and mod assemblies. Exports CSV on stop."),
                    WindowScaling.ScaledWidth(100));

                tooltipRect = GUILayoutUtility.GetLastRect();
                tooltipRect.y += 20;
                tooltipRect.width += 800;

                if (recNow != recWas)
                {
                    if (recNow) ConstructorProfilerInternal.StartRecording();
                    else
                    {
                        ConstructorProfilerInternal.StopRecording();
                        var path = System.IO.Path.Combine(mod.Path,
                            "ConstructorProfiler_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv");
                        ConstructorProfilerInternal.ExportCSV(path);
                    }
                }

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
            DrawKeybinding(patchProfilerKey, ref previousToolTip);
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

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
