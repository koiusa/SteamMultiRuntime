using UnityEngine;
using Koiusa.TargetingSystem.Runtime;

namespace Koiusa.TargetingSystem.Sample
{
    public sealed class RandomTargetSpawner : MonoBehaviour
    {
        [SerializeField] private TargetMarker targetPrefab;
        [SerializeField] private TargetMarkerRegistry registry;
        [SerializeField, Min(1)] private int spawnCount = 10;
        [SerializeField] private Vector3 areaSize = new Vector3(20f, 0f, 20f);
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool clearChildrenBeforeSpawn = true;

        private void Awake()
        {
            if (registry == null)
            {
                registry = FindFirstObjectByType<TargetMarkerRegistry>();
            }
        }

        private void Start()
        {
            if (!spawnOnStart)
            {
                return;
            }

            Spawn();
        }

        [ContextMenu("Spawn")]
        public void Spawn()
        {
            if (targetPrefab == null)
            {
                return;
            }

            if (clearChildrenBeforeSpawn)
            {
                ClearSpawnedChildren();
            }

            var half = areaSize * 0.5f;
            for (var i = 0; i < spawnCount; i++)
            {
                Generate(half);
            }
        }

        private void Generate(Vector3 half)
        {
            var localPos = new Vector3(
             Random.Range(-half.x, half.x),
             Random.Range(-half.y, half.y),
             Random.Range(-half.z, half.z));

            var marker = Instantiate(targetPrefab, transform.TransformPoint(localPos), Quaternion.identity, transform);
            marker.Registry = registry;
        }

        private void ClearSpawnedChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, areaSize);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
