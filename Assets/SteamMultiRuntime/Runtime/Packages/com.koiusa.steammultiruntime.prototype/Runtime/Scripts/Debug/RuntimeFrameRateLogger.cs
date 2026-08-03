#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public static class RuntimeFrameRateLogging
    {
        public static void Refresh() => RuntimeFrameRateLogger.RefreshInstallation();
    }

    /// <summary>Samples every rendered frame and emits one aggregate line per second.</summary>
    [DisallowMultipleComponent]
    internal sealed class RuntimeFrameRateLogger : MonoBehaviour
    {
        private const float LogInterval = 1f;
        private const int MaximumDetailMarkers = 24;
        private const int MaximumDiscoveredRecorders = 96;
        private static readonly string[] DetailMarkerFragments =
        {
            "Network",
            "Netcode",
            "Transport",
            "Steam",
            "Facepunch",
            "Behaviour",
            "ScriptRun",
            "PlayerLoop",
            "Transform",
            "Skin",
            "JobHandle",
            "WaitForJob",
            "Semaphore"
        };
        private static readonly HashSet<string> DetailMarkerNames = new(StringComparer.Ordinal)
        {
            "Main Thread",
            "Render Thread",
            "Animator.ProcessGraph",
            "Animator.Update",
            "Animation.Update",
            "DirectorUpdateAnimationBegin",
            "DirectorUpdateAnimationEnd",
            "BehaviourUpdate",
            "LateBehaviourUpdate",
            "Physics.FixedUpdate",
            "FixedUpdate.PhysicsFixedUpdate",
            "Physics.Simulate",
            "Physics.PublishResultsStage",
            "Physics.SendEvents",
            "Physics.SendTriggerEvents",
            "UpdateRendererBoundingVolumes",
            "CullScriptable",
            "SceneCulling",
            "BatchRendererGroup.UpdateMetadataCache",
            "Skinning.Update",
            "Animation.NpcCrowdSpring.TransformJob",
            "Animation.NpcCrowdSpring.Snapshot",
            "Animation.NpcCrowdSpring.ColliderSnapshot",
            "Animation.NpcCrowdSpring.Solve",
            "Animation.NpcCrowdSpring.Apply",
            "Physics.NpcCrowd.PrepareProbes",
            "Physics.NpcCrowd.Prepare.Recovery",
            "Physics.NpcCrowd.Prepare.Commands",
              "Physics.NpcCrowd.Prepare.Motor",
              "Physics.NpcCrowd.Prepare.ControllerCommand",
              "Physics.NpcCrowd.Prepare.AgentSnapshot",
            "Physics.NpcCrowd.Prepare.ProbeCommands",
            "Physics.NpcCrowd.Presentation",
            "Physics.NpcCrowd.Presentation.Controller",
            "Physics.NpcCrowd.Presentation.Skill",
            "Physics.NpcCrowd.Presentation.Navigation",
            "Physics.NpcCrowd.Maintenance",
            "Physics.NpcCrowd.PathfindingBudget",
            "Physics.NpcCrowd.QueryAndSteeringWait",
            "Physics.NpcCrowd.ApplyProbeResults",
            "Physics.NpcCrowd.ResolvePenetration",
            "Physics.NpcCrowd.MovementJob",
            "Physics.NpcCrowd.ApplyMovementAndContacts",
            "Physics.NpcCrowd.MovingPlatformFollow",
            "Physics.NpcCrowd.MovingPlatformBinding",
            "Physics.NpcCrowd.PrepareMovingPlatformPairs",
            "Network.NpcMovingPlatformSync",
            "Physics.MovingPlatform.FixedUpdate",
            "Physics.MovingPlatform.SamplePose",
            "Physics.MovingPlatform.ApplyPose",
            "Physics.MovingPlatform.NotifyCrowd"
        };

        private sealed class MarkerRecorder : IDisposable
        {
            public string Name;
            public ProfilerRecorder Recorder;
            public long TotalNanoseconds;
            public long MaximumNanoseconds;
            public int Samples;

            public double AverageMilliseconds => Samples > 0
                ? TotalNanoseconds / (double)Samples / 1_000_000d
                : 0d;

            public void Dispose() => Recorder.Dispose();
        }

        private static RuntimeFrameRateLogger instance;

        private int sampledFrames;
        private float elapsedTime;
        private float maximumFrameTime;
        private float minimumFrameTime = float.PositiveInfinity;
        private readonly FrameTiming[] frameTimings = new FrameTiming[1];
        private double cpuFrameTimeTotal;
        private double gpuFrameTimeTotal;
        private double maximumCpuFrameTime;
        private double maximumGpuFrameTime;
        private int cpuTimingSamples;
        private int gpuTimingSamples;
        private readonly List<MarkerRecorder> markerRecorders = new();

        private void Awake() => StartMarkerRecorders();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Application.isPlaying
                || !RuntimeToolSettings.FrameRateLoggingEnabled
                || instance != null)
                return;
            var host = new GameObject("RuntimeFrameRateLogger");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            instance = host.AddComponent<RuntimeFrameRateLogger>();
        }

        internal static void RefreshInstallation()
        {
            // The Tools menu is also available in Edit Mode. In that state only the
            // persisted preference changes; runtime object lifetime starts with Play.
            if (!Application.isPlaying)
                return;

            if (RuntimeToolSettings.FrameRateLoggingEnabled)
            {
                Install();
                return;
            }

            if (instance != null)
                Destroy(instance.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Update()
        {
            var frameTime = Time.unscaledDeltaTime;
            if (frameTime <= 0f)
                return;

            sampledFrames++;
            elapsedTime += frameTime;
            maximumFrameTime = Mathf.Max(maximumFrameTime, frameTime);
            minimumFrameTime = Mathf.Min(minimumFrameTime, frameTime);
            CaptureCpuGpuTiming();
            CaptureMarkerTimings();
            if (elapsedTime < LogInterval)
                return;

            var averageFps = sampledFrames / elapsedTime;
            var minimumFps = 1f / maximumFrameTime;
            var maximumFps = 1f / minimumFrameTime;
            Debug.Log(
                $"[FrameRate] role={GetNetworkRole()} avg={averageFps:F1}fps " +
                $"min={minimumFps:F1}fps max={maximumFps:F1}fps " +
                $"avgFrame={(elapsedTime / sampledFrames) * 1000f:F2}ms " +
                $"maxFrame={maximumFrameTime * 1000f:F2}ms frames={sampledFrames} " +
                $"cpuAvg={FormatAverageTiming(cpuFrameTimeTotal, cpuTimingSamples)} " +
                $"cpuMax={FormatMaximumTiming(maximumCpuFrameTime, cpuTimingSamples)} " +
                $"gpuAvg={FormatAverageTiming(gpuFrameTimeTotal, gpuTimingSamples)} " +
                $"gpuMax={FormatMaximumTiming(maximumGpuFrameTime, gpuTimingSamples)} " +
                $"vSync={QualitySettings.vSyncCount} target={Application.targetFrameRate}");
            LogMarkerTimings();

            sampledFrames = 0;
            elapsedTime = 0f;
            maximumFrameTime = 0f;
            minimumFrameTime = float.PositiveInfinity;
            cpuFrameTimeTotal = 0d;
            gpuFrameTimeTotal = 0d;
            maximumCpuFrameTime = 0d;
            maximumGpuFrameTime = 0d;
            cpuTimingSamples = 0;
            gpuTimingSamples = 0;
            ResetMarkerTimings();
        }

        private void StartMarkerRecorders()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            for (var i = 0; i < handles.Count; i++)
            {
                var description = ProfilerRecorderHandle.GetDescription(handles[i]);
                if (description.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds
                    || !IsDetailMarkerName(description.Name)
                    || ContainsMarkerRecorder(description.Name))
                    continue;
                AddMarkerRecorder(description.Category, description.Name);
                if (markerRecorders.Count >= MaximumDiscoveredRecorders)
                    break;
            }

            AddExplicitMarkerRecorders(ProfilerCategory.Scripts,
                "Animation.NpcCrowdSpring.TransformJob",
                "Animation.NpcCrowdSpring.Snapshot",
                "Animation.NpcCrowdSpring.ColliderSnapshot",
                "Animation.NpcCrowdSpring.Solve",
                "Animation.NpcCrowdSpring.Apply",
                "Physics.NpcCrowd.PrepareProbes",
                "Physics.NpcCrowd.Prepare.Recovery",
                "Physics.NpcCrowd.Prepare.Commands",
                  "Physics.NpcCrowd.Prepare.Motor",
                  "Physics.NpcCrowd.Prepare.ControllerCommand",
                  "Physics.NpcCrowd.Prepare.AgentSnapshot",
                "Physics.NpcCrowd.Prepare.ProbeCommands",
                "Physics.NpcCrowd.Presentation",
                "Physics.NpcCrowd.Presentation.Controller",
                "Physics.NpcCrowd.Presentation.Skill",
                "Physics.NpcCrowd.Presentation.Navigation",
                "Physics.NpcCrowd.Maintenance",
                "Physics.NpcCrowd.PathfindingBudget",
                "Physics.NpcCrowd.QueryAndSteeringWait",
                "Physics.NpcCrowd.ApplyProbeResults",
                "Physics.NpcCrowd.ResolvePenetration",
                "Physics.NpcCrowd.MovementJob",
                "Physics.NpcCrowd.ApplyMovementAndContacts");
            AddExplicitMarkerRecorders(ProfilerCategory.Physics,
                "FixedUpdate.PhysicsFixedUpdate",
                "Physics.PublishResultsStage",
                "Physics.SendEvents",
                "Physics.SendTriggerEvents");
            AddExplicitMarkerRecorders(ProfilerCategory.Scripts,
                "Physics.NpcCrowd.MovingPlatformFollow",
                "Physics.NpcCrowd.MovingPlatformBinding",
                "Physics.NpcCrowd.PrepareMovingPlatformPairs",
                "Network.NpcMovingPlatformSync",
                "Physics.MovingPlatform.FixedUpdate",
                "Physics.MovingPlatform.SamplePose",
                "Physics.MovingPlatform.ApplyPose",
                "Physics.MovingPlatform.NotifyCrowd");
        }

        private void AddExplicitMarkerRecorders(ProfilerCategory category, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
                if (!ContainsMarkerRecorder(names[i]))
                    AddMarkerRecorder(category, names[i]);
        }

        private void AddMarkerRecorder(ProfilerCategory category, string markerName)
        {
            var recorder = ProfilerRecorder.StartNew(category, markerName, 1);
            if (!recorder.Valid)
            {
                recorder.Dispose();
                return;
            }
            markerRecorders.Add(new MarkerRecorder { Name = markerName, Recorder = recorder });
        }

        private bool ContainsMarkerRecorder(string markerName)
        {
            for (var i = 0; i < markerRecorders.Count; i++)
                if (string.Equals(markerRecorders[i].Name, markerName, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool IsDetailMarkerName(string markerName)
        {
            if (DetailMarkerNames.Contains(markerName))
                return true;
            for (var i = 0; i < DetailMarkerFragments.Length; i++)
                if (markerName.IndexOf(DetailMarkerFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private void CaptureMarkerTimings()
        {
            for (var i = 0; i < markerRecorders.Count; i++)
            {
                var item = markerRecorders[i];
                var value = item.Recorder.LastValue;
                item.Samples++;
                if (value > 0)
                {
                    item.TotalNanoseconds += value;
                    item.MaximumNanoseconds = Math.Max(item.MaximumNanoseconds, value);
                }
            }
        }

        private void LogMarkerTimings()
        {
            markerRecorders.Sort((left, right) =>
                right.AverageMilliseconds.CompareTo(left.AverageMilliseconds));
            var builder = new StringBuilder(512);
            var count = 0;
            for (var i = 0; i < markerRecorders.Count && count < MaximumDetailMarkers; i++)
            {
                var item = markerRecorders[i];
                if (item.Samples == 0 || item.TotalNanoseconds == 0)
                    continue;
                if (count++ > 0)
                    builder.Append(' ');
                builder.Append(item.Name)
                    .Append('=')
                    .Append(item.AverageMilliseconds.ToString("F2"))
                    .Append("ms(max=")
                    .Append((item.MaximumNanoseconds / 1_000_000d).ToString("F2"))
                    .Append("ms)");
            }
            if (builder.Length > 0)
                Debug.Log($"[FrameRateDetail] role={GetNetworkRole()} {builder}");
        }

        private void ResetMarkerTimings()
        {
            for (var i = 0; i < markerRecorders.Count; i++)
            {
                markerRecorders[i].TotalNanoseconds = 0;
                markerRecorders[i].MaximumNanoseconds = 0;
                markerRecorders[i].Samples = 0;
            }
        }

        private void CaptureCpuGpuTiming()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, frameTimings) == 0)
                return;
            var timing = frameTimings[0];
            if (timing.cpuFrameTime > 0d)
            {
                cpuFrameTimeTotal += timing.cpuFrameTime;
                maximumCpuFrameTime = System.Math.Max(maximumCpuFrameTime, timing.cpuFrameTime);
                cpuTimingSamples++;
            }
            if (timing.gpuFrameTime > 0d)
            {
                gpuFrameTimeTotal += timing.gpuFrameTime;
                maximumGpuFrameTime = System.Math.Max(maximumGpuFrameTime, timing.gpuFrameTime);
                gpuTimingSamples++;
            }
        }

        private static string FormatAverageTiming(double value, int samples) =>
            samples > 0 ? $"{value / samples:F2}ms" : "n/a";

        private static string FormatMaximumTiming(double value, int samples) =>
            samples > 0 ? $"{value:F2}ms" : "n/a";

        private static string GetNetworkRole()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
                return "Offline";
            if (manager.IsHost)
                return "Host";
            return manager.IsServer ? "Server" : "Client";
        }

        private void OnDestroy()
        {
            for (var i = 0; i < markerRecorders.Count; i++)
                markerRecorders[i].Dispose();
            markerRecorders.Clear();
            if (instance == this)
                instance = null;
        }
    }
}
#endif
