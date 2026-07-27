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
        [SerializeField] private GameObject markerPrefab;

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
            // Network NPC marker lifetime is authoritative on the server and is
            // applied by NpcDestinationDebugMarkerNetSync. Remote agents do not
            // own a NavMesh path, so their local hasPath cannot determine arrival.
            if (GetComponent<Unity.Netcode.NetworkObject>() != null)
                return;

            if (!_hasDestination || _marker == null || _agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            if (HasArrived())
                DestroyMarker();
        }

        public bool HasArrived()
        {
            if (!_hasDestination || _agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return false;
            if (_agent.pathPending)
                return false;

            return !_agent.hasPath
                || _agent.remainingDistance <= _agent.stoppingDistance + arriveBuffer;
        }

        public void SetDestination(Vector3 destination)
        {
            _currentDestination = destination;
            _hasDestination = true;

            if (_marker == null)
                _marker = CreateMarker();

            if (_marker == null)
                return;

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
            if (markerPrefab == null)
            {
                Debug.LogWarning("[NpcDestinationDebugMarker] markerPrefab is not assigned.", this);
                return null;
            }

            // 親を指定したInstantiateで、Active SceneではなくNPCと同じSceneへ直接生成する。
            var marker = Instantiate(markerPrefab, _currentDestination, Quaternion.identity, transform);
            marker.transform.SetParent(null, true);
            marker.transform.localScale = Vector3.one * Mathf.Max(0.01f, markerScale);
            marker.name = $"{gameObject.name}_DestinationMarker";
            return marker;
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
