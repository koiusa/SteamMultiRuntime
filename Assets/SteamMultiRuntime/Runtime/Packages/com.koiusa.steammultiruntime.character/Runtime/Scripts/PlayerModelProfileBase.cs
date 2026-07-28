using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Character
{
    public abstract class PlayerModelProfileBase : MonoBehaviour, IRuntimeUserProfileModelSource
    {
        public abstract CharacterModelIdList ModelIdList { get; }
        public abstract int SelectedModelIndex { get; }
        public abstract void SetSelectedModel(int index);
        public abstract void ApplySelectedModel();
    }
}
