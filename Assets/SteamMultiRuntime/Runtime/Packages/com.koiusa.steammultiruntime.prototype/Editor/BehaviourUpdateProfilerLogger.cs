using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    /// <summary>
    /// Reads the Editor Profiler hierarchy so MonoBehaviour callbacks hidden below
    /// BehaviourUpdate can be attributed without manually expanding the CPU Profiler.
    /// </summary>
    [InitializeOnLoad]
    internal static class BehaviourUpdateProfilerLogger
    {
        private const double WarmupSeconds = 5d;
        private const double LogIntervalSeconds = 3d;
        private const int MaximumFramesPerReport = 90;
        private const int MaximumEntries = 20;
        private const string SessionProfilerWasEnabled =
            "Koiusa.SteamMultiRuntime.BehaviourProfiler.WasEnabled";
        private const string EnabledPreference =
            "Koiusa.SteamMultiRuntime.BehaviourProfiler.Enabled";
        private const string MenuPath =
            "Tools/SteamMultiRuntime/Diagnostics/Automatic Behaviour Profiler";

        private sealed class Timing
        {
            public double TotalMilliseconds;
            public double MaximumMilliseconds;
            public int Calls;
        }

        private static double nextLogTime;
        private static int lastFrameIndex = -1;
        private static bool captureActive;

        static BehaviourUpdateProfilerLogger()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += Update;
        }

        [MenuItem(MenuPath)]
        private static void ToggleEnabled()
        {
            var enabled = !EditorPrefs.GetBool(EnabledPreference, false);
            EditorPrefs.SetBool(EnabledPreference, enabled);
            Menu.SetChecked(MenuPath, enabled);
            Debug.Log($"[BehaviourUpdateProfiler] automatic capture={(enabled ? "ON" : "OFF")}");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggleEnabled()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(EnabledPreference, false));
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!EditorPrefs.GetBool(EnabledPreference, false))
                    return;
                SessionState.SetBool(SessionProfilerWasEnabled, ProfilerDriver.enabled);
                ProfilerDriver.enabled = true;
                captureActive = true;
                lastFrameIndex = ProfilerDriver.lastFrameIndex;
                nextLogTime = EditorApplication.timeSinceStartup + WarmupSeconds;
                return;
            }

            if (state != PlayModeStateChange.ExitingPlayMode)
                return;
            if (!captureActive)
                return;
            if (!SessionState.GetBool(SessionProfilerWasEnabled, false))
                ProfilerDriver.enabled = false;
            captureActive = false;
            lastFrameIndex = -1;
        }

        private static void Update()
        {
            if (!EditorPrefs.GetBool(EnabledPreference, false)
                || !EditorApplication.isPlaying
                || EditorApplication.timeSinceStartup < nextLogTime)
                return;
            nextLogTime = EditorApplication.timeSinceStartup + LogIntervalSeconds;
            LogBehaviourUpdateBreakdown();
        }

        private static void LogBehaviourUpdateBreakdown()
        {
            var latestFrame = ProfilerDriver.lastFrameIndex;
            if (latestFrame < 0 || latestFrame <= lastFrameIndex)
                return;
            var firstFrame = Math.Max(lastFrameIndex + 1, latestFrame - MaximumFramesPerReport + 1);
            var timings = new Dictionary<string, Timing>(StringComparer.Ordinal);
            var sampledFrames = 0;
            var worstFrameIndex = -1;
            var worstFrameMilliseconds = 0d;
            for (var frameIndex = firstFrame; frameIndex <= latestFrame; frameIndex++)
            {
                using var frame = ProfilerDriver.GetRawFrameDataView(frameIndex, 0);
                if (!frame.valid)
                    continue;
                sampledFrames++;
                var frameMilliseconds = GetLongestMainThreadSample(frame);
                if (frameMilliseconds > worstFrameMilliseconds)
                {
                    worstFrameMilliseconds = frameMilliseconds;
                    worstFrameIndex = frameIndex;
                }
                for (var sampleIndex = 0; sampleIndex < frame.sampleCount; sampleIndex++)
                {
                    if (!string.Equals(frame.GetSampleName(sampleIndex), "BehaviourUpdate", StringComparison.Ordinal))
                        continue;
                    var nextIndex = sampleIndex + 1;
                    var childCount = frame.GetSampleChildrenCount(sampleIndex);
                    for (var child = 0; child < childCount; child++)
                        nextIndex = CollectSubtree(frame, nextIndex, timings);
                    sampleIndex = Math.Max(sampleIndex, nextIndex - 1);
                }
            }
            lastFrameIndex = latestFrame;
            if (sampledFrames == 0 || timings.Count == 0)
                return;

            var builder = new StringBuilder(1024);
            var entries = timings
                .Where(pair => pair.Value.TotalMilliseconds > 0d)
                .OrderByDescending(pair => pair.Value.TotalMilliseconds)
                .Take(MaximumEntries);
            foreach (var pair in entries)
            {
                if (builder.Length > 0)
                    builder.Append(' ');
                builder.Append(pair.Key)
                    .Append('=')
                    .Append((pair.Value.TotalMilliseconds / sampledFrames).ToString("F3"))
                    .Append("ms(max=")
                    .Append(pair.Value.MaximumMilliseconds.ToString("F3"))
                    .Append("ms calls=")
                    .Append((pair.Value.Calls / (double)sampledFrames).ToString("F1"))
                    .Append(')');
            }
            if (builder.Length > 0)
                Debug.Log($"[BehaviourUpdateDetail] frames={sampledFrames} {builder}");
            if (worstFrameIndex >= 0)
                LogWorstFrameBreakdown(worstFrameIndex, worstFrameMilliseconds);
        }

        private static void LogWorstFrameBreakdown(int frameIndex, double frameMilliseconds)
        {
            using var frame = ProfilerDriver.GetRawFrameDataView(frameIndex, 0);
            if (!frame.valid || frame.sampleCount == 0)
                return;
            var timings = new Dictionary<string, Timing>(StringComparer.Ordinal);
            for (var sampleIndex = 0; sampleIndex < frame.sampleCount; sampleIndex++)
                CollectSample(frame, sampleIndex, timings);
            var builder = new StringBuilder(1024);
            var entries = timings
                .Where(pair => pair.Value.TotalMilliseconds > 0d
                    && !string.Equals(pair.Key, "Main Thread", StringComparison.Ordinal))
                .OrderByDescending(pair => pair.Value.TotalMilliseconds)
                .Take(MaximumEntries);
            foreach (var pair in entries)
            {
                if (builder.Length > 0)
                    builder.Append(' ');
                builder.Append(pair.Key)
                    .Append('=')
                    .Append(pair.Value.TotalMilliseconds.ToString("F3"))
                    .Append("ms(calls=")
                    .Append(pair.Value.Calls)
                    .Append(')');
            }
            if (builder.Length > 0)
                Debug.Log($"[WorstFrameDetail] frame={frameIndex} main={frameMilliseconds:F3}ms {builder}");
        }

        private static double GetLongestMainThreadSample(RawFrameDataView frame)
        {
            var longestMilliseconds = 0d;
            for (var sampleIndex = 0; sampleIndex < frame.sampleCount; sampleIndex++)
                longestMilliseconds = Math.Max(longestMilliseconds, frame.GetSampleTimeMs(sampleIndex));
            return longestMilliseconds;
        }

        private static void CollectSample(
            RawFrameDataView frame,
            int sampleIndex,
            Dictionary<string, Timing> timings)
        {
            var name = frame.GetSampleName(sampleIndex);
            if (string.IsNullOrEmpty(name))
                return;
            if (!timings.TryGetValue(name, out var timing))
            {
                timing = new Timing();
                timings.Add(name, timing);
            }
            var milliseconds = frame.GetSampleTimeMs(sampleIndex);
            timing.TotalMilliseconds += milliseconds;
            timing.MaximumMilliseconds = Math.Max(timing.MaximumMilliseconds, milliseconds);
            timing.Calls++;
        }

        private static int CollectSubtree(
            RawFrameDataView frame,
            int sampleIndex,
            Dictionary<string, Timing> timings)
        {
            if (sampleIndex < 0 || sampleIndex >= frame.sampleCount)
                return frame.sampleCount;
            CollectSample(frame, sampleIndex, timings);

            var nextIndex = sampleIndex + 1;
            var childCount = frame.GetSampleChildrenCount(sampleIndex);
            for (var child = 0; child < childCount; child++)
                nextIndex = CollectSubtree(frame, nextIndex, timings);
            return nextIndex;
        }
    }
}
