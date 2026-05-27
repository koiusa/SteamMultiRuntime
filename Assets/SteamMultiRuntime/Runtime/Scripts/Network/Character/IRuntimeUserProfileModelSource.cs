using Koiusa.SteamMultiRuntime.Network;

namespace Koiusa.SteamMultiRuntime
{
    public interface IRuntimeUserProfileModelSource
    {
        CharacterModelIdList ModelIdList { get; }
        int SelectedModelIndex { get; }
        void SetSelectedModel(int index);
        void ApplySelectedModel();
    }
}
