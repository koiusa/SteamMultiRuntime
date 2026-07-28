namespace Koiusa.SteamMultiRuntime.Character
{
    public interface IPlayerModelSync
    {
        CharacterModelIdList ModelIdList { get; set; }
        int CurrentModelIndex { get; }
        void ApplyModelIndex(int index);
    }
}
