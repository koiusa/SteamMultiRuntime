using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Core
{
    public interface IFallRecoveryMotionReset
    {
        void ResetAfterFallRecovery(Vector3 position, Quaternion rotation);
    }
}
