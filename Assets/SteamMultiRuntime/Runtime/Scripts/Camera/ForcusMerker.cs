using TNRD;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public class ForcusMerker : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _cinemachineCamera;

        [Tooltip("Network 用は NetworkFocusMarkerContext、Local 用は LocalFocusMarkerContext を指定")]
        [SerializeField] private SerializableInterface<IFocusMarkerContext> _contextSource;

        private IFocusMarkerContext _context;
        private GameObject _activePlayer;
        private Transform _defaultTrackingTarget;

        private void Awake()
        {
            ResolveContext();
            ResolveCamera();
            _defaultTrackingTarget = _cinemachineCamera != null ? _cinemachineCamera.Follow : null;
        }

        private void OnEnable()
        {
            ResolveContext();
            if (_context != null)
            {
                _context.StateChanged += RefreshTrackingTarget;
            }

            RefreshTrackingTarget();
        }

        private void OnDisable()
        {
            if (_context != null)
            {
                _context.StateChanged -= RefreshTrackingTarget;
            }

            SetTrackingTarget(null);
        }

        private void RefreshTrackingTarget()
        {
            SetTrackingTarget(ResolvePlayerObject());
        }

        private GameObject ResolvePlayerObject()
        {
            if (_context != null && !_context.IsActive)
            {
                return null;
            }

            if (_context is NetworkFocusMarkerContext)
            {
                return NetworkManager.Singleton?.LocalClient?.PlayerObject?.gameObject;
            }

            if (LocalManager.Singleton != null && LocalManager.Singleton.LocalPlayerObject != null)
            {
                return LocalManager.Singleton.LocalPlayerObject;
            }

            var controller = FindFirstObjectByType<LocalPlayerController>();
            return controller != null ? controller.gameObject : null;
        }

        private void SetTrackingTarget(GameObject player)
        {
            _activePlayer = player;
            if (_cinemachineCamera == null)
            {
                return;
            }

            var marker = player != null
                ? player.GetComponentInChildren<CameraTrackMarker>(true)
                : null;
            _cinemachineCamera.Follow = marker != null
                ? marker.transform
                : _defaultTrackingTarget;
        }

        private void ResolveCamera()
        {
            var attachedCamera = GetComponent<CinemachineCamera>();
            if (attachedCamera != null)
            {
                _cinemachineCamera = attachedCamera;
            }
            else if (_cinemachineCamera == null)
            {
                _cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
            }
        }

        private void ResolveContext()
        {
            if (_context != null)
            {
                return;
            }

            _context = _contextSource != null ? _contextSource.Value : null;
            if (_context == null)
            {
                _context = GetComponent<IFocusMarkerContext>()
                    ?? GetComponentInChildren<IFocusMarkerContext>();
            }
        }
    }
}
