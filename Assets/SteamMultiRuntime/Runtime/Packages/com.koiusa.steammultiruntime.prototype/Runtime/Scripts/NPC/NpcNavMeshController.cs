using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class NpcNavMeshController : MonoBehaviour, IPlayerController
    {
        [SerializeField] private NpcNavMeshMovementModule movement = new();
        [SerializeField] private NpcNavMeshRotationModule rotation = new();
        [SerializeField] private NpcNavMeshSpeedModule speed = new();
        [SerializeField] private NpcNavMeshJumpModule jump = new();

        [Header("Manual Locomotion")]
        [SerializeField, Min(0f)] private float moveAcceleration = 22f;
        [SerializeField, Min(0f)] private float moveDeceleration = 28f;
        [SerializeField, Min(0f)] private float destinationStopBuffer = 0.05f;
        [SerializeField, Min(0f)] private float groundedProbeDistance = 0.08f;

        private NavMeshAgent _agent;
        private Rigidbody _rigidbody;
        private Collider _bodyCollider;
        private Vector3 _estimatedVelocity;
        private Vector3 _simulatedPlanarVelocity;
        private Vector3 _previousPosition;
        private bool _hasPreviousPosition;
        private bool _isGrounded;

        public event System.Action ReturnToCenterStarted;
        public event System.Action<Vector3> DestinationSet;

        public bool HasPath => _agent != null && _agent.isOnNavMesh && _agent.hasPath;
        public bool IsMoving => _agent != null && _agent.isOnNavMesh && !_agent.isStopped && _simulatedPlanarVelocity.sqrMagnitude > 0.01f;
        public bool IsGrounded => _isGrounded;
        public bool IsJumping => jump.IsJumping;
        public bool IsFreefall => !_isGrounded && !jump.IsJumping;
        public bool IsFallingAfterJump => jump.IsFallingAfterJump;
        public bool IsStrafeMode => false;
        public Vector3 InheritedGroundVelocity => Vector3.zero;
        public Vector2 MoveInput
        {
            get
            {
                if (_agent == null || !_agent.isOnNavMesh)
                    return Vector2.zero;

                var localDesired = transform.InverseTransformDirection(_agent.desiredVelocity);
                var planar = new Vector2(localDesired.x, localDesired.z);
                return planar.sqrMagnitude > 1f ? planar.normalized : planar;
            }
        }

        public Vector3 MoveDirection
        {
            get
            {
                if (_agent == null || !_agent.isOnNavMesh)
                    return Vector3.zero;

                var upAxis = PlayerMotor.GetUpAxis();
                var direction = Vector3.ProjectOnPlane(_agent.desiredVelocity, upAxis);
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            }
        }

        public float HorizontalVelocity
        {
            get
            {
                if (_agent == null || !_agent.isOnNavMesh)
                    return 0f;
                return _simulatedPlanarVelocity.magnitude;
            }
        }

        public float VerticalVelocity
        {
            get
            {
                if (_agent == null || !_agent.isOnNavMesh)
                    return 0f;
                if (jump.IsJumpActive)
                    return jump.VerticalVelocity;
                return Vector3.Dot(_estimatedVelocity, PlayerMotor.GetUpAxis());
            }
        }

        public float MaxMoveSpeed
        {
            get
            {
                var baseSpeed = speed.BaseAgentSpeed;
                if (baseSpeed > 0f)
                    return Mathf.Max(baseSpeed, 0.01f);
                return _agent != null ? Mathf.Max(_agent.speed, 0.01f) : 1f;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _rigidbody = GetComponent<Rigidbody>();
            _bodyCollider = GetComponent<Collider>();
            movement.NormalizeSettings();
            speed.NormalizeSettings();
            jump.NormalizeSettings();
            ApplyAgentOrientationSettings();

            if (_agent != null)
            {
                _agent.updatePosition = false;
                _agent.nextPosition = transform.position;
            }

            speed.Initialize(_agent);
            movement.Initialize(_agent, transform);
            rotation.Initialize(_agent, transform);
            jump.Initialize(_agent);

            _previousPosition = transform.position;
            _hasPreviousPosition = true;
            _estimatedVelocity = Vector3.zero;
            _simulatedPlanarVelocity = Vector3.zero;
            _isGrounded = true;
        }

        private void OnEnable()
        {
            ApplyAgentOrientationSettings();
            if (_agent != null)
            {
                _agent.updatePosition = false;
                _agent.nextPosition = transform.position;
            }

            speed.OnEnable();
            movement.OnEnable();
            jump.OnEnable();
            movement.OnReturnToCenterStarted += OnReturnToCenterStarted;
            movement.OnRandomDestinationNeeded += OnRandomDestinationNeeded;
            movement.OnCenterDestinationNeeded += OnCenterDestinationNeeded;
            movement.OnDestinationNeeded += OnDestinationNeeded;

            _previousPosition = transform.position;
            _hasPreviousPosition = true;
            _estimatedVelocity = Vector3.zero;
            _simulatedPlanarVelocity = Vector3.zero;
            _isGrounded = true;
        }

        private void OnDisable()
        {
            movement.OnReturnToCenterStarted -= OnReturnToCenterStarted;
            movement.OnRandomDestinationNeeded -= OnRandomDestinationNeeded;
            movement.OnCenterDestinationNeeded -= OnCenterDestinationNeeded;
            movement.OnDestinationNeeded -= OnDestinationNeeded;
            jump.OnDisable();

            _simulatedPlanarVelocity = Vector3.zero;
            _isGrounded = false;
            StopAgent();
            ResetAgentPath();
        }

        private void OnDestroy()
        {
            StopAgent();
            ResetAgentPath();
        }

        private void StopAgent()
        {
            if (_agent == null) return;
            try
            {
                if (_agent.enabled && _agent.isOnNavMesh)
                    _agent.isStopped = true;
            }
            catch { }
        }

        private void ResetAgentPath()
        {
            if (_agent == null)
                return;
            try
            {
                if (_agent.isOnNavMesh)
                    _agent.ResetPath();
            }
            catch { }
        }

        private void OnValidate()
        {
            movement.NormalizeSettings();
            speed.NormalizeSettings();
            jump.NormalizeSettings();
            ApplyAgentOrientationSettings();

            if (Application.isPlaying)
                speed.ApplyAgentSpeedScale();
        }

        private void Update()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            movement.ObserveState();
            rotation.UpdateRotation();
            UpdateEstimatedVelocity();

            _agent.nextPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || _rigidbody == null)
                return;

            UpdateGroundedState();
            jump.UpdateState();
            ApplyJumpPhysics();
            UpdateManualLocomotion();
            _agent.nextPosition = _rigidbody.position;
        }

        private void LateUpdate()
        {
            if (!rotation.KeepUpright)
                return;
            rotation.StabilizeUpright();
        }

        private void OnRandomDestinationNeeded()
        {
            speed.RandomizeForSegment();
            movement.RequestRandomDestination();
        }

        private void OnCenterDestinationNeeded()
        {
            movement.RequestCenterDestination();
        }

        private void OnDestinationNeeded()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;
            DestinationSet?.Invoke(_agent.destination);
        }

        private void OnReturnToCenterStarted()
        {
            speed.ApplyReturnToCenterSpeedBoost();
            ReturnToCenterStarted?.Invoke();
        }

        private void ApplyAgentOrientationSettings()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
                return;
            _agent.updateRotation = false;
            _agent.updateUpAxis = false;
            _agent.updatePosition = false;
        }

        private void UpdateManualLocomotion()
        {
            var upAxis = PlayerMotor.GetUpAxis();
            var desiredPlanar = Vector3.ProjectOnPlane(_agent.desiredVelocity, upAxis);
            var targetPlanarVelocity = desiredPlanar;

            if (_agent.pathPending || _agent.isStopped || !_agent.hasPath)
                targetPlanarVelocity = Vector3.zero;

            var remainingDistance = _agent.remainingDistance;
            var stopDistance = _agent.stoppingDistance + destinationStopBuffer;
            if (!_agent.pathPending && _agent.hasPath && remainingDistance <= stopDistance)
                targetPlanarVelocity = Vector3.zero;

            var currentSpeed = _simulatedPlanarVelocity.magnitude;
            var targetSpeed = targetPlanarVelocity.magnitude;
            var accel = targetSpeed >= currentSpeed ? moveAcceleration : moveDeceleration;
            var maxDelta = Mathf.Max(0f, accel) * Time.fixedDeltaTime;
            _simulatedPlanarVelocity = Vector3.MoveTowards(_simulatedPlanarVelocity, targetPlanarVelocity, maxDelta);

            var currentVelocity = _rigidbody.linearVelocity;
            var verticalVelocity = Vector3.Project(currentVelocity, upAxis);
            _rigidbody.linearVelocity = _simulatedPlanarVelocity + verticalVelocity;
        }

        private void ApplyJumpPhysics()
        {
            var upAxis = PlayerMotor.GetUpAxis();
            var velocity = _rigidbody.linearVelocity;
            var verticalVelocity = Vector3.Dot(velocity, upAxis);

            if (jump.ConsumeJumpRequest() && _isGrounded)
            {
                velocity -= upAxis * verticalVelocity;
                velocity += upAxis * jump.JumpVerticalVelocity;
                _rigidbody.linearVelocity = velocity;
                jump.NotifyJumpStarted(jump.JumpVerticalVelocity);
                _isGrounded = false;
                return;
            }

            if (jump.IsJumpActive && verticalVelocity < 0f)
            {
                _rigidbody.linearVelocity += Physics.gravity * (jump.FallMultiplier - 1f) * Time.fixedDeltaTime;
            }

            if (jump.IsJumpActive && _isGrounded && verticalVelocity <= 0.05f)
            {
                jump.NotifyLanded();
            }
        }

        private void UpdateGroundedState()
        {
            if (_bodyCollider == null)
            {
                _isGrounded = false;
                return;
            }

            var upAxis = PlayerMotor.GetUpAxis().normalized;
            var bounds = _bodyCollider.bounds;
            var upExtent = Mathf.Abs(upAxis.x) * bounds.extents.x + Mathf.Abs(upAxis.y) * bounds.extents.y + Mathf.Abs(upAxis.z) * bounds.extents.z;
            var origin = bounds.center;
            var maxDistance = upExtent + Mathf.Max(0.01f, groundedProbeDistance);
            _isGrounded = Physics.Raycast(origin, -upAxis, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        }

        private void UpdateEstimatedVelocity()
        {
            if (_rigidbody != null)
            {
                _estimatedVelocity = _rigidbody.linearVelocity;
                _previousPosition = transform.position;
                _hasPreviousPosition = true;
                return;
            }

            if (!_hasPreviousPosition)
            {
                _previousPosition = transform.position;
                _hasPreviousPosition = true;
                _estimatedVelocity = Vector3.zero;
                return;
            }

            var deltaTime = Time.deltaTime;
            if (deltaTime <= Mathf.Epsilon)
                return;

            var currentPosition = transform.position;
            var rawVelocity = (currentPosition - _previousPosition) / deltaTime;
            _previousPosition = currentPosition;

            const float lerp = 0.35f;
            const float stopLerp = 0.7f;
            const float deadZone = 0.08f;
            var deadZoneSqr = deadZone * deadZone;

            if (rawVelocity.sqrMagnitude <= deadZoneSqr)
                rawVelocity = Vector3.zero;

            var nearStopped = _agent.isStopped || (!_agent.pathPending && !_agent.hasPath);
            if (nearStopped)
            {
                var damped = Vector3.Lerp(_estimatedVelocity, Vector3.zero, stopLerp);
                _estimatedVelocity = damped.sqrMagnitude <= deadZoneSqr ? Vector3.zero : damped;
                return;
            }

            _estimatedVelocity = Vector3.Lerp(_estimatedVelocity, rawVelocity, lerp);
        }
    }
}
