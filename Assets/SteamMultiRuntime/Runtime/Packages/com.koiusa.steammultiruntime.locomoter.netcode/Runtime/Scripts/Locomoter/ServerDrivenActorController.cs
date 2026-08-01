using Koiusa.Input;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [RequireComponent(typeof(ActorCompositeMotor))]
    [RequireComponent(typeof(NetworkRigidbody))]
    public class ServerDrivenActorController : NetworkBehaviour, IActorLocomotionState, IActorLadderState, IActorWallRunState
    {
        private const float MovingPlatformPositionThreshold = 0.02f;
        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private GamepadAimCursorSettings gamepadAimCursorSettings = new();

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Network State")]
        [SerializeField] private NetworkControlMode controlMode = NetworkControlMode.Player;

        private Rigidbody targetRigidbody;
        private PlayerGameplayInputReader baseInputSource;
        private IActorInputSource activeInputSource;
        private Transform injectedInputReferenceTransform;
        private ActorCompositeMotor motor;
        private IActorMoveInputReceiver moveInputReceiver;
        private IActorTraversalCoordinator traversalCoordinator;
        private ActorFacingRequestResolver facingRequestResolver;
        private PhysicsPresentationSmoother presentationSmoother;
        private NetworkTransform networkTransform;
        private float defaultPositionThreshold;
        private bool useMovingPlatformSync;
        private int jumpToken;
        private int lastConsumedJumpToken;
        private int grappleFireToken;
        private int lastConsumedGrappleFireToken;
        private bool isStrafeMode;
        private bool hasInitializedSettings;
        private ActorInputSyncState localInputState;
        private ActorInputSyncState serverInputState;
        private float nextInputSendTime;
        private int lastSentJumpToken = -1;
        private int lastSentGrappleFireToken = -1;
        private bool lastSentGrappleHeld;
        private float lastSentReelInput = float.NaN;
        private float nextStateSyncTime;
        private bool hasServerNpcCrowdState;
        private bool serverNpcConventionalMotorEnabled;
        private ActorKinematicState serverNpcCrowdKinematicState;
        private ActorMovementFlagsState serverNpcCrowdMovementState;

        private readonly NetworkVariable<ActorInputSyncState> netInputState = new NetworkVariable<ActorInputSyncState>(
            new ActorInputSyncState(Vector3.zero, Vector2.zero, Quaternion.identity, 0, false), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Settings Sync (Server -> All Clients)
        private readonly NetworkVariable<ActorMotorSettingsNetData> netActorMotorSettings = new NetworkVariable<ActorMotorSettingsNetData>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<TraversalFeatureSettingsNetData> netTraversalFeatureSettings = new NetworkVariable<TraversalFeatureSettingsNetData>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ActorKinematicState> netKinematicState = new NetworkVariable<ActorKinematicState>(
            new ActorKinematicState(0f, 0f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ActorMovementFlagsState> netMovementFlagsState = new NetworkVariable<ActorMovementFlagsState>(
            new ActorMovementFlagsState(true, false, false, false, false, 0f, false, Vector3.zero), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<WireSwingNetworkState> netWireSwingState = new NetworkVariable<WireSwingNetworkState>(
            new WireSwingNetworkState(false, Vector3.zero, 0f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkControlPolicy ControlPolicy => NetworkControlPolicies.Get(controlMode);
        private bool UseLocalMotorState => IsSpawned && IsServer;
        private bool UseServerNpcCrowdState => UseLocalMotorState && controlMode == NetworkControlMode.ServerNpc && hasServerNpcCrowdState;

        public bool IsGrounded => UseServerNpcCrowdState ? serverNpcCrowdMovementState.IsGrounded : UseLocalMotorState && motor != null ? motor.IsGrounded : netMovementFlagsState.Value.IsGrounded;
        public bool IsJumping => UseServerNpcCrowdState ? serverNpcCrowdMovementState.IsJumping : UseLocalMotorState && motor != null ? motor.IsJumping : netMovementFlagsState.Value.IsJumping;
        public bool IsFreefall => UseServerNpcCrowdState ? serverNpcCrowdMovementState.IsFreefall : UseLocalMotorState && motor != null ? motor.IsFreefall : netMovementFlagsState.Value.IsFreefall;
        public bool IsFallingAfterJump => UseServerNpcCrowdState ? serverNpcCrowdMovementState.IsFallingAfterJump : UseLocalMotorState && motor != null ? motor.IsFallingAfterJump : netMovementFlagsState.Value.IsFallingAfterJump;
        public bool IsOnLadder => UseLocalMotorState ? traversalCoordinator != null && traversalCoordinator.IsOnLadder : netMovementFlagsState.Value.IsOnLadder;
        public float LadderSpeed => UseLocalMotorState && traversalCoordinator != null ? traversalCoordinator.LadderSpeed : netMovementFlagsState.Value.LadderSpeed;
        public bool IsWallRunning => UseLocalMotorState ? traversalCoordinator != null && traversalCoordinator.IsWallRunning : netMovementFlagsState.Value.IsWallRunning;
        public Vector3 WallNormal => UseLocalMotorState && traversalCoordinator != null ? traversalCoordinator.WallNormal : netMovementFlagsState.Value.WallNormal;
        public bool IsStrafeMode => IsOwner ? localInputState.IsStrafeMode : netInputState.Value.IsStrafeMode;
        public Vector3 InheritedGroundVelocity => UseLocalMotorState && motor != null ? motor.InheritedGroundVelocity : Vector3.zero;
        public Vector2 MoveInput => IsOwner ? localInputState.MoveInput : netInputState.Value.MoveInput;
        public Vector3 MoveDirection => IsOwner ? localInputState.MoveDirection : netInputState.Value.MoveDirection;
        public float HorizontalVelocity => UseServerNpcCrowdState ? serverNpcCrowdKinematicState.HorizontalVelocity : UseLocalMotorState && motor != null ? motor.HorizontalVelocity : netKinematicState.Value.HorizontalVelocity;
        public float VerticalVelocity => UseServerNpcCrowdState ? serverNpcCrowdKinematicState.VerticalVelocity : UseLocalMotorState && motor != null ? motor.VerticalVelocity : netKinematicState.Value.VerticalVelocity;
        public float MaxMoveSpeed => 5f;
        public InputActionsConfig InputActionsConfig => inputActionsConfig;

        public void SetInputConfig(InputActionsConfig config)
        {
            if (config == null)
            {
                Debug.LogError("InputProfile cannot be null.", this);
                return;
            }

            inputActionsConfig = config;

            // Reinitialize input source if already created
            activeInputSource?.Disable();

            baseInputSource = new PlayerGameplayInputReader(config, gamepadAimCursorSettings);
            activeInputSource = baseInputSource;
            injectedInputReferenceTransform = null;

            if (IsSpawned && IsOwner)
            {
                activeInputSource.Enable();
            }
        }

        public void SetInputSource(IActorInputSource source, Transform referenceTransform = null)
        {
            if (ReferenceEquals(activeInputSource, source)
                && injectedInputReferenceTransform == referenceTransform)
                return;

            activeInputSource?.Disable();
            activeInputSource = source;
            injectedInputReferenceTransform = referenceTransform;
            if (IsSpawned && IsOwner && isActiveAndEnabled)
                activeInputSource?.Enable();
                Cursor.visible = false;
        }

        public void ClearInputSource(IActorInputSource source)
        {
            if (!ReferenceEquals(activeInputSource, source))
                return;

            activeInputSource?.Disable();
            activeInputSource = baseInputSource;
            injectedInputReferenceTransform = null;
            if (IsSpawned && IsOwner && isActiveAndEnabled)
                activeInputSource?.Enable();
        }

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody>();
            targetRigidbody.freezeRotation = true;
            targetRigidbody.interpolation = RigidbodyInterpolation.None;
            networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform != null)
                defaultPositionThreshold = networkTransform.PositionThreshold;

            presentationSmoother = GetComponent<PhysicsPresentationSmoother>();
            if (presentationSmoother == null)
            {
                presentationSmoother = gameObject.AddComponent<PhysicsPresentationSmoother>();
            }
            presentationSmoother.Initialize(targetRigidbody);

            motor = GetComponent<ActorCompositeMotor>();
            if (motor == null)
            {
                motor = gameObject.AddComponent<ActorCompositeMotor>();
            }

            moveInputReceiver = motor as IActorMoveInputReceiver;
            traversalCoordinator = GetComponent<IActorTraversalCoordinator>();
            facingRequestResolver = new ActorFacingRequestResolver(gameObject);

            if (inputActionsConfig == null)
            {
                return;
            }

            baseInputSource = new PlayerGameplayInputReader(inputActionsConfig, gamepadAimCursorSettings);
            activeInputSource = baseInputSource;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ApplyServerNpcPositionThreshold();
            // Server NPC physics is owned by NpcConventionalPhysicsLoop or the Crowd
            // simulation. Registering it here as well makes the player loop scan every
            // NPC each fixed step only to return immediately.
            if (controlMode != NetworkControlMode.ServerNpc || serverNpcConventionalMotorEnabled)
                ServerDrivenActorPhysicsLoop.Register(this);
            if (controlMode != NetworkControlMode.ServerNpc)
                ServerDrivenActorInputLoop.Register(this);
            if (IsServer && controlMode == NetworkControlMode.Player)
                Core.CrowdPhysicsBodyRegistry.RegisterPlayer(targetRigidbody);

            if (targetRigidbody != null)
            {
                // NetworkRigidbody owns the authority-based Dynamic/Kinematic state.
                // Presentation is smoothed separately, so Rigidbody interpolation
                // remains disabled on both authority and replicas.
                targetRigidbody.interpolation = RigidbodyInterpolation.None;
            }

            // Sync motor settings from server on first spawn
            if (IsServer && motor != null && !hasInitializedSettings)
            {
                var baseMotor = motor.GetComponent<IActorMotor>();
                if (baseMotor != null)
                {
                    // Initialize network variables with current motor settings
                    SyncMotorSettingsToNetwork();
                    hasInitializedSettings = true;
                }
            }

            // Subscribe to settings changes on clients
            if (!IsServer)
            {
                netActorMotorSettings.OnValueChanged += OnActorMotorSettingsChanged;
                netTraversalFeatureSettings.OnValueChanged += OnTraversalFeatureSettingsChanged;
                netWireSwingState.OnValueChanged += OnWireSwingStateChanged;
                ApplyWireSwingState(netWireSwingState.Value);
            }

            if (IsOwner)
            {
                if (cameraTransform == null && Camera.main != null)
                {
                    cameraTransform = Camera.main.transform;
                }

                activeInputSource?.Enable();
            }
        }

        public override void OnNetworkDespawn()
        {
            useMovingPlatformSync = false;
            ApplyServerNpcPositionThreshold();
            ServerDrivenActorPhysicsLoop.Unregister(this);
            ServerDrivenActorInputLoop.Unregister(this);
            Core.CrowdPhysicsBodyRegistry.UnregisterPlayer(targetRigidbody);
            // Unsubscribe from settings changes
            if (!IsServer)
            {
                netActorMotorSettings.OnValueChanged -= OnActorMotorSettingsChanged;
                netTraversalFeatureSettings.OnValueChanged -= OnTraversalFeatureSettingsChanged;
                netWireSwingState.OnValueChanged -= OnWireSwingStateChanged;
            }

            activeInputSource?.Disable();
            traversalCoordinator?.SetWireAimCursor(default, false);
            if (IsOwner)
            {
                Cursor.visible = true;
            }
            motor?.ResetState();
            base.OnNetworkDespawn();
        }

        public void SetServerNpcMovingPlatformSync(bool enabled)
        {
            if (controlMode != NetworkControlMode.ServerNpc || useMovingPlatformSync == enabled)
                return;
            useMovingPlatformSync = enabled;
            ApplyServerNpcPositionThreshold();
        }

        private void ApplyServerNpcPositionThreshold()
        {
            if (networkTransform == null)
                return;
            networkTransform.PositionThreshold = useMovingPlatformSync
                ? Mathf.Min(defaultPositionThreshold, MovingPlatformPositionThreshold)
                : defaultPositionThreshold;
        }

        private void OnEnable()
        {
            if (IsSpawned && IsOwner)
            {
                activeInputSource?.Enable();
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            activeInputSource?.Disable();
            traversalCoordinator?.SetWireAimCursor(default, false);
            if (IsOwner)
            {
                Cursor.visible = true;
            }
            motor?.ResetState();
        }

        internal void TickRegisteredInput()
        {
            if (!IsSpawned || controlMode == NetworkControlMode.ServerNpc)
            {
                return;
            }

            if (IsOwner)
            {
                ReadAndSendInput();
            }
        }

        internal void TickRegisteredPhysics()
        {
            if (controlMode == NetworkControlMode.ServerNpc && !serverNpcConventionalMotorEnabled)
                return;

            if (!IsSpawned || motor == null)
            {
                return;
            }

            if (IsServer)
            {
                // NetworkRigidbody also configures the body during spawn. Depending
                // on component callback order it can overwrite the value set in
                // OnNetworkSpawn, exposing MovePosition's fixed-tick steps on a host.
                // Presentation is smoothed separately; interpolating the Rigidbody
                // here would apply interpolation twice to the rendered hierarchy.
                if (targetRigidbody != null &&
                    targetRigidbody.interpolation != RigidbodyInterpolation.None)
                {
                    targetRigidbody.interpolation = RigidbodyInterpolation.None;
                }

                TickServerPhysics();
                presentationSmoother?.CapturePhysicsPose();
            }
        }

        public void TickServerNpcPhysics(
            ActorInputState inputState,
            Vector3 moveDirection,
            Quaternion moveReferenceRotation,
            Vector3 grappleTargetPoint)
        {
            SubmitServerNpcInput(inputState, moveDirection, moveReferenceRotation, grappleTargetPoint);
            if (controlMode != NetworkControlMode.ServerNpc || !IsSpawned || !IsServer || motor == null)
                return;
            TickServerPhysics();
            presentationSmoother?.CapturePhysicsPose();
        }

        public void SubmitServerNpcInput(
            ActorInputState inputState,
            Vector3 moveDirection,
            Quaternion moveReferenceRotation,
            Vector3 grappleTargetPoint)
        {
            if (controlMode != NetworkControlMode.ServerNpc || !IsSpawned || !IsServer || motor == null)
                return;

            if (inputState.JumpPressed)
                jumpToken++;
            if (inputState.GrappleFirePressed)
                grappleFireToken++;
            serverInputState = new ActorInputSyncState(
                moveDirection,
                inputState.Move,
                moveReferenceRotation,
                jumpToken,
                inputState.IsStrafeMode,
                default,
                inputState.GrappleHeld,
                inputState.ReelInput,
                grappleTargetPoint,
                grappleFireToken);
        }

        public void SetServerNpcConventionalMotorEnabled(bool enabled)
        {
            if (controlMode != NetworkControlMode.ServerNpc || serverNpcConventionalMotorEnabled == enabled)
                return;
            serverNpcConventionalMotorEnabled = enabled;
            if (!IsSpawned)
                return;
            if (enabled)
                ServerDrivenActorPhysicsLoop.Register(this);
            else
                ServerDrivenActorPhysicsLoop.Unregister(this);
        }

        public void ApplyServerNpcCrowdState(
            float horizontalVelocity,
            float verticalVelocity,
            bool isGrounded,
            bool isJumping,
            bool isFreefall,
            bool isFallingAfterJump)
        {
            if (controlMode != NetworkControlMode.ServerNpc || !IsSpawned || !IsServer)
                return;
            hasServerNpcCrowdState = true;
            serverNpcCrowdKinematicState = new ActorKinematicState(horizontalVelocity, verticalVelocity);
            serverNpcCrowdMovementState = new ActorMovementFlagsState(
                isGrounded, isJumping, isFreefall, isFallingAfterJump,
                traversalCoordinator != null && traversalCoordinator.IsOnLadder,
                traversalCoordinator != null ? traversalCoordinator.LadderSpeed : 0f,
                traversalCoordinator != null && traversalCoordinator.IsWallRunning,
                traversalCoordinator != null ? traversalCoordinator.WallNormal : Vector3.zero);
            if (Time.unscaledTime < nextStateSyncTime)
                return;
            nextStateSyncTime = Time.unscaledTime + ControlPolicy.StateSyncInterval;
            netKinematicState.Value = serverNpcCrowdKinematicState;
            netMovementFlagsState.Value = serverNpcCrowdMovementState;
            netWireSwingState.Value = CreateWireSwingState();
        }

        private void ReadAndSendInput()
        {
            if (activeInputSource == null)
            {
                var emptyInputState = netInputState.Value;
                emptyInputState.MoveDirection = Vector3.zero;
                emptyInputState.MoveInput = Vector2.zero;
                emptyInputState.IsStrafeMode = false;
                emptyInputState.FacingDirection = Vector3.zero;
                emptyInputState.FacingPriority = 0;
                emptyInputState.FacingBlend = 0f;
                emptyInputState.FacingRotationSpeed = 0f;
                emptyInputState.GrappleHeld = false;
                emptyInputState.ReelInput = 0f;
                emptyInputState.GrappleTargetPoint = Vector3.zero;
                SubmitInput(emptyInputState);
                return;
            }

            var inputState = activeInputSource.ReadState();
            if (inputState.GrappleFirePressed) grappleFireToken++;
            var moveInput = inputState.Move;

            var referenceTransform = injectedInputReferenceTransform != null
                ? injectedInputReferenceTransform
                : cameraTransform != null ? cameraTransform : transform;
            var moveDirection = ActorMotor.GetMoveDirection(referenceTransform, moveInput);

            if (inputState.JumpPressed)
            {
                jumpToken++;
            }

            isStrafeMode = inputState.IsStrafeMode;
            var facingRequest = facingRequestResolver.Resolve(
                targetRigidbody.worldCenterOfMass,
                isStrafeMode);
            var aimPoint = default(Vector2);
            var hasAimPoint = baseInputSource != null && baseInputSource.TryReadAimPoint(out aimPoint);
            var showAimCursor = inputState.GrappleHeld
                || (baseInputSource != null && baseInputSource.IsAimCursorRecentlyMoved);
            var grappleTargetPoint = default(Vector3);
            if (activeInputSource == baseInputSource && injectedInputReferenceTransform == null)
            {
                PlayerPointerAim.ResolveDirection(
                    cameraTransform,
                    referenceTransform,
                    targetRigidbody.worldCenterOfMass,
                    targetRigidbody,
                    hasAimPoint,
                    aimPoint,
                    out grappleTargetPoint,
                    out _);
            }
            else
            {
                grappleTargetPoint = targetRigidbody.worldCenterOfMass + referenceTransform.forward * 1000f;
            }
            traversalCoordinator?.SetWireAimCursor(
                aimPoint,
                hasAimPoint && showAimCursor,
                targetRigidbody.worldCenterOfMass,
                grappleTargetPoint,
                inputState.GrappleHeld);
            SubmitInput(new ActorInputSyncState(
                moveDirection,
                moveInput,
                referenceTransform.rotation,
                jumpToken,
                isStrafeMode,
                facingRequest,
                inputState.GrappleHeld,
                inputState.ReelInput,
                grappleTargetPoint,
                grappleFireToken));
        }

        private void SubmitInput(ActorInputSyncState inputState)
        {
            localInputState = inputState;

            var jumpChanged = inputState.JumpToken != lastSentJumpToken;
            var grappleFireChanged = inputState.GrappleFireToken != lastSentGrappleFireToken;
            var grappleChanged = inputState.GrappleHeld != lastSentGrappleHeld;
            var reelChanged = float.IsNaN(lastSentReelInput)
                || !Mathf.Approximately(inputState.ReelInput, lastSentReelInput);
            if (!jumpChanged && !grappleFireChanged && !grappleChanged && !reelChanged && Time.unscaledTime < nextInputSendTime)
                return;

            var tickRate = NetworkManager != null
                ? Mathf.Max(1, NetworkManager.NetworkConfig.TickRate)
                : 30;
            nextInputSendTime = Time.unscaledTime + 1f / tickRate;
            lastSentJumpToken = inputState.JumpToken;
            lastSentGrappleFireToken = inputState.GrappleFireToken;
            lastSentGrappleHeld = inputState.GrappleHeld;
            lastSentReelInput = inputState.ReelInput;

            if (IsServer)
                StoreServerInput(inputState);
            else
                SubmitInputServerRpc(inputState);
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SubmitInputServerRpc(ActorInputSyncState inputState)
        {
            StoreServerInput(inputState);
        }

        private void StoreServerInput(ActorInputSyncState inputState)
        {
            serverInputState = inputState;
            if (ControlPolicy.BroadcastInputState)
                netInputState.Value = inputState;
        }

        private void TickServerPhysics()
        {
            var inputState = serverInputState;
            var moveDirection = inputState.MoveDirection;

            var jumpThisFrame = inputState.JumpToken != lastConsumedJumpToken;
            if (jumpThisFrame)
            {
                lastConsumedJumpToken = inputState.JumpToken;
            }

            traversalCoordinator?.SetWireInput(
                inputState.GrappleHeld,
                inputState.GrappleFireToken != lastConsumedGrappleFireToken,
                inputState.ReelInput,
                targetRigidbody.worldCenterOfMass,
                inputState.GrappleTargetPoint);
            lastConsumedGrappleFireToken = inputState.GrappleFireToken;

            if (motor != null)
            {
                motor.SetStrafeMode(inputState.IsStrafeMode);
                var facingRequest = new ActorFacingRequest(
                    inputState.FacingDirection,
                    inputState.FacingPriority,
                    inputState.FacingBlend,
                    inputState.FacingRotationSpeed);
                var authoritativeRequest = facingRequestResolver.Resolve(
                    targetRigidbody.worldCenterOfMass,
                    inputState.IsStrafeMode);
                if (authoritativeRequest.IsValid)
                {
                    facingRequest = authoritativeRequest;
                }
                motor.SetFacingRequest(facingRequest);

                moveInputReceiver?.SetMoveInput(inputState.MoveInput);
                moveInputReceiver?.SetMoveReferenceRotation(inputState.MoveReferenceRotation);
            }

            motor.Tick(moveDirection, jumpThisFrame);

            if (Time.unscaledTime < nextStateSyncTime)
                return;

            nextStateSyncTime = Time.unscaledTime + ControlPolicy.StateSyncInterval;

            var kinematicState = netKinematicState.Value;
            kinematicState.HorizontalVelocity = motor.HorizontalVelocity;
            kinematicState.VerticalVelocity = motor.VerticalVelocity;
            netKinematicState.Value = kinematicState;

            netMovementFlagsState.Value = new ActorMovementFlagsState(
                motor.IsGrounded,
                motor.IsJumping,
                motor.IsFreefall,
                motor.IsFallingAfterJump,
                traversalCoordinator != null && traversalCoordinator.IsOnLadder,
                traversalCoordinator != null ? traversalCoordinator.LadderSpeed : 0f,
                traversalCoordinator != null && traversalCoordinator.IsWallRunning,
                traversalCoordinator != null ? traversalCoordinator.WallNormal : Vector3.zero);

            netWireSwingState.Value = CreateWireSwingState();
        }

        private WireSwingNetworkState CreateWireSwingState()
        {
            if (traversalCoordinator == null || !traversalCoordinator.IsWireAttached)
            {
                return new WireSwingNetworkState(false, Vector3.zero, 0f);
            }

            var anchorPoint = traversalCoordinator.WireAnchorPoint;
            var anchorObject = traversalCoordinator.WireAnchorTransform != null
                ? traversalCoordinator.WireAnchorTransform.GetComponentInParent<NetworkObject>()
                : null;
            if (anchorObject == null || !anchorObject.IsSpawned)
            {
                return new WireSwingNetworkState(true, anchorPoint, traversalCoordinator.WireRopeLength);
            }

            return new WireSwingNetworkState(
                true,
                anchorPoint,
                traversalCoordinator.WireRopeLength,
                new NetworkObjectReference(anchorObject),
                anchorObject.transform.InverseTransformPoint(anchorPoint),
                true);
        }

        private void OnWireSwingStateChanged(WireSwingNetworkState oldValue, WireSwingNetworkState newValue)
        {
            ApplyWireSwingState(newValue);
        }

        private void ApplyWireSwingState(WireSwingNetworkState state)
        {
            Transform anchorTransform = null;
            var anchorPoint = state.AnchorPoint;
            if (state.IsAttached && state.HasAnchorObject && state.AnchorObject.TryGet(out var anchorObject))
            {
                anchorTransform = anchorObject.transform;
                anchorPoint = anchorTransform.TransformPoint(state.AnchorLocalPoint);
            }

            traversalCoordinator?.SetReplicatedWireState(
                state.IsAttached,
                anchorPoint,
                state.RopeLength,
                anchorTransform);
        }

        private void SyncMotorSettingsToNetwork()
        {
            var baseMotor = motor?.GetComponent<IActorMotor>();
            if (baseMotor == null)
                return;

            netActorMotorSettings.Value = ActorMotorSettingsNetData.FromCore(baseMotor.GetSettings());

            var traversalSettings = TraversalFeatureSettings.CreateDefault();
            var traversalSettingsSyncs = motor.GetComponents<ITraversalSettingsSync>();
            for (var i = 0; i < traversalSettingsSyncs.Length; i++)
            {
                traversalSettingsSyncs[i].WriteSettings(ref traversalSettings);
            }

            netTraversalFeatureSettings.Value = TraversalFeatureSettingsNetData.FromCore(traversalSettings);
        }

        private void OnActorMotorSettingsChanged(ActorMotorSettingsNetData oldValue, ActorMotorSettingsNetData newValue)
        {
            if (motor != null)
            {
                var baseMotor = motor.GetComponent<IActorMotor>();
                if (baseMotor != null)
                {
                    var currentSettings = baseMotor.GetSettings();
                    baseMotor.ApplySettings(newValue.ToCore(currentSettings.GroundLayer));
                }
            }
        }

        private void OnTraversalFeatureSettingsChanged(TraversalFeatureSettingsNetData oldValue, TraversalFeatureSettingsNetData newValue)
        {
            if (motor == null)
            {
                return;
            }

            var coreSettings = newValue.ToCore();
            var traversalSettingsTargets = motor.GetComponents<ITraversalSettingsSync>();
            for (var i = 0; i < traversalSettingsTargets.Length; i++)
            {
                traversalSettingsTargets[i].ReadSettings(coreSettings);
            }
        }
    }
}
