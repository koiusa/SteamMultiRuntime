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
        private IWireSwingTraversalFeature wireSwingFeature;
        private Vector2 rawMoveInput;
        private Quaternion moveReferenceRotation;

        private void Awake()
        {
            EnsureRequiredComponents();
            rb = GetComponent<Rigidbody>();
            baseMotor = GetComponent<IPlayerMotor>();
            traversalCoordinator = GetComponent<IPlayerTraversalCoordinator>();
            wireSwingFeature = GetComponent<IWireSwingTraversalFeature>();
            moveReferenceRotation = transform.rotation;
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

        public bool IsGrounded => baseMotor != null && baseMotor.IsEnabled && baseMotor.IsGrounded;
        public bool IsJumping => baseMotor != null && baseMotor.IsEnabled && baseMotor.IsJumping;
        public bool IsFallingAfterJump => baseMotor != null && baseMotor.IsEnabled && baseMotor.IsFallingAfterJump;
        public bool IsTraversalActive =>
            (traversalCoordinator != null && traversalCoordinator.IsEnabled && traversalCoordinator.IsTraversalActive)
            || (wireSwingFeature != null && wireSwingFeature.IsEnabled && wireSwingFeature.IsAttached);
        public bool IsFreefall => baseMotor != null && baseMotor.IsEnabled && baseMotor.IsFreefall && !IsTraversalActive;
        public Vector3 InheritedGroundVelocity => baseMotor != null && baseMotor.IsEnabled ? baseMotor.InheritedGroundVelocity : Vector3.zero;

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
            wireSwingFeature?.Detach();
        }

        public void SetMoveInput(Vector2 moveInput)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            rawMoveInput = moveInput;
        }

        public void SetMoveReferenceRotation(Quaternion referenceRotation)
        {
            if (isActiveAndEnabled)
            {
                moveReferenceRotation = referenceRotation;
            }
        }

        public void Tick(Vector3 moveDirection, bool jumpRequested)
        {
            if (!isActiveAndEnabled || baseMotor == null || !baseMotor.IsEnabled)
            {
                return;
            }

            if (traversalCoordinator == null)
            {
                traversalCoordinator = GetComponent<IPlayerTraversalCoordinator>();
            }

            if (wireSwingFeature == null)
            {
                wireSwingFeature = GetComponent<IWireSwingTraversalFeature>();
            }

            baseMotor.Tick(moveDirection, jumpRequested);
            if (traversalCoordinator != null && traversalCoordinator.IsEnabled)
            {
                traversalCoordinator.ApplyTraversal(moveDirection, rawMoveInput, moveReferenceRotation, jumpRequested, baseMotor.IsGrounded);
            }
        }

        private static Vector3 GetUpAxis()
        {
            return Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        }
    }
}
