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

        [Header("Boid Separation")]
        [SerializeField] private bool boidSeparationEnabled = true;
        [SerializeField, Min(0.1f)] private float boidSeparationRadius = 1.6f;
        [SerializeField, Min(0f)] private float boidGoalWeight = 1f;
        [SerializeField, Min(0f)] private float boidSeparationWeight = 1.25f;
        [SerializeField, Min(1f)] private float boidSeparationExponent = 2.2f;
        [SerializeField] private bool boidUseForwardNeighborFilter = true;
        [SerializeField, Range(-1f, 1f)] private float boidNeighborForwardDotMin = 0f;
        [SerializeField, Min(1)] private int boidMaxNeighbors = 8;

        [Header("Steering Filter")]
        [SerializeField, Min(0.1f)] private float boidLowPassCutoffHz = 4f;
        [SerializeField, Min(0f)] private float boidDeadband = 0.05f;
        [SerializeField, Min(1f)] private float boidMaxTurnDegPerSec = 240f;


        private NavMeshAgent _agent;
        private Rigidbody _rigidbody;
        private PlayerCompositeMotor _motor;
        private IPlayerMoveInputReceiver _moveInputReceiver;
        private IPlayerMotor _baseMotor;
        private AiPlayerInputSource _inputSource;

        private Vector2 _moveInput;
        private Vector3 _moveDirection;
        private Vector3 _filteredSteeringPlanar;
        private int _jumpToken;
        private int _lastConsumedJumpToken;
        private float _nextJumpAllowedTime;

        private readonly Collider[] _boidNeighborBuffer = new Collider[32];


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
        }

        private void OnCollisionStay(Collision collision)
        {
            _motor?.OnCollisionStay(collision);
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
            if (boidSeparationExponent < 1f)
                boidSeparationExponent = 1f;
            boidNeighborForwardDotMin = Mathf.Clamp(boidNeighborForwardDotMin, -1f, 1f);
            if (boidLowPassCutoffHz < 0.1f)
                boidLowPassCutoffHz = 0.1f;
            if (boidDeadband < 0f)
                boidDeadband = 0f;
            if (boidMaxTurnDegPerSec < 1f)
                boidMaxTurnDegPerSec = 1f;
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

            var steeringPlanar = targetPlanarVelocity;
            if (boidSeparationEnabled)
            {
                steeringPlanar = BuildBoidSteeringPlanar(upAxis, targetPlanarVelocity);
            }

            steeringPlanar = ApplySteeringLowPass(upAxis, steeringPlanar);
            steeringPlanar = ApplySteeringTurnRateLimit(upAxis, steeringPlanar);
            steeringPlanar = ApplySteeringDeadband(steeringPlanar);

            var localDesired = transform.InverseTransformDirection(steeringPlanar);
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

        private Vector3 ApplySteeringLowPass(Vector3 upAxis, Vector3 steeringPlanar)
        {
            var target = Vector3.ProjectOnPlane(steeringPlanar, upAxis);
            if (_filteredSteeringPlanar.sqrMagnitude <= 0.000001f)
            {
                _filteredSteeringPlanar = target;
                return target;
            }

            var dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var cutoff = Mathf.Max(0.1f, boidLowPassCutoffHz);
            var alpha = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * dt);
            _filteredSteeringPlanar = Vector3.Lerp(_filteredSteeringPlanar, target, alpha);
            return Vector3.ProjectOnPlane(_filteredSteeringPlanar, upAxis);
        }

        private Vector3 ApplySteeringTurnRateLimit(Vector3 upAxis, Vector3 steeringPlanar)
        {
            var current = Vector3.ProjectOnPlane(_moveDirection, upAxis);
            var target = Vector3.ProjectOnPlane(steeringPlanar, upAxis);
            if (target.sqrMagnitude <= 0.000001f || current.sqrMagnitude <= 0.000001f)
                return target;

            var maxTurn = Mathf.Max(1f, boidMaxTurnDegPerSec) * Time.deltaTime;
            var limited = Vector3.RotateTowards(current.normalized, target.normalized, maxTurn * Mathf.Deg2Rad, 0f);
            return limited * target.magnitude;
        }

        private Vector3 ApplySteeringDeadband(Vector3 steeringPlanar)
        {
            var deadband = Mathf.Max(0f, boidDeadband);
            return steeringPlanar.sqrMagnitude <= deadband * deadband ? Vector3.zero : steeringPlanar;
        }

        private Vector3 BuildBoidSteeringPlanar(Vector3 upAxis, Vector3 goalPlanarVelocity)
        {
            var radius = Mathf.Max(0.1f, boidSeparationRadius);
            var count = Physics.OverlapSphereNonAlloc(transform.position, radius, _boidNeighborBuffer, ~0, QueryTriggerInteraction.Ignore);
            if (count <= 0)
                return goalPlanarVelocity;

            var separation = Vector3.zero;
            var neighborCount = 0;
            var maxNeighbors = Mathf.Clamp(boidMaxNeighbors, 1, _boidNeighborBuffer.Length);
            var radiusSqr = radius * radius;
            var uniqueNeighborIds = new int[32];
            var uniqueNeighborCount = 0;

            var referenceForward = Vector3.ProjectOnPlane(goalPlanarVelocity, upAxis);
            if (referenceForward.sqrMagnitude <= 0.0001f)
                referenceForward = Vector3.ProjectOnPlane(transform.forward, upAxis);
            if (referenceForward.sqrMagnitude > 0.0001f)
                referenceForward.Normalize();

            for (var i = 0; i < count && neighborCount < maxNeighbors; i++)
            {
                var col = _boidNeighborBuffer[i];
                if (col == null)
                    continue;
                if (col.attachedRigidbody == _rigidbody)
                    continue;

                var other = col.GetComponentInParent<IPlayerController>();
                if (other == null)
                    continue;

                var neighborKey = col.attachedRigidbody != null
                    ? col.attachedRigidbody.GetInstanceID()
                    : col.transform.root.GetInstanceID();

                var alreadyAdded = false;
                for (var keyIndex = 0; keyIndex < uniqueNeighborCount; keyIndex++)
                {
                    if (uniqueNeighborIds[keyIndex] != neighborKey)
                        continue;
                    alreadyAdded = true;
                    break;
                }

                if (alreadyAdded)
                    continue;

                uniqueNeighborIds[uniqueNeighborCount++] = neighborKey;

                var neighborPosition = col.attachedRigidbody != null ? col.attachedRigidbody.worldCenterOfMass : col.bounds.center;
                var delta = transform.position - neighborPosition;
                var planarDelta = Vector3.ProjectOnPlane(delta, upAxis);
                var sqr = planarDelta.sqrMagnitude;
                if (sqr <= 0.0001f || sqr > radiusSqr)
                    continue;

                var directionToNeighbor = -planarDelta.normalized;
                if (boidUseForwardNeighborFilter && referenceForward.sqrMagnitude > 0.0001f)
                {
                    var forwardDot = Vector3.Dot(referenceForward, directionToNeighbor);
                    if (forwardDot < boidNeighborForwardDotMin)
                        continue;
                }

                var distance = Mathf.Sqrt(sqr);
                var normalizedDistance = Mathf.Clamp01(distance / radius);
                var strength = 1f - normalizedDistance;
                var exponent = Mathf.Max(1f, boidSeparationExponent);
                strength = Mathf.Pow(strength, exponent);
                separation += planarDelta.normalized * strength;
                neighborCount++;
            }

            if (neighborCount == 0)
                return goalPlanarVelocity;

            separation /= neighborCount;

            var goalPlanar = Vector3.ProjectOnPlane(goalPlanarVelocity, upAxis);
            if (goalPlanar.sqrMagnitude > 0.0001f)
            {
                var goalDir = goalPlanar.normalized;
                var lateralSeparation = Vector3.ProjectOnPlane(separation, upAxis);
                var forwardComponent = Vector3.Dot(lateralSeparation, goalDir);
                if (forwardComponent > 0f)
                    lateralSeparation -= goalDir * forwardComponent;
                separation = lateralSeparation;
            }

            var blended = goalPlanarVelocity * boidGoalWeight + separation * boidSeparationWeight;
            return Vector3.ProjectOnPlane(blended, upAxis);
        }

        private void ResetInputState()
        {
            _moveInput = Vector2.zero;
            _moveDirection = Vector3.zero;
            _filteredSteeringPlanar = Vector3.zero;
            _jumpToken = 0;
            _lastConsumedJumpToken = 0;
            _inputSource.SetMove(Vector2.zero);
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
