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
        private static readonly int[] Counts = { 200, 300 };
        private const int WarmupFrames = 180;
        private const int SampleFrames = 300;

        private static int runIndex;
        private static int frame;
        private static double mainThreadNanoseconds;
        private static long gcBytes;
        private static long drawCalls;
        private static ProfilerRecorder mainThreadRecorder;
        private static ProfilerRecorder gcRecorder;
        private static ProfilerRecorder drawCallRecorder;
        private static readonly List<NamedRecorder> subsystemRecorders = new();

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
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                frame = 0;
                mainThreadNanoseconds = 0;
                gcBytes = 0;
                drawCalls = 0;
                mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
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
            gcBytes += gcRecorder.LastValue;
            drawCalls += drawCallRecorder.LastValue;
            for (var i = 0; i < subsystemRecorders.Count; i++)
                subsystemRecorders[i].Total += subsystemRecorders[i].Recorder.LastValue;
            if (frame < WarmupFrames + SampleFrames)
                return;

            var npcCount = UnityEngine.Object.FindObjectsByType<NpcNavMeshController>(FindObjectsSortMode.None).Length;
            Debug.Log(
                $"[NpcBenchmark] requested={Counts[runIndex]} actual={npcCount} " +
                $"mainThreadMs={mainThreadNanoseconds / SampleFrames / 1_000_000d:F3} " +
                $"gcBytesPerFrame={(double)gcBytes / SampleFrames:F1} " +
                $"drawCalls={(double)drawCalls / SampleFrames:F1}");

            EditorApplication.update -= Sample;
            mainThreadRecorder.Dispose();
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
                if (!ContainsAny(name, "Physics", "NavMesh", "Animation", "Animator", "Behaviour", "UIElements", "Render"))
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
                Debug.Log($"[NpcBenchmarkDetail] requested={Counts[runIndex]} marker={item.Name} ms={item.Total / SampleFrames / 1_000_000d:F3}");
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

        private static void Fail(string message)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= Sample;
            Debug.LogError($"[NpcBenchmark] {message}");
            EditorApplication.Exit(1);
        }
    }
}
