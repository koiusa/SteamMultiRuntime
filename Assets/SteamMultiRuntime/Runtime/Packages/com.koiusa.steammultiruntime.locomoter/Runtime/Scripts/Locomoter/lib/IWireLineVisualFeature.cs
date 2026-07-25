using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IWireLineVisualFeature
    {
        bool IsEnabled { get; }

        void Initialize();
        void SetVisible(bool visible);
        void UpdateEndpoints(Vector3 anchorPoint);
    }
}
