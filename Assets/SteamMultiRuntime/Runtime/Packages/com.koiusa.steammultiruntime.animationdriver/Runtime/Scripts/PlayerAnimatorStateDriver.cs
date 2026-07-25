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

        private readonly System.Collections.Generic.Dictionary<string, int> animatorParameterHashes = new();
        private RuntimeAnimatorController cachedAnimatorController;
        private IPlayerController playerController;
        private ILadderTraversalFeature ladderTraversalFeature;
        private IPlayerLadderState playerLadderState;
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
            var isLadder = playerLadderState != null
                ? playerLadderState.IsOnLadder
                : ladderTraversalFeature != null && ladderTraversalFeature.IsOnLadder;
            var ladderSpeed = playerLadderState != null
                ? playerLadderState.LadderSpeed
                : ladderTraversalFeature != null ? ladderTraversalFeature.ClimbSpeed : 0f;
            var isAnimationFinished = IsCurrentStateFinished(animationFinishedLayerIndex);

            SetFloat(horizontalSpeedParameter, horizontalSpeed, speedDampTime);
            SetFloat(verticalSpeedParameter, verticalSpeed);
            SetBool(groundedParameter, isGrounded);
            SetFloat(motionSpeedParameter, motionSpeed);
            SetBool(jumpingParameter, isJumping);
            SetBool(freefallParameter, isFreefall);
            SetBool(fallingAfterJumpParameter, isFallingAfterJump);
            SetFloat(inputForwardParameter, inputForward, speedDampTime);
            SetFloat(inputRightParameter, inputRight, speedDampTime);
            SetFloat(moveDirectionForwardParameter, moveDirectionForward, speedDampTime);
            SetFloat(moveDirectionRightParameter, moveDirectionRight, speedDampTime);
            SetBool(strafeModeParameter, isStrafeMode);
            SetBool(ladderParameter, isLadder);
            SetFloat(ladderSpeedParameter, ladderSpeed);
            SetBool(animationFinishedParameter, isAnimationFinished);
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

        private void SetBool(string parameterName, bool value)
        {
            if (TryGetParameterHash(parameterName, out var hash))
            {
                targetAnimator.SetBool(hash, value);
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
