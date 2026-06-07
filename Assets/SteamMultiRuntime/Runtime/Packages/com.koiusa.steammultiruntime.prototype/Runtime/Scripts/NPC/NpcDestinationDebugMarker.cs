using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NpcNavMeshController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcDestinationDebugMarker : MonoBehaviour
    {
        [SerializeField] private float markerScale = 0.35f;
        [SerializeField] private float arriveBuffer = 0.1f;

        private NavMeshAgent _agent;
        private NpcNavMeshController _controller;
        private GameObject _marker;
        private Vector3 _currentDestination;
        private bool _hasDestination;
        private System.Action<Vector3> _destinationProvider;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _controller = GetComponent<NpcNavMeshController>();
        }

        private void OnEnable()
        {
            // NetworkObject がない（ローカルモード）場合は自分で DestinationSet を購読する
            // Network モードは NpcDestinationDebugMarkerNetSync が NetworkVariable 経由で処理する
            if (_controller != null && GetComponent<Unity.Netcode.NetworkObject>() == null)
            {
                _controller.DestinationSet += SetDestination;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.DestinationSet -= SetDestination;
            }

            UnregisterDestinationProvider();
            DestroyMarker();
            _hasDestination = false;
        }

        public void RegisterDestinationProvider(System.Action<Vector3> destinationProvider)
        {
            UnregisterDestinationProvider();
            _destinationProvider = destinationProvider;
            if (_destinationProvider != null)
            {
                _destinationProvider += OnDestinationSet;
            }
        }

        public void UnregisterDestinationProvider()
        {
            if (_destinationProvider != null)
            {
                _destinationProvider -= OnDestinationSet;
                _destinationProvider = null;
            }
        }

        private void Update()
        {
            if (!_hasDestination || _marker == null || _agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            var arrived = !_agent.pathPending && (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + arriveBuffer);
            if (arrived)
                DestroyMarker();
        }

        public void SetDestination(Vector3 destination)
        {
            _currentDestination = destination;
            _hasDestination = true;

            if (_marker == null)
                _marker = CreateMarker();

            _marker.transform.position = _currentDestination;
            _marker.SetActive(true);
        }

        public void ClearDestination()
        {
            _hasDestination = false;
            DestroyMarker();
        }

        private void OnDestinationSet(Vector3 destination)
        {
            SetDestination(destination);
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
            return Shader.Find("HDRP/Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
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
