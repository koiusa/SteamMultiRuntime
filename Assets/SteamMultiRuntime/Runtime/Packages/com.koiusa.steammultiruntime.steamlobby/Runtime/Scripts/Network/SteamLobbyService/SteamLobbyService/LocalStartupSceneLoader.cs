using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Network
{
    [DisallowMultipleComponent]
    public class LocalStartupSceneLoader : MonoBehaviour, IStartupStageSceneLoaderContext
    {
        [Header("Startup Scene")]
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private StageSceneList stageSceneList;
        [SerializeField, Min(0)] private int startupStageSceneIndex;
        [SerializeField] private LoadSceneMode sceneLoadMode = LoadSceneMode.Single;
        [SerializeField] private bool setLoadedSceneAsActive = true;

        public StageSceneList StageSceneList => stageSceneList;
        public int StartupStageSceneIndex => startupStageSceneIndex;
        public LoadSceneMode SceneLoadMode => sceneLoadMode;
        public bool SetLoadedSceneAsActive => setLoadedSceneAsActive;

        private async void Start()
        {
            if (!loadOnStart)
            {
                return;
            }

            await LoadStartupSceneAsync();
        }

        public Task<bool> LoadStartupSceneAsync()
        {
            return StageStartupSceneLoader.LoadStartupSceneAsync(this, this, nameof(LocalStartupSceneLoader));
        }
    }
}
