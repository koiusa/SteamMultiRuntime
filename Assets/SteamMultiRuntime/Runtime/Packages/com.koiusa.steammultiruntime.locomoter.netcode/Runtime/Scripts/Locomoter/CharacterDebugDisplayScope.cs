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

        public bool IsVisible => state == null || state.IsVisible;

        public void Bind(CharacterDebugDisplayState displayState)
        {
            state = displayState;
        }
    }
}
