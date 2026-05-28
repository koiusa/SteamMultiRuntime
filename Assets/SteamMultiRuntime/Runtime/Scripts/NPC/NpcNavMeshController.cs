using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcNavMeshController : MonoBehaviour, IPlayerController
    {
        [SerializeField] private NpcNavMeshMovementModule movement = new();
        [SerializeField] private NpcNavMeshRotationModule rotation = new();
        [SerializeField] private NpcNavMeshSpeedModule speed = new();
        [SerializeField] private NpcNavMeshJumpModule jump = new();

        private NavMeshAgent _agent;
        private Vector3 _estimatedVelocity;
        private Vector3 _previousPosition;
        private bool _hasPreviousPosition;

        public event System.Action ReturnToCenterStarted;
        public event System.Action<Vector3> DestinationSet;

        public bool HasPath => _agent != null && _agent.isOnNavMesh && _agent.hasPath;
        public bool IsMoving => _agent != null && _agent.isOnNavMesh && !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.01f;
        public bool IsGrounded => _agent != null && _agent.isOnNavMesh && !jump.IsJumpActive;
        public bool IsJumping => jump.IsJumping;
        public bool IsFreefall => false;
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
                return Vector3.ProjectOnPlane(_estimatedVelocity, PlayerMotor.GetUpAxis()).magnitude;
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
            movement.NormalizeSettings();
            speed.NormalizeSettings();
            jump.NormalizeSettings();
            ApplyAgentOrientationSettings();

            speed.Initialize(_agent);
            movement.Initialize(_agent, transform);
            rotation.Initialize(_agent, transform);
            jump.Initialize(_agent);

            _previousPosition = transform.position;
            _hasPreviousPosition = true;
            _estimatedVelocity = Vector3.zero;
        }

        private void OnEnable()
        {
            ApplyAgentOrientationSettings();
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
        }

        private void OnDisable()
        {
            movement.OnReturnToCenterStarted -= OnReturnToCenterStarted;
            movement.OnRandomDestinationNeeded -= OnRandomDestinationNeeded;
            movement.OnCenterDestinationNeeded -= OnCenterDestinationNeeded;
            movement.OnDestinationNeeded -= OnDestinationNeeded;
            jump.OnDisable();
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
            jump.UpdateState();
            UpdateEstimatedVelocity();
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
        }

        private void UpdateEstimatedVelocity()
        {
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
