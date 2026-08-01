#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Samples every rendered frame and emits one aggregate line per second.</summary>
    [DisallowMultipleComponent]
    internal sealed class RuntimeFrameRateLogger : MonoBehaviour
    {
        private const float LogInterval = 1f;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null)
                return;
            var host = new GameObject("RuntimeFrameRateLogger");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            instance = host.AddComponent<RuntimeFrameRateLogger>();
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
    }
}
#endif
