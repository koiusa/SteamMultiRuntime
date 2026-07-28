using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IFootstepReceiver
    {
        void PlayFootstep(Vector3 worldPosition);
        void PlayLand(Vector3 worldPosition);
    }
}
