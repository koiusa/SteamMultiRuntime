using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
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
        float RopeLength { get; }
        float MinimumRopeLength { get; }
        float MaximumRopeLength { get; }
        Rigidbody Body { get; }
        Rigidbody AnchorBody { get; }

        void SetRopeLength(float value);
        void SetReplicatedState(bool isAttached, Vector3 anchorPoint, float ropeLength);
        void Attach(Vector3 worldPoint, Transform movingAnchor = null);
        void Detach();
    }

    public interface IWireAttachAction
    {
        bool IsEnabled { get; }
        void SetInput(bool held, Vector3 origin, Vector3 aimDirection);
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
        void SetInput(float reelInput);
        void ReelStep();
    }

    public interface IWireGroundAction
    {
        bool IsEnabled { get; }
        bool BlocksSwing { get; }
        bool HandlesConnectionPhysics { get; }
        bool UsesStrafeMovement { get; }
        bool UsesMaximumRangeConstraint { get; }
        float FacingRotationSpeed { get; }
        void SetMoveDirection(Vector3 moveDirection);
    }
}
