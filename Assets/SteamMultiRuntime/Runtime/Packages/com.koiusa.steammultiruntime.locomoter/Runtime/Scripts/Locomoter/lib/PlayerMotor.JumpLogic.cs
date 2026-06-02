using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public static class PlayerMotorJumpLogic
    {
        public readonly struct JumpUpdateResult
        {
            public JumpUpdateResult(Vector3 velocity, float jumpDetachUntilTime, Vector3 inheritedGroundVelocity, bool isAirborneFromJump)
            {
                Velocity = velocity;
                JumpDetachUntilTime = jumpDetachUntilTime;
                InheritedGroundVelocity = inheritedGroundVelocity;
                IsAirborneFromJump = isAirborneFromJump;
            }

            public Vector3 Velocity { get; }
            public float JumpDetachUntilTime { get; }
            public Vector3 InheritedGroundVelocity { get; }
            public bool IsAirborneFromJump { get; }
        }

        public static JumpUpdateResult ApplyJumpIfRequested(
            bool jumpRequested,
            bool canJump,
            Vector3 upAxis,
            float jumpForce,
            float jumpDetachDuration,
            Vector3 groundVelocity,
            SlopeContactResolver slopeContactResolver,
            GroundMotionTracker groundMotionTracker,
            Vector3 velocity,
            float jumpDetachUntilTime,
            Vector3 inheritedGroundVelocity,
            bool isAirborneFromJump)
        {
            if (!jumpRequested || !canJump)
            {
                return new JumpUpdateResult(velocity, jumpDetachUntilTime, inheritedGroundVelocity, isAirborneFromJump);
            }

            inheritedGroundVelocity = groundVelocity;
            jumpDetachUntilTime = Time.time + jumpDetachDuration;
            velocity -= Vector3.Project(velocity, upAxis);
            velocity += upAxis * jumpForce;
            velocity += inheritedGroundVelocity;
            slopeContactResolver.Clear();
            groundMotionTracker.ClearGroundContacts();
            isAirborneFromJump = true;

            return new JumpUpdateResult(velocity, jumpDetachUntilTime, inheritedGroundVelocity, isAirborneFromJump);
        }

        public static Vector3 ApplyExtraFallGravity(
            bool isAirborne,
            bool isOnSteepSlope,
            Vector3 upAxis,
            float fallMultiplier,
            Vector3 velocity)
        {
            if (!isAirborne || isOnSteepSlope || Vector3.Dot(velocity, upAxis) >= 0f)
            {
                return velocity;
            }

            return velocity + Physics.gravity * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }
}
