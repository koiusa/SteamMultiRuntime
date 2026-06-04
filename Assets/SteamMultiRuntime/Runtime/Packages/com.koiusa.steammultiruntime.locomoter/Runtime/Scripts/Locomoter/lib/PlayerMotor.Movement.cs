using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// プレイヤー移動に関する速度計算と向き計算をまとめたユーティリティです。
    /// 地上・急斜面・空中での加速処理と、接地補助（段差アシスト）を提供します。
    /// </summary>
    public static class PlayerMotorMovementLogic
    {
        public static Vector3 AccelerateOnGround(
            Vector3 velocity,
            Vector3 moveDirection,
            Vector3 upAxis,
            SlopeContactResolver slopeContactResolver,
            PlayerMotorSettings settings,
            bool forcedStrafeMode)
        {
            var groundNormal = slopeContactResolver.GetGroundNormal(upAxis);
            var currentSurfaceVelocity = Vector3.ProjectOnPlane(velocity, groundNormal);

            var strafeInfo = DetectStrafeMovement(moveDirection, forcedStrafeMode);
            var targetSpeed = strafeInfo.isStrafing ? settings.MoveSpeed * settings.StrafeMoveSpeedMultiplier : settings.MoveSpeed;
            var acceleration = strafeInfo.isStrafing ? settings.GroundAcceleration * settings.StrafeAccelerationMultiplier : settings.GroundAcceleration;

            var targetSurfaceVelocity = Vector3.ProjectOnPlane(moveDirection * targetSpeed, groundNormal);
            targetSurfaceVelocity = slopeContactResolver.ConstrainHorizontalVelocity(targetSurfaceVelocity, upAxis, settings.MinGroundNormalDot);
            var nextSurfaceVelocity = Vector3.MoveTowards(currentSurfaceVelocity, targetSurfaceVelocity, acceleration * Time.fixedDeltaTime);
            nextSurfaceVelocity = slopeContactResolver.ConstrainHorizontalVelocity(nextSurfaceVelocity, upAxis, settings.MinGroundNormalDot);
            var normalVelocity = Vector3.Project(velocity, groundNormal);
            return normalVelocity + nextSurfaceVelocity;
        }

        public static Vector3 AccelerateOnSteepSlope(
            Vector3 velocity,
            Vector3 moveDirection,
            Vector3 upAxis,
            SlopeContactResolver slopeContactResolver,
            PlayerMotorSettings settings,
            bool forcedStrafeMode)
        {
            var slopeNormal = slopeContactResolver.GetSteepSlopeNormal(upAxis);
            var currentSlopeVelocity = Vector3.ProjectOnPlane(velocity, slopeNormal);
            var strafeInfo = DetectStrafeMovement(moveDirection, forcedStrafeMode);
            var targetSpeed = strafeInfo.isStrafing ? settings.MoveSpeed * settings.StrafeMoveSpeedMultiplier : settings.MoveSpeed;
            var targetSlopeVelocity = Vector3.ProjectOnPlane(moveDirection * targetSpeed, slopeNormal);
            targetSlopeVelocity = slopeContactResolver.ConstrainHorizontalVelocity(targetSlopeVelocity, upAxis, settings.MinGroundNormalDot);
            var slideAcceleration = Vector3.ProjectOnPlane(Physics.gravity, slopeNormal);

            if (slideAcceleration.sqrMagnitude > 0f)
            {
                var slideDirection = slideAcceleration.normalized;
                var uphillSpeed = Vector3.Dot(targetSlopeVelocity, -slideDirection);
                if (uphillSpeed > 0f)
                {
                    targetSlopeVelocity += slideDirection * uphillSpeed;
                }
            }

            var nextSlopeVelocity = Vector3.MoveTowards(currentSlopeVelocity, targetSlopeVelocity, settings.AirAcceleration * Time.fixedDeltaTime);
            nextSlopeVelocity = slopeContactResolver.ConstrainHorizontalVelocity(nextSlopeVelocity, upAxis, settings.MinGroundNormalDot);
            var normalVelocity = Vector3.Project(velocity, slopeNormal);
            return normalVelocity + nextSlopeVelocity + slideAcceleration * Time.fixedDeltaTime;
        }

        public static Vector3 AccelerateInAir(
            Vector3 velocity,
            Vector3 moveDirection,
            Vector3 upAxis,
            Vector3 inheritedGroundVelocity,
            SlopeContactResolver slopeContactResolver,
            PlayerMotorSettings settings,
            bool forcedStrafeMode)
        {
            var currentRelativeHorizontalVelocity = Vector3.ProjectOnPlane(velocity - inheritedGroundVelocity, upAxis);
            currentRelativeHorizontalVelocity = slopeContactResolver.ConstrainHorizontalVelocity(currentRelativeHorizontalVelocity, upAxis, settings.MinGroundNormalDot);
            var strafeInfo = DetectStrafeMovement(moveDirection, forcedStrafeMode);
            var targetSpeed = strafeInfo.isStrafing ? settings.MoveSpeed * settings.StrafeMoveSpeedMultiplier : settings.MoveSpeed;
            var targetHorizontalVelocity = slopeContactResolver.ConstrainHorizontalVelocity(moveDirection * targetSpeed, upAxis, settings.MinGroundNormalDot);
            var nextRelativeHorizontalVelocity = Vector3.MoveTowards(currentRelativeHorizontalVelocity, targetHorizontalVelocity, settings.AirAcceleration * Time.fixedDeltaTime);
            nextRelativeHorizontalVelocity = slopeContactResolver.ConstrainHorizontalVelocity(nextRelativeHorizontalVelocity, upAxis, settings.MinGroundNormalDot);
            return velocity + (nextRelativeHorizontalVelocity - currentRelativeHorizontalVelocity);
        }

        public static Quaternion CalculateRotation(
            Quaternion currentRotation,
            Vector3 moveDirection,
            Vector3 upAxis,
            Quaternion groundRotationDelta,
            PlayerMotorSettings settings,
            bool forcedStrafeMode)
        {
            var rotatedForward = groundRotationDelta * (currentRotation * Vector3.forward);
            var flattenedForward = Vector3.ProjectOnPlane(rotatedForward, upAxis);
            if (flattenedForward.sqrMagnitude <= 0.0001f)
            {
                flattenedForward = Vector3.ProjectOnPlane(currentRotation * Vector3.forward, upAxis);
            }

            var baseRotation = flattenedForward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(flattenedForward.normalized, upAxis)
                : currentRotation;

            var strafeInfo = DetectStrafeMovement(moveDirection, forcedStrafeMode);
            var facingDirection = GetFacingDirection(moveDirection, upAxis);
            if (facingDirection.sqrMagnitude <= 0.0001f)
            {
                return baseRotation;
            }

            var targetRotation = Quaternion.LookRotation(facingDirection, upAxis);
            var rotationSpeed = strafeInfo.isStrafing && settings.StrafeRotationSpeed > 0f
                ? settings.StrafeRotationSpeed
                : (!strafeInfo.isStrafing ? settings.RotationSpeed : 0f);

            if (rotationSpeed <= 0f)
            {
                return baseRotation;
            }

            return Quaternion.RotateTowards(baseRotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        public static Vector3 ApplyGroundStepAssist(
            Vector3 velocity,
            Vector3 moveDirection,
            Vector3 upAxis,
            Rigidbody rb,
            Collider bodyCollider,
            SlopeContactResolver slopeContactResolver,
            PlayerMotorSettings settings)
        {
            if (!settings.EnableStepAssist || settings.StepAssistMaxHeight <= 0f || rb == null || bodyCollider == null)
            {
                return velocity;
            }

            var horizontalMove = Vector3.ProjectOnPlane(moveDirection, upAxis);
            if (horizontalMove.sqrMagnitude <= 0.0001f)
            {
                return velocity;
            }

            var horizontalVelocity = Vector3.ProjectOnPlane(velocity, upAxis);
            if (horizontalVelocity.magnitude < settings.StepAssistMinMoveSpeed)
            {
                return velocity;
            }

            var bounds = bodyCollider.bounds;
            var upExtent = Mathf.Abs(upAxis.x) * bounds.extents.x + Mathf.Abs(upAxis.y) * bounds.extents.y + Mathf.Abs(upAxis.z) * bounds.extents.z;
            var feetPoint = bounds.center - upAxis * upExtent;
            var lowOrigin = feetPoint + upAxis * 0.05f;
            var highOrigin = lowOrigin + upAxis * settings.StepAssistMaxHeight;
            var checkDirection = horizontalMove.normalized;
            var checkDistance = Mathf.Max(0.05f, settings.StepAssistCheckDistance);

            var horizontalExtents = bounds.extents - new Vector3(Mathf.Abs(upAxis.x) * bounds.extents.x, Mathf.Abs(upAxis.y) * bounds.extents.y, Mathf.Abs(upAxis.z) * bounds.extents.z);
            var probeRadius = Mathf.Max(0.05f, Mathf.Max(horizontalExtents.x, Mathf.Max(horizontalExtents.y, horizontalExtents.z)) * 0.6f);

            if (!Physics.SphereCast(lowOrigin, probeRadius, checkDirection, out var lowHit, checkDistance, settings.GroundLayer, QueryTriggerInteraction.Ignore))
            {
                return velocity;
            }

            var lowHitUpDot = Mathf.Abs(Vector3.Dot(lowHit.normal, upAxis));
            if (lowHitUpDot > settings.StepAssistObstacleUpDot)
            {
                return velocity;
            }

            var moveIntoObstacle = Vector3.Dot(checkDirection, lowHit.normal) < -0.05f;
            if (!moveIntoObstacle)
            {
                return velocity;
            }

            if (Physics.SphereCast(highOrigin, probeRadius, checkDirection, out _, checkDistance, settings.GroundLayer, QueryTriggerInteraction.Ignore))
            {
                return velocity;
            }

            var downStart = highOrigin + checkDirection * checkDistance + upAxis * 0.05f;
            if (!Physics.Raycast(downStart, -upAxis, out var downHit, settings.StepAssistMaxHeight + 0.25f, settings.GroundLayer, QueryTriggerInteraction.Ignore))
            {
                return velocity;
            }

            var stepHeight = Vector3.Dot(downHit.point - feetPoint, upAxis);
            if (stepHeight <= 0.01f || stepHeight > settings.StepAssistMaxHeight)
            {
                return velocity;
            }

            rb.MovePosition(rb.position + upAxis * stepHeight);
            var verticalSpeed = Vector3.Dot(velocity, upAxis);
            if (verticalSpeed < 0f)
            {
                velocity -= upAxis * verticalSpeed;
            }

            return velocity;
        }

        private struct StrafeInfo
        {
            public bool isStrafing;
            public bool isBackward;
        }

        private static StrafeInfo DetectStrafeMovement(Vector3 moveDirection, bool forcedStrafeMode)
        {
            if (moveDirection.sqrMagnitude < 0.0001f)
            {
                return new StrafeInfo { isStrafing = false, isBackward = false };
            }

            return new StrafeInfo { isStrafing = forcedStrafeMode, isBackward = false };
        }

        private static Vector3 GetFacingDirection(Vector3 moveDirection, Vector3 upAxis)
        {
            if (moveDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            return Vector3.ProjectOnPlane(moveDirection, upAxis).normalized;
        }
    }
}
