using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Core
{
    public interface ISpawnPoseAppliedReceiver
    {
        void OnSpawnPoseApplied(Vector3 position, Quaternion rotation);
    }
}
