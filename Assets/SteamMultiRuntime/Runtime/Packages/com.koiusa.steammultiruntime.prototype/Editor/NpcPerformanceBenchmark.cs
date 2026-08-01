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
        private const string NetworkNpcPrefabPath = "Assets/SteamMultiRuntime/Runtime/Prefabs/Character/NetworkNPC.prefab";
        private const string NetworkManagerResourcePath = "System/NetworkManager";
        private static readonly int[] Counts = { 100, 200, 300 };
        private const int WarmupFrames = 180;
        private const int SampleFrames = 300;
        private const int RandomSeed = 481516;

        private static int runIndex;
        private static int frame;
        private static double mainThreadNanoseconds;
        private static double renderThreadNanoseconds;
        private static double gpuFrameNanoseconds;
        private static long gcBytes;
        private static long drawCalls;
        private static double fixedTimeAtSampleStart;
        private static readonly List<float> frameTimesMs = new(SampleFrames);
        private static ProfilerRecorder mainThreadRecorder;
        private static ProfilerRecorder renderThreadRecorder;
        private static ProfilerRecorder gpuFrameTimeRecorder;
        private static ProfilerRecorder gcRecorder;
        private static ProfilerRecorder drawCallRecorder;
        private static readonly List<NamedRecorder> subsystemRecorders = new();
        private static bool useCrowdSimulation = true;
        private static bool usePreCrowdPrefabBaseline;
        private static bool useNetworkNpc;
        private static bool recordSubsystems = true;
        private static bool useLegacyEnvironment;
        private static bool diagnoseSpringStages;
        private static bool disableSpringColliders;
        private static readonly HashSet<string> PostCrowdPrefabFeatureNames = new()
        {
            nameof(NpcCrowdTraversalTestDriver),
            "WallTraversalFeature",
            "WallRunAction",
            "WallSlideAction",
            "WallJumpAction",
            "LadderTraversalFeature",
            "LadderClimbAction",
            "LadderDetachAction",
            "WireLineVisualFeature",
            "WireTraversalFeature",
            "WireGrappleTargetingFeature",
            "WireAttachAction",
            "WireSwingAction",
            "WireReelAction",
            "WireGroundAction"
        };

        private sealed class NamedRecorder
        {
            public string Name;
            public ProfilerRecorder Recorder;
            public double Total;
        }

        public static void Run100200300()
        {
            runIndex = 0;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            StartRun();
        }

        // Keep the former entry point for existing command lines and editor tooling.
        public static void Run200Vs300() => Run100200300();

        public static void Run300Only()
        {
            runIndex = Array.IndexOf(Counts, 300);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            StartRun();
        }

        public static void RunCrowdComparisonFromCommandLine()
        {
            useCrowdSimulation = ReadIntArgument("-npcBenchmarkCrowd", 1) != 0;
            usePreCrowdPrefabBaseline = ReadIntArgument("-npcBenchmarkPreCrowdPrefab", 0) != 0;
            useNetworkNpc = ReadIntArgument("-npcBenchmarkNetwork", 0) != 0;
            recordSubsystems = ReadIntArgument("-npcBenchmarkSubsystems", 1) != 0;
            useLegacyEnvironment = ReadIntArgument("-npcBenchmarkLegacyEnvironment", 0) != 0;
            diagnoseSpringStages = ReadIntArgument("-npcBenchmarkSpringStages", 0) != 0;
            disableSpringColliders = ReadIntArgument("-npcBenchmarkSpringColliders", 1) == 0;
            NpcCrowdSpringSimulation.DiagnosticCompleteStages = diagnoseSpringStages;
            NpcCrowdSpringSimulation.DiagnosticDisableColliders = disableSpringColliders;
            Run100200300();
        }

        private static void StartRun()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (useLegacyEnvironment)
                DisableTraversalTestEnvironment();
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
            serializedSpawner.FindProperty("spawnOnStart").boolValue = false;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

            var prefabPath = useNetworkNpc ? NetworkNpcPrefabPath : LocalNpcPrefabPath;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var controller = prefab != null ? prefab.GetComponent<NpcNavMeshController>() : null;
            if (controller == null)
            {
                Fail($"NpcNavMeshController was not found on {prefabPath}.");
                return;
            }
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("useCrowdSimulation").boolValue = useCrowdSimulation;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Random.InitState(RandomSeed);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (useNetworkNpc && !StartBenchmarkServer())
                    return;
                var spawner = UnityEngine.Object.FindFirstObjectByType<NetworkNpcRandomSpawnManager>();
                if (spawner == null)
                {
                    Fail("NPC spawner disappeared before benchmark spawn.");
                    return;
                }
                spawner.SetDeterministicRandomSeed(RandomSeed);
                spawner.SpawnNow();
                if (usePreCrowdPrefabBaseline)
                    DisablePostCrowdPrefabFeatures();
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
                if (recordSubsystems)
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
            {
                if (frame == WarmupFrames)
                    fixedTimeAtSampleStart = Time.fixedTimeAsDouble;
                return;
            }

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
            var controllers = UnityEngine.Object.FindObjectsByType<NpcNavMeshController>(FindObjectsSortMode.None);
            var networkNpcCount = CountNetworkNpcs();
            if (useNetworkNpc && networkNpcCount != Counts[runIndex])
            {
                Fail($"Expected {Counts[runIndex]} NetworkNPCs, but found {networkNpcCount} (all NPCs: {npcCount}).");
                return;
            }
            frameTimesMs.Sort();
            var averageFrameMs = frameTimesMs.Count > 0 ? frameTimesMs.Average() : 0d;
            var p95FrameMs = frameTimesMs.Count > 0
                ? frameTimesMs[Mathf.Clamp(Mathf.CeilToInt(frameTimesMs.Count * 0.95f) - 1, 0, frameTimesMs.Count - 1)]
                : 0f;
            Debug.Log(
                $"[NpcBenchmark] crowd={(useCrowdSimulation ? 1 : 0)} network={(useNetworkNpc ? 1 : 0)} " +
                $"preCrowdPrefab={(usePreCrowdPrefabBaseline ? 1 : 0)} legacyEnvironment={(useLegacyEnvironment ? 1 : 0)} " +
                $"requested={Counts[runIndex]} " +
                $"actual={npcCount} networkActual={networkNpcCount} " +
                $"mainThreadMs={mainThreadNanoseconds / SampleFrames / 1_000_000d:F3} " +
                $"renderThreadMs={renderThreadNanoseconds / SampleFrames / 1_000_000d:F3} " +
                $"gpuFrameMs={gpuFrameNanoseconds / SampleFrames / 1_000_000d:F3} " +
                $"frameMs={averageFrameMs:F3} p95FrameMs={p95FrameMs:F3} " +
                $"fps={(averageFrameMs > 0d ? 1000d / averageFrameMs : 0d):F1} " +
                $"fixedStepsPerFrame={(Time.fixedTimeAsDouble - fixedTimeAtSampleStart) / Time.fixedDeltaTime / SampleFrames:F2} " +
                $"gcBytesPerFrame={(double)gcBytes / SampleFrames:F1} " +
                $"drawCalls={(double)drawCalls / SampleFrames:F1}");
            LogMovementState(controllers);

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
            AddExplicitRecorder(ProfilerCategory.Scripts, "Animation.NpcCrowdSpring.TransformJob");
            AddExplicitRecorder(ProfilerCategory.Scripts, "Animation.NpcCrowdSpring.Snapshot");
            AddExplicitRecorder(ProfilerCategory.Scripts, "Animation.NpcCrowdSpring.ColliderSnapshot");
            AddExplicitRecorder(ProfilerCategory.Scripts, "Animation.NpcCrowdSpring.Solve");
            AddExplicitRecorder(ProfilerCategory.Scripts, "Animation.NpcCrowdSpring.Apply");
        }

        private static void AddExplicitRecorder(ProfilerCategory category, string name)
        {
            for (var i = 0; i < subsystemRecorders.Count; i++)
                if (string.Equals(subsystemRecorders[i].Name, name, StringComparison.Ordinal))
                    return;
            var recorder = ProfilerRecorder.StartNew(category, name, 1);
            if (recorder.Valid)
                subsystemRecorders.Add(new NamedRecorder { Name = name, Recorder = recorder });
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

        private static void DisablePostCrowdPrefabFeatures()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<NpcNavMeshController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < controllers.Length; i++)
            {
                var behaviours = controllers[i].GetComponents<Behaviour>();
                for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                {
                    var behaviour = behaviours[behaviourIndex];
                    if (behaviour != null && PostCrowdPrefabFeatureNames.Contains(behaviour.GetType().Name))
                        behaviour.enabled = false;
                }
            }
        }

        private static void DisableTraversalTestEnvironment()
        {
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != "Env")
                    continue;
                roots[i].SetActive(false);
                return;
            }
        }

        private static bool StartBenchmarkServer()
        {
            var prefab = Resources.Load<GameObject>(NetworkManagerResourcePath);
            if (prefab == null)
            {
                Fail($"NetworkManager resource '{NetworkManagerResourcePath}' was not found.");
                return false;
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "NetworkManager (NPC Benchmark)";
            var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                    continue;
                var fullName = behaviour.GetType().FullName;
                if (fullName != "Unity.Netcode.NetworkManager"
                    && fullName != "Netcode.Transports.Facepunch.FacepunchTransport")
                    behaviour.enabled = false;
            }
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().FullName != "Unity.Netcode.NetworkManager")
                    continue;
                var startServer = behaviour.GetType().GetMethod("StartServer", Type.EmptyTypes);
                if (startServer != null && startServer.Invoke(behaviour, null) is true)
                    return true;
                Fail("NetworkManager.StartServer() failed.");
                return false;
            }

            Fail("Unity.Netcode.NetworkManager component was not found on the resource prefab.");
            return false;
        }

        private static int CountNetworkNpcs()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<NpcNavMeshController>(FindObjectsSortMode.None);
            var count = 0;
            for (var i = 0; i < controllers.Length; i++)
            {
                var behaviours = controllers[i].GetComponents<MonoBehaviour>();
                for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                {
                    var behaviour = behaviours[behaviourIndex];
                    if (behaviour != null && behaviour.GetType().Name == "ServerDrivenActorController")
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        private static void LogMovementState(NpcNavMeshController[] controllers)
        {
            var onNavMesh = 0;
            var pathPending = 0;
            var hasPath = 0;
            var hasGoal = 0;
            var hasSteering = 0;
            var moving = 0;
            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller.DiagnosticIsOnNavMesh) onNavMesh++;
                if (controller.DiagnosticPathPending) pathPending++;
                if (controller.HasPath) hasPath++;
                if (controller.DiagnosticHasGoalVelocity) hasGoal++;
                if (controller.DiagnosticHasSteeringVelocity) hasSteering++;
                if (controller.IsMoving) moving++;
            }

            Debug.Log(
                $"[NpcBenchmarkMovement] crowd={(useCrowdSimulation ? 1 : 0)} requested={Counts[runIndex]} " +
                $"total={controllers.Length} onNavMesh={onNavMesh} pathPending={pathPending} " +
                $"hasPath={hasPath} hasGoal={hasGoal} hasSteering={hasSteering} moving={moving}");
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
