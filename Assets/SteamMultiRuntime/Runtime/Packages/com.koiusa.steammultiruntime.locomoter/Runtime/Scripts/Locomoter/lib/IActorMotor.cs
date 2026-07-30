using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public readonly struct ActorMotorTickResult
    {
        public ActorMotorTickResult(bool jumpConsumed)
        {
            JumpConsumed = jumpConsumed;
        }

        public bool JumpConsumed { get; }
    }

    public interface IActorMotor
    {
        bool IsEnabled { get; }
        bool IsGrounded { get; }
        bool IsAirborneFromJump { get; }
        bool IsJumping { get; }
        bool IsFallingAfterJump { get; }
        bool IsFreefall { get; }
        Vector3 InheritedGroundVelocity { get; }
        float HorizontalVelocity { get; }
        float VerticalVelocity { get; }

        ActorMotorSettings GetSettings();
        void ApplySettings(ActorMotorSettings newSettings);
        void SetStrafeMode(bool enabled);
        void SetFacingRequest(ActorFacingRequest request);

        void ResetState();
        ActorMotorTickResult Tick(Vector3 moveDirection, bool jumpRequested);
    }
}
