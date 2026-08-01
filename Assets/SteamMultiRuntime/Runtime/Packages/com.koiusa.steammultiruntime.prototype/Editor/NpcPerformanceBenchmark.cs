using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    public static class NpcPerformanceBenchmark
    {
        private const string ScenePath = "Assets/SteamMultiRuntime/Samples/Gameplay/Stages/ServerScene.unity";
        private const string LocalNpcPrefabPath = "Assets/SteamMultiRuntime/Runtime/Prefabs/Character/LocalNPC.prefab";
        private static readonly int[] Counts = { 100, 300 };
        private const int WarmupFrames = 180;
        private const int SampleFrames = 300;

        private static int runIndex;
        private static int frame;
        private static double mainThreadNanoseconds;
        private static double renderThreadNanoseconds;
        private static double gpuFrameNanoseconds;
        private static long gcBytes;
        private static long drawCalls;
        private static readonly List<float> frameTimesMs = new(SampleFrames);
        private static ProfilerRecorder mainThreadRecorder;
        private static ProfilerRecorder renderThreadRecorder;
        private static ProfilerRecorder gpuFrameTimeRecorder;
        private static ProfilerRecorder gcRecorder;
        private static ProfilerRecorder drawCallRecorder;
        private static readonly List<NamedRecorder> subsystemRecorders = new();
        private static bool useCrowdSimulation = true;

        private sealed class NamedRecorder
        {
            public string Name;
            public ProfilerRecorder Recorder;
            public double Total;
        }

        public static void Run200Vs300()
        {
            runIndex = 0;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            StartRun();
        }

        public static void Run300Only()
        {
            runIndex = 1;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            StartRun();
        }

        public static void RunCrowdComparisonFromCommandLine()
        {
            useCrowdSimulation = ReadIntArgument("-npcBenchmarkCrowd", 1) != 0;
            Run200Vs300();
        }

        private static void StartRun()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var spawner = UnityEngine.Object.FindFirstObjectByType<NetworkNpcRandomSpawnManager>();
            if (spawner == null)
            {
                Fail("NPC spawner was not found.");
                return;
            }

            var serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("spawnCount").intValue = Counts[runIndex];
            serializedSpawner.FindProperty("showNpcDestinationMarkers").boolValue = false;
            serializedSpawner.FindProperty("showCharacterDebugUi").boolValue = false;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalNpcPrefabPath);
            var controller = prefab != null ? prefab.GetComponent<NpcNavMeshController>() : null;
            if (controller == null)
            {
                Fail("LocalNPC NpcNavMeshController was not found.");
                return;
            }
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("useCrowdSimulation").boolValue = useCrowdSimulation;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                frame = 0;
                mainThreadNanoseconds = 0;
                renderThreadNanoseconds = 0;
                gpuFrameNanoseconds = 0;
                gcBytes = 0;
                drawCalls = 0;
                frameTimesMs.Clear();
                mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
                renderThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
                gpuFrameTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1);
                gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                drawCallRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
                StartSubsystemRecorders();
                EditorApplication.update += Sample;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                runIndex++;
                if (runIndex < Counts.Length)
                    StartRun();
                else
                    Finish();
            }
        }

        private static void Sample()
        {
            frame++;
            if (frame <= WarmupFrames)
                return;

            mainThreadNanoseconds += mainThreadRecorder.LastValue;
            renderThreadNanoseconds += renderThreadRecorder.LastValue;
            gpuFrameNanoseconds += gpuFrameTimeRecorder.LastValue;
            frameTimesMs.Add(Time.unscaledDeltaTime * 1000f);
            gcBytes += gcRecorder.LastValue;
            drawCalls += drawCallRecorder.LastValue;
            for (var i = 0; i < subsystemRecorders.Count; i++)
                subsystemRecorders[i].Total += subsystemRecorders[i].Recorder.LastValue;
            if (frame < WarmupFrames + SampleFrames)
                return;

            var npcCount = UnityEngine.Object.FindObjectsByType<NpcNavMeshController>(FindObjectsSortMode.None).Length;
            frameTimesMs.Sort();
            var averageFrameMs = frameTimesMs.Count > 0 ? frameTimesMs.Average() : 0d;
            var p95FrameMs = frameTimesMs.Count > 0
                ? frameTimesMs[Mathf.Clamp(Mathf.CeilToInt(frameTimesMs.Count * 0.95f) - 1, 0, frameTimesMs.Count - 1)]
                : 0f;
            Debug.Log(
                $"[NpcBenchmark] crowd={(useCrowdSimulation ? 1 : 0)} requested={Counts[runIndex]} actual={npcCount} " +
                $"mainThreadMs={mainThreadNanoseconds / SampleFrames / 1_000_000d:F3} " +
                $"renderThreadMs={renderThreadNanoseconds / SampleFrames / 1_000_000d:F3} " +
                $"gpuFrameMs={gpuFrameNanoseconds / SampleFrames / 1_000_000d:F3} " +
                $"frameMs={averageFrameMs:F3} p95FrameMs={p95FrameMs:F3} " +
                $"fps={(averageFrameMs > 0d ? 1000d / averageFrameMs : 0d):F1} " +
                $"gcBytesPerFrame={(double)gcBytes / SampleFrames:F1} " +
                $"drawCalls={(double)drawCalls / SampleFrames:F1}");

            EditorApplication.update -= Sample;
            mainThreadRecorder.Dispose();
            renderThreadRecorder.Dispose();
            gpuFrameTimeRecorder.Dispose();
            gcRecorder.Dispose();
            drawCallRecorder.Dispose();
            LogAndDisposeSubsystemRecorders();
            EditorApplication.ExitPlaymode();
        }

        private static void StartSubsystemRecorders()
        {
            subsystemRecorders.Clear();
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            for (var i = 0; i < handles.Count; i++)
            {
                var description = ProfilerRecorderHandle.GetDescription(handles[i]);
                if (description.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds)
                    continue;
                var name = description.Name;
                if (!ContainsAny(name, "Physics", "NavMesh", "Animation", "Animator", "Behaviour", "UIElements",
                        "Render", "Gfx", "GPU", "Present", "Wait", "Skin", "Shadow", "Camera", "Batch"))
                    continue;
                var recorder = ProfilerRecorder.StartNew(description.Category, name, 1);
                if (recorder.Valid)
                    subsystemRecorders.Add(new NamedRecorder { Name = name, Recorder = recorder });
            }
        }

        private static bool ContainsAny(string value, params string[] fragments)
        {
            for (var i = 0; i < fragments.Length; i++)
                if (value.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static void LogAndDisposeSubsystemRecorders()
        {
            var top = subsystemRecorders
                .Where(item => item.Total > 0d)
                .OrderByDescending(item => item.Total)
                .Take(20);
            foreach (var item in top)
                Debug.Log($"[NpcBenchmarkDetail] crowd={(useCrowdSimulation ? 1 : 0)} requested={Counts[runIndex]} marker={item.Name} ms={item.Total / SampleFrames / 1_000_000d:F3}");
            for (var i = 0; i < subsystemRecorders.Count; i++)
                subsystemRecorders[i].Recorder.Dispose();
            subsystemRecorders.Clear();
        }

        private static void Finish()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("[NpcBenchmark] complete");
            EditorApplication.Exit(0);
        }

        private static int ReadIntArgument(string name, int fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[i + 1], out var value))
                    return value;
            }
            return fallback;
        }

        private static void Fail(string message)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= Sample;
            Debug.LogError($"[NpcBenchmark] {message}");
            EditorApplication.Exit(1);
        }
    }
}
