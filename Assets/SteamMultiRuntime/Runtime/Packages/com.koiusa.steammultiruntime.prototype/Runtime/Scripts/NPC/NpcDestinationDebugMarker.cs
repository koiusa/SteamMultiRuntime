using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NpcNavMeshController))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NetworkObject))]
    public class NpcDestinationDebugMarker : NetworkBehaviour
    {
        [SerializeField] private float markerScale = 0.35f;
        [SerializeField] private float arriveBuffer = 0.1f;

        private NpcNavMeshController _controller;
        private NavMeshAgent _agent;
        private GameObject _marker;

        private NetworkVariable<Vector3> _syncedDestination = new NetworkVariable<Vector3>(Vector3.zero);

        private void Awake()
        {
            _controller = GetComponent<NpcNavMeshController>();
            _agent = GetComponent<NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            if (_controller != null)
                _controller.DestinationSet += OnDestinationSet;

            _syncedDestination.OnValueChanged += OnSyncedDestinationChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (_controller != null)
                _controller.DestinationSet -= OnDestinationSet;

            _syncedDestination.OnValueChanged -= OnSyncedDestinationChanged;
            DestroyMarker();
            
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (_marker == null || _agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            var arrived = !_agent.pathPending && (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + arriveBuffer);
            if (arrived)
                DestroyMarker();
        }

        private void OnDestinationSet(Vector3 destination)
        {
            if (IsServer)
            {
                _syncedDestination.Value = destination;
            }
        }

        private void OnSyncedDestinationChanged(Vector3 previousValue, Vector3 newValue)
        {
            if (_marker == null)
                _marker = CreateMarker();

            _marker.transform.position = newValue;
            _marker.SetActive(true);
        }

        private GameObject CreateMarker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"{gameObject.name}_DestinationMarker";
            marker.transform.localScale = Vector3.one * Mathf.Max(0.01f, markerScale);

            var collider = marker.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = ResolveMarkerShader();
                if (shader != null)
                {
                    var material = new Material(shader);
                    material.color = Color.red;
                    renderer.material = material;
                }
            }

            return marker;
        }

        private static Shader ResolveMarkerShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("HDRP/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
        }

        private void DestroyMarker()
        {
            if (_marker == null)
                return;

            Destroy(_marker);
            _marker = null;
        }
    }
}
