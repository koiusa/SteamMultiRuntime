using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Local プレイヤー用の IFocusMarkerContext 実装。
    /// LocalManager があればそれを優先し、なければ LocalPlayerController を参照する。
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalFocusMarkerContext : MonoBehaviour, IFocusMarkerContext
    {
        public bool IsActive => ResolveLocalPlayerObject() != null;

        public event Action StateChanged;

        private void OnEnable()
        {
            // LocalPlayerController が生成されたタイミングを Update で監視して StateChanged を発火させる
            _wasActive = IsActive;
        }

        private bool _wasActive;

        private void Update()
        {
            var isActive = IsActive;
            if (isActive != _wasActive)
            {
                _wasActive = isActive;
                StateChanged?.Invoke();
            }
        }

        private static GameObject ResolveLocalPlayerObject()
        {
            if (LocalManager.Singleton != null && LocalManager.Singleton.LocalPlayerObject != null)
            {
                return LocalManager.Singleton.LocalPlayerObject;
            }

            var controller = FindFirstObjectByType<LocalPlayerController>();
            return controller != null ? controller.gameObject : null;
        }
    }
}
