using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Koiusa.SteamMultiRuntime.Network;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class NetworkNpcRandomSpawner : MonoBehaviour
    {
        [Header("NPC Prefab")]
        [SerializeField] private GameObject npcPrefab;

        [Header("Model")]
        [SerializeField] private CharacterModelIdList npcModelIdList;
        [SerializeField] private bool randomizeModelOnSpawn = true;

        [Header("Spawn")]
        [SerializeField, Min(1)] private int spawnCount = 5;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool oneShot = true;
        [SerializeField, Min(0.1f)] private float spawnRetryInterval = 2f;
        [SerializeField] private float spawnHeightOffset = 0.05f;

        [Header("Area")]
        [SerializeField] private Transform areaCenter;
        [SerializeField] private Vector3 areaSize = new Vector3(20f, 0f, 20f);

        [Header("NavMesh")]
        [SerializeField] private bool sampleOnNavMesh = true;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 6f;
        [SerializeField, Min(1)] private int maxAttemptsPerNpc = 8;
        [SerializeField] private bool fallbackToAnyAgentType = true;
        [SerializeField, Min(0.1f)] private float fallbackNavMeshSampleRadius = 24f;
        [SerializeField, Min(1)] private int fallbackMaxAttemptsPerNpc = 12;

        private bool hasSpawned;
        private bool hasSubscribedServerStarted;
        private float nextSpawnRetryTime;

        private void Start()
        {
            if (!spawnOnStart)
            {
                return;
            }

            TrySpawnOrSubscribe();
        }

        private void Update()
        {
            if (!spawnOnStart || hasSpawned || Time.time < nextSpawnRetryTime)
            {
                return;
            }

            TrySpawnOrSubscribe();
        }

        private void OnDestroy()
        {
            UnsubscribeServerStarted();
        }

        public void SpawnNow()
        {
            TrySpawnOrSubscribe();
        }

        private void TrySpawnOrSubscribe()
        {
            if (oneShot && hasSpawned)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                nextSpawnRetryTime = Time.time + spawnRetryInterval;
                return;
            }

            if (networkManager.IsServer)
            {
                SpawnInternal();
                return;
            }

            if (!hasSubscribedServerStarted)
            {
                networkManager.OnServerStarted += OnServerStarted;
                hasSubscribedServerStarted = true;
            }

            nextSpawnRetryTime = Time.time + spawnRetryInterval;
        }

        private void OnServerStarted()
        {
            SpawnInternal();
        }

        private void SpawnInternal()
        {
            UnsubscribeServerStarted();

            if (oneShot && hasSpawned)
            {
                return;
            }

            var prefab = npcPrefab;
            if (prefab == null)
            {
                Debug.LogError("[NetworkNpcRandomSpawner] NPC prefab is not assigned.", this);
                nextSpawnRetryTime = Time.time + spawnRetryInterval;
                return;
            }

            var prefabAgent = prefab.GetComponentInChildren<NavMeshAgent>(true);
            var sampleAgentTypeId = prefabAgent != null ? prefabAgent.agentTypeID : 0;
            var sampleAreaMask = prefabAgent != null ? prefabAgent.areaMask : NavMesh.AllAreas;

            var spawnedCount = 0;
            for (var i = 0; i < spawnCount; i++)
            {
                if (!TryGetSpawnPosition(sampleAgentTypeId, sampleAreaMask, out var spawnPosition))
                {
                    continue;
                }

                var spawnUp = Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
                var finalSpawnPosition = spawnPosition + spawnUp * spawnHeightOffset;

                var instance = Instantiate(prefab, finalSpawnPosition, Quaternion.identity);
                var networkObject = instance.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    Debug.LogError("[NetworkNpcRandomSpawner] Spawned prefab does not have NetworkObject.", instance);
                    Destroy(instance);
                    continue;
                }

                if (!networkObject.IsSpawned)
                {
                    networkObject.Spawn();
                }

                ApplyModelSync(instance);

                spawnedCount++;
            }

            if (spawnedCount > 0)
            {
                hasSpawned = true;
                return;
            }

            Debug.LogWarning($"[NetworkNpcRandomSpawner] NPC spawn failed (no valid spawn point). agentTypeId={sampleAgentTypeId}, sampleRadius={navMeshSampleRadius}, fallback={fallbackToAnyAgentType}", this);
            nextSpawnRetryTime = Time.time + spawnRetryInterval;
        }

        private void ApplyModelSync(GameObject instance)
        {
            if (npcModelIdList == null)
            {
                return;
            }

            var modelSync = instance.GetComponent<NetworkPlayerModelSync>();
            if (modelSync == null)
            {
                return;
            }

            modelSync.modelIdList = npcModelIdList;

            if (!randomizeModelOnSpawn || npcModelIdList.modelIds == null || npcModelIdList.modelIds.Length == 0)
            {
                return;
            }

            modelSync.SelectedModelIndex.Value = Random.Range(0, npcModelIdList.modelIds.Length);
        }

        private bool TryGetSpawnPosition(int agentTypeId, int areaMask, out Vector3 spawnPosition)
        {
            var center = areaCenter != null ? areaCenter.position : transform.position;

            if (TryGetSpawnPositionInternal(center, navMeshSampleRadius, maxAttemptsPerNpc, true, agentTypeId, areaMask, out spawnPosition))
            {
                return true;
            }

            if (!fallbackToAnyAgentType)
            {
                spawnPosition = center;
                return false;
            }

            return TryGetSpawnPositionInternal(center, fallbackNavMeshSampleRadius, fallbackMaxAttemptsPerNpc, false, 0, NavMesh.AllAreas, out spawnPosition);
        }

        private bool TryGetSpawnPositionInternal(
            Vector3 center,
            float sampleRadius,
            int attempts,
            bool useAgentTypeFilter,
            int agentTypeId,
            int areaMask,
            out Vector3 spawnPosition)
        {
            if (!sampleOnNavMesh)
            {
                spawnPosition = center + GetRandomOffset();
                return true;
            }

            if (TrySampleNavMesh(center, sampleRadius, useAgentTypeFilter, agentTypeId, areaMask, out spawnPosition))
            {
                return true;
            }

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var candidate = center + GetRandomOffset();
                if (TrySampleNavMesh(candidate, sampleRadius, useAgentTypeFilter, agentTypeId, areaMask, out spawnPosition))
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

        private static bool TrySampleNavMesh(
            Vector3 candidate,
            float sampleRadius,
            bool useAgentTypeFilter,
            int agentTypeId,
            int areaMask,
            out Vector3 sampledPosition)
        {
            if (useAgentTypeFilter)
            {
                var filter = new NavMeshQueryFilter
                {
                    agentTypeID = agentTypeId,
                    areaMask = areaMask
                };

                if (NavMesh.SamplePosition(candidate, out var filteredHit, sampleRadius, filter))
                {
                    sampledPosition = filteredHit.position;
                    return true;
                }
            }
            else if (NavMesh.SamplePosition(candidate, out var anyHit, sampleRadius, NavMesh.AllAreas))
            {
                sampledPosition = anyHit.position;
                return true;
            }

            sampledPosition = candidate;
            return false;
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
