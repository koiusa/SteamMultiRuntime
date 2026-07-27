using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Koiusa.SteamMultiRuntime.Network;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class NetworkNpcRandomSpawnManager : MonoBehaviour
    {
        private struct NpcSpawnSceneData : INetworkSerializable
        {
            public int SceneBuildIndex;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref SceneBuildIndex);
            }
        }

        private sealed class NpcScenePrefabHandler : NetworkPrefabInstanceHandlerWithData<NpcSpawnSceneData>
        {
            private readonly GameObject prefab;

            public NpcScenePrefabHandler(GameObject prefab)
            {
                this.prefab = prefab;
            }

            public override NetworkObject Instantiate(
                ulong ownerClientId,
                Vector3 position,
                Quaternion rotation,
                NpcSpawnSceneData instantiationData)
            {
                var anchor = FindSpawnAnchor(instantiationData.SceneBuildIndex, prefab);
                if (anchor == null)
                {
                    Debug.LogError(
                        $"[NetworkNpcRandomSpawnManager] Scene buildIndex={instantiationData.SceneBuildIndex} " +
                        "のNPC生成先が見つかりません。クライアントのScene LoadComplete前にSpawnされています。");
                    return null;
                }

                var instance = InstantiateInScene(prefab, anchor.transform, position, rotation);
                anchor.ConfigureDebugDisplay(instance);
                return instance.GetComponent<NetworkObject>();
            }

            public override void Destroy(NetworkObject networkObject)
            {
                if (networkObject != null)
                {
                    Object.Destroy(networkObject.gameObject);
                }
            }
        }

        private static readonly List<NetworkNpcRandomSpawnManager> SpawnAnchors = new();

        private readonly SpawnedObjectCollection spawnedNpcs = new();

        [Header("NPC Prefab")]
        [SerializeField] private GameObject networkNpcPrefab;
        [SerializeField] private GameObject localNpcPrefab;

        [Header("Model")]
        [SerializeField] private CharacterModelIdList npcModelIdList;
        [SerializeField] private bool randomizeModelOnSpawn = true;

        [Header("Spawn")]
        [SerializeField, Min(1)] private int spawnCount = 5;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool oneShot = true;
        [SerializeField] private float spawnHeightOffset = 0.05f;

        [Header("Area")]
        [SerializeField] private Transform areaCenter;
        [SerializeField] private Vector3 areaSize = new Vector3(20f, 0f, 20f);
        [SerializeField, Min(0f)] private float minSpawnDistance = 1.5f;

        [Header("NavMesh")]
        [SerializeField] private bool sampleOnNavMesh = true;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 6f;
        [SerializeField, Min(1)] private int maxAttemptsPerNpc = 8;
        [SerializeField] private bool fallbackToAnyAgentType = true;
        [SerializeField, Min(0.1f)] private float fallbackNavMeshSampleRadius = 24f;
        [SerializeField, Min(1)] private int fallbackMaxAttemptsPerNpc = 12;

        [Header("Debug")]
        [SerializeField] private bool showNpcDestinationMarkers;
        [SerializeField] private bool showCharacterDebugUi;

        private bool hasSpawned;
        private bool hasSubscribedServerStarted;
        private bool isWaitingForActiveScene;
        private bool isWaitingForNavMeshUpdate;
        private bool isWaitingForNetworkSceneLoad;
        private NetworkSceneManager subscribedSceneManager;
        private Transform debugDisplayRoot;
        private readonly CharacterDebugDisplayState characterDebugDisplayState = new();

        private void Awake()
        {
            characterDebugDisplayState.IsVisible = showCharacterDebugUi;
            EnsureDebugDisplayRoot();
            if (!SpawnAnchors.Contains(this))
            {
                SpawnAnchors.Add(this);
            }

            // SceneのLoadComplete直後にSpawnを受信しても間に合うよう、可能ならAwakeで登録する。
            RegisterNetworkPrefabHandler();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void Start()
        {
            RegisterNetworkPrefabHandler();

            if (!spawnOnStart)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                WaitForNavMeshUpdate();
                return;
            }

            if (!networkManager.IsListening)
            {
                SubscribeServerStarted(networkManager);
                return;
            }

            if (!networkManager.IsServer)
            {
                enabled = false;
                return;
            }

            SubscribeNetworkSceneEvents(networkManager);

            // Network開始前からロード済みのSceneにはLoadEventCompletedが来ない。
            if (IsOwnSceneActive())
            {
                WaitForNavMeshUpdate();
            }
            else
            {
                isWaitingForNetworkSceneLoad = true;
            }
        }

        private void OnDestroy()
        {
            spawnedNpcs.DestroyAll();
            SpawnAnchors.Remove(this);
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            NavMesh.onPreUpdate -= OnNavMeshPreUpdate;
            UnsubscribeNetworkSceneEvents();
            UnsubscribeServerStarted();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (previous == gameObject.scene && next != previous)
            {
                spawnedNpcs.DestroyAll();
            }

            if (!isWaitingForActiveScene || !IsOwnSceneActive())
            {
                return;
            }

            isWaitingForActiveScene = false;
            WaitForNavMeshUpdate();
        }

        private void WaitForNavMeshUpdate()
        {
            if (isWaitingForNavMeshUpdate)
            {
                return;
            }

            isWaitingForNavMeshUpdate = true;
            NavMesh.onPreUpdate += OnNavMeshPreUpdate;
        }

        private void OnNavMeshPreUpdate()
        {
            NavMesh.onPreUpdate -= OnNavMeshPreUpdate;
            isWaitingForNavMeshUpdate = false;
            TrySpawnOrSubscribe();
        }

        private bool IsOwnSceneActive()
        {
            return gameObject.scene == SceneManager.GetActiveScene();
        }

        public void SpawnNow()
        {
            TrySpawnOrSubscribe();
        }

        public void SetNpcDestinationMarkersVisible(bool visible)
        {
            showNpcDestinationMarkers = visible;
            EnsureDebugDisplayRoot().gameObject.SetActive(visible);
        }

        public void SetCharacterDebugUiVisible(bool visible)
        {
            showCharacterDebugUi = visible;
            characterDebugDisplayState.IsVisible = visible;
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                SetNpcDestinationMarkersVisible(showNpcDestinationMarkers);
                SetCharacterDebugUiVisible(showCharacterDebugUi);
            }
        }

        private void TrySpawnOrSubscribe()
        {
            if (oneShot && hasSpawned)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;

            // NavMesh はアクティブシーンに紐付くため、自シーンがアクティブになるまで待つ
            // Local/Network を問わず共通のガード
            if (!IsOwnSceneActive())
            {
                if (!isWaitingForActiveScene)
                {
                    isWaitingForActiveScene = true;
                }
                return;
            }

            if (networkManager == null || networkManager.IsServer)
            {
                SpawnInternal();
                return;
            }

            if (!hasSubscribedServerStarted)
            {
                networkManager.OnServerStarted += OnServerStarted;
                hasSubscribedServerStarted = true;
            }

        }

        private void OnServerStarted()
        {
            UnsubscribeServerStarted();

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            SubscribeNetworkSceneEvents(networkManager);
            if (IsOwnSceneActive())
            {
                WaitForNavMeshUpdate();
            }
            else
            {
                isWaitingForNetworkSceneLoad = true;
            }
        }

        private void SubscribeServerStarted(NetworkManager networkManager)
        {
            if (hasSubscribedServerStarted)
            {
                return;
            }

            networkManager.OnServerStarted += OnServerStarted;
            hasSubscribedServerStarted = true;
        }

        private void SubscribeNetworkSceneEvents(NetworkManager networkManager)
        {
            if (subscribedSceneManager == networkManager.SceneManager || networkManager.SceneManager == null)
            {
                return;
            }

            UnsubscribeNetworkSceneEvents();
            subscribedSceneManager = networkManager.SceneManager;
            subscribedSceneManager.OnSceneEvent += OnNetworkSceneEvent;
        }

        private void UnsubscribeNetworkSceneEvents()
        {
            if (subscribedSceneManager == null)
            {
                return;
            }

            subscribedSceneManager.OnSceneEvent -= OnNetworkSceneEvent;
            subscribedSceneManager = null;
        }

        private void OnNetworkSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted
                || sceneEvent.SceneName != gameObject.scene.name)
            {
                return;
            }

            UnsubscribeNetworkSceneEvents();
            isWaitingForNetworkSceneLoad = false;
            WaitForNavMeshUpdate();
        }

        private void SpawnInternal()
        {
            if (oneShot && hasSpawned)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            var useNetworkSpawn = networkManager != null && networkManager.IsListening;
            var prefab = SelectNpcPrefab(useNetworkSpawn);
            if (prefab == null)
            {
                Debug.LogError(useNetworkSpawn
                    ? "[NetworkNpcRandomSpawnManager] networkNpcPrefab is not assigned."
                    : "[NetworkNpcRandomSpawnManager] localNpcPrefab is not assigned.", this);
                return;
            }

            var prefabAgent = prefab.GetComponentInChildren<NavMeshAgent>(true);
            var sampleAgentTypeId = prefabAgent != null ? prefabAgent.agentTypeID : 0;
            var sampleAreaMask = prefabAgent != null ? prefabAgent.areaMask : NavMesh.AllAreas;

            // prefab の agentTypeID に対応する NavMesh 面が存在するか事前確認
            // agentTypeID 不一致の場合 "Failed to create agent" が発生する
            if (prefabAgent != null && sampleOnNavMesh)
            {
                var center = areaCenter != null ? areaCenter.position : transform.position;
                var check = new NavMeshQueryFilter { agentTypeID = sampleAgentTypeId, areaMask = sampleAreaMask };
                if (!NavMesh.SamplePosition(center, out _, fallbackNavMeshSampleRadius, check))
                {
                    Debug.LogWarning(
                        $"[NetworkNpcRandomSpawnManager] agentTypeID={sampleAgentTypeId} に対応する NavMesh 面が見つかりません。" +
                        "Prefab の NavMeshAgent.AgentType と NavMesh のベイク設定が一致しているか確認してください。",
                        this);
                }
            }

            var spawnedCount = 0;
            var usedSpawnPositions = new List<Vector3>(spawnCount);
            for (var i = 0; i < spawnCount; i++)
            {
                if (!TryGetSpawnPosition(sampleAgentTypeId, sampleAreaMask, usedSpawnPositions, out var spawnPosition))
                {
                    continue;
                }

                var spawnUp = Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
                var finalSpawnPosition = prefabAgent != null
                    ? spawnPosition
                    : spawnPosition + spawnUp * spawnHeightOffset;

                var spawnerScene = gameObject.scene;
                var instance = InstantiateInScene(prefab, transform, finalSpawnPosition, Quaternion.identity);
                ConfigureDebugDisplay(instance);

                var networkObject = instance.GetComponent<NetworkObject>();

                if (networkObject != null && useNetworkSpawn)
                {
                    // OnNetworkSpawn がプレイヤー用の既定モデルを解決する前に、
                    // NPC 固有のモデル情報を確定させる。
                    PrepareNetworkModelSync(instance);
                    if (!networkObject.IsSpawned)
                    {
                        networkManager.PrefabHandler.SetInstantiationData(
                            networkObject,
                            new NpcSpawnSceneData { SceneBuildIndex = spawnerScene.buildIndex });
                        networkObject.Spawn(destroyWithScene: true);
                    }

                    ApplyNetworkModelSelection(instance);
                }
                else if (networkObject == null && useNetworkSpawn)
                {
                    Debug.LogWarning("[NetworkNpcRandomSpawnManager] Spawned network NPC prefab does not have NetworkObject. Spawned as local instance.", instance);
                }

                if (!useNetworkSpawn)
                {
                    ApplyLocalModelSync(instance);
                }

                spawnedNpcs.Add(instance);
                usedSpawnPositions.Add(spawnPosition);
                spawnedCount++;
            }

            if (spawnedCount > 0)
            {
                hasSpawned = true;
                return;
            }

            Debug.LogWarning($"[NetworkNpcRandomSpawnManager] NPC spawn failed (no valid spawn point). agentTypeId={sampleAgentTypeId}, sampleRadius={navMeshSampleRadius}, fallback={fallbackToAnyAgentType}", this);
        }

        private GameObject SelectNpcPrefab(bool useNetworkSpawn)
        {
            return useNetworkSpawn ? networkNpcPrefab : localNpcPrefab;
        }

        private void ConfigureDebugDisplay(GameObject instance)
        {
            var markerParent = EnsureDebugDisplayRoot();
            var markers = instance.GetComponentsInChildren<NpcDestinationDebugMarker>(includeInactive: true);
            for (var i = 0; i < markers.Length; i++)
            {
                markers[i].SetMarkerParent(markerParent);
            }

            var scope = instance.GetComponent<CharacterDebugDisplayScope>();
            if (scope == null)
            {
                scope = instance.AddComponent<CharacterDebugDisplayScope>();
            }

            scope.Bind(characterDebugDisplayState);

            var overlay = instance.GetComponent<CharacterDebugOverlay>();
            if (overlay == null)
            {
                overlay = instance.AddComponent<CharacterDebugOverlay>();
            }

            scope.SetOwnerOverlay(overlay);
            overlay.ResolveReferences();
        }

        private Transform EnsureDebugDisplayRoot()
        {
            if (debugDisplayRoot != null)
            {
                return debugDisplayRoot;
            }

            var root = new GameObject("NpcDebugDisplay");
            root.transform.SetParent(transform, worldPositionStays: false);
            root.SetActive(showNpcDestinationMarkers);
            debugDisplayRoot = root.transform;
            return debugDisplayRoot;
        }

        private void RegisterNetworkPrefabHandler()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkNpcPrefab == null)
            {
                return;
            }

            networkManager.PrefabHandler.AddHandler(
                networkNpcPrefab,
                new NpcScenePrefabHandler(networkNpcPrefab));
        }

        private static NetworkNpcRandomSpawnManager FindSpawnAnchor(int sceneBuildIndex, GameObject prefab)
        {
            for (var i = SpawnAnchors.Count - 1; i >= 0; i--)
            {
                var anchor = SpawnAnchors[i];
                if (anchor == null)
                {
                    SpawnAnchors.RemoveAt(i);
                    continue;
                }

                if (anchor.gameObject.scene.buildIndex != sceneBuildIndex
                    || anchor.networkNpcPrefab != prefab)
                {
                    continue;
                }

                return anchor;
            }

            return null;
        }

        private static GameObject InstantiateInScene(
            GameObject prefab,
            Transform sceneAnchor,
            Vector3 position,
            Quaternion rotation)
        {
            // 位置・回転・親を同時指定し、Awakeより前に正しい初期位置を確定する。
            // 親のSceneへ直接生成されるため、Active Sceneにも依存しない。
            var instance = Instantiate(prefab, position, rotation, sceneAnchor);
            instance.transform.SetParent(null, true);
            return instance;
        }

        private void PrepareNetworkModelSync(GameObject instance)
        {
            if (npcModelIdList == null)
            {
                return;
            }

            var networkModelSync = instance.GetComponent<NetworkPlayerModelSync>();
            if (networkModelSync == null)
            {
                return;
            }

            networkModelSync.modelIdList = npcModelIdList;
        }

        private void ApplyNetworkModelSelection(GameObject instance)
        {
            if (!randomizeModelOnSpawn || npcModelIdList == null || npcModelIdList.modelIds == null || npcModelIdList.modelIds.Length == 0)
            {
                return;
            }

            var networkModelSync = instance.GetComponent<NetworkPlayerModelSync>();
            if (networkModelSync == null || !networkModelSync.IsSpawned || !networkModelSync.IsServer)
            {
                return;
            }

            networkModelSync.SelectedModelIndex.Value = Random.Range(0, npcModelIdList.modelIds.Length);
        }

        private void ApplyLocalModelSync(GameObject instance)
        {
            if (npcModelIdList == null)
            {
                return;
            }

            var localModelSync = instance.GetComponent<LocalPlayerModelSync>();
            if (localModelSync == null)
            {
                return;
            }

            localModelSync.modelIdList = npcModelIdList;
            if (randomizeModelOnSpawn && npcModelIdList.modelIds != null && npcModelIdList.modelIds.Length > 0)
            {
                localModelSync.ApplyModelIndex(Random.Range(0, npcModelIdList.modelIds.Length));
            }
        }

        private bool TryGetSpawnPosition(int agentTypeId, int areaMask, List<Vector3> usedPositions, out Vector3 spawnPosition)
        {
            var center = areaCenter != null ? areaCenter.position : transform.position;

            if (TryGetSpawnPositionInternal(center, navMeshSampleRadius, maxAttemptsPerNpc, true, agentTypeId, areaMask, usedPositions, out spawnPosition))
            {
                return true;
            }

            if (!fallbackToAnyAgentType)
            {
                spawnPosition = center;
                return false;
            }

            return TryGetSpawnPositionInternal(center, fallbackNavMeshSampleRadius, fallbackMaxAttemptsPerNpc, false, 0, NavMesh.AllAreas, usedPositions, out spawnPosition);
        }

        private bool TryGetSpawnPositionInternal(
            Vector3 center,
            float sampleRadius,
            int attempts,
            bool useAgentTypeFilter,
            int agentTypeId,
            int areaMask,
            List<Vector3> usedPositions,
            out Vector3 spawnPosition)
        {
            if (!sampleOnNavMesh)
            {
                for (var attempt = 0; attempt < attempts; attempt++)
                {
                    var raw = center + GetRandomOffset();
                    if (!IsFarEnoughFromUsedPositions(raw, usedPositions))
                        continue;

                    spawnPosition = raw;
                    return true;
                }

                spawnPosition = center;
                return false;
            }

            if (TrySampleNavMesh(center, sampleRadius, useAgentTypeFilter, agentTypeId, areaMask, usedPositions, out spawnPosition))
            {
                return true;
            }

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var candidate = center + GetRandomOffset();
                if (TrySampleNavMesh(candidate, sampleRadius, useAgentTypeFilter, agentTypeId, areaMask, usedPositions, out spawnPosition))
                {
                    return true;
                }
            }

            spawnPosition = center;
            return false;
        }

        private Vector3 GetRandomOffset()
        {
            var y = areaSize.y > 0f
                ? Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f)
                : 0f;

            return new Vector3(
                Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                y,
                Random.Range(-areaSize.z * 0.5f, areaSize.z * 0.5f));
        }

        private bool TrySampleNavMesh(
            Vector3 candidate,
            float sampleRadius,
            bool useAgentTypeFilter,
            int agentTypeId,
            int areaMask,
            List<Vector3> usedPositions,
            out Vector3 sampledPosition)
        {
            if (useAgentTypeFilter)
            {
                var filter = new NavMeshQueryFilter
                {
                    agentTypeID = agentTypeId,
                    areaMask = areaMask
                };

                if (NavMesh.SamplePosition(candidate, out var filteredHit, sampleRadius, filter)
                    && IsFarEnoughFromUsedPositions(filteredHit.position, usedPositions))
                {
                    sampledPosition = filteredHit.position;
                    return true;
                }
            }
            else if (NavMesh.SamplePosition(candidate, out var anyHit, sampleRadius, NavMesh.AllAreas)
                && IsFarEnoughFromUsedPositions(anyHit.position, usedPositions))
            {
                sampledPosition = anyHit.position;
                return true;
            }

            sampledPosition = candidate;
            return false;
        }

        private bool IsFarEnoughFromUsedPositions(Vector3 position, List<Vector3> usedPositions)
        {
            if (usedPositions == null || usedPositions.Count == 0 || minSpawnDistance <= 0f)
                return true;

            var minDistanceSqr = minSpawnDistance * minSpawnDistance;
            for (var i = 0; i < usedPositions.Count; i++)
            {
                if ((position - usedPositions[i]).sqrMagnitude < minDistanceSqr)
                    return false;
            }

            return true;
        }

        private void UnsubscribeServerStarted()
        {
            if (!hasSubscribedServerStarted)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= OnServerStarted;
            }

            hasSubscribedServerStarted = false;
        }
    }
}
