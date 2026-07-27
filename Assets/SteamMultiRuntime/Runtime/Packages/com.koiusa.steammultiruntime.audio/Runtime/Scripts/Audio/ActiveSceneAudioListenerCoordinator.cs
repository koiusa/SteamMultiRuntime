using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    internal static class ActiveSceneAudioListenerCoordinator
    {
        private const string FallbackObjectName = "[SteamMultiRuntime] Fallback Audio Listener";
        private static AudioListener fallbackListener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscriptions()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Camera.onPreCull -= OnCameraPreCull;
            fallbackListener = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            ResetSubscriptions();
            EnsureFallbackListener();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Camera.onPreCull += OnCameraPreCull;
            ReconcileListeners();
        }

        private static void EnsureFallbackListener()
        {
            if (fallbackListener != null)
            {
                return;
            }

            var fallbackObject = new GameObject(FallbackObjectName)
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
            };
            Object.DontDestroyOnLoad(fallbackObject);
            fallbackListener = fallbackObject.AddComponent<AudioListener>();
        }

        private static void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            ReconcileListeners();
        }

        private static void OnSceneUnloaded(Scene _)
        {
            ReconcileListeners();
        }

        private static void OnActiveSceneChanged(Scene _, Scene __)
        {
            ReconcileListeners();
        }

        private static void OnCameraPreCull(Camera camera)
        {
            if (camera == null || !camera.isActiveAndEnabled)
            {
                return;
            }

            var cameraListener = camera.GetComponent<AudioListener>();
            if (cameraListener != null && cameraListener.gameObject.activeInHierarchy)
            {
                ReconcileListeners(cameraListener);
            }
        }

        private static void ReconcileListeners(AudioListener preferred = null)
        {
            EnsureFallbackListener();

            var listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            var activeScene = SceneManager.GetActiveScene();
            var selected = preferred != fallbackListener
                && preferred != null
                && preferred.gameObject.activeInHierarchy
                    ? preferred
                    : null;

            for (var i = 0; selected == null && i < listeners.Length; i++)
            {
                var listener = listeners[i];
                if (listener != fallbackListener
                    && listener.gameObject.scene == activeScene
                    && listener.gameObject.activeInHierarchy)
                {
                    selected = listener;
                }
            }

            if (selected == null)
            {
                for (var i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i] != fallbackListener
                        && listeners[i].gameObject.activeInHierarchy)
                    {
                        selected = listeners[i];
                        break;
                    }
                }
            }

            // Scene側に有効なListenerがない遷移中も、必ずFallbackを1つ有効に保つ。
            fallbackListener.enabled = selected == null;
            if (selected != null)
            {
                selected.enabled = true;
            }

            for (var i = 0; i < listeners.Length; i++)
            {
                var listener = listeners[i];
                if (listener != null && listener != fallbackListener && listener != selected)
                {
                    listener.enabled = false;
                }
            }
        }
    }
}
