namespace Koiusa.SteamMultiRuntime.Character
{
    public interface IActorModelSync
    {
        CharacterModelIdList ModelIdList { get; set; }
        int CurrentModelIndex { get; }
        void ApplyModelIndex(int index);
    }
}
