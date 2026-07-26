using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public enum ScreenAimTargetState
    {
        Invalid = 0,
        Obstructed = 1,
        Valid = 2,
    }

    public interface IScreenAimCursor
    {
        void SetPosition(Vector2 screenPosition);
        void SetVisible(bool visible);
        void SetTargetState(ScreenAimTargetState state);
    }
}
