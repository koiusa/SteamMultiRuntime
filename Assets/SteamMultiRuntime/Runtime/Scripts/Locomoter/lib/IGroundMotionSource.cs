using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IGroundMotionSource
    {
        Vector3 GetPointVelocity(Vector3 samplePoint);
        Vector3 GetPointDisplacement(Vector3 samplePoint, float deltaTime);
        Quaternion GetRotationDelta(float deltaTime);
    }
}
