using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class PlayerAnimatorStateDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Rigidbody targetRigidbody;

        [Header("Animator Parameters")]
        [SerializeField] private string horizontalSpeedParameter = "Speed";
        [SerializeField] private string verticalSpeedParameter = "VerticalVelocity";
        [SerializeField] private string groundedParameter = "IsGrounded";
        [SerializeField] private string motionSpeedParameter = "MotionSpeed";
        [SerializeField] private string jumpingParameter = "IsJumping";
        [SerializeField] private string freefallParameter = "IsFreefall";
        [SerializeField] private string fallingAfterJumpParameter = "IsFallingAfterJump";

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
        private bool hasHorizontalSpeedParameter;
        private bool hasVerticalSpeedParameter;
        private bool hasGroundedParameter;
        private bool hasMotionSpeedParameter;
        private bool hasJumpingParameter;
        private bool hasFreefallParameter;
        private bool hasFallingAfterJumpParameter;
        private IPlayerController playerController;
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
            var motionSpeed = playerController != null ? horizontalSpeed / playerController.MaxMoveSpeed * motionSpeedMultiplier : horizontalSpeed * motionSpeedMultiplier;
            var isJumping = playerController != null && playerController.IsJumping;
            var isFreefall = playerController != null && playerController.IsFreefall;
            var isFallingAfterJump = playerController != null && playerController.IsFallingAfterJump;

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

            horizontalSpeedHash = hasHorizontalSpeedParameter ? Animator.StringToHash(horizontalSpeedParameter) : 0;
            verticalSpeedHash = hasVerticalSpeedParameter ? Animator.StringToHash(verticalSpeedParameter) : 0;
            groundedHash = hasGroundedParameter ? Animator.StringToHash(groundedParameter) : 0;
            motionSpeedHash = hasMotionSpeedParameter ? Animator.StringToHash(motionSpeedParameter) : 0;
            jumpingHash = hasJumpingParameter ? Animator.StringToHash(jumpingParameter) : 0;
            freefallHash = hasFreefallParameter ? Animator.StringToHash(freefallParameter) : 0;
            fallingAfterJumpHash = hasFallingAfterJumpParameter ? Animator.StringToHash(fallingAfterJumpParameter) : 0;
        }
    }
}
