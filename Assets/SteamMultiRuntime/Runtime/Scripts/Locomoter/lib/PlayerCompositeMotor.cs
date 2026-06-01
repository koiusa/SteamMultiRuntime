using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    public sealed class PlayerCompositeMotor : MonoBehaviour, IPlayerMoveInputReceiver
    {
        private Rigidbody rb;
        private IPlayerMotor baseMotor;
        private IPlayerTraversalCoordinator traversalCoordinator;
        private Vector2 rawMoveInput;

        private void Awake()
        {
            EnsureRequiredComponents();
            rb = GetComponent<Rigidbody>();
            baseMotor = GetComponent<IPlayerMotor>();
            traversalCoordinator = GetComponent<IPlayerTraversalCoordinator>();
        }

        private void OnValidate()
        {
            EnsureRequiredComponents();
        }

        private void EnsurePlayerMotor()
        {
            if (GetComponent<IPlayerMotor>() == null)
            {
                gameObject.AddComponent<PlayerMotor>();
            }
        }

        private void EnsureTraversalCoordinator()
        {
            if (GetComponent<IPlayerTraversalCoordinator>() == null)
            {
                gameObject.AddComponent<PlayerTraversalCoordinator>();
            }
        }

        private void EnsureRequiredComponents()
        {
            EnsurePlayerMotor();
            EnsureTraversalCoordinator();

            if (GetComponent<Rigidbody>() == null)
            {
                gameObject.AddComponent<Rigidbody>();
            }

            if (GetComponent<SlopeContactResolver>() == null)
            {
                gameObject.AddComponent<SlopeContactResolver>();
            }

            if (GetComponent<GroundMotionTracker>() == null)
            {
                gameObject.AddComponent<GroundMotionTracker>();
            }
        }

        public bool IsGrounded => baseMotor != null && baseMotor.IsGrounded;
        public bool IsJumping => baseMotor != null && baseMotor.IsJumping;
        public bool IsFallingAfterJump => baseMotor != null && baseMotor.IsFallingAfterJump;
        public bool IsTraversalActive => traversalCoordinator != null && traversalCoordinator.IsTraversalActive;
        public bool IsFreefall => baseMotor != null && baseMotor.IsFreefall && !IsTraversalActive;
        public Vector3 InheritedGroundVelocity => baseMotor != null ? baseMotor.InheritedGroundVelocity : Vector3.zero;

        public float HorizontalVelocity
        {
            get
            {
                if (rb == null)
                {
                    return baseMotor != null ? baseMotor.HorizontalVelocity : 0f;
                }

                var upAxis = GetUpAxis();
                return Vector3.ProjectOnPlane(rb.linearVelocity, upAxis).magnitude;
            }
        }

        public float VerticalVelocity
        {
            get
            {
                if (rb == null)
                {
                    return baseMotor != null ? baseMotor.VerticalVelocity : 0f;
                }

                var upAxis = GetUpAxis();
                return Vector3.Dot(rb.linearVelocity, upAxis);
            }
        }

        public void ResetState()
        {
            baseMotor?.ResetState();
            traversalCoordinator?.ResetState();
        }

        public void SetMoveInput(Vector2 moveInput)
        {
            rawMoveInput = moveInput;
        }

        public void Tick(Vector3 moveDirection, bool jumpRequested)
        {
            if (baseMotor == null)
            {
                return;
            }

            if (traversalCoordinator == null)
            {
                traversalCoordinator = GetComponent<IPlayerTraversalCoordinator>();
            }

            baseMotor.Tick(moveDirection, jumpRequested);
            traversalCoordinator?.ApplyTraversal(moveDirection, rawMoveInput, jumpRequested, baseMotor.IsGrounded);
        }

        public void OnCollisionEnter(Collision collision)
        {
            baseMotor?.OnCollisionEnter(collision);
        }

        public void OnCollisionStay(Collision collision)
        {
            baseMotor?.OnCollisionStay(collision);
        }

        public void OnCollisionExit(Collision collision)
        {
            baseMotor?.OnCollisionExit(collision);
        }

        private static Vector3 GetUpAxis()
        {
            return Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        }
    }
}
