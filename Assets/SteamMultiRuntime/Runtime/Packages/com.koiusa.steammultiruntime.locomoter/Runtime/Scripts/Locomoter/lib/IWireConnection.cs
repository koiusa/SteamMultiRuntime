using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public enum WireConstraintMode
    {
        Elastic = 0,
        Rope = 1,
    }

    /// <summary>
    /// Contract for a wire-swing traversal feature. Consumers such as player
    /// controllers, AI, and network authority code can use this interface
    /// without depending on the concrete MonoBehaviour implementation.
    /// </summary>
    public interface IWireConnection
    {
        bool IsEnabled { get; }
        bool IsAttached { get; }
        Vector3 AnchorPoint { get; }
        Transform AnchorTransform { get; }
        float RopeLength { get; }
        float MinimumRopeLength { get; }
        float MaximumRopeLength { get; }
        Rigidbody Body { get; }
        Rigidbody AnchorBody { get; }
        WireConstraintMode ConstraintMode { get; }
        float ElasticStretchLimit { get; }

        void SetRopeLength(float value);
        void CaptureCurrentLength();
        void SetReplicatedState(bool isAttached, Vector3 anchorPoint, float ropeLength, Transform movingAnchor = null);
        void Attach(Vector3 worldPoint, Transform movingAnchor = null);
        void Detach();
    }

    public interface IWireAttachAction
    {
        bool IsEnabled { get; }
        void SetInput(bool held, bool fireRequested, WireAimResult aimResult);
        void DetachUntilInputRelease();
    }

    public interface IWireSwingAction
    {
        bool IsEnabled { get; }
        void SetMoveDirection(Vector3 moveDirection);
    }

    public interface IWireReelAction
    {
        bool IsEnabled { get; }
        bool IsReelingIn { get; }
        void SetInput(float reelInput);
        void ApplyReel(float deltaTime);
        void ReelStep();
    }

    public interface IWireGroundAction
    {
        bool IsEnabled { get; }
        bool BlocksSwing { get; }
        bool HandlesConnectionPhysics { get; }
        bool UsesStrafeMovement { get; }
        float StrafeBlend { get; }
        float FacingBlend { get; }
        float FacingRotationSpeed { get; }
        void SetMoveDirection(Vector3 moveDirection);
    }
}
