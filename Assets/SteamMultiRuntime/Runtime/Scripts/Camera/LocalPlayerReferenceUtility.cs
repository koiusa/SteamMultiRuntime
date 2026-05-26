using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Local プレイヤーの参照元を解決する共通ユーティリティ。
    /// LocalManager がある場合はそれを優先し、なければ LocalPlayerController を探す。
    /// </summary>
    internal static class LocalPlayerReferenceUtility
    {
        public static GameObject ResolveLocalPlayerObject()
        {
            if (LocalManager.Singleton != null && LocalManager.Singleton.LocalPlayerObject != null)
            {
                return LocalManager.Singleton.LocalPlayerObject;
            }

            var controller = Object.FindFirstObjectByType<LocalPlayerController>();
            if (controller != null)
            {
                return controller.gameObject;
            }

            return null;
        }
    }
}
