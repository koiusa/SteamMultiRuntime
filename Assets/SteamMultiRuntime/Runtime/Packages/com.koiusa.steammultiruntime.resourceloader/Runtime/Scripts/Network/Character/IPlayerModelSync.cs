using Koiusa.SteamMultiRuntime.Network;

namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerModelSync
    {
        CharacterModelIdList ModelIdList { get; set; }
        int CurrentModelIndex { get; }
        void ApplyModelIndex(int index);
    }
}
