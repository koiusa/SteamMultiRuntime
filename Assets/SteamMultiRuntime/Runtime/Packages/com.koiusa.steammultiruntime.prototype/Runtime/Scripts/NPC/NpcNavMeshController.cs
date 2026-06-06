using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [RequireComponent(typeof(PlayerCompositeMotor))]
    public class NpcNavMeshController : MonoBehaviour, IPlayerController
    {

        [SerializeField] private NpcNavMeshMovementModule movement = new();
        [SerializeField] private NpcNavMeshSpeedModule speed = new();

        [Header("AI Input")]
        [SerializeField] private bool randomJumpEnabled = true;
        [SerializeField, Range(0f, 1f)] private float jumpChancePerSecond = 0.1f;
        [SerializeField, Min(0f)] private float jumpCooldownMin = 1.5f;
        [SerializeField, Min(0f)] private float jumpCooldownMax = 4.0f;
        [SerializeField, Min(0f)] private float minHorizontalSpeedToJump = 0.35f;

        [Header("Dynamic Avoidance")]
        [SerializeField, Min(0f)] private float collisionRepathCooldown = 0.25f;
        [SerializeField, Min(0.1f)] private float collisionAvoidanceOffset = 1.2f;
        [SerializeField, Min(0.1f)] private float collisionAvoidanceDuration = 0.6f;

        private NavMeshAgent _agent;
        private Rigidbody _rigidbody;
        private PlayerCompositeMotor _motor;
        private IPlayerMoveInputReceiver _moveInputReceiver;
        private IPlayerMotor _baseMotor;
        private AiPlayerInputSource _inputSource;

        private Vector2 _moveInput;
        private Vector3 _moveDirection;
        private int _jumpToken;
        private int _lastConsumedJumpToken;
        private float _nextJumpAllowedTime;

        private float _nextCollisionRepathTime;
        private bool _isAvoidingCollision;
        private bool _hasResumeDestination;
        private Vector3 _resumeDestination;
        private float _collisionAvoidanceUntilTime;

        public event System.Action ReturnToCenterStarted;
        public event System.Action<Vector3> DestinationSet;

        public bool HasPath => _agent != null && _agent.isOnNavMesh && _agent.hasPath;
        public bool IsMoving => _motor != null && _motor.HorizontalVelocity > 0.01f;
        public bool IsGrounded => _motor != null && _motor.IsGrounded;
        public bool IsJumping => _motor != null && _motor.IsJumping;
        public bool IsFreefall => _motor != null && _motor.IsFreefall;
        public bool IsFallingAfterJump => _motor != null && _motor.IsFallingAfterJump;
        public bool IsStrafeMode => false;
        public Vector3 InheritedGroundVelocity => _motor != null ? _motor.InheritedGroundVelocity : Vector3.zero;
        public Vector2 MoveInput => _moveInput;
        public Vector3 MoveDirection => _moveDirection;
        public float HorizontalVelocity => _motor != null ? _motor.HorizontalVelocity : 0f;
        public float VerticalVelocity => _motor != null ? _motor.VerticalVelocity : 0f;
        public float MaxMoveSpeed
        {
            get
            {
                if (_baseMotor != null)
                {
                    var settings = _baseMotor.GetSettings();
                    if (settings.MoveSpeed > 0f)
                        return settings.MoveSpeed;
                }

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
            _motor = GetComponent<PlayerCompositeMotor>();
            _moveInputReceiver = _motor as IPlayerMoveInputReceiver;
            _baseMotor = GetComponent<IPlayerMotor>();
            _inputSource = new AiPlayerInputSource();

            movement.NormalizeSettings();
            speed.NormalizeSettings();
            ApplyAgentSettings();

            if (_rigidbody != null)
            {
                _rigidbody.freezeRotation = true;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            speed.Initialize(_agent);
            movement.Initialize(_agent, transform);
            ResetInputState();
            ScheduleNextJump(true);
        }

        private void OnEnable()
        {
            ApplyAgentSettings();
            speed.OnEnable();
            movement.OnEnable();
            movement.OnReturnToCenterStarted += OnReturnToCenterStarted;
            movement.OnRandomDestinationNeeded += OnRandomDestinationNeeded;
            movement.OnCenterDestinationNeeded += OnCenterDestinationNeeded;
            movement.OnDestinationNeeded += OnDestinationNeeded;

            _inputSource.Enable();
            ResetInputState();
            ScheduleNextJump(true);
        }

        private void OnDisable()
        {
            movement.OnReturnToCenterStarted -= OnReturnToCenterStarted;
            movement.OnRandomDestinationNeeded -= OnRandomDestinationNeeded;
            movement.OnCenterDestinationNeeded -= OnCenterDestinationNeeded;
            movement.OnDestinationNeeded -= OnDestinationNeeded;

            _inputSource.Disable();
            _motor?.ResetState();
            ResetInputState();
            _isAvoidingCollision = false;
            _hasResumeDestination = false;
            _collisionAvoidanceUntilTime = 0f;
            StopAgent();
            ResetAgentPath();
        }

        private void OnDestroy()
        {
            StopAgent();
            ResetAgentPath();
        }

        private void Update()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            movement.ObserveState();
            UpdateCollisionAvoidanceState();
            UpdateAiInputSignal();

            if (_rigidbody != null)
                _agent.nextPosition = _rigidbody.position;
            else
                _agent.nextPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (_motor == null)
                return;

            var inputState = _inputSource.ReadState();
            _moveInput = inputState.Move;
            _moveDirection = PlayerMotor.GetMoveDirection(transform, _moveInput);

            if (inputState.JumpPressed)
                _jumpToken++;

            var jumpThisFrame = _jumpToken != _lastConsumedJumpToken;
            if (jumpThisFrame)
                _lastConsumedJumpToken = _jumpToken;

            _baseMotor?.SetStrafeMode(false);
            _moveInputReceiver?.SetMoveInput(_moveInput);
            _motor.Tick(_moveDirection, jumpThisFrame);

            if (_agent != null && _agent.isOnNavMesh && _rigidbody != null)
                _agent.nextPosition = _rigidbody.position;
        }

        private void OnCollisionEnter(Collision collision)
        {
            _motor?.OnCollisionEnter(collision);
            TryRepathOnCharacterCollision(collision.collider);
        }

        private void OnCollisionStay(Collision collision)
        {
            _motor?.OnCollisionStay(collision);
            TryRepathOnCharacterCollision(collision.collider);
        }

        private void OnCollisionExit(Collision collision)
        {
            _motor?.OnCollisionExit(collision);
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

        private void OnValidate()
        {
            movement.NormalizeSettings();
            speed.NormalizeSettings();
            if (jumpCooldownMin < 0f)
                jumpCooldownMin = 0f;
            if (jumpCooldownMax < jumpCooldownMin)
                jumpCooldownMax = jumpCooldownMin;
            if (minHorizontalSpeedToJump < 0f)
                minHorizontalSpeedToJump = 0f;
            jumpChancePerSecond = Mathf.Clamp01(jumpChancePerSecond);
            ApplyAgentSettings();

            if (Application.isPlaying)
                speed.ApplyAgentSpeedScale();
        }

        private void ApplyAgentSettings()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
                return;

            _agent.updateRotation = false;
            _agent.updateUpAxis = false;
            _agent.updatePosition = false;
            _agent.autoRepath = true;
        }

        private void UpdateAiInputSignal()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                _inputSource.SetMove(Vector2.zero);
                return;
            }

            var upAxis = PlayerMotor.GetUpAxis();
            var desiredPlanar = Vector3.ProjectOnPlane(_agent.desiredVelocity, upAxis);
            var targetPlanarVelocity = desiredPlanar;
            if (_agent.pathPending || _agent.isStopped || !_agent.hasPath)
                targetPlanarVelocity = Vector3.zero;

            var localDesired = transform.InverseTransformDirection(targetPlanarVelocity);
            var nextMoveInput = new Vector2(localDesired.x, localDesired.z);
            if (nextMoveInput.sqrMagnitude > 1f)
                nextMoveInput = nextMoveInput.normalized;

            _inputSource.SetMove(nextMoveInput);

            if (!randomJumpEnabled)
                return;
            if (Time.time < _nextJumpAllowedTime)
                return;
            if (!IsGrounded)
                return;
            if (targetPlanarVelocity.magnitude < minHorizontalSpeedToJump)
                return;

            var chanceThisFrame = jumpChancePerSecond * Time.deltaTime;
            if (Random.value > chanceThisFrame)
                return;

            _inputSource.QueueJump();
            ScheduleNextJump(false);
        }

        private void ScheduleNextJump(bool allowImmediate)
        {
            var minCooldown = jumpCooldownMin;
            var maxCooldown = Mathf.Max(minCooldown, jumpCooldownMax);
            _nextJumpAllowedTime = Time.time + (allowImmediate ? Random.Range(0f, maxCooldown) : Random.Range(minCooldown, maxCooldown));
        }

        private void ResetInputState()
        {
            _moveInput = Vector2.zero;
            _moveDirection = Vector3.zero;
            _jumpToken = 0;
            _lastConsumedJumpToken = 0;
            _inputSource.SetMove(Vector2.zero);
        }

        private void TryRepathOnCharacterCollision(Collider otherCollider)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;
            if (otherCollider == null)
                return;
            if (Time.time < _nextCollisionRepathTime)
                return;
            if (_isAvoidingCollision)
                return;

            var otherController = otherCollider.GetComponentInParent<IPlayerController>();
            if (otherController == null)
                return;
            if (!otherController.IsGrounded)
                return;

            if (!_agent.hasPath || _agent.pathPending)
                return;

            var upAxis = PlayerMotor.GetUpAxis().normalized;
            var away = Vector3.ProjectOnPlane(transform.position - otherCollider.bounds.center, upAxis);
            if (away.sqrMagnitude <= 0.0001f)
            {
                away = Vector3.ProjectOnPlane(_agent.steeringTarget - transform.position, upAxis);
                away = away.sqrMagnitude > 0.0001f ? -away.normalized : transform.right;
            }
            else
            {
                away = away.normalized;
            }

            var avoidTarget = transform.position + away * collisionAvoidanceOffset;
            if (!NavMesh.SamplePosition(avoidTarget, out var hit, collisionAvoidanceOffset + 1f, _agent.areaMask))
                return;

            _resumeDestination = _agent.destination;
            _hasResumeDestination = true;
            _isAvoidingCollision = true;
            _collisionAvoidanceUntilTime = Time.time + collisionAvoidanceDuration;
            _nextCollisionRepathTime = Time.time + collisionRepathCooldown;

            _agent.SetDestination(hit.position);
            DestinationSet?.Invoke(hit.position);
        }

        private void UpdateCollisionAvoidanceState()
        {
            if (!_isAvoidingCollision)
                return;
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            var expired = Time.time >= _collisionAvoidanceUntilTime;
            var reachedAvoidPoint = !_agent.pathPending && (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance);
            if (!expired && !reachedAvoidPoint)
                return;

            _isAvoidingCollision = false;
            if (!_hasResumeDestination)
                return;

            _hasResumeDestination = false;
            _agent.SetDestination(_resumeDestination);
            DestinationSet?.Invoke(_resumeDestination);
        }

        private void StopAgent()
        {
            if (_agent == null)
                return;

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
    }
}
