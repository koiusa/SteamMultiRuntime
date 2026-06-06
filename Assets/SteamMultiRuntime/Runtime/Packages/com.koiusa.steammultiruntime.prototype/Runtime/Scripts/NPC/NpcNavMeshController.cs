using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [RequireComponent(typeof(PlayerCompositeMotor))]
    public partial class NpcNavMeshController : MonoBehaviour, IPlayerController
    {

        [SerializeField] private NpcNavMeshMovementModule movement = new();
        [SerializeField] private NpcNavMeshSpeedModule speed = new();

        [Header("AI Input")]
        [SerializeField] private bool randomJumpEnabled = true;
        [SerializeField, Range(0f, 1f)] private float jumpChancePerSecond = 0.1f;
        [SerializeField, Min(0f)] private float jumpCooldownMin = 1.5f;
        [SerializeField, Min(0f)] private float jumpCooldownMax = 4.0f;
        [SerializeField, Min(0f)] private float minHorizontalSpeedToJump = 0.35f;

        private enum LocalAvoidanceMode
        {
            None = 0,
            Boid = 1,
            Rvo = 2
        }

        [Header("Local Avoidance")]
        [SerializeField] private LocalAvoidanceMode localAvoidanceMode = LocalAvoidanceMode.Rvo;
        [SerializeField, Min(0.01f)] private float steeringUpdateInterval = 0.08f;
        [SerializeField] private bool steeringHoldLastValueBetweenUpdates = true;
        [SerializeField, Range(0f, 1f)] private float navCornerDirectionWeight = 0.65f;
        [SerializeField, Min(0f)] private float navCornerMinDistance = 0.1f;

        [Header("Boid Separation")]
        [SerializeField, Min(0.1f)] private float boidSeparationRadius = 1.6f;
        [SerializeField, Min(0f)] private float boidGoalWeight = 1f;
        [SerializeField, Min(0f)] private float boidSeparationWeight = 1.25f;
        [SerializeField, Min(1f)] private float boidSeparationExponent = 2.2f;
        [SerializeField] private bool boidUseForwardNeighborFilter = true;
        [SerializeField, Range(-1f, 1f)] private float boidNeighborForwardDotMin = 0f;
        [SerializeField, Min(1)] private int boidMaxNeighbors = 8;

        [Header("RVO-style Local Avoidance")]
        [SerializeField, Min(0.1f)] private float rvoNeighborRadius = 2f;
        [SerializeField, Min(0.05f)] private float rvoAgentRadius = 0.45f;
        [SerializeField, Min(0.1f)] private float rvoTimeHorizon = 1.2f;
        [SerializeField, Min(0f)] private float rvoGoalWeight = 1f;
        [SerializeField, Min(0f)] private float rvoAvoidanceWeight = 1.35f;
        [SerializeField, Min(0f)] private float rvoMinApproachSpeed = 0.05f;
        [SerializeField, Min(1)] private int rvoMaxNeighbors = 10;
        [SerializeField, Min(0f)] private float rvoSideBias = 0.15f;
        [SerializeField, Min(0f)] private float rvoSideSwitchThreshold = 0.2f;
        [SerializeField, Min(0f)] private float rvoSideHoldTime = 0.35f;
        [SerializeField, Min(1)] private int rvoPrimaryNeighborCount = 2;

        [Header("Steering Filter")]
        [SerializeField, Min(0.1f)] private float boidLowPassCutoffHz = 3f;
        [SerializeField, Min(0f)] private float boidDeadband = 0.06f;
        [SerializeField, Min(1f)] private float boidMaxTurnDegPerSec = 180f;
        [SerializeField, Min(0f)] private float boidSideSwitchMin = 0.18f;
        [SerializeField, Min(0f)] private float boidSideHoldTime = 0.2f;


        private NavMeshAgent _agent;
        private Rigidbody _rigidbody;
        private PlayerCompositeMotor _motor;
        private IPlayerMoveInputReceiver _moveInputReceiver;
        private IPlayerMotor _baseMotor;
        private AiPlayerInputSource _inputSource;

        private Vector2 _moveInput;
        private Vector3 _moveDirection;
        private Vector3 _filteredSteeringPlanar;
        private Vector3 _cachedRawSteeringPlanar;
        private Vector3 _cachedSteeringPlanar;
        private float _nextSteeringUpdateTime;
        private float _avoidanceSideSign = 1f;
        private float _avoidanceSideLockUntilTime;
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

            if (_rigidbody != null)
                _agent.nextPosition = _rigidbody.position;
            else
                _agent.nextPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (_motor == null)
                return;

            UpdateAiInputSignal();

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
            if (steeringUpdateInterval < 0.01f)
                steeringUpdateInterval = 0.01f;
            navCornerDirectionWeight = Mathf.Clamp01(navCornerDirectionWeight);
            if (navCornerMinDistance < 0f)
                navCornerMinDistance = 0f;
            if (boidSeparationRadius < 0.1f)
                boidSeparationRadius = 0.1f;
            if (boidSeparationExponent < 1f)
                boidSeparationExponent = 1f;
            boidNeighborForwardDotMin = Mathf.Clamp(boidNeighborForwardDotMin, -1f, 1f);
            if (boidMaxNeighbors < 1)
                boidMaxNeighbors = 1;
            if (rvoNeighborRadius < 0.1f)
                rvoNeighborRadius = 0.1f;
            if (rvoAgentRadius < 0.05f)
                rvoAgentRadius = 0.05f;
            if (rvoTimeHorizon < 0.1f)
                rvoTimeHorizon = 0.1f;
            if (rvoGoalWeight < 0f)
                rvoGoalWeight = 0f;
            if (rvoAvoidanceWeight < 0f)
                rvoAvoidanceWeight = 0f;
            if (rvoMinApproachSpeed < 0f)
                rvoMinApproachSpeed = 0f;
            if (rvoMaxNeighbors < 1)
                rvoMaxNeighbors = 1;
            if (rvoSideBias < 0f)
                rvoSideBias = 0f;
            if (rvoSideSwitchThreshold < 0f)
                rvoSideSwitchThreshold = 0f;
            if (rvoSideHoldTime < 0f)
                rvoSideHoldTime = 0f;
            if (rvoPrimaryNeighborCount < 1)
                rvoPrimaryNeighborCount = 1;
            if (boidLowPassCutoffHz < 0.1f)
                boidLowPassCutoffHz = 0.1f;
            if (boidDeadband < 0f)
                boidDeadband = 0f;
            if (boidMaxTurnDegPerSec < 1f)
                boidMaxTurnDegPerSec = 1f;
            if (boidSideSwitchMin < 0f)
                boidSideSwitchMin = 0f;
            if (boidSideHoldTime < 0f)
                boidSideHoldTime = 0f;
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
            if (_inputSource == null)
                return;

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                _inputSource.SetMove(Vector2.zero);
                return;
            }

            var upAxis = PlayerMotor.GetUpAxis();
            var targetPlanarVelocity = BuildTargetPlanarVelocity(upAxis);

            var now = Time.time;
            if (now >= _nextSteeringUpdateTime)
            {
                _nextSteeringUpdateTime = now + steeringUpdateInterval;

                var rawSteering = targetPlanarVelocity;
                switch (localAvoidanceMode)
                {
                    case LocalAvoidanceMode.Boid:
                        rawSteering = BuildBoidSteeringPlanar(upAxis, targetPlanarVelocity);
                        break;
                    case LocalAvoidanceMode.Rvo:
                        rawSteering = BuildRvoSteeringPlanar(upAxis, targetPlanarVelocity);
                        break;
                    default:
                        rawSteering = targetPlanarVelocity;
                        break;
                }

                _cachedRawSteeringPlanar = rawSteering;
            }
            else if (!steeringHoldLastValueBetweenUpdates)
            {
                _cachedRawSteeringPlanar = targetPlanarVelocity;
            }

            var steeringPlanar = ApplySteeringLowPass(upAxis, _cachedRawSteeringPlanar);
            steeringPlanar = ApplySteeringTurnRateLimit(upAxis, steeringPlanar);
            steeringPlanar = ApplySteeringDeadband(steeringPlanar);
            _cachedSteeringPlanar = steeringPlanar;

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

        private Vector3 BuildTargetPlanarVelocity(Vector3 upAxis)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return Vector3.zero;
            if (_agent.pathPending || _agent.isStopped || !_agent.hasPath)
                return Vector3.zero;

            var desiredPlanar = Vector3.ProjectOnPlane(_agent.desiredVelocity, upAxis);
            var desiredSpeed = desiredPlanar.magnitude;
            if (desiredSpeed <= 0.0001f)
                return Vector3.zero;

            var desiredDirection = desiredPlanar / desiredSpeed;
            var cornerDirection = desiredDirection;
            var corners = _agent.path.corners;
            if (corners != null && corners.Length > 1)
            {
                var cornerIndex = 1;
                var minDistance = Mathf.Max(0f, navCornerMinDistance);
                while (cornerIndex < corners.Length)
                {
                    var toCorner = Vector3.ProjectOnPlane(corners[cornerIndex] - transform.position, upAxis);
                    if (toCorner.magnitude > minDistance)
                    {
                        cornerDirection = toCorner.normalized;
                        break;
                    }
                    cornerIndex++;
                }
            }

            var cornerWeight = Mathf.Clamp01(navCornerDirectionWeight);
            var blendedDirection = Vector3.Lerp(desiredDirection, cornerDirection, cornerWeight);
            if (blendedDirection.sqrMagnitude <= 0.000001f)
                blendedDirection = desiredDirection;

            return Vector3.ProjectOnPlane(blendedDirection.normalized * desiredSpeed, upAxis);
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

        private Vector3 BuildRvoSteeringPlanar(Vector3 upAxis, Vector3 goalPlanarVelocity)
        {
            var radius = Mathf.Max(0.1f, rvoNeighborRadius);
            var count = Physics.OverlapSphereNonAlloc(transform.position, radius, _boidNeighborBuffer, ~0, QueryTriggerInteraction.Ignore);
            if (count <= 0)
                return goalPlanarVelocity;

            var maxNeighbors = Mathf.Clamp(rvoMaxNeighbors, 1, _boidNeighborBuffer.Length);
            var primaryNeighborCount = Mathf.Clamp(rvoPrimaryNeighborCount, 1, maxNeighbors);
            var uniqueNeighborIds = new int[32];
            var uniqueNeighborCount = 0;
            var candidateScores = new float[32];
            var candidateAvoidances = new Vector3[32];
            var candidateCount = 0;
            var selfPos = transform.position;
            var selfVel = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
            var selfPlanarVel = Vector3.ProjectOnPlane(selfVel, upAxis);
            var agentRadius = Mathf.Max(0.05f, rvoAgentRadius);
            var timeHorizon = Mathf.Max(0.1f, rvoTimeHorizon);
            var goalPlanar = Vector3.ProjectOnPlane(goalPlanarVelocity, upAxis);
            var goalDirection = goalPlanar.sqrMagnitude > 0.000001f ? goalPlanar.normalized : transform.forward;
            goalDirection = Vector3.ProjectOnPlane(goalDirection, upAxis).normalized;
            var now = Time.time;

            for (var i = 0; i < count && candidateCount < maxNeighbors; i++)
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

                var otherPos = col.attachedRigidbody != null ? col.attachedRigidbody.worldCenterOfMass : col.bounds.center;
                var relPos = Vector3.ProjectOnPlane(otherPos - selfPos, upAxis);
                var dist = relPos.magnitude;
                if (dist <= 0.0001f || dist > radius)
                    continue;

                var relDir = relPos / dist;
                var otherVel = col.attachedRigidbody != null ? col.attachedRigidbody.linearVelocity : Vector3.zero;
                var otherPlanarVel = Vector3.ProjectOnPlane(otherVel, upAxis);
                var relVel = selfPlanarVel - otherPlanarVel;
                var approachSpeed = Vector3.Dot(relVel, relDir);
                if (approachSpeed <= rvoMinApproachSpeed)
                    continue;

                var combinedRadius = agentRadius * 2f;
                var timeToCollision = (dist - combinedRadius) / Mathf.Max(approachSpeed, 0.001f);
                if (timeToCollision < 0f || timeToCollision > timeHorizon)
                    continue;

                var side = Vector3.Cross(upAxis, relDir);
                if (side.sqrMagnitude <= 0.0001f)
                    continue;

                side.Normalize();
                var preferredSign = Vector3.Dot(side, goalDirection) >= 0f ? 1f : -1f;
                var chosenSign = preferredSign;
                var signedPreference = preferredSign * _avoidanceSideSign;
                var canSwitchSide = now >= _avoidanceSideLockUntilTime;
                if (!canSwitchSide)
                {
                    chosenSign = _avoidanceSideSign;
                }
                else if (signedPreference < -Mathf.Max(0f, rvoSideSwitchThreshold))
                {
                    chosenSign = preferredSign;
                    _avoidanceSideSign = preferredSign;
                    _avoidanceSideLockUntilTime = now + Mathf.Max(0f, rvoSideHoldTime);
                }
                else
                {
                    chosenSign = _avoidanceSideSign;
                }

                var urgency = 1f - Mathf.Clamp01(timeToCollision / timeHorizon);
                var proximity = 1f - Mathf.Clamp01(dist / radius);
                var score = urgency * urgency + proximity * 0.5f;
                var bias = chosenSign == _avoidanceSideSign ? rvoSideBias : 0f;
                candidateScores[candidateCount] = score + bias;
                candidateAvoidances[candidateCount] = side * (chosenSign * (score + bias));
                candidateCount++;
            }

            if (candidateCount == 0)
                return goalPlanarVelocity;

            var selectedCount = 0;
            var avoidance = Vector3.zero;
            for (var selectedIndex = 0; selectedIndex < primaryNeighborCount; selectedIndex++)
            {
                var bestIndex = -1;
                var bestScore = float.MinValue;
                for (var candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
                {
                    var score = candidateScores[candidateIndex];
                    if (score <= bestScore)
                        continue;
                    bestScore = score;
                    bestIndex = candidateIndex;
                }

                if (bestIndex < 0)
                    break;

                avoidance += candidateAvoidances[bestIndex];
                candidateScores[bestIndex] = float.MinValue;
                selectedCount++;
            }

            if (selectedCount == 0)
                return goalPlanarVelocity;

            avoidance /= selectedCount;
            _avoidanceSideSign = Mathf.Sign(Vector3.Dot(Vector3.Cross(upAxis, avoidance), goalDirection));
            if (Mathf.Approximately(_avoidanceSideSign, 0f))
                _avoidanceSideSign = 1f;

            var blended = goalPlanarVelocity * rvoGoalWeight + avoidance * rvoAvoidanceWeight;
            return Vector3.ProjectOnPlane(blended, upAxis);
        }

        private void ResetInputState()
        {
            _moveInput = Vector2.zero;
            _moveDirection = Vector3.zero;
            _filteredSteeringPlanar = Vector3.zero;
            _cachedRawSteeringPlanar = Vector3.zero;
            _cachedSteeringPlanar = Vector3.zero;
            _nextSteeringUpdateTime = 0f;
            _avoidanceSideSign = 1f;
            _avoidanceSideLockUntilTime = 0f;
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
