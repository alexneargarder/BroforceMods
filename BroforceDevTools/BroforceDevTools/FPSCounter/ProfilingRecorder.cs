using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BroforceDevTools.FPSCounter
{
    internal static class ProfilingRecorder
    {
        private struct FrameRecord
        {
            public float timestamp;
            public FrameData data;
        }

        private struct ModRecord
        {
            public float timestamp;
            public ModTimingData data;
        }

        private struct PatchRecord
        {
            public float timestamp;
            public PatchTimingData data;
        }

        private static List<FrameRecord> frameRecords;
        private static List<ModRecord> modRecords;
        private static List<PatchRecord> patchRecords;

        private static bool recording;
        private static bool perFrame;
        private static float startTime;
        private static float lastOverlayUpdate;
        private static Action stopAction;

        public static string StringOutput { get; private set; }
        public static bool IsRecording { get { return recording; } }
        public static bool IsPerFrame { get { return recording && perFrame; } }
        public static float RecordingDuration
        {
            get { return recording ? Time.realtimeSinceStartup - startTime : 0f; }
        }

        public static void Start(MonoBehaviour mb, bool perFrameMode = false)
        {
            if (recording) return;
            if (!FrameCounter.IsRunning)
            {
                Main.Log("Start FPS Counter first before enabling Profiling Recorder");
                return;
            }

            recording = true;
            perFrame = perFrameMode;
            startTime = Time.realtimeSinceStartup;
            lastOverlayUpdate = -1f;

            frameRecords = new List<FrameRecord>();
            modRecords = null;
            patchRecords = null;

            if (!perFrame)
            {
                var co = mb.StartCoroutine(PerSecondLoop());
                stopAction = () => mb.StopCoroutine(co);
            }

            Main.Log("ProfilingRecorder: Started (" + (perFrame ? "per-frame" : "per-second") + " mode)");
        }

        public static void Stop()
        {
            if (!recording) return;
            recording = false;

            if (stopAction != null)
            {
                stopAction();
                stopAction = null;
            }

            Export();

            frameRecords = null;
            modRecords = null;
            patchRecords = null;
            StringOutput = null;
        }

        internal static void RecordFrameData(FrameData data)
        {
            if (!recording) return;
            var ts = Time.realtimeSinceStartup - startTime;
            frameRecords.Add(new FrameRecord { timestamp = ts, data = data });
            UpdateOverlay(ts);
        }

        internal static void RecordModTimings(List<ModTimingData> timings)
        {
            if (!recording || timings == null || timings.Count == 0) return;
            if (modRecords == null) modRecords = new List<ModRecord>();
            var ts = Time.realtimeSinceStartup - startTime;
            foreach (var t in timings)
                modRecords.Add(new ModRecord { timestamp = ts, data = t });
        }

        internal static void RecordPatchTimings(List<PatchTimingData> timings)
        {
            if (!recording || timings == null || timings.Count == 0) return;
            if (patchRecords == null) patchRecords = new List<PatchRecord>();
            var ts = Time.realtimeSinceStartup - startTime;
            foreach (var t in timings)
                patchRecords.Add(new PatchRecord { timestamp = ts, data = t });
        }

        private static void UpdateOverlay(float ts)
        {
            if (ts - lastOverlayUpdate < 1f) return;
            lastOverlayUpdate = ts;
            var mins = (int)(ts / 60f);
            var secs = (int)(ts % 60f);
            StringOutput = "REC " + mins + ":" + secs.ToString("D2");
        }

        #region Per-Second Mode

        private static IEnumerator PerSecondLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (!recording) yield break;

                var ts = Time.realtimeSinceStartup - startTime;

                frameRecords.Add(new FrameRecord
                {
                    timestamp = ts,
                    data = FrameCounter.GetFrameData()
                });

                var modTimings = ModCounter.GetModTimings();
                if (modTimings.Count > 0)
                {
                    if (modRecords == null) modRecords = new List<ModRecord>();
                    foreach (var mt in modTimings)
                        modRecords.Add(new ModRecord { timestamp = ts, data = mt });
                }

                if (HarmonyPatchProfiler.IsRunning)
                {
                    var patchTimings = HarmonyPatchProfiler.GetPatchTimings();
                    if (patchTimings.Count > 0)
                    {
                        if (patchRecords == null) patchRecords = new List<PatchRecord>();
                        foreach (var pt in patchTimings)
                            patchRecords.Add(new PatchRecord { timestamp = ts, data = pt });
                    }
                }

                UpdateOverlay(ts);
            }
        }

        #endregion

        #region Export

        private static void Export()
        {
            if (frameRecords == null || frameRecords.Count == 0)
            {
                Main.Log("ProfilingRecorder: No data to export");
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var runDir = Path.Combine(Path.Combine(Main.mod.Path, "Results"), timestamp);
            Directory.CreateDirectory(runDir);

            ExportFramesCsv(Path.Combine(runDir, "frames.csv"));
            if (modRecords != null && modRecords.Count > 0)
                ExportModsCsv(Path.Combine(runDir, "mods.csv"));
            if (patchRecords != null && patchRecords.Count > 0)
                ExportPatchesCsv(Path.Combine(runDir, "patches.csv"));
            ExportSummary(Path.Combine(runDir, "summary.md"));

            Main.Log("ProfilingRecorder: Exported to " + runDir);
        }

        private static void ExportFramesCsv(string path)
        {
            var lines = new List<string>();
            lines.Add("Timestamp,FPS,TotalMs,FixedUpdateMs,UpdateMs,YieldMs,LateUpdateMs,RenderMs,OnGUIMs,OtherMs,GCHeapMB,GCAllocKBPerSec,ProcessMemMB,FreeMemMB");

            foreach (var r in frameRecords)
            {
                var d = r.data;
                var procMB = d.ProcessMemoryBytes >= 0 ? (d.ProcessMemoryBytes / (1024f * 1024f)).ToString("F1", Fmt) : "-1";
                var freeMB = d.SystemFreeMemBytes >= 0 ? (d.SystemFreeMemBytes / (1024f * 1024f)).ToString("F1", Fmt) : "-1";

                lines.Add(string.Join(",", new[] {
                    Ff(r.timestamp, 2), Ff(d.FPS, 1), Ff(d.TotalMs, 2),
                    Ff(d.FixedUpdateMs, 2), Ff(d.UpdateMs, 2), Ff(d.YieldMs, 2),
                    Ff(d.LateUpdateMs, 2), Ff(d.RenderMs, 2), Ff(d.OnGUIMs, 2), Ff(d.OtherMs, 2),
                    Ff(d.GCMemoryBytes / (1024f * 1024f), 1), Ff(d.GCAllocKBPerSec, 1),
                    procMB, freeMB }));
            }

            File.WriteAllLines(path, lines.ToArray());
        }

        private static void ExportModsCsv(string path)
        {
            var lines = new List<string>();
            lines.Add("Timestamp,ModName,UpdateMs,FixedUpdateMs,LateUpdateMs,OnGUIMs,TotalMs");

            foreach (var r in modRecords)
            {
                var d = r.data;
                lines.Add(string.Join(",", new[] {
                    Ff(r.timestamp, 2), EscapeCsv(StripRichText(d.ModName)),
                    Ff(d.UpdateMs, 3), Ff(d.FixedUpdateMs, 3),
                    Ff(d.LateUpdateMs, 3), Ff(d.OnGUIMs, 3), Ff(d.TotalMs, 3) }));
            }

            File.WriteAllLines(path, lines.ToArray());
        }

        private static void ExportPatchesCsv(string path)
        {
            var lines = new List<string>();
            lines.Add("Timestamp,ModName,TargetMethod,PatchType,AverageMs");

            foreach (var r in patchRecords)
            {
                var d = r.data;
                lines.Add(string.Join(",", new[] {
                    Ff(r.timestamp, 2), EscapeCsv(StripRichText(d.ModName)),
                    EscapeCsv(d.TargetMethod), d.PatchType, Ff(d.AverageMs, 3) }));
            }

            File.WriteAllLines(path, lines.ToArray());
        }

        private static readonly System.Globalization.CultureInfo Fmt = System.Globalization.CultureInfo.InvariantCulture;

        private static string Ff(float value, int decimals)
        {
            return value.ToString("F" + decimals, Fmt);
        }

        private static string EscapeCsv(string value)
        {
            if (value != null && (value.Contains(",") || value.Contains("\"") || value.Contains("\n")))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value ?? "";
        }

        #endregion

        #region Summary Report

        private static void ExportSummary(string path)
        {
            var lines = new List<string>();
            var duration = frameRecords.Count > 0
                ? frameRecords[frameRecords.Count - 1].timestamp
                : 0f;
            var durationSpan = TimeSpan.FromSeconds(duration);
            var avgFrame = frameRecords.Count > 0 ? frameRecords.Average(r => r.data.TotalMs) : 0f;

            lines.Add("# BroforceDevTools Profiling Summary");
            lines.Add("");
            lines.Add("**Recording:** " + (int)durationSpan.TotalMinutes + ":" + durationSpan.Seconds.ToString("D2")
                + " (" + frameRecords.Count + " samples, " + (perFrame ? "per-frame" : "per-second") + " mode)  ");
            lines.Add("**Date:** " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("");

            WriteFrameTimingSection(lines, avgFrame);
            WriteMemorySection(lines);
            WriteModSection(lines, duration, avgFrame);
            WritePatchSection(lines, avgFrame);
            WriteFooterSection(lines);

            File.WriteAllLines(path, lines.ToArray());
        }

        private static void WriteFrameTimingSection(List<string> lines, float avgFrame)
        {
            if (frameRecords.Count == 0) return;

            lines.Add("## Frame Timing");
            lines.Add("");

            var fpsList = frameRecords.Select(r => r.data.FPS).OrderBy(x => x).ToList();
            var frameMsList = frameRecords.Select(r => r.data.TotalMs).OrderBy(x => x).ToList();

            lines.Add("| | avg | min | max | p95 | p99 |");
            lines.Add("|---|---|---|---|---|---|");
            lines.Add("| **FPS** | " + Ff(Average(fpsList), 1)
                + " | " + Ff(fpsList[0], 1)
                + " | " + Ff(fpsList[fpsList.Count - 1], 1)
                + " | " + Ff(Percentile(fpsList, 0.05f), 1)
                + " | " + Ff(Percentile(fpsList, 0.01f), 1) + " |");
            lines.Add("| **Frame** | " + Ff(Average(frameMsList), 1) + "ms"
                + " | " + Ff(frameMsList[0], 1) + "ms"
                + " | " + Ff(frameMsList[frameMsList.Count - 1], 1) + "ms"
                + " | " + Ff(Percentile(frameMsList, 0.95f), 1) + "ms"
                + " | " + Ff(Percentile(frameMsList, 0.99f), 1) + "ms |");
            lines.Add("");

            var spikes33 = frameRecords.Count(r => r.data.TotalMs > 33f);
            var spikes20 = frameRecords.Count(r => r.data.TotalMs > 20f);
            lines.Add("**Spikes:** " + spikes33 + " frames >33ms (30fps), " + spikes20 + " frames >20ms (50fps)");
            lines.Add("");

            lines.Add("### Phase Breakdown (avg)");
            lines.Add("");
            lines.Add("| Phase | Time | % of frame |");
            lines.Add("|---|---|---|");

            var phases = new List<KeyValuePair<string, float>>();
            phases.Add(new KeyValuePair<string, float>("Render/VSync", frameRecords.Average(r => r.data.RenderMs)));
            phases.Add(new KeyValuePair<string, float>("Update", frameRecords.Average(r => r.data.UpdateMs)));
            phases.Add(new KeyValuePair<string, float>("OnGUI", frameRecords.Average(r => r.data.OnGUIMs)));
            phases.Add(new KeyValuePair<string, float>("FixedUpdate", frameRecords.Average(r => r.data.FixedUpdateMs)));
            phases.Add(new KeyValuePair<string, float>("Yield/Anim", frameRecords.Average(r => r.data.YieldMs)));
            phases.Add(new KeyValuePair<string, float>("LateUpdate", frameRecords.Average(r => r.data.LateUpdateMs)));
            phases.Add(new KeyValuePair<string, float>("Other", frameRecords.Average(r => r.data.OtherMs)));
            phases.Sort((a, b) => b.Value.CompareTo(a.Value));

            foreach (var phase in phases)
            {
                var pct = avgFrame > 0 ? (phase.Value / avgFrame * 100f) : 0f;
                lines.Add("| " + phase.Key + " | " + Ff(phase.Value, 2) + "ms | " + Ff(pct, 1) + "% |");
            }

            lines.Add("");
        }

        private static void WriteMemorySection(List<string> lines)
        {
            if (frameRecords.Count == 0) return;

            lines.Add("## Memory");
            lines.Add("");

            var first = frameRecords[0].data;
            var last = frameRecords[frameRecords.Count - 1].data;

            var gcStartMB = first.GCMemoryBytes / (1024f * 1024f);
            var gcEndMB = last.GCMemoryBytes / (1024f * 1024f);
            var gcDelta = gcEndMB - gcStartMB;
            lines.Add("- **GC Heap:** " + Ff(gcStartMB, 0) + "MB -> " + Ff(gcEndMB, 0) + "MB ("
                + (gcDelta >= 0 ? "+" : "") + Ff(gcDelta, 0) + "MB)");

            if (first.ProcessMemoryBytes >= 0 && last.ProcessMemoryBytes >= 0)
            {
                var procStartMB = first.ProcessMemoryBytes / (1024f * 1024f);
                var procEndMB = last.ProcessMemoryBytes / (1024f * 1024f);
                var procDelta = procEndMB - procStartMB;
                lines.Add("- **Process RAM:** " + Ff(procStartMB, 0) + "MB -> " + Ff(procEndMB, 0) + "MB ("
                    + (procDelta >= 0 ? "+" : "") + Ff(procDelta, 0) + "MB)");
            }

            var avgAllocRate = frameRecords.Average(r => r.data.GCAllocKBPerSec);
            var peakAllocRate = frameRecords.Max(r => r.data.GCAllocKBPerSec);
            lines.Add("- **GC Alloc Rate:** avg " + Ff(avgAllocRate, 1) + " KB/s | peak " + Ff(peakAllocRate, 1) + " KB/s");

            if (first.GCCollectionCounts != null && last.GCCollectionCounts != null)
            {
                int totalCollections = 0;
                var gens = System.Math.Min(first.GCCollectionCounts.Length, last.GCCollectionCounts.Length);
                for (int g = 0; g < gens; g++)
                    totalCollections += last.GCCollectionCounts[g] - first.GCCollectionCounts[g];
                lines.Add("- **GC Collections:** " + totalCollections + " during recording");
            }

            lines.Add("");
        }

        private static void WriteModSection(List<string> lines, float duration, float avgFrame)
        {
            var hasModData = modRecords != null && modRecords.Count > 0;
            var hasPatchData = patchRecords != null && patchRecords.Count > 0;
            if (!hasModData && !hasPatchData) return;

            // Build per-mod callback data
            var modCallbacks = new Dictionary<string, float[]>(); // 0:update, 1:fixed, 2:late, 3:gui
            var modCallbackPeaks = new Dictionary<string, float>();
            var modCallbackFirstHalf = new Dictionary<string, float>();
            var modCallbackSecondHalf = new Dictionary<string, float>();
            var midpoint = duration / 2f;

            if (hasModData)
            {
                foreach (var g in modRecords.GroupBy(r => StripRichText(r.data.ModName)))
                {
                    modCallbacks[g.Key] = new[] {
                        g.Average(r => r.data.UpdateMs),
                        g.Average(r => r.data.FixedUpdateMs),
                        g.Average(r => r.data.LateUpdateMs),
                        g.Average(r => r.data.OnGUIMs)
                    };
                    modCallbackPeaks[g.Key] = g.Max(r => r.data.TotalMs);
                    var first = g.Where(r => r.timestamp <= midpoint).ToList();
                    var second = g.Where(r => r.timestamp > midpoint).ToList();
                    modCallbackFirstHalf[g.Key] = first.Count > 0 ? first.Average(r => r.data.TotalMs) : 0f;
                    modCallbackSecondHalf[g.Key] = second.Count > 0 ? second.Average(r => r.data.TotalMs) : 0f;
                }
            }

            // Build per-mod patch data
            var modPatchTime = new Dictionary<string, float>();
            var modPatchPeaks = new Dictionary<string, float>();
            if (hasPatchData)
            {
                foreach (var g in patchRecords.GroupBy(r => StripRichText(r.data.ModName)))
                {
                    // Sum average ms across all patches for this mod
                    var patchGroups = g.GroupBy(r => r.data.TargetMethod + "|" + r.data.PatchType);
                    float totalPatchAvg = 0;
                    float maxPatchPeak = 0;
                    foreach (var pg in patchGroups)
                    {
                        var avg = pg.Average(r => r.data.AverageMs);
                        var peak = pg.Max(r => r.data.AverageMs);
                        totalPatchAvg += avg;
                        if (peak > maxPatchPeak) maxPatchPeak = peak;
                    }
                    modPatchTime[g.Key] = totalPatchAvg;
                    modPatchPeaks[g.Key] = maxPatchPeak;
                }
            }

            // Merge all mod names
            var allMods = new HashSet<string>();
            foreach (var k in modCallbacks.Keys) allMods.Add(k);
            foreach (var k in modPatchTime.Keys) allMods.Add(k);

            // Compute combined totals
            var modEntries = new List<ModSummaryEntry>();
            foreach (var name in allMods)
            {
                var entry = new ModSummaryEntry();
                entry.Name = name;

                float[] cb;
                if (modCallbacks.TryGetValue(name, out cb))
                {
                    entry.AvgUpdate = cb[0]; entry.AvgFixed = cb[1];
                    entry.AvgLate = cb[2]; entry.AvgGui = cb[3];
                    entry.CallbackTotal = cb[0] + cb[1] + cb[2] + cb[3];
                }

                float pt;
                if (modPatchTime.TryGetValue(name, out pt))
                    entry.PatchTotal = pt;

                entry.CombinedTotal = entry.CallbackTotal + entry.PatchTotal;

                float cbPeak;
                float pPeak;
                var hasCbPeak = modCallbackPeaks.TryGetValue(name, out cbPeak);
                var hasPPeak = modPatchPeaks.TryGetValue(name, out pPeak);
                entry.PeakTotal = System.Math.Max(hasCbPeak ? cbPeak : 0, hasPPeak ? pPeak : 0);

                float fh, sh;
                entry.FirstHalfAvg = modCallbackFirstHalf.TryGetValue(name, out fh) ? fh : 0;
                entry.SecondHalfAvg = modCallbackSecondHalf.TryGetValue(name, out sh) ? sh : 0;

                modEntries.Add(entry);
            }

            modEntries.Sort((a, b) => b.CombinedTotal.CompareTo(a.CombinedTotal));

            lines.Add("## Mod Performance");
            lines.Add("");
            lines.Add("*" + modEntries.Count + " mods with measurable overhead. Mods with negligible impact are omitted.*");
            lines.Add("");

            int rank = 1;
            float totalOverhead = 0;
            foreach (var mod in modEntries)
            {
                totalOverhead += mod.CombinedTotal;
                var framePct = avgFrame > 0 ? (mod.CombinedTotal / avgFrame * 100f) : 0f;
                lines.Add("### #" + rank + " " + mod.Name + " — " + Ff(mod.CombinedTotal, 2) + "ms avg (" + Ff(framePct, 1) + "% of frame) | peak " + Ff(mod.PeakTotal, 2) + "ms");
                lines.Add("");

                // Callback breakdown
                if (mod.CallbackTotal > 0.001f)
                {
                    lines.Add("**Callbacks:** " + Ff(mod.CallbackTotal, 2) + "ms");
                    var callbacks = new List<KeyValuePair<string, float>>();
                    if (mod.AvgUpdate > 0.001f) callbacks.Add(new KeyValuePair<string, float>("Update", mod.AvgUpdate));
                    if (mod.AvgFixed > 0.001f) callbacks.Add(new KeyValuePair<string, float>("FixedUpdate", mod.AvgFixed));
                    if (mod.AvgLate > 0.001f) callbacks.Add(new KeyValuePair<string, float>("LateUpdate", mod.AvgLate));
                    if (mod.AvgGui > 0.001f) callbacks.Add(new KeyValuePair<string, float>("OnGUI", mod.AvgGui));
                    callbacks.Sort((a, b) => b.Value.CompareTo(a.Value));
                    foreach (var cb in callbacks)
                        lines.Add("- " + cb.Key + ": " + Ff(cb.Value, 2) + "ms");
                }

                // Patch breakdown
                if (mod.PatchTotal > 0.001f)
                {
                    lines.Add("**Patches:** " + Ff(mod.PatchTotal, 2) + "ms");
                    if (hasPatchData)
                    {
                        var patches = patchRecords
                            .Where(r => StripRichText(r.data.ModName) == mod.Name)
                            .GroupBy(r => r.data.TargetMethod + "|" + r.data.PatchType)
                            .Select(pg => new {
                                Target = pg.First().data.TargetMethod,
                                Type = pg.First().data.PatchType,
                                Avg = pg.Average(r => r.data.AverageMs)
                            })
                            .OrderByDescending(p => p.Avg)
                            .ToList();
                        foreach (var p in patches)
                            lines.Add("- " + p.Target + " (" + p.Type + "): " + Ff(p.Avg, 3) + "ms");
                    }
                }

                var trend = ComputeTrend(mod.FirstHalfAvg, mod.SecondHalfAvg);
                if (trend == "stable")
                    lines.Add("- Trend: **stable**");
                else
                    lines.Add("- Trend: **" + trend + "** (first half " + Ff(mod.FirstHalfAvg, 2)
                        + "ms -> second half " + Ff(mod.SecondHalfAvg, 2) + "ms)");

                lines.Add("");
                rank++;
            }

            var totalPct = avgFrame > 0 ? (totalOverhead / avgFrame * 100f) : 0f;
            lines.Add("**Total mod overhead:** " + Ff(totalOverhead, 2) + "ms avg (" + Ff(totalPct, 1) + "% of frame budget)");
            lines.Add("");
        }

        private class ModSummaryEntry
        {
            public string Name;
            public float AvgUpdate, AvgFixed, AvgLate, AvgGui;
            public float CallbackTotal;
            public float PatchTotal;
            public float CombinedTotal;
            public float PeakTotal;
            public float FirstHalfAvg, SecondHalfAvg;
        }

        private static void WritePatchSection(List<string> lines, float avgFrame)
        {
            if (patchRecords == null || patchRecords.Count == 0) return;

            var patchGroups = patchRecords
                .GroupBy(r => StripRichText(r.data.ModName) + "|" + r.data.TargetMethod + "|" + r.data.PatchType)
                .Select(g =>
                {
                    var first = g.First().data;
                    return new
                    {
                        ModName = StripRichText(first.ModName),
                        TargetMethod = first.TargetMethod,
                        PatchType = first.PatchType,
                        AvgMs = g.Average(r => r.data.AverageMs),
                        PeakMs = g.Max(r => r.data.AverageMs)
                    };
                })
                .OrderByDescending(x => x.AvgMs)
                .ToList();

            lines.Add("## Patch Details");
            lines.Add("");
            lines.Add("*" + patchGroups.Count + " patches with measurable overhead.*");
            lines.Add("");

            lines.Add("| # | Mod | Target | Type | Avg | Peak | % of frame |");
            lines.Add("|---|---|---|---|---|---|---|");

            int rank = 1;
            float totalPatchOverhead = 0;
            foreach (var patch in patchGroups)
            {
                totalPatchOverhead += patch.AvgMs;
                var framePct = avgFrame > 0 ? (patch.AvgMs / avgFrame * 100f) : 0f;
                lines.Add("| " + rank + " | " + patch.ModName + " | " + patch.TargetMethod
                    + " | " + patch.PatchType + " | " + Ff(patch.AvgMs, 3) + "ms | " + Ff(patch.PeakMs, 3) + "ms | " + Ff(framePct, 1) + "% |");
                rank++;
            }

            lines.Add("");
            var totalPct = avgFrame > 0 ? (totalPatchOverhead / avgFrame * 100f) : 0f;
            lines.Add("**Total patch overhead:** " + Ff(totalPatchOverhead, 2) + "ms avg (" + Ff(totalPct, 1) + "% of frame budget)");
            lines.Add("");
        }

        private static void WriteFooterSection(List<string> lines)
        {
            lines.Add("## Data Sources");
            lines.Add("");

            lines.Add("- [x] Frame timing");

            if (modRecords != null && modRecords.Count > 0)
                lines.Add("- [x] Mod callback timing");
            else
                lines.Add("- [ ] Mod callback timing");

            if (patchRecords != null && patchRecords.Count > 0)
                lines.Add("- [x] Harmony patch timing");
            else
                lines.Add("- [ ] Harmony patch timing");

            lines.Add("");
            lines.Add("*Files exported to: " + Path.Combine(Main.mod.Path, "Results") + "*");
        }

        #endregion

        #region Helpers

        private static float Average(List<float> sorted)
        {
            if (sorted.Count == 0) return 0;
            float sum = 0;
            for (int i = 0; i < sorted.Count; i++)
                sum += sorted[i];
            return sum / sorted.Count;
        }

        private static float Percentile(List<float> sorted, float p)
        {
            if (sorted.Count == 0) return 0;
            var index = (int)(p * (sorted.Count - 1));
            if (index < 0) index = 0;
            if (index >= sorted.Count) index = sorted.Count - 1;
            return sorted[index];
        }

        private static readonly System.Text.RegularExpressions.Regex RichTextRegex =
            new System.Text.RegularExpressions.Regex(@"<\/?[a-zA-Z][^>]*>");

        private static string StripRichText(string value)
        {
            if (value == null) return "";
            return RichTextRegex.Replace(value, "").Trim();
        }

        private static string ComputeTrend(float firstHalf, float secondHalf)
        {
            if (firstHalf < 0.001f) return "stable";
            var change = (secondHalf - firstHalf) / firstHalf;
            if (change > 0.2f) return "increasing";
            if (change < -0.2f) return "decreasing";
            return "stable";
        }

        #endregion
    }
}
