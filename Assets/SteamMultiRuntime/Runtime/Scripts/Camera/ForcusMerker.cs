using TNRD;
using Unity.Cinemachine;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public class ForcusMerker : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _cinemachineCamera;

        [Tooltip("Network 用は NetworkFocusMarkerContext、Local 用は LocalFocusMarkerContext を指定")]
        [SerializeField] private SerializableInterface<IFocusMarkerContext> _contextSource;

        private IFocusMarkerContext _context;
        private CameraTrackMarker _activeMarker;
        private Transform _defaultTrackingTarget;

        private void Awake()
        {
            ResolveContext();

            var followCamera = GetComponent<CinemachineCamera>();
            if (followCamera != null)
            {
                _cinemachineCamera = followCamera;
            }

            if (_cinemachineCamera == null)
            {
                _cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
            }

            _defaultTrackingTarget = FocusMarkerUtility.GetTrackingTarget(_cinemachineCamera);
        }

        private void OnEnable()
        {
            ResolveContext();
            if (_context != null)
            {
                _context.StateChanged += OnContextStateChanged;
            }

            RefreshTrackingTarget();
        }

        private void OnDisable()
        {
            if (_context != null)
            {
                _context.StateChanged -= OnContextStateChanged;
            }

            ClearActiveMarker();
            RestoreDefaultTarget();
        }

        private void Update()
        {
            if (_context != null && !_context.IsActive)
            {
                if (_activeMarker != null)
                {
                    ClearActiveMarker();
                    RestoreDefaultTarget();
                }

                return;
            }

            if (_activeMarker != null)
            {
                if (_activeMarker.IsLocalPlayerMarker)
                {
                    return;
                }

                ClearActiveMarker();
                RestoreDefaultTarget();
            }

            TryResolveLocalMarker();
        }

        private void OnContextStateChanged()
        {
            RefreshTrackingTarget();
        }

        private void RefreshTrackingTarget()
        {
            if (_context != null && !_context.IsActive)
            {
                ClearActiveMarker();
                RestoreDefaultTarget();
                return;
            }

            if (_activeMarker != null && !_activeMarker.IsLocalPlayerMarker)
            {
                ClearActiveMarker();
                RestoreDefaultTarget();
            }

            TryResolveLocalMarker();
        }

        private void TryResolveLocalMarker()
        {
            var markers = FindObjectsByType<CameraTrackMarker>(FindObjectsSortMode.None);
            for (var i = 0; i < markers.Length; i++)
            {
                if (!markers[i].IsLocalPlayerMarker)
                {
                    continue;
                }

                AttachToMarker(markers[i]);
                return;
            }
        }

        private void AttachToMarker(CameraTrackMarker marker)
        {
            _activeMarker = marker;
            if (marker == null)
            {
                return;
            }

            FocusMarkerUtility.SetTrackingTarget(_cinemachineCamera, marker.transform);
        }

        private void RestoreDefaultTarget()
        {
            FocusMarkerUtility.SetTrackingTarget(_cinemachineCamera, _defaultTrackingTarget);
        }

        private void ClearActiveMarker()
        {
            _activeMarker = null;
        }

        private void ResolveContext()
        {
            if (_context != null)
            {
                return;
            }

            var configuredContext = _contextSource != null ? _contextSource.Value : null;
            if (configuredContext != null)
            {
                _context = configuredContext;
                return;
            }

            _context = GetComponent<IFocusMarkerContext>();
            if (_context == null)
            {
                _context = GetComponentInChildren<IFocusMarkerContext>();
            }
        }
    }
}

