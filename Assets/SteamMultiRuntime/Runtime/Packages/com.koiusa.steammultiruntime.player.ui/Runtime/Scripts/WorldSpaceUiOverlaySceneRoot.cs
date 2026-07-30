using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [DisallowMultipleComponent]
    public sealed class WorldSpaceUiOverlaySceneRoot : MonoBehaviour
    {
        internal const string SceneName = "SteamMultiRuntime_UI";
        private static WorldSpaceUiOverlaySceneRoot active;
        private bool runtimeFallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadUiScene()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || active != null)
                return;

            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.isLoaded)
                return;

            if (Application.CanStreamedLevelBeLoaded(SceneName))
            {
                SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
                return;
            }

            CreateRuntimeFallback();
        }

        internal static void EnsureAvailable()
        {
            if (active == null && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                CreateRuntimeFallback();
        }

        private static void CreateRuntimeFallback()
        {
            var host = new GameObject("World Space UI Overlay (Runtime Fallback)");
            host.SetActive(false);
            var root = host.AddComponent<WorldSpaceUiOverlaySceneRoot>();
            root.runtimeFallback = true;
            Object.DontDestroyOnLoad(host);
            host.SetActive(true);
        }

        private void Awake()
        {
            if (active != null && active != this)
            {
                if (active.runtimeFallback && !runtimeFallback)
                    Destroy(active.gameObject);
                else
                {
                    Destroy(gameObject);
                    return;
                }
            }

            active = this;
            WorldSpaceUiOverlayCamera.SetHost(transform);
        }

        private void OnDestroy()
        {
            if (active != this)
                return;

            active = null;
            WorldSpaceUiOverlayCamera.ClearHost(transform);
        }
    }
}
