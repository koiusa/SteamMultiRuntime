using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    internal static class ActiveSceneAudioListenerCoordinator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscriptions()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            ResetSubscriptions();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ReconcileListeners();
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

        private static void ReconcileListeners()
        {
            var listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (listeners.Length == 0)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            AudioListener selected = null;

            for (var i = 0; i < listeners.Length; i++)
            {
                var listener = listeners[i];
                if (listener.gameObject.scene == activeScene && listener.gameObject.activeInHierarchy)
                {
                    selected = listener;
                    break;
                }
            }

            if (selected == null)
            {
                for (var i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i].gameObject.activeInHierarchy)
                    {
                        selected = listeners[i];
                        break;
                    }
                }
            }

            for (var i = 0; i < listeners.Length; i++)
            {
                listeners[i].enabled = listeners[i] == selected;
            }
        }
    }
}
