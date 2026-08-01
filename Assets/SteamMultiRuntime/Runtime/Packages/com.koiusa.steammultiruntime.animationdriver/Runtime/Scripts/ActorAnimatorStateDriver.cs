using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public enum ActorLocomotionAnimationMode
    {
        Grounded = 0,
        Airborne = 1,
        Ladder = 2,
        WallRun = 3,
        WireSwing = 4
    }

    public enum ActorAirAnimationState
    {
        None = 0,
        Rising = 1,
        Falling = 2
    }

    [DisallowMultipleComponent]
    public class ActorAnimatorStateDriver : MonoBehaviour, IActorAnimatorStateDriver
    {
        [Header("References")]
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Rigidbody targetRigidbody;

        public Animator TargetAnimator => targetAnimator;

        [Header("Animator Parameters")]
        [SerializeField] private string horizontalSpeedParameter = "Speed";
        [SerializeField] private string verticalSpeedParameter = "VerticalVelocity";
        [SerializeField] private string motionSpeedParameter = "MotionSpeed";
        [SerializeField] private string locomotionModeParameter = "LocomotionMode";
        [SerializeField] private string airStateParameter = "AirState";
        [SerializeField] private string wallRunSideParameter = "WallRunSide";

        [Header("Smoothing")]
        [SerializeField, Min(0f)] private float speedDampTime = 0.08f;
        [SerializeField, Min(0.0001f)] private float motionSpeedMultiplier = 2f;

        [Header("Animation Update LOD")]
        [SerializeField, Min(0f)] private float nearAnimationDistance = 12f;
        [SerializeField, Min(0f)] private float midAnimationDistance = 30f;
        [SerializeField, Min(0.1f)] private float nearAnimationUpdateRate = 30f;
        [SerializeField, Min(0.1f)] private float midAnimationUpdateRate = 15f;
        [SerializeField, Min(0.1f)] private float farAnimationUpdateRate = 5f;

        private readonly System.Collections.Generic.Dictionary<string, int> animatorParameterHashes = new();
        private RuntimeAnimatorController cachedAnimatorController;
        private IActorController playerController;
        private ILadderTraversalFeature ladderTraversalFeature;
        private IActorLadderState playerLadderState;
        private IActorWallRunState playerWallRunState;
        private IWallRunAction wallRunAction;
        private IActorTraversalCoordinator traversalCoordinator;
        private Renderer[] targetRenderers;
        private Vector3 previousPosition;
        private float scheduledDeltaTime;

        internal float NearAnimationDistance => Mathf.Max(0f, nearAnimationDistance);
        internal float MidAnimationDistance => Mathf.Max(NearAnimationDistance, midAnimationDistance);
        internal float NearAnimationUpdateInterval => 1f / Mathf.Max(0.1f, nearAnimationUpdateRate);
        internal float MidAnimationUpdateInterval => 1f / Mathf.Max(0.1f, midAnimationUpdateRate);
        internal float FarAnimationUpdateInterval => 1f / Mathf.Max(0.1f, farAnimationUpdateRate);
        internal bool IsPresentationVisible => IsAnyRendererVisible();
        public int LastScheduledFrame { get; private set; } = -1;

        private void Reset()
        {
            targetAnimator = GetComponent<Animator>();
            targetRigidbody = GetComponentInParent<Rigidbody>();
            CacheParameterHashes();
        }

        private void Awake()
        {

            if (targetAnimator == null)
            {
                targetAnimator = GetComponent<Animator>();
            }

            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponentInParent<Rigidbody>();
            }

            playerController = GetComponentInParent<IActorController>();
            ladderTraversalFeature = GetComponentInParent<ILadderTraversalFeature>();
            playerLadderState = playerController as IActorLadderState;
            playerWallRunState = playerController as IActorWallRunState;
            wallRunAction = GetComponentInParent<IWallRunAction>();
            traversalCoordinator = GetComponentInParent<IActorTraversalCoordinator>();
            targetRenderers = targetAnimator != null
                ? targetAnimator.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            if (targetAnimator != null)
                targetAnimator.keepAnimatorStateOnDisable = true;

            CacheParameterHashes();
            previousPosition = transform.position;
            ActorAnimatorUpdateScheduler.Register(this);
            enabled = false;
        }

        internal void TickScheduled(float deltaTime)
        {
            LastScheduledFrame = Time.frameCount;
            scheduledDeltaTime = Mathf.Max(0.0001f, deltaTime);
            UpdateAnimatorState(scheduledDeltaTime);
        }

        internal void TickFarScheduled(float deltaTime)
        {
            if (targetAnimator == null)
                return;
            SetScheduledAnimatorActive(true);
            TickScheduled(deltaTime);
            targetAnimator.Update(scheduledDeltaTime);
            SetScheduledAnimatorActive(false);
        }

        internal void SetScheduledAnimatorActive(bool active)
        {
            if (targetAnimator != null && targetAnimator.enabled != active)
                targetAnimator.enabled = active;
        }

        private void OnDestroy()
        {
            ActorAnimatorUpdateScheduler.Unregister(this);
        }

        private void UpdateAnimatorState(float deltaTime)
        {
            if (targetAnimator == null)
            {
                return;
            }

            // Animator culling stops pose evaluation, but this driver would still calculate and
            // write every parameter for every off-screen NPC. Resume with a clean velocity sample
            // when the renderer becomes visible again.
            if (!IsAnyRendererVisible())
            {
                previousPosition = transform.position;
                return;
            }

            var velocity = GetEstimatedVelocity();
            var upAxis = ActorMotor.GetUpAxis();

            var horizontalSpeed = playerController != null ? playerController.HorizontalVelocity : Vector3.ProjectOnPlane(velocity, upAxis).magnitude;
            var verticalSpeed = playerController != null ? playerController.VerticalVelocity : Vector3.Dot(velocity, upAxis);
            var isGrounded = playerController != null ? playerController.IsGrounded : true;
            // Gameplay keeps HorizontalVelocity planar, but grounded locomotion
            // animation should follow the distance actually travelled along a slope.
            var animationMoveSpeed = isGrounded
                ? Mathf.Sqrt(horizontalSpeed * horizontalSpeed + verticalSpeed * verticalSpeed)
                : horizontalSpeed;
            var motionSpeed = playerController != null && playerController.MaxMoveSpeed > Mathf.Epsilon
                ? animationMoveSpeed / playerController.MaxMoveSpeed * motionSpeedMultiplier
                : animationMoveSpeed * motionSpeedMultiplier;
            var isJumping = playerController != null && playerController.IsJumping;
            var isFreefall = playerController != null && playerController.IsFreefall;
            var isFallingAfterJump = playerController != null && playerController.IsFallingAfterJump;
            var hasTraversalCoordinator = traversalCoordinator != null;
            var isWireSwinging = hasTraversalCoordinator
                && traversalCoordinator.IsEnabled
                && traversalCoordinator.CurrentState == ActorTraversalState.WireSwing
                && !traversalCoordinator.IsWireGroundActionActive;
            var isLadder = playerLadderState != null
                ? playerLadderState.IsOnLadder
                : hasTraversalCoordinator
                    ? traversalCoordinator.IsEnabled && traversalCoordinator.CurrentState == ActorTraversalState.Ladder
                    : ladderTraversalFeature != null && ladderTraversalFeature.IsOnLadder;
            var ladderSpeed = playerLadderState != null
                ? playerLadderState.LadderSpeed
                : ladderTraversalFeature != null ? ladderTraversalFeature.ClimbSpeed : 0f;
            var isWallRunning = playerWallRunState != null
                ? playerWallRunState.IsWallRunning
                : hasTraversalCoordinator
                    ? traversalCoordinator.IsEnabled && traversalCoordinator.CurrentState == ActorTraversalState.WallRun
                    : wallRunAction != null && wallRunAction.IsWallRunning;
            var wallNormal = isWallRunning
                ? playerWallRunState != null
                    ? playerWallRunState.WallNormal
                    : wallRunAction != null ? wallRunAction.WallNormal : Vector3.zero
                : Vector3.zero;
            var useWallRunAnimation = isWallRunning;
            var locomotionMode = isLadder
                ? ActorLocomotionAnimationMode.Ladder
                : useWallRunAnimation
                ? ActorLocomotionAnimationMode.WallRun
                : isWireSwinging
                    ? ActorLocomotionAnimationMode.WireSwing
                : isGrounded && !isWireSwinging
                    ? ActorLocomotionAnimationMode.Grounded
                    : ActorLocomotionAnimationMode.Airborne;
            var wallRunSide = locomotionMode == ActorLocomotionAnimationMode.WallRun
                ? GetWallRunSide(wallNormal, upAxis)
                : 0;
            var usesAirAnimation = locomotionMode == ActorLocomotionAnimationMode.Airborne
                || locomotionMode == ActorLocomotionAnimationMode.WireSwing;
            var airState = !usesAirAnimation
                ? ActorAirAnimationState.None
                : isJumping ? ActorAirAnimationState.Rising
                : isFreefall || isFallingAfterJump
                    ? ActorAirAnimationState.Falling
                    : isWireSwinging
                        ? verticalSpeed > 0f
                            ? ActorAirAnimationState.Rising
                            : ActorAirAnimationState.Falling
                    : ActorAirAnimationState.None;
            var animationVerticalSpeed = isLadder ? ladderSpeed : verticalSpeed;

            SetFloat(horizontalSpeedParameter, animationMoveSpeed, speedDampTime, deltaTime);
            SetFloat(verticalSpeedParameter, animationVerticalSpeed);
            SetFloat(motionSpeedParameter, motionSpeed);
            SetInt(locomotionModeParameter, (int)locomotionMode);
            SetInt(airStateParameter, (int)airState);
            SetInt(wallRunSideParameter, wallRunSide);
        }

        private bool IsAnyRendererVisible()
        {
            // Keep updating when the model has no renderer so this optimization cannot
            // accidentally suppress non-visual animator users.
            if (targetRenderers == null || targetRenderers.Length == 0)
                return true;

            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var targetRenderer = targetRenderers[i];
                if (targetRenderer != null && targetRenderer.isVisible)
                    return true;
            }

            return false;
        }

        private int GetWallRunSide(Vector3 wallNormal, Vector3 upAxis)
        {
            var characterTransform = targetRigidbody != null ? targetRigidbody.transform : transform;
            var characterRight = Vector3.ProjectOnPlane(characterTransform.right, upAxis).normalized;
            if (characterRight.sqrMagnitude <= 0.0001f || wallNormal.sqrMagnitude <= 0.0001f)
            {
                return 0;
            }

            // The contact normal points from the wall toward the character.
            // A negative dot therefore means that the wall is on the right.
            return Vector3.Dot(wallNormal, characterRight) < 0f ? 1 : -1;
        }

        private Vector3 GetEstimatedVelocity()
        {
            if (targetRigidbody != null && !targetRigidbody.isKinematic)
            {
                return targetRigidbody.linearVelocity;
            }

            var deltaTime = Time.deltaTime;
            if (deltaTime <= Mathf.Epsilon)
            {
                previousPosition = transform.position;
                return Vector3.zero;
            }

            var currentPosition = transform.position;
            var velocity = (currentPosition - previousPosition) / deltaTime;
            previousPosition = currentPosition;
            return velocity;
        }

        public void SetTargetAnimator(Animator animator)
        {
            targetAnimator = animator;
            CacheParameterHashes();
        }

        private void OnValidate()
        {
            CacheParameterHashes();
        }

        private void CacheParameterHashes()
        {
            animatorParameterHashes.Clear();
            cachedAnimatorController = targetAnimator != null ? targetAnimator.runtimeAnimatorController : null;
            if (targetAnimator == null)
            {
                return;
            }

            if (cachedAnimatorController != targetAnimator.runtimeAnimatorController)
            {
                CacheParameterHashes();
            }

            foreach (var parameter in targetAnimator.parameters)
            {
                animatorParameterHashes[parameter.name] = parameter.nameHash;
            }
        }

        private void SetFloat(string parameterName, float value, float dampTime = 0f, float deltaTime = 0f)
        {
            if (!TryGetParameterHash(parameterName, out var hash))
            {
                return;
            }

            if (dampTime > 0f)
            {
                targetAnimator.SetFloat(hash, value, dampTime, deltaTime > 0f ? deltaTime : Time.deltaTime);
                return;
            }

            targetAnimator.SetFloat(hash, value);
        }

        private void SetInt(string parameterName, int value)
        {
            if (TryGetParameterHash(parameterName, out var hash))
            {
                targetAnimator.SetInteger(hash, value);
            }
        }

        private bool TryGetParameterHash(string parameterName, out int hash)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                hash = 0;
                return false;
            }

            return animatorParameterHashes.TryGetValue(parameterName, out hash);
        }
    }
}
