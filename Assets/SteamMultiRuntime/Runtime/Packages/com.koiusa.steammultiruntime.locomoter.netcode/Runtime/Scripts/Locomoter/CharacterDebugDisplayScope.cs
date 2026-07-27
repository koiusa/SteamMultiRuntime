using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public sealed class CharacterDebugDisplayState
    {
        public bool IsVisible { get; set; }
    }

    [DisallowMultipleComponent]
    public sealed class CharacterDebugDisplayScope : MonoBehaviour
    {
        private CharacterDebugDisplayState state;
        private CharacterDebugOverlay ownerOverlay;

        public bool IsVisible => state == null || state.IsVisible;

        public void Bind(CharacterDebugDisplayState displayState)
        {
            state = displayState;
        }

        public void SetOwnerOverlay(CharacterDebugOverlay overlay)
        {
            ownerOverlay = overlay;
        }

        public bool CanRender(CharacterDebugOverlay overlay)
        {
            return IsVisible && (ownerOverlay == null || ownerOverlay == overlay);
        }
    }
}
