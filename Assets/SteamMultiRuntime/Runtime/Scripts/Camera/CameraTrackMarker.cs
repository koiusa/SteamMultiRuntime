using Unity.Netcode;
using UnityEngine;

public class CameraTrackMarker : MonoBehaviour
{
    [Tooltip("LocalPlayer（NetworkObject なし）として扱う場合に有効にする")]
    [SerializeField] private bool isLocalPlayer;

    private NetworkObject _ownerNetworkObject;

    /// <summary>
    /// このマーカーがローカルプレイヤーのものかどうか。
    /// isLocalPlayer フラグが true の場合は NetworkObject によらず true を返す。
    /// </summary>
    public bool IsLocalPlayerMarker
    {
        get
        {
            if (isLocalPlayer)
            {
                return true;
            }

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
