using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IScreenAimCursor
    {
        void SetPosition(Vector2 screenPosition);
        void SetVisible(bool visible);
    }
}
