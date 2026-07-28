using System.Threading.Tasks;

namespace Koiusa.SteamMultiRuntime
{
    public interface ILobbySceneTransitionController
    {
        Task<bool> SwitchLobbySceneAsync(string previousSceneName);
        Task PrepareForLobbyExitAsync();
    }
}
