using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Network
{
    public interface IStartupStageSceneLoaderContext
    {
        StageSceneList StageSceneList { get; }
        int StartupStageSceneIndex { get; }
        LoadSceneMode SceneLoadMode { get; }
        bool SetLoadedSceneAsActive { get; }
    }
}
