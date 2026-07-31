using UnityEngine;
using UnityEngine.AI;
using Unity.Mathematics;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [RequireComponent(typeof(ActorCompositeMotor))]
    public partial class NpcNavMeshController : MonoBehaviour, INpcLocomotionState
    {
        [Header("Crowd Contact")]
        [SerializeField] private NpcCrowdContactSettings crowdContactSettings = NpcCrowdContactSettings.CreateDefault();

        private NpcNavMeshMovementModule movement;
        private NpcNavMeshSpeedModule speed;
        private NpcNavMeshJumpModule jump;
        private NpcNavMeshSteeringModule steering;
        private NpcNavMeshAvoidanceModule avoidance;

        private float minMoveInputMagnitude => steering != null ? steering.MinMoveInputMagnitude : 0.2f;
        private float corneringInputReduction => steering != null ? steering.CorneringInputReduction : 0f;
        private float corneringInputMaxAngle => steering != null ? steering.CorneringInputMaxAngle : 90f;
        private float arrivalInputMinScale => steering != null ? steering.ArrivalInputMinScale : 0.3f;
        private float steeringUpdateInterval => avoidance != null ? avoidance.UpdateInterval : 0.08f;
        private float navCornerDirectionWeight => steering != null ? steering.NavCornerDirectionWeight : 0.65f;
        private float navCornerMinDistance => steering != null ? steering.NavCornerMinDistance : 0.1f;
        private float boidSeparationRadius => avoidance.BoidSeparationRadius;
        private float boidGoalWeight => avoidance.BoidGoalWeight;
        private float boidSeparationWeight => avoidance.BoidSeparationWeight;
        private float boidSeparationExponent => avoidance.BoidSeparationExponent;
        private bool boidUseForwardNeighborFilter => avoidance.BoidUseForwardNeighborFilter;
        private float boidNeighborForwardDotMin => avoidance.BoidNeighborForwardDotMin;
        private int boidMaxNeighbors => avoidance.BoidMaxNeighbors;
        private float rvoNeighborRadius => avoidance.RvoNeighborRadius;
        private float rvoTimeHorizon => avoidance.RvoTimeHorizon;
        private float rvoGoalWeight => avoidance.RvoGoalWeight;
        private float rvoAvoidanceWeight => avoidance.RvoAvoidanceWeight;
        private float rvoMinApproachSpeed => avoidance.RvoMinApproachSpeed;
        private int rvoMaxNeighbors => avoidance.RvoMaxNeighbors;
        private float rvoSideBias => avoidance.RvoSideBias;
        private float rvoSideSwitchThreshold => avoidance.RvoSideSwitchThreshold;
        private float rvoSideHoldTime => avoidance.RvoSideHoldTime;
        private int rvoPrimaryNeighborCount => avoidance.RvoPrimaryNeighborCount;
        private float steeringLowPassCutoffHz => steering != null ? steering.SteeringLowPassCutoffHz : 3f;
        private float steeringDeadband => steering != null ? steering.SteeringDeadband : 0f;
        private float steeringDirectionDeadbandDeg => steering != null ? steering.SteeringDirectionDeadbandDeg : 0f;
        private float steeringMaxTurnDegPerSec => steering != null ? steering.SteeringMaxTurnDegPerSec : 360f;


        private NavMeshAgent _agent;
        private Rigidbody _rigidbody;
        private ActorCompositeMotor _motor;
        private IActorMoveInputReceiver _moveInputReceiver;
        private IActorMotor _baseMotor;
        private AiActorInputSource _inputSource;
        private ServerDrivenActorController _networkPlayerController;
        private PhysicsPresentationSmoother _presentationSmoother;
        private NpcCrowdMotor _crowdMotor;
        private Core.FallRecovery _fallRecovery;
        private ActorSkillCoordinator _skillCoordinator;
        private IActorTraversalCoordinator _traversalCoordinator;
        private NpcCrowdTraversalInput _traversalInput;
        private NpcCrowdTraversalTestDriver _traversalTestDriver;

        private Vector2 _moveInput;
        private Vector3 _moveDirection;
        private Vector3 _filteredSteeringPlanar;
        private Vector3 _cachedRawSteeringPlanar;
        private Vector3 _cachedTargetPlanarVelocity;
        private float _nextSteeringUpdateTime;
        private float _avoidanceSideSign = 1f;
        private float _avoidanceSideLockUntilTime;
        private int _jumpToken;
        private int _lastConsumedJumpToken;
        private bool _clientSimulationDisabled;
        private Vector3 _crowdSteeringPlanar;
        private bool _hasCrowdSteering;

        private readonly NpcNavMeshController[] _npcNeighborBuffer = new NpcNavMeshController[32];
        private readonly float[] _avoidanceCandidateScores = new float[32];
        private readonly Vector3[] _avoidanceCandidates = new Vector3[32];


        public event System.Action ReturnToCenterStarted;
        public event System.Action<Vector3> DestinationSet;

        public bool HasPath => _agent != null && _agent.isOnNavMesh && _agent.hasPath;
        public bool IsMoving => HorizontalVelocity > 0.01f;
        public bool IsGrounded => _crowdMotor != null ? _crowdMotor.IsGrounded : _networkPlayerController != null ? _networkPlayerController.IsGrounded : _motor != null && _motor.IsGrounded;
        public bool IsJumping => _crowdMotor != null ? _crowdMotor.IsJumping : _networkPlayerController != null ? _networkPlayerController.IsJumping : _motor != null && _motor.IsJumping;
        public bool IsFreefall => _crowdMotor != null ? _crowdMotor.IsFreefall : _networkPlayerController != null ? _networkPlayerController.IsFreefall : _motor != null && _motor.IsFreefall;
        public bool IsFallingAfterJump => _crowdMotor != null ? _crowdMotor.IsFallingAfterJump : _networkPlayerController != null ? _networkPlayerController.IsFallingAfterJump : _motor != null && _motor.IsFallingAfterJump;
        public bool IsStrafeMode => false;
        public Vector3 InheritedGroundVelocity => _networkPlayerController != null ? _networkPlayerController.InheritedGroundVelocity : _motor != null ? _motor.InheritedGroundVelocity : Vector3.zero;
        public Vector2 MoveInput => _moveInput;
        public Vector3 MoveDirection => _moveDirection;
        public float HorizontalVelocity => _crowdMotor != null ? _crowdMotor.HorizontalVelocity : _networkPlayerController != null ? _networkPlayerController.HorizontalVelocity : _motor != null ? _motor.HorizontalVelocity : 0f;
        public float VerticalVelocity => _crowdMotor != null ? _crowdMotor.VerticalVelocity : _networkPlayerController != null ? _networkPlayerController.VerticalVelocity : _motor != null ? _motor.VerticalVelocity : 0f;
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

                if (_networkPlayerController != null)
                    return _networkPlayerController.MaxMoveSpeed;

                return _agent != null ? Mathf.Max(_agent.speed, 0.01f) : 1f;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _rigidbody = GetComponent<Rigidbody>();
            _motor = GetComponent<ActorCompositeMotor>();
            _moveInputReceiver = _motor as IActorMoveInputReceiver;
            _baseMotor = GetComponent<IActorMotor>();
            _inputSource = new AiActorInputSource();
            _networkPlayerController = GetComponent<ServerDrivenActorController>();
            _presentationSmoother = GetComponent<PhysicsPresentationSmoother>();
            if (_presentationSmoother == null)
                _presentationSmoother = gameObject.AddComponent<PhysicsPresentationSmoother>();
            _presentationSmoother.Initialize(_rigidbody);
            _presentationSmoother.enabled = false;
            _fallRecovery = GetComponent<Core.FallRecovery>();
            if (_fallRecovery != null)
                _fallRecovery.enabled = false;
            _skillCoordinator = GetComponent<ActorSkillCoordinator>();
            if (_skillCoordinator != null)
                _skillCoordinator.enabled = false;
            _traversalCoordinator = GetComponent<IActorTraversalCoordinator>();
            _traversalInput = GetComponent<NpcCrowdTraversalInput>();
            if (_traversalInput == null)
                _traversalInput = gameObject.AddComponent<NpcCrowdTraversalInput>();
            _traversalTestDriver = GetComponent<NpcCrowdTraversalTestDriver>();
            _crowdMotor = GetComponent<NpcCrowdMotor>();
            if (_crowdMotor == null)
                _crowdMotor = gameObject.AddComponent<NpcCrowdMotor>();
            _crowdMotor.Initialize(_baseMotor, crowdContactSettings);
            if (_baseMotor is Behaviour baseMotorBehaviour)
                baseMotorBehaviour.enabled = false;
            if (_motor != null)
                _motor.enabled = false;
            movement = GetComponent<NpcNavMeshMovementModule>();
            speed = GetComponent<NpcNavMeshSpeedModule>();
            jump = GetComponent<NpcNavMeshJumpModule>();
            steering = GetComponent<NpcNavMeshSteeringModule>();
            avoidance = GetComponent<NpcNavMeshAvoidanceModule>();

            ApplyAgentSettings();

            if (_rigidbody != null)
            {
                _rigidbody.freezeRotation = true;
                _rigidbody.interpolation = RigidbodyInterpolation.None;
            }

            ResetInputState();
            _networkPlayerController?.SetInputSource(_inputSource, transform);
        }

        private void OnEnable()
        {
            NpcCrowdSimulation.Register(this);
            movement = GetComponent<NpcNavMeshMovementModule>();
            speed = GetComponent<NpcNavMeshSpeedModule>();
            jump = GetComponent<NpcNavMeshJumpModule>();
            steering = GetComponent<NpcNavMeshSteeringModule>();
            avoidance = GetComponent<NpcNavMeshAvoidanceModule>();
            ApplyAgentSettings();
            if (movement != null)
            {
                movement.OnReturnToCenterStarted += OnReturnToCenterStarted;
                movement.OnRandomDestinationNeeded += OnRandomDestinationNeeded;
                movement.OnCenterDestinationNeeded += OnCenterDestinationNeeded;
                movement.OnDestinationNeeded += OnDestinationNeeded;
            }

            _inputSource.Enable();
            _networkPlayerController?.SetInputSource(_inputSource, transform);
            ResetInputState();
        }

        private void OnDisable()
        {
            NpcCrowdSimulation.Unregister(this);
            if (movement != null)
            {
                movement.OnReturnToCenterStarted -= OnReturnToCenterStarted;
                movement.OnRandomDestinationNeeded -= OnRandomDestinationNeeded;
                movement.OnCenterDestinationNeeded -= OnCenterDestinationNeeded;
                movement.OnDestinationNeeded -= OnDestinationNeeded;
            }

            _inputSource.Disable();
            _networkPlayerController?.ClearInputSource(_inputSource);
            _motor?.ResetState();
            ResetInputState();
            StopAgent();
            ResetAgentPath();
        }

        private void OnDestroy()
        {
            NpcCrowdSimulation.Unregister(this);
            StopAgent();
            ResetAgentPath();
        }

        internal void TickCrowdUpdate(float deltaTime)
        {
            _presentationSmoother?.TickPresentation();
            _skillCoordinator?.TickSkills(deltaTime);

            if (_networkPlayerController != null)
            {
                if (!_networkPlayerController.IsSpawned)
                    return;

                if (!_networkPlayerController.IsServer)
                {
                    DisableClientSimulation();
                    return;
                }
            }

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            if (movement != null && movement.isActiveAndEnabled)
                movement.ObserveState();

            if (_rigidbody != null)
                _agent.nextPosition = _rigidbody.position;
            else
                _agent.nextPosition = transform.position;

        }

        internal void TickCrowdRecovery() => _fallRecovery?.TickRecovery();

        private void DisableClientSimulation()
        {
            if (_clientSimulationDisabled)
                return;

            _clientSimulationDisabled = true;

            // Remote clients receive the authoritative pose through NetworkTransform.
            // NavMesh and all AI planning are server-only and otherwise add one simulation
            // agent per NPC to every client for no visual benefit.
            if (movement != null)
                movement.enabled = false;
            if (speed != null)
                speed.enabled = false;
            if (jump != null)
                jump.enabled = false;
            if (steering != null)
                steering.enabled = false;
            if (avoidance != null)
                avoidance.enabled = false;
            if (_agent != null)
                _agent.enabled = false;

            _inputSource?.Disable();
            enabled = false;
        }

        internal void TickCrowdPhysics()
        {
            if (_networkPlayerController != null)
            {
                _networkPlayerController.TickServerNpcPhysicsFromCrowd();
                return;
            }

            if (_motor == null)
                return;

            UpdateAiInputSignal();

            var inputState = _inputSource.ReadState();
            _moveInput = inputState.Move;
            _moveDirection = ActorMotor.GetMoveDirection(transform, _moveInput);

            if (inputState.JumpPressed)
                _jumpToken++;

            var jumpThisFrame = _jumpToken != _lastConsumedJumpToken;
            if (jumpThisFrame)
                _lastConsumedJumpToken = _jumpToken;

            _baseMotor?.SetStrafeMode(false);
            _moveInputReceiver?.SetMoveInput(_moveInput);
            _moveInputReceiver?.SetMoveReferenceRotation(transform.rotation);
            _motor.Tick(_moveDirection, jumpThisFrame);
            _presentationSmoother?.CapturePhysicsPose();

            if (_agent != null && _agent.isOnNavMesh && _rigidbody != null)
                _agent.nextPosition = _rigidbody.position;
        }

        internal void PrepareCrowdPhysics(float deltaTime)
        {
            _crowdMotor.BeginSimulationStep(deltaTime);
            UpdateAiInputSignal();
            var inputState = _inputSource.ReadState();
            _moveInput = inputState.Move;
            var jumpRequested = inputState.JumpPressed;
            if (_traversalTestDriver != null && _traversalTestDriver.IsControlling)
                jumpRequested = false;
            if (_traversalTestDriver != null && _traversalTestDriver.ShouldTick)
                _traversalTestDriver.TickTest(_traversalInput, _traversalCoordinator, IsGrounded);
            _traversalInput.Consume(ref _moveInput, ref jumpRequested, out var wireHeld,
                out var wireFire, out var reelInput, out var wireTarget);
            _moveDirection = ActorMotor.GetMoveDirection(transform, _moveInput);
            var wireOrigin = _rigidbody.worldCenterOfMass;
            if (wireHeld)
            {
                // SetWireInput caches its aim result by requested target point. A Crowd
                // NPC can approach a fixed target after an out-of-range result, so the
                // changing origin must explicitly refresh that cached aim before retrying.
                _traversalCoordinator?.SetWireAimCursor(default, false, wireOrigin, wireTarget, true);
            }
            _traversalCoordinator?.SetWireInput(wireHeld, wireFire, reelInput, wireOrigin, wireTarget);
            _traversalCoordinator?.ProcessMotorInput(_moveDirection, jumpRequested, IsGrounded);
            _traversalCoordinator?.ApplyTraversal(_moveDirection, _moveInput, transform.rotation, jumpRequested, IsGrounded);
            _crowdMotor.SetTraversalState(_traversalCoordinator);
            _crowdMotor.SetCommand(_moveDirection * MaxMoveSpeed, jumpRequested);
        }

        internal void CreateCrowdGroundProbes(out CapsulecastCommand castCommand, out OverlapCapsuleCommand overlapCommand) =>
            _crowdMotor.CreateGroundProbes(out castCommand, out overlapCommand);
        internal void CreateCrowdWallProbes(out CapsulecastCommand forward, out CapsulecastCommand left, out CapsulecastCommand right) =>
            _crowdMotor.CreateWallProbes(_moveDirection, out forward, out left, out right);
        internal bool ShouldProbeCrowdWalls => _crowdMotor.ShouldProbeWalls;
        internal bool ShouldProbeCrowdWallsEveryFixedStep => _crowdMotor.ShouldProbeWallsEveryFixedStep;
        internal void ApplyCrowdGroundProbe(RaycastHit hit, ColliderHit overlapHit) => _crowdMotor.ApplyGroundProbe(hit, overlapHit);
        internal void ResolveCrowdEnvironmentOverlaps(ColliderHit hit0, ColliderHit hit1, ColliderHit hit2, ColliderHit hit3) =>
            _crowdMotor.ResolveEnvironmentOverlaps(hit0, hit1, hit2, hit3);
        internal void ApplyCrowdWallProbes(RaycastHit forward, RaycastHit left, RaycastHit right) =>
            _crowdMotor.ApplyWallProbes(forward, left, right);
        internal void ClearCrowdWallProbe() => _crowdMotor.ClearWallProbe();
        internal NpcCrowdMotor.MovementData CaptureCrowdMovementData() => _crowdMotor.CaptureMovementData();

        internal void ApplyCrowdMovement(NpcCrowdMotor.MovementResult result, float deltaTime)
        {
            _crowdMotor.ApplyMovement(result);
            _presentationSmoother?.CapturePhysicsPose(deltaTime);
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh && _crowdMotor.IsGrounded)
                _agent.nextPosition = _rigidbody.position;
            _networkPlayerController?.ApplyServerNpcCrowdState(
                HorizontalVelocity,
                VerticalVelocity,
                IsGrounded,
                IsJumping,
                IsFreefall,
                IsFallingAfterJump);
        }

        internal NpcCrowdSimulation.AgentData CaptureCrowdAgentData()
        {
            var mode = avoidance != null ? (int)avoidance.Mode : 0;
            var isRvo = mode == (int)NpcNavMeshAvoidanceModule.AvoidanceMode.Rvo;
            return new NpcCrowdSimulation.AgentData
            {
                Position = transform.position,
                Velocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero,
                GoalVelocity = _cachedTargetPlanarVelocity,
                UpAxis = ActorMotor.GetUpAxis(),
                Radius = isRvo ? rvoNeighborRadius : boidSeparationRadius,
                TimeHorizon = isRvo ? rvoTimeHorizon : 1f,
                GoalWeight = isRvo ? rvoGoalWeight : boidGoalWeight,
                AvoidanceWeight = isRvo ? rvoAvoidanceWeight : boidSeparationWeight,
                SeparationExponent = isRvo ? 1f : boidSeparationExponent,
                MinApproachSpeed = isRvo ? rvoMinApproachSpeed : 0f,
                ForwardDotMin = isRvo ? -1f : boidNeighborForwardDotMin,
                MaxNeighbors = isRvo ? rvoMaxNeighbors : boidMaxNeighbors,
                Mode = mode,
                UseForwardFilter = !isRvo && boidUseForwardNeighborFilter ? 1 : 0
            };
        }

        internal void ApplyCrowdSteering(float3 steering)
        {
            // PrepareCrowdPhysics already wrote the deterministic test command.
            // Applying the random NavMesh/boid steering afterward would replace it.
            if (_traversalTestDriver != null && _traversalTestDriver.IsControlling)
                return;
            _crowdSteeringPlanar = new Vector3(steering.x, steering.y, steering.z);
            _hasCrowdSteering = true;
            _crowdMotor?.SetCommand(_crowdSteeringPlanar, false);
        }

        private void OnRandomDestinationNeeded()
        {
            if (speed != null && speed.isActiveAndEnabled)
                speed.RandomizeForSegment();
            if (movement != null && movement.isActiveAndEnabled)
                movement.RequestRandomDestination();
        }

        private void OnCenterDestinationNeeded()
        {
            if (movement != null && movement.isActiveAndEnabled)
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
            if (speed != null && speed.isActiveAndEnabled)
                speed.ApplyReturnToCenterSpeedBoost();
            ReturnToCenterStarted?.Invoke();
        }

        private void OnValidate()
        {
            ApplyAgentSettings();
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

            var now = Time.time;
            var steeringPlanUpdated = now >= _nextSteeringUpdateTime;
            if (steeringPlanUpdated)
            {
                _nextSteeringUpdateTime = now + Mathf.Max(0.01f, steeringUpdateInterval);

                // Path extraction and neighborhood queries stay rate-limited. Their result is
                // consumed by the inexpensive filter below on every player-loop update.
                var planningUpAxis = ActorMotor.GetUpAxis();
                _cachedTargetPlanarVelocity = BuildTargetPlanarVelocity(planningUpAxis);
                _cachedRawSteeringPlanar = _cachedTargetPlanarVelocity;
                // Crowd avoidance is calculated once for every NPC by SteeringJob.
                // Running the legacy per-NPC Boid/RVO neighbor scan here duplicates
                // that work on the Main Thread and becomes superlinear in dense crowds.
            }

            var upAxis = ActorMotor.GetUpAxis();
            var steeringPlanar = ApplySteeringLowPass(upAxis, _cachedRawSteeringPlanar);
            steeringPlanar = ApplySteeringTurnRateLimit(upAxis, steeringPlanar);
            steeringPlanar = ApplySteeringDeadband(steeringPlanar);

            var localDesired = transform.InverseTransformDirection(steeringPlanar);
            var nextMoveInput = new Vector2(localDesired.x, localDesired.z);
            nextMoveInput = ApplyAdaptiveMoveInputMagnitude(nextMoveInput, _cachedTargetPlanarVelocity, upAxis);
            if (nextMoveInput.sqrMagnitude > 1f)
                nextMoveInput = nextMoveInput.normalized;

            _moveInput = nextMoveInput;
            _moveDirection = ActorMotor.GetMoveDirection(transform, nextMoveInput);
            _inputSource.SetMove(nextMoveInput);

            if (steeringPlanUpdated && jump != null
                && jump.TryRequestJump(IsGrounded, _cachedTargetPlanarVelocity.magnitude))
                _inputSource.QueueJump();
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

            // NavMeshAgent.path creates a managed NavMeshPath. With hundreds of NPCs,
            // reading it on every steering refresh produced MBs of garbage per frame.
            // desiredVelocity already follows the current corridor and its corners, so
            // use that allocation-free result as the crowd goal velocity.
            return desiredPlanar;
        }

        private Vector2 ApplyAdaptiveMoveInputMagnitude(Vector2 moveInput, Vector3 targetPlanarVelocity, Vector3 upAxis)
        {
            var directionMagnitude = moveInput.magnitude;
            if (directionMagnitude <= 0.0001f)
                return Vector2.zero;

            var normalizedInput = moveInput / directionMagnitude;
            var maxMoveSpeed = Mathf.Max(0.01f, MaxMoveSpeed);
            var targetSpeedRatio = Mathf.Clamp01(targetPlanarVelocity.magnitude / maxMoveSpeed);
            var inputScale = Mathf.Lerp(minMoveInputMagnitude, 1f, targetSpeedRatio);

            var currentPlanarDirection = Vector3.ProjectOnPlane(transform.forward, upAxis);
            if (currentPlanarDirection.sqrMagnitude > 0.0001f && targetPlanarVelocity.sqrMagnitude > 0.0001f)
            {
                var targetDirection = targetPlanarVelocity.normalized;
                var turnAngle = Vector3.Angle(currentPlanarDirection.normalized, targetDirection);
                var maxAngle = Mathf.Max(0.0001f, corneringInputMaxAngle);
                var turnRatio = Mathf.Clamp01(turnAngle / maxAngle);
                var cornerScale = Mathf.Lerp(1f, 1f - corneringInputReduction, turnRatio);
                inputScale *= cornerScale;
            }

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh && _agent.hasPath)
            {
                var remainingDistance = _agent.remainingDistance;
                if (!float.IsInfinity(remainingDistance) && !float.IsNaN(remainingDistance))
                {
                    var slowDistance = Mathf.Max(0.001f, _agent.stoppingDistance);
                    var arrivalRatio = Mathf.Clamp01(remainingDistance / slowDistance);
                    var arrivalScale = Mathf.Lerp(arrivalInputMinScale, 1f, arrivalRatio);
                    inputScale *= arrivalScale;
                }
            }

            inputScale = Mathf.Clamp(inputScale, 0f, 1f);
            return normalizedInput * inputScale;
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
            var cutoff = Mathf.Max(0.1f, steeringLowPassCutoffHz);
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

            var directionDelta = Vector3.Angle(current, target);
            if (directionDelta <= Mathf.Max(0f, steeringDirectionDeadbandDeg))
                return current.normalized * target.magnitude;

            var maxTurn = Mathf.Max(1f, steeringMaxTurnDegPerSec)
                * Mathf.Max(Time.deltaTime, 0.0001f);
            var limited = Vector3.RotateTowards(current.normalized, target.normalized, maxTurn * Mathf.Deg2Rad, 0f);
            return limited * target.magnitude;
        }

        private Vector3 ApplySteeringDeadband(Vector3 steeringPlanar)
        {
            var deadband = Mathf.Max(0f, steeringDeadband);
            return steeringPlanar.sqrMagnitude <= deadband * deadband ? Vector3.zero : steeringPlanar;
        }

        private Vector3 BuildRvoSteeringPlanar(Vector3 upAxis, Vector3 goalPlanarVelocity)
        {
            if (_hasCrowdSteering)
                return Vector3.ProjectOnPlane(_crowdSteeringPlanar, upAxis);

            var goalPlanar = Vector3.ProjectOnPlane(goalPlanarVelocity, upAxis);
            var goalSpeed = goalPlanar.magnitude;
            if (goalSpeed <= 0.0001f)
                return goalPlanarVelocity;

            var radius = Mathf.Max(0.1f, rvoNeighborRadius);
            var count = GetSpatialNeighbors(this, radius, _npcNeighborBuffer);
            if (count <= 0)
                return goalPlanarVelocity;

            var maxNeighbors = Mathf.Clamp(rvoMaxNeighbors, 1, _npcNeighborBuffer.Length);
            var primaryNeighborCount = Mathf.Clamp(rvoPrimaryNeighborCount, 1, maxNeighbors);
            var candidateCount = 0;
            var selfPos = transform.position;
            var selfVel = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
            var selfPlanarVel = Vector3.ProjectOnPlane(selfVel, upAxis);
            var agentRadius = Mathf.Max(0.05f, _agent.radius);
            var timeHorizon = Mathf.Max(0.1f, rvoTimeHorizon);
            var goalDirection = goalPlanar.sqrMagnitude > 0.000001f ? goalPlanar.normalized : transform.forward;
            goalDirection = Vector3.ProjectOnPlane(goalDirection, upAxis).normalized;
            var now = Time.time;

            for (var i = 0; i < count && candidateCount < maxNeighbors; i++)
            {
                var other = _npcNeighborBuffer[i];
                if (other == null)
                    continue;
                var otherBody = other._rigidbody;
                var otherPos = otherBody != null ? otherBody.worldCenterOfMass : other.transform.position;
                var relPos = Vector3.ProjectOnPlane(otherPos - selfPos, upAxis);
                var dist = relPos.magnitude;
                if (dist <= 0.0001f || dist > radius)
                    continue;

                var relDir = relPos / dist;
                var otherVel = otherBody != null ? otherBody.linearVelocity : Vector3.zero;
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
                _avoidanceCandidateScores[candidateCount] = score + bias;
                _avoidanceCandidates[candidateCount] = side * (chosenSign * (score + bias));
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
                    var score = _avoidanceCandidateScores[candidateIndex];
                    if (score <= bestScore)
                        continue;
                    bestScore = score;
                    bestIndex = candidateIndex;
                }

                if (bestIndex < 0)
                    break;

                avoidance += _avoidanceCandidates[bestIndex];
                _avoidanceCandidateScores[bestIndex] = float.MinValue;
                selectedCount++;
            }

            if (selectedCount == 0)
                return goalPlanarVelocity;

            avoidance /= selectedCount;
            _avoidanceSideSign = Mathf.Sign(Vector3.Dot(Vector3.Cross(upAxis, avoidance), goalDirection));
            if (Mathf.Approximately(_avoidanceSideSign, 0f))
                _avoidanceSideSign = 1f;

            var goalContribution = goalPlanarVelocity * rvoGoalWeight;
            var avoidanceContribution = avoidance * rvoAvoidanceWeight;
            // Avoidance is a lateral correction. It must not overpower the requested travel
            // speed and turn a near-stationary agent around its own axis.
            avoidanceContribution = Vector3.ClampMagnitude(
                avoidanceContribution,
                goalContribution.magnitude * 0.75f);
            var blended = goalContribution + avoidanceContribution;
            return Vector3.ProjectOnPlane(blended, upAxis);
        }

        private void ResetInputState()
        {
            _moveInput = Vector2.zero;
            _moveDirection = Vector3.zero;
            _filteredSteeringPlanar = Vector3.zero;
            _cachedRawSteeringPlanar = Vector3.zero;
            _cachedTargetPlanarVelocity = Vector3.zero;
            _crowdSteeringPlanar = Vector3.zero;
            _hasCrowdSteering = true;
            // Spread expensive steering/physics queries across frames. Without this phase,
            // every NPC spawned in one batch performs its query on the same frame.
            var phase = (GetInstanceID() & 0x7fffffff) % 997 / 997f;
            _nextSteeringUpdateTime = Time.time + Mathf.Max(0.01f, steeringUpdateInterval) * phase;
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
