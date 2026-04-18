using Unity.Netcode;
using UnityEngine;

public class CameraTrackMarker : MonoBehaviour
{
    private NetworkObject _ownerNetworkObject;

    public bool IsLocalPlayerMarker
    {
        get
        {
            CacheOwnerNetworkObject();
            return _ownerNetworkObject != null && _ownerNetworkObject.IsOwner;
        }
    }

    private void Awake()
    {
        CacheOwnerNetworkObject();
    }

    private void CacheOwnerNetworkObject()
    {
        if (_ownerNetworkObject == null)
        {
            _ownerNetworkObject = GetComponentInParent<NetworkObject>();
        }
    }
}
