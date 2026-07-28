namespace Koiusa.SteamMultiRuntime.Character
{
    public interface IRuntimeUserProfileModelSource
    {
        CharacterModelIdList ModelIdList { get; }
        int SelectedModelIndex { get; }
        void SetSelectedModel(int index);
        void ApplySelectedModel();
    }
}
