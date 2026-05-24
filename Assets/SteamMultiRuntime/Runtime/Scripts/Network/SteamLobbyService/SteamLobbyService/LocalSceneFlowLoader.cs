using System.Threading.Tasks;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LocalSceneFlowLoader : MonoBehaviour, ISceneLoadContext
    {
        [Header("Scenes")]
        [SerializeField] private string defaultSceneName = "";
        [SerializeField] private string lobbySceneName = "";

        [Header("Policy")]
        [SerializeField] private bool disableCamerasInLoadedScenes = true;
        [SerializeField] private bool unloadDefaultSceneOnLobbyEntered = true;
        [SerializeField] private bool loadDefaultSceneOnLobbyLeft = true;
        [SerializeField] private bool unloadLobbySceneOnLeft = true;

        public string DefaultSceneName => defaultSceneName;
        public string LobbySceneName => lobbySceneName;
        public bool DisableCamerasInLoadedScenes => disableCamerasInLoadedScenes;
        public bool UnloadDefaultSceneOnLobbyEntered => unloadDefaultSceneOnLobbyEntered;
        public bool LoadDefaultSceneOnLobbyLeft => loadDefaultSceneOnLobbyLeft;
        public bool ShouldUnloadLobbySceneOnLeft => unloadLobbySceneOnLeft;

        public Task<bool> LoadLobbySceneAsync()
        {
            return SceneLoadUtility.LoadPresentationSceneAsync(lobbySceneName, this, this, nameof(LocalSceneFlowLoader));
        }

        public Task<bool> LoadDefaultSceneAsync()
        {
            return SceneLoadUtility.LoadPresentationSceneAsync(defaultSceneName, this, this, nameof(LocalSceneFlowLoader));
        }

        public Task<bool> UnloadLobbySceneAsync()
        {
            return SceneLoadUtility.UnloadSceneAsync(lobbySceneName);
        }

        public Task<bool> UnloadDefaultSceneAsync()
        {
            return SceneLoadUtility.UnloadSceneAsync(defaultSceneName);
        }
    }
}
