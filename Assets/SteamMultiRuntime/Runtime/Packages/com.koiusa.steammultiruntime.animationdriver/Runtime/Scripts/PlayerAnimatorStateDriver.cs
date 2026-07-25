using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public enum PlayerLocomotionAnimationMode
    {
        Grounded = 0,
        Airborne = 1,
        Ladder = 2,
        WallRun = 3
    }

    public enum PlayerAirAnimationState
    {
        None = 0,
        Rising = 1,
        Falling = 2
    }

    [DisallowMultipleComponent]
    public class PlayerAnimatorStateDriver : MonoBehaviour, IPlayerAnimatorStateDriver
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

        private readonly System.Collections.Generic.Dictionary<string, int> animatorParameterHashes = new();
        private RuntimeAnimatorController cachedAnimatorController;
        private IPlayerController playerController;
        private ILadderTraversalFeature ladderTraversalFeature;
        private IPlayerLadderState playerLadderState;
        private IPlayerWallRunState playerWallRunState;
        private IWallRunTraversalFeature wallRunTraversalFeature;
        private IPlayerTraversalCoordinator traversalCoordinator;
        private Vector3 previousPosition;

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

            playerController = GetComponentInParent<IPlayerController>();
            ladderTraversalFeature = GetComponentInParent<ILadderTraversalFeature>();
            playerLadderState = playerController as IPlayerLadderState;
            playerWallRunState = playerController as IPlayerWallRunState;
            wallRunTraversalFeature = GetComponentInParent<IWallRunTraversalFeature>();
            traversalCoordinator = GetComponentInParent<IPlayerTraversalCoordinator>();

            CacheParameterHashes();
            previousPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (targetAnimator == null)
            {
                return;
            }

            var velocity = GetEstimatedVelocity();
            var upAxis = PlayerMotor.GetUpAxis();

            var horizontalSpeed = playerController != null ? playerController.HorizontalVelocity : Vector3.ProjectOnPlane(velocity, upAxis).magnitude;
            var verticalSpeed = playerController != null ? playerController.VerticalVelocity : Vector3.Dot(velocity, upAxis);
            var isGrounded = playerController != null ? playerController.IsGrounded : true;
            var motionSpeed = playerController != null && playerController.MaxMoveSpeed > Mathf.Epsilon
                ? horizontalSpeed / playerController.MaxMoveSpeed * motionSpeedMultiplier
                : horizontalSpeed * motionSpeedMultiplier;
            var isJumping = playerController != null && playerController.IsJumping;
            var isFreefall = playerController != null && playerController.IsFreefall;
            var isFallingAfterJump = playerController != null && playerController.IsFallingAfterJump;
            var hasTraversalCoordinator = traversalCoordinator != null;
            var isLadder = playerLadderState != null
                ? playerLadderState.IsOnLadder
                : hasTraversalCoordinator
                    ? traversalCoordinator.IsEnabled && traversalCoordinator.CurrentState == PlayerTraversalState.Ladder
                    : ladderTraversalFeature != null && ladderTraversalFeature.IsOnLadder;
            var ladderSpeed = playerLadderState != null
                ? playerLadderState.LadderSpeed
                : ladderTraversalFeature != null ? ladderTraversalFeature.ClimbSpeed : 0f;
            var isWallRunning = playerWallRunState != null
                ? playerWallRunState.IsWallRunning
                : hasTraversalCoordinator
                    ? traversalCoordinator.IsEnabled && traversalCoordinator.CurrentState == PlayerTraversalState.WallRun
                    : wallRunTraversalFeature != null && wallRunTraversalFeature.IsWallRunning;
            var wallNormal = isWallRunning
                ? playerWallRunState != null
                    ? playerWallRunState.WallNormal
                    : wallRunTraversalFeature != null ? wallRunTraversalFeature.WallNormal : Vector3.zero
                : Vector3.zero;
            var useWallRunAnimation = isWallRunning;
            var locomotionMode = isLadder
                ? PlayerLocomotionAnimationMode.Ladder
                : useWallRunAnimation
                ? PlayerLocomotionAnimationMode.WallRun
                : isGrounded ? PlayerLocomotionAnimationMode.Grounded : PlayerLocomotionAnimationMode.Airborne;
            var wallRunSide = locomotionMode == PlayerLocomotionAnimationMode.WallRun
                ? GetWallRunSide(wallNormal, upAxis)
                : 0;
            var airState = locomotionMode != PlayerLocomotionAnimationMode.Airborne
                ? PlayerAirAnimationState.None
                : isJumping ? PlayerAirAnimationState.Rising
                : isFreefall || isFallingAfterJump
                    ? PlayerAirAnimationState.Falling
                    : PlayerAirAnimationState.None;
            var animationVerticalSpeed = isLadder ? ladderSpeed : verticalSpeed;

            SetFloat(horizontalSpeedParameter, horizontalSpeed, speedDampTime);
            SetFloat(verticalSpeedParameter, animationVerticalSpeed);
            SetFloat(motionSpeedParameter, motionSpeed);
            SetInt(locomotionModeParameter, (int)locomotionMode);
            SetInt(airStateParameter, (int)airState);
            SetInt(wallRunSideParameter, wallRunSide);
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

        private void SetFloat(string parameterName, float value, float dampTime = 0f)
        {
            if (!TryGetParameterHash(parameterName, out var hash))
            {
                return;
            }

            if (dampTime > 0f)
            {
                targetAnimator.SetFloat(hash, value, dampTime, Time.deltaTime);
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
