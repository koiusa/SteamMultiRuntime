using System.Reflection;
using Koiusa.SteamMultiRuntime;
using Unity.Cinemachine;
using UnityEngine;

public class ForcusMerker : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _cinemachineCamera;
    [SerializeField] private SteamLobbyService _lobbyService;

    private CameraTrackMarker _activeMarker;
    private Transform _defaultTrackingTarget;

    private void Awake()
    {
        if (_lobbyService == null)
        {
            _lobbyService = FindFirstObjectByType<SteamLobbyService>();
        }

        if (_cinemachineCamera == null)
        {
            _cinemachineCamera = GetComponent<CinemachineCamera>();
        }

        if (_cinemachineCamera == null)
        {
            _cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
        }

        _defaultTrackingTarget = GetTrackingTarget();
    }

    private void OnEnable()
    {
        if (_lobbyService != null)
        {
            _lobbyService.StateChanged += OnLobbyStateChanged;
        }

        RefreshTrackingTarget();
    }

    private void OnDisable()
    {
        if (_lobbyService != null)
        {
            _lobbyService.StateChanged -= OnLobbyStateChanged;
        }

        ClearActiveMarker();
        RestoreDefaultTarget();
    }

    private void Update()
    {
        if (_lobbyService != null && !_lobbyService.IsInLobby)
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

    private void OnLobbyStateChanged()
    {
        RefreshTrackingTarget();
    }

    private void RefreshTrackingTarget()
    {
        if (_lobbyService != null && !_lobbyService.IsInLobby)
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

        SetTrackingTarget(marker.transform);
    }

    private void RestoreDefaultTarget()
    {
        SetTrackingTarget(_defaultTrackingTarget);
    }

    private void ClearActiveMarker()
    {
        _activeMarker = null;
    }

    private Transform GetTrackingTarget()
    {
        if (_cinemachineCamera == null)
        {
            return null;
        }

        if (TryGetMemberValue(_cinemachineCamera, "TrackingTarget", out var trackingTarget))
        {
            return trackingTarget as Transform;
        }

        if (TryGetMemberValue(_cinemachineCamera, "Follow", out var followTarget))
        {
            return followTarget as Transform;
        }

        if (TryGetNestedMemberValue(_cinemachineCamera, "Target", "TrackingTarget", out var nestedTrackingTarget))
        {
            return nestedTrackingTarget as Transform;
        }

        return null;
    }

    private void SetTrackingTarget(Transform target)
    {
        if (_cinemachineCamera == null)
        {
            return;
        }

        if (TrySetMemberValue(_cinemachineCamera, "TrackingTarget", target))
        {
            return;
        }

        if (TrySetMemberValue(_cinemachineCamera, "Follow", target))
        {
            return;
        }

        TrySetNestedMemberValue(_cinemachineCamera, "Target", "TrackingTarget", target);
    }

    private static bool TryGetMemberValue(object instance, string memberName, out object value)
    {
        value = null;
        if (instance == null)
        {
            return false;
        }

        var type = instance.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanRead)
        {
            value = property.GetValue(instance);
            return true;
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            value = field.GetValue(instance);
            return true;
        }

        return false;
    }

    private static bool TrySetMemberValue(object instance, string memberName, object value)
    {
        if (instance == null)
        {
            return false;
        }

        var type = instance.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return true;
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(instance, value);
            return true;
        }

        return false;
    }

    private static bool TryGetNestedMemberValue(object instance, string memberName, string nestedMemberName, out object value)
    {
        value = null;
        if (!TryGetMemberValue(instance, memberName, out var nestedInstance) || nestedInstance == null)
        {
            return false;
        }

        return TryGetMemberValue(nestedInstance, nestedMemberName, out value);
    }

    private static bool TrySetNestedMemberValue(object instance, string memberName, string nestedMemberName, object value)
    {
        if (!TryGetMemberValue(instance, memberName, out var nestedInstance) || nestedInstance == null)
        {
            return false;
        }

        return TrySetMemberValue(nestedInstance, nestedMemberName, value);
    }
}
