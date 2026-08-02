using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IGroundMotionSource
    {
        Vector3 GetPointVelocity(Vector3 samplePoint);
        Vector3 GetPointDisplacement(Vector3 samplePoint, float deltaTime);
        Quaternion GetRotationDelta(float deltaTime);
    }

    /// <summary>
    /// Optional fast path for motion sources that can return a coherent fixed-tick
    /// sample without recalculating their motion for every requested value.
    /// </summary>
    public interface IGroundMotionSnapshotSource
    {
        void GetGroundMotion(
            Vector3 samplePoint,
            float deltaTime,
            out Vector3 pointVelocity,
            out Vector3 pointDisplacement,
            out Quaternion rotationDelta);
    }

    /// <summary>
    /// Optional push notification for snapshot sources that apply a discrete physics
    /// pose. Kinematic followers can subscribe only while bound to the source.
    /// </summary>
    public interface IGroundMotionPhysicsPoseSource : IGroundMotionSnapshotSource
    {
        event System.Action<IGroundMotionPhysicsPoseSource, float> PhysicsPoseApplied;
        Collider[] InteractionColliders { get; }
    }
}
