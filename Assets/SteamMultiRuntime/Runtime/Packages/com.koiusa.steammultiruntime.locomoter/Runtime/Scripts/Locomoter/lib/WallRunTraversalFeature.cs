using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class WallRunTraversalFeature : MonoBehaviour, IWallRunTraversalFeature, ITraversalSettingsSync
    {
        private const float ExitAwayInputDot = 0.25f;
        private const float EnterAlongWallInput = 0.65f;
        private const float MaintainAlongWallInput = 0.35f;

        [SerializeField] private WallRunTraversalSettings settings;

        private Rigidbody rb;
        private SlopeContactResolver slopeContactResolver;
        private ITraversalIntentContext traversalIntentContext;

        private int wallContactStreak;
        private bool wallRunGateClosed;
        private float wallRunInputReleaseUntilTime;
        private bool isInputReleaseGraceActive;
        private bool applyArcImpulse;

        public bool IsEnabled => isActiveAndEnabled;
        public bool IsWallRunning { get; private set; }
        public Vector3 WallNormal { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();
            traversalIntentContext = GetComponent<ITraversalIntentContext>();

            if (rb == null || slopeContactResolver == null)
            {
                Debug.LogError("WallRunTraversalFeature requires Rigidbody and SlopeContactResolver components.", this);
                enabled = false;
                return;
            }

            if (IsSettingsEmpty(settings))
            {
                settings = WallRunTraversalSettings.CreateDefault();
            }
        }

        private void OnValidate()
        {
            if (IsSettingsEmpty(settings))
            {
                settings = WallRunTraversalSettings.CreateDefault();
            }
        }

        public void WriteSettings(ref TraversalFeatureSettings traversalSettings)
        {
            traversalSettings.WallRun = settings;
        }

        public void ReadSettings(TraversalFeatureSettings traversalSettings)
        {
            settings = traversalSettings.WallRun;
        }

        public void ResetState()
        {
            IsWallRunning = false;
            WallNormal = Vector3.zero;
            wallContactStreak = 0;
            wallRunGateClosed = false;
            wallRunInputReleaseUntilTime = 0f;
            isInputReleaseGraceActive = false;
            applyArcImpulse = false;
        }

        public void NotifyWallJump()
        {
            IsWallRunning = false;
            WallNormal = Vector3.zero;
            wallContactStreak = 0;
            wallRunGateClosed = true;
            wallRunInputReleaseUntilTime = 0f;
            isInputReleaseGraceActive = false;
            applyArcImpulse = false;
        }

        public bool TryAccelerateOnWall(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, out Vector3 nextVelocity)
        {
            nextVelocity = velocity;
            var wasWallRunning = IsWallRunning;

            if (traversalIntentContext != null && traversalIntentContext.HasIntent(TraversalIntentFlags.JumpRequested))
            {
                IsWallRunning = false;
                WallNormal = Vector3.zero;
                wallContactStreak = 0;
                return false;
            }

            if (!TryGetWallNormal(upAxis, out var wallNormal))
            {
                IsWallRunning = false;
                WallNormal = Vector3.zero;
                wallContactStreak = 0;
                wallRunGateClosed = false;
                return false;
            }

            if (wallRunGateClosed)
            {
                IsWallRunning = false;
                WallNormal = Vector3.zero;
                return false;
            }

            if (!MeetsWallRunConditions(moveDirection, velocity, upAxis, wallNormal))
            {
                IsWallRunning = false;
                WallNormal = Vector3.zero;
                wallContactStreak = 0;
                return false;
            }

            if (!IsWallRunning)
            {
                wallContactStreak++;
                if (wallContactStreak < Mathf.Max(1, settings.WallRunStartContactFrames))
                {
                    return false;
                }
            }

            IsWallRunning = true;
            WallNormal = wallNormal;
            if (!wasWallRunning)
            {
                applyArcImpulse = true;
            }
            nextVelocity = AccelerateOnWall(velocity, moveDirection, upAxis, wallNormal);
            return true;
        }

        public Vector3 ApplyVerticalMotion(Vector3 velocity, Vector3 upAxis)
        {
            var verticalSpeed = Vector3.Dot(velocity, upAxis);
            var horizontalVelocity = Vector3.ProjectOnPlane(velocity, upAxis);
            var gravityDelta = Vector3.Dot(Physics.gravity, upAxis) * Time.fixedDeltaTime;
            float targetVerticalSpeed;

            switch (settings.VerticalMotionMode)
            {
                case WallRunVerticalMotionMode.MaintainHeight:
                    applyArcImpulse = false;
                    var holdAcceleration = settings.HeightHoldAcceleration > 0f
                        ? settings.HeightHoldAcceleration
                        : WallRunTraversalSettings.CreateDefault().HeightHoldAcceleration;
                    targetVerticalSpeed = Mathf.MoveTowards(verticalSpeed, 0f, holdAcceleration * Time.fixedDeltaTime);
                    break;

                case WallRunVerticalMotionMode.Gravity:
                    applyArcImpulse = false;
                    targetVerticalSpeed = verticalSpeed + gravityDelta * Mathf.Max(0f, settings.WallRunGravityMultiplier);
                    break;

                default:
                    if (applyArcImpulse)
                    {
                        var initialUpSpeed = settings.ArcInitialUpSpeed > 0f
                            ? settings.ArcInitialUpSpeed
                            : WallRunTraversalSettings.CreateDefault().ArcInitialUpSpeed;
                        verticalSpeed = Mathf.Max(verticalSpeed, initialUpSpeed);
                        applyArcImpulse = false;
                    }

                    var arcGravityMultiplier = settings.ArcGravityMultiplier > 0f
                        ? settings.ArcGravityMultiplier
                        : WallRunTraversalSettings.CreateDefault().ArcGravityMultiplier;
                    targetVerticalSpeed = verticalSpeed + gravityDelta * arcGravityMultiplier;
                    break;
            }

            targetVerticalSpeed = Mathf.Max(targetVerticalSpeed, -settings.WallRunMaxFallSpeed);

            // Rigidbody gravity is integrated after FixedUpdate. Offset the standard
            // gravity step here so the selected mode describes the final velocity.
            var prePhysicsVerticalSpeed = targetVerticalSpeed - gravityDelta;
            return horizontalVelocity + upAxis * prePhysicsVerticalSpeed;
        }

        private bool MeetsWallRunConditions(Vector3 moveDirection, Vector3 velocity, Vector3 upAxis, Vector3 wallNormal)
        {
            if (IsMovingAwayFromWall(velocity, wallNormal))
            {
                return false;
            }

            if (IsMoveInputAwayFromWall(moveDirection, upAxis, wallNormal))
            {
                return false;
            }

            if (!HasWallRunIntent(moveDirection, upAxis, wallNormal))
            {
                if (!IsWallRunning)
                {
                    return false;
                }

                var graceTime = GetInputReleaseGraceTime();
                if (!isInputReleaseGraceActive)
                {
                    isInputReleaseGraceActive = true;
                    wallRunInputReleaseUntilTime = Time.time + graceTime;
                }

                if (Time.time > wallRunInputReleaseUntilTime)
                {
                    return false;
                }
            }
            else
            {
                wallRunInputReleaseUntilTime = 0f;
                isInputReleaseGraceActive = false;
            }

            return true;
        }

        private bool IsMovingAwayFromWall(Vector3 velocity, Vector3 wallNormal)
        {
            if (settings.WallRunAwayFromWallMinSpeed <= 0f)
            {
                return false;
            }

            var awaySpeed = Vector3.Dot(velocity, wallNormal);
            return awaySpeed > settings.WallRunAwayFromWallMinSpeed;
        }

        private bool HasWallRunIntent(Vector3 moveDirection, Vector3 upAxis, Vector3 wallNormal)
        {
            var horizontalInput = Vector3.ProjectOnPlane(moveDirection, upAxis);
            if (horizontalInput.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            var alongWallInput = Vector3.ProjectOnPlane(horizontalInput.normalized, wallNormal).magnitude;
            // Camera-relative movement picks up a small wall-tangent component whenever
            // the view rotates. Require a deliberate angle to enter, then use a lower
            // threshold while already running to avoid state flicker.
            var configuredThreshold = Mathf.Clamp01(settings.WallRunMinInputDot);
            var threshold = IsWallRunning
                ? Mathf.Max(configuredThreshold, MaintainAlongWallInput)
                : Mathf.Max(configuredThreshold, EnterAlongWallInput);
            return alongWallInput >= threshold;
        }

        private static bool IsMoveInputAwayFromWall(Vector3 moveDirection, Vector3 upAxis, Vector3 wallNormal)
        {
            var horizontalInput = Vector3.ProjectOnPlane(moveDirection, upAxis);
            return horizontalInput.sqrMagnitude > 0.0001f
                && Vector3.Dot(horizontalInput.normalized, wallNormal) > ExitAwayInputDot;
        }

        private float GetInputReleaseGraceTime()
        {
            // 既存データ（新規フィールド未保存）互換: 0以下ならデフォルト値を使う
            return settings.WallRunInputReleaseGraceTime > 0f
                ? settings.WallRunInputReleaseGraceTime
                : WallRunTraversalSettings.CreateDefault().WallRunInputReleaseGraceTime;
        }

        private bool TryGetWallNormal(Vector3 upAxis, out Vector3 wallNormal)
        {
            return slopeContactResolver.TryGetObstacleNormal(upAxis, settings.WallMaxUpDot, out wallNormal);
        }

        private Vector3 AccelerateOnWall(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, Vector3 wallNormal)
        {
            var verticalSpeed = Vector3.Dot(velocity, upAxis);
            var horizontalVelocity = Vector3.ProjectOnPlane(velocity, upAxis);
            var wallTangentVelocity = Vector3.ProjectOnPlane(horizontalVelocity, wallNormal);
            var horizontalWallDirection = Vector3.ProjectOnPlane(moveDirection, upAxis);
            var fallbackDirection = Vector3.Cross(upAxis, wallNormal).normalized;
            if (fallbackDirection.sqrMagnitude <= 0.0001f)
            {
                fallbackDirection = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, upAxis).normalized;
            }

            var alongWallDirection = Vector3.ProjectOnPlane(horizontalWallDirection, wallNormal);
            if (alongWallDirection.sqrMagnitude <= 0.0001f)
            {
                alongWallDirection = fallbackDirection;
            }
            else
            {
                alongWallDirection = alongWallDirection.normalized;
            }

            var targetWallVelocity = alongWallDirection * settings.WallRunSpeed;
            var nextWallVelocity = Vector3.MoveTowards(wallTangentVelocity, targetWallVelocity, settings.WallRunAcceleration * Time.fixedDeltaTime);
            return nextWallVelocity + upAxis * verticalSpeed;
        }

        private static bool IsSettingsEmpty(WallRunTraversalSettings s)
        {
            return s.WallRunSpeed == 0f && s.WallRunAcceleration == 0f;
        }
    }
}
