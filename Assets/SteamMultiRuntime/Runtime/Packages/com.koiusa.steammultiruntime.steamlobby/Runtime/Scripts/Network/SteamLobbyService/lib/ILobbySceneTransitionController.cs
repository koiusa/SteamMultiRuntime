using System.Threading.Tasks;

namespace Koiusa.SteamMultiRuntime
{
    internal interface ILobbySceneTransitionController
    {
        Task<bool> SwitchLobbySceneAsync(string previousSceneName);
    }
}
