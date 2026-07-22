using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
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
        [SerializeField] private string groundedParameter = "IsGrounded";
        [SerializeField] private string motionSpeedParameter = "MotionSpeed";
        [SerializeField] private string jumpingParameter = "IsJumping";
        [SerializeField] private string freefallParameter = "IsFreefall";
        [SerializeField] private string fallingAfterJumpParameter = "IsFallingAfterJump";
        [SerializeField] private string inputForwardParameter = "InputForward";
        [SerializeField] private string inputRightParameter = "InputRight";
        [SerializeField] private string moveDirectionForwardParameter = "MoveDirectionForward";
        [SerializeField] private string moveDirectionRightParameter = "MoveDirectionRight";
        [SerializeField] private string strafeModeParameter = "IsStrafeMode";
        [SerializeField] private string ladderParameter = "IsLadder";
        [SerializeField] private string ladderSpeedParameter = "LadderSpeed";
        [SerializeField] private string animationFinishedParameter = "IsAnimationFinished";
        [SerializeField, Min(0)] private int animationFinishedLayerIndex = 0;

        [Header("Smoothing")]
        [SerializeField, Min(0f)] private float speedDampTime = 0.08f;
        [SerializeField, Min(0.0001f)] private float motionSpeedMultiplier = 2f;

        private int horizontalSpeedHash;
        private int verticalSpeedHash;
        private int groundedHash;
        private int motionSpeedHash;
        private int jumpingHash;
        private int freefallHash;
        private int fallingAfterJumpHash;
        private int inputForwardHash;
        private int inputRightHash;
        private int moveDirectionForwardHash;
        private int moveDirectionRightHash;
        private int strafeModeHash;
        private int ladderHash;
        private int ladderSpeedHash;
        private int animationFinishedHash;
        private bool hasHorizontalSpeedParameter;
        private bool hasVerticalSpeedParameter;
        private bool hasGroundedParameter;
        private bool hasMotionSpeedParameter;
        private bool hasJumpingParameter;
        private bool hasFreefallParameter;
        private bool hasFallingAfterJumpParameter;
        private bool hasInputForwardParameter;
        private bool hasInputRightParameter;
        private bool hasMoveDirectionForwardParameter;
        private bool hasMoveDirectionRightParameter;
        private bool hasStrafeModeParameter;
        private bool hasLadderParameter;
        private bool hasLadderSpeedParameter;
        private bool hasAnimationFinishedParameter;
        private IPlayerController playerController;
        private ILadderTraversalFeature ladderTraversalFeature;
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
            var moveInput = playerController != null ? playerController.MoveInput : Vector2.zero;
            var inputForward = moveInput.y;
            var inputRight = moveInput.x;
            var moveDirection = playerController != null ? playerController.MoveDirection : Vector3.zero;
            var moveDirectionForward = Vector3.Dot(moveDirection, transform.forward);
            var moveDirectionRight = Vector3.Dot(moveDirection, transform.right);
            var isStrafeMode = playerController != null && playerController.IsStrafeMode;
            var isLadder = ladderTraversalFeature != null && ladderTraversalFeature.IsOnLadder;
            var ladderSpeed = ladderTraversalFeature != null ? ladderTraversalFeature.ClimbSpeed : 0f;
            var isAnimationFinished = IsCurrentStateFinished(animationFinishedLayerIndex);

            if (hasHorizontalSpeedParameter)
            {
                targetAnimator.SetFloat(horizontalSpeedHash, horizontalSpeed, speedDampTime, Time.deltaTime);
            }

            if (hasVerticalSpeedParameter)
            {
                targetAnimator.SetFloat(verticalSpeedHash, verticalSpeed);
            }

            if (hasGroundedParameter)
            {
                targetAnimator.SetBool(groundedHash, isGrounded);
            }

            if (hasMotionSpeedParameter)
            {
                targetAnimator.SetFloat(motionSpeedHash, motionSpeed);
            }

            if (hasJumpingParameter)
            {
                targetAnimator.SetBool(jumpingHash, isJumping);
            }

            if (hasFreefallParameter)
            {
                targetAnimator.SetBool(freefallHash, isFreefall);
            }

            if (hasFallingAfterJumpParameter)
            {
                targetAnimator.SetBool(fallingAfterJumpHash, isFallingAfterJump);
            }

            if (hasInputForwardParameter)
            {
                targetAnimator.SetFloat(inputForwardHash, inputForward, speedDampTime, Time.deltaTime);
            }

            if (hasInputRightParameter)
            {
                targetAnimator.SetFloat(inputRightHash, inputRight, speedDampTime, Time.deltaTime);
            }

            if (hasMoveDirectionForwardParameter)
            {
                targetAnimator.SetFloat(moveDirectionForwardHash, moveDirectionForward, speedDampTime, Time.deltaTime);
            }

            if (hasMoveDirectionRightParameter)
            {
                targetAnimator.SetFloat(moveDirectionRightHash, moveDirectionRight, speedDampTime, Time.deltaTime);
            }

            if (hasStrafeModeParameter)
            {
                targetAnimator.SetBool(strafeModeHash, isStrafeMode);
            }

            if (hasLadderParameter)
            {
                targetAnimator.SetBool(ladderHash, isLadder);
            }

            if (hasLadderSpeedParameter)
            {
                targetAnimator.SetFloat(ladderSpeedHash, ladderSpeed);
            }

            if (hasAnimationFinishedParameter)
            {
                targetAnimator.SetBool(animationFinishedHash, isAnimationFinished);
            }
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
        }

        public bool IsCurrentStateFinished(int layerIndex)
        {
            if (targetAnimator == null || layerIndex < 0 || layerIndex >= targetAnimator.layerCount)
            {
                return false;
            }

            if (targetAnimator.IsInTransition(layerIndex))
            {
                return false;
            }

            var stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(layerIndex);
            if (stateInfo.loop)
            {
                return false;
            }

            return stateInfo.normalizedTime >= 1f;
        }

        public bool IsStateFinished(int stateShortNameHash, int layerIndex)
        {
            if (targetAnimator == null || stateShortNameHash == 0 || layerIndex < 0 || layerIndex >= targetAnimator.layerCount)
            {
                return false;
            }

            if (targetAnimator.IsInTransition(layerIndex))
            {
                return false;
            }

            var stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(layerIndex);
            if (stateInfo.shortNameHash != stateShortNameHash || stateInfo.loop)
            {
                return false;
            }

            return stateInfo.normalizedTime >= 1f;
        }

        private void OnValidate()
        {
            CacheParameterHashes();
        }

        private void CacheParameterHashes()
        {
            hasHorizontalSpeedParameter = !string.IsNullOrWhiteSpace(horizontalSpeedParameter);
            hasVerticalSpeedParameter = !string.IsNullOrWhiteSpace(verticalSpeedParameter);
            hasGroundedParameter = !string.IsNullOrWhiteSpace(groundedParameter);
            hasMotionSpeedParameter = !string.IsNullOrWhiteSpace(motionSpeedParameter);
            hasJumpingParameter = !string.IsNullOrWhiteSpace(jumpingParameter);
            hasFreefallParameter = !string.IsNullOrWhiteSpace(freefallParameter);
            hasFallingAfterJumpParameter = !string.IsNullOrWhiteSpace(fallingAfterJumpParameter);
            hasInputForwardParameter = !string.IsNullOrWhiteSpace(inputForwardParameter);
            hasInputRightParameter = !string.IsNullOrWhiteSpace(inputRightParameter);
            hasMoveDirectionForwardParameter = !string.IsNullOrWhiteSpace(moveDirectionForwardParameter);
            hasMoveDirectionRightParameter = !string.IsNullOrWhiteSpace(moveDirectionRightParameter);
            hasStrafeModeParameter = !string.IsNullOrWhiteSpace(strafeModeParameter);
            hasLadderParameter = !string.IsNullOrWhiteSpace(ladderParameter);
            hasLadderSpeedParameter = !string.IsNullOrWhiteSpace(ladderSpeedParameter);
            hasAnimationFinishedParameter = !string.IsNullOrWhiteSpace(animationFinishedParameter);

            horizontalSpeedHash = hasHorizontalSpeedParameter ? Animator.StringToHash(horizontalSpeedParameter) : 0;
            verticalSpeedHash = hasVerticalSpeedParameter ? Animator.StringToHash(verticalSpeedParameter) : 0;
            groundedHash = hasGroundedParameter ? Animator.StringToHash(groundedParameter) : 0;
            motionSpeedHash = hasMotionSpeedParameter ? Animator.StringToHash(motionSpeedParameter) : 0;
            jumpingHash = hasJumpingParameter ? Animator.StringToHash(jumpingParameter) : 0;
            freefallHash = hasFreefallParameter ? Animator.StringToHash(freefallParameter) : 0;
            fallingAfterJumpHash = hasFallingAfterJumpParameter ? Animator.StringToHash(fallingAfterJumpParameter) : 0;
            inputForwardHash = hasInputForwardParameter ? Animator.StringToHash(inputForwardParameter) : 0;
            inputRightHash = hasInputRightParameter ? Animator.StringToHash(inputRightParameter) : 0;
            moveDirectionForwardHash = hasMoveDirectionForwardParameter ? Animator.StringToHash(moveDirectionForwardParameter) : 0;
            moveDirectionRightHash = hasMoveDirectionRightParameter ? Animator.StringToHash(moveDirectionRightParameter) : 0;
            strafeModeHash = hasStrafeModeParameter ? Animator.StringToHash(strafeModeParameter) : 0;
            ladderHash = hasLadderParameter ? Animator.StringToHash(ladderParameter) : 0;
            ladderSpeedHash = hasLadderSpeedParameter ? Animator.StringToHash(ladderSpeedParameter) : 0;
            animationFinishedHash = hasAnimationFinishedParameter ? Animator.StringToHash(animationFinishedParameter) : 0;
        }
    }
}
