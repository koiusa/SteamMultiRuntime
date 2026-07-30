using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public readonly struct PlayerMotorTickResult
    {
        public PlayerMotorTickResult(bool jumpConsumed)
        {
            JumpConsumed = jumpConsumed;
        }

        public bool JumpConsumed { get; }
    }

    public interface IPlayerMotor
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

        PlayerMotorSettings GetSettings();
        void ApplySettings(PlayerMotorSettings newSettings);
        void SetStrafeMode(bool enabled);
        void SetFacingRequest(PlayerFacingRequest request);

        void ResetState();
        PlayerMotorTickResult Tick(Vector3 moveDirection, bool jumpRequested);
    }
}
