using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerMotor
    {
        bool IsGrounded { get; }
        bool IsAirborneFromJump { get; }
        bool IsJumping { get; }
        bool IsFallingAfterJump { get; }
        bool IsFreefall { get; }
        Vector3 InheritedGroundVelocity { get; }
        float HorizontalVelocity { get; }
        float VerticalVelocity { get; }

        PlayerMotorSettings GetSettings();
        void UpdateSettingsFromStruct(PlayerMotorSettings newSettings);
        void SetStrafeMode(bool enabled);

        void ResetState();
        void Tick(Vector3 moveDirection, bool jumpRequested);
        void OnCollisionEnter(Collision collision);
        void OnCollisionStay(Collision collision);
        void OnCollisionExit(Collision collision);
    }
}
