using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetMarkerRandomSpawner : MonoBehaviour
    {
        [SerializeField] private TargetMarker targetPrefab;
        [SerializeField] private TargetMarkerRegistry registry;
        [SerializeField, Min(1)] private int spawnCount = 10;
        [SerializeField] private Vector3 areaSize = new Vector3(18f, 4f, 12f);
        [SerializeField] private bool spawnOnStart = true;

        private void Start()
        {
            if (spawnOnStart)
            {
                Spawn();
            }
        }

        [ContextMenu("Respawn Targets")]
        public void Spawn()
        {
            if (targetPrefab == null)
            {
                return;
            }

            ClearSpawnedTargets();
            var half = areaSize * 0.5f;
            for (var index = 0; index < spawnCount; index++)
            {
                var localPosition = new Vector3(
                    Random.Range(-half.x, half.x),
                    Random.Range(-half.y, half.y),
                    Random.Range(-half.z, half.z));
                var marker = Instantiate(targetPrefab, transform.TransformPoint(localPosition), Quaternion.identity, transform);
                marker.name = $"Target {index + 1:00}";
                marker.Registry = registry;
            }
        }

        private void ClearSpawnedTargets()
        {
            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                var child = transform.GetChild(index).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.75f, 1f, 0.9f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, areaSize);
        }
    }
}
