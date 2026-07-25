using Koiusa.Input;
using UnityEngine;
using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [RequireComponent(typeof(PlayerCompositeMotor))]
    public class ServerDrivenPlayerController : NetworkBehaviour, IPlayerController, IPlayerLadderState, IPlayerWallRunState
    {
        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Network State")]
        [SerializeField] private NetworkControlMode controlMode = NetworkControlMode.Player;

        private Rigidbody targetRigidbody;
        private PlayerGameplayInputReader baseInputSource;
        private IPlayerInputSource activeInputSource;
        private Transform injectedInputReferenceTransform;
        private PlayerCompositeMotor motor;
        private IPlayerMoveInputReceiver moveInputReceiver;
        private ILadderTraversalFeature ladderTraversalFeature;
        private IWallRunTraversalFeature wallRunTraversalFeature;
        private IWireSwingTraversalFeature wireSwingFeature;
        private PhysicsPresentationSmoother presentationSmoother;
        private int jumpToken;
        private int lastConsumedJumpToken;
        private bool isStrafeMode;
        private bool hasInitializedSettings;
        private PlayerInputSyncState localInputState;
        private PlayerInputSyncState serverInputState;
        private float nextInputSendTime;
        private int lastSentJumpToken = -1;
        private bool lastSentGrappleHeld;
        private float nextStateSyncTime;

        private readonly NetworkVariable<PlayerInputSyncState> netInputState = new NetworkVariable<PlayerInputSyncState>(
            new PlayerInputSyncState(Vector3.zero, Vector2.zero, Quaternion.identity, 0, false), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Settings Sync (Server -> All Clients)
        private readonly NetworkVariable<PlayerMotorSettingsNetData> netPlayerMotorSettings = new NetworkVariable<PlayerMotorSettingsNetData>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<TraversalFeatureSettingsNetData> netTraversalFeatureSettings = new NetworkVariable<TraversalFeatureSettingsNetData>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<PlayerKinematicState> netKinematicState = new NetworkVariable<PlayerKinematicState>(
            new PlayerKinematicState(0f, 0f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<PlayerMovementFlagsState> netMovementFlagsState = new NetworkVariable<PlayerMovementFlagsState>(
            new PlayerMovementFlagsState(true, false, false, false, false, 0f, false, Vector3.zero), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<WireSwingNetworkState> netWireSwingState = new NetworkVariable<WireSwingNetworkState>(
            new WireSwingNetworkState(false, Vector3.zero, 0f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkControlPolicy ControlPolicy => NetworkControlPolicies.Get(controlMode);
        private bool UseLocalMotorState => IsSpawned && IsServer;

        public bool IsGrounded => UseLocalMotorState && motor != null ? motor.IsGrounded : netMovementFlagsState.Value.IsGrounded;
        public bool IsJumping => UseLocalMotorState && motor != null ? motor.IsJumping : netMovementFlagsState.Value.IsJumping;
        public bool IsFreefall => UseLocalMotorState && motor != null ? motor.IsFreefall : netMovementFlagsState.Value.IsFreefall;
        public bool IsFallingAfterJump => UseLocalMotorState && motor != null ? motor.IsFallingAfterJump : netMovementFlagsState.Value.IsFallingAfterJump;
        public bool IsOnLadder => UseLocalMotorState ? ladderTraversalFeature != null && ladderTraversalFeature.IsOnLadder : netMovementFlagsState.Value.IsOnLadder;
        public float LadderSpeed => UseLocalMotorState && ladderTraversalFeature != null ? ladderTraversalFeature.ClimbSpeed : netMovementFlagsState.Value.LadderSpeed;
        public bool IsWallRunning => UseLocalMotorState ? wallRunTraversalFeature != null && wallRunTraversalFeature.IsWallRunning : netMovementFlagsState.Value.IsWallRunning;
        public Vector3 WallNormal => UseLocalMotorState && wallRunTraversalFeature != null ? wallRunTraversalFeature.WallNormal : netMovementFlagsState.Value.WallNormal;
        public bool IsStrafeMode => IsOwner ? localInputState.IsStrafeMode : netInputState.Value.IsStrafeMode;
        public Vector3 InheritedGroundVelocity => UseLocalMotorState && motor != null ? motor.InheritedGroundVelocity : Vector3.zero;
        public Vector2 MoveInput => IsOwner ? localInputState.MoveInput : netInputState.Value.MoveInput;
        public Vector3 MoveDirection => IsOwner ? localInputState.MoveDirection : netInputState.Value.MoveDirection;
        public float HorizontalVelocity => UseLocalMotorState && motor != null ? motor.HorizontalVelocity : netKinematicState.Value.HorizontalVelocity;
        public float VerticalVelocity => UseLocalMotorState && motor != null ? motor.VerticalVelocity : netKinematicState.Value.VerticalVelocity;
        public float MaxMoveSpeed => 5f;

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

            baseInputSource = new PlayerGameplayInputReader(config);
            activeInputSource = baseInputSource;
            injectedInputReferenceTransform = null;

            if (IsSpawned && IsOwner)
            {
                activeInputSource.Enable();
            }
        }

        public void SetInputSource(IPlayerInputSource source, Transform referenceTransform = null)
        {
            if (ReferenceEquals(activeInputSource, source)
                && injectedInputReferenceTransform == referenceTransform)
                return;

            activeInputSource?.Disable();
            activeInputSource = source;
            injectedInputReferenceTransform = referenceTransform;
            if (IsSpawned && IsOwner && isActiveAndEnabled)
                activeInputSource?.Enable();
        }

        public void ClearInputSource(IPlayerInputSource source)
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
            targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            presentationSmoother = GetComponent<PhysicsPresentationSmoother>();
            if (presentationSmoother == null)
            {
                presentationSmoother = gameObject.AddComponent<PhysicsPresentationSmoother>();
            }
            presentationSmoother.Initialize(targetRigidbody);

            motor = GetComponent<PlayerCompositeMotor>();
            if (motor == null)
            {
                motor = gameObject.AddComponent<PlayerCompositeMotor>();
            }

            moveInputReceiver = motor as IPlayerMoveInputReceiver;
            ladderTraversalFeature = GetComponent<ILadderTraversalFeature>();
            wallRunTraversalFeature = GetComponent<IWallRunTraversalFeature>();
            wireSwingFeature = GetComponent<IWireSwingTraversalFeature>();

            if (inputActionsConfig == null)
            {
                return;
            }

            baseInputSource = new PlayerGameplayInputReader(inputActionsConfig);
            activeInputSource = baseInputSource;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (targetRigidbody != null)
            {
                // Physics is server authoritative. Remote and owning clients are
                // presentation-only; NetworkTransform applies the interpolated pose.
                targetRigidbody.isKinematic = !IsServer;
                targetRigidbody.interpolation = IsServer
                    ? RigidbodyInterpolation.Interpolate
                    : RigidbodyInterpolation.None;
            }

            // Sync motor settings from server on first spawn
            if (IsServer && motor != null && !hasInitializedSettings)
            {
                var baseMotor = motor.GetComponent<IPlayerMotor>();
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
                netPlayerMotorSettings.OnValueChanged += OnPlayerMotorSettingsChanged;
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
            // Unsubscribe from settings changes
            if (!IsServer)
            {
                netPlayerMotorSettings.OnValueChanged -= OnPlayerMotorSettingsChanged;
                netTraversalFeatureSettings.OnValueChanged -= OnTraversalFeatureSettingsChanged;
                netWireSwingState.OnValueChanged -= OnWireSwingStateChanged;
            }

            activeInputSource?.Disable();
            motor?.ResetState();
            base.OnNetworkDespawn();
        }

        private void OnEnable()
        {
            if (IsSpawned && IsOwner)
            {
                activeInputSource?.Enable();
            }
        }

        private void OnDisable()
        {
            activeInputSource?.Disable();
            motor?.ResetState();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                ReadAndSendInput();
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || motor == null)
            {
                return;
            }

            if (IsServer)
            {
                // NetworkRigidbody also configures the body during spawn. Depending
                // on component callback order it can overwrite the value set in
                // OnNetworkSpawn, exposing MovePosition's fixed-tick steps on a host.
                // Keep interpolation enabled on the physics authority only; remote
                // clients continue to use NetworkTransform interpolation.
                if (targetRigidbody != null &&
                    targetRigidbody.interpolation != RigidbodyInterpolation.Interpolate)
                {
                    targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                }

                TickServerPhysics();
                presentationSmoother?.CapturePhysicsPose();
            }
        }

        private void ReadAndSendInput()
        {
            if (activeInputSource == null)
            {
                var emptyInputState = netInputState.Value;
                emptyInputState.MoveDirection = Vector3.zero;
                emptyInputState.MoveInput = Vector2.zero;
                emptyInputState.GrappleHeld = false;
                emptyInputState.ReelInput = 0f;
                emptyInputState.GrappleAimDirection = Vector3.zero;
                SubmitInput(emptyInputState);
                return;
            }

            var inputState = activeInputSource.ReadState();
            var moveInput = inputState.Move;

            var referenceTransform = injectedInputReferenceTransform != null
                ? injectedInputReferenceTransform
                : cameraTransform != null ? cameraTransform : transform;
            var moveDirection = PlayerMotor.GetMoveDirection(referenceTransform, moveInput);

            if (inputState.JumpPressed)
            {
                jumpToken++;
            }

            isStrafeMode = inputState.IsStrafeMode;
            var grappleAimDirection = activeInputSource == baseInputSource && injectedInputReferenceTransform == null
                ? PlayerPointerAim.ResolveDirection(
                    cameraTransform,
                    referenceTransform,
                    targetRigidbody.worldCenterOfMass,
                    targetRigidbody)
                : referenceTransform.forward;
            SubmitInput(new PlayerInputSyncState(
                moveDirection,
                moveInput,
                referenceTransform.rotation,
                jumpToken,
                isStrafeMode,
                inputState.GrappleHeld,
                inputState.ReelInput,
                grappleAimDirection));
        }

        private void SubmitInput(PlayerInputSyncState inputState)
        {
            localInputState = inputState;

            var jumpChanged = inputState.JumpToken != lastSentJumpToken;
            var grappleChanged = inputState.GrappleHeld != lastSentGrappleHeld;
            if (!jumpChanged && !grappleChanged && Time.unscaledTime < nextInputSendTime)
                return;

            var tickRate = NetworkManager != null
                ? Mathf.Max(1, NetworkManager.NetworkConfig.TickRate)
                : 30;
            nextInputSendTime = Time.unscaledTime + 1f / tickRate;
            lastSentJumpToken = inputState.JumpToken;
            lastSentGrappleHeld = inputState.GrappleHeld;

            if (IsServer)
                StoreServerInput(inputState);
            else
                SubmitInputServerRpc(inputState);
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SubmitInputServerRpc(PlayerInputSyncState inputState)
        {
            StoreServerInput(inputState);
        }

        private void StoreServerInput(PlayerInputSyncState inputState)
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

            if (wireSwingFeature != null && wireSwingFeature.IsEnabled)
            {
                wireSwingFeature.SetReelInput(inputState.ReelInput);
                wireSwingFeature.SetGrappleInput(
                    inputState.GrappleHeld,
                    targetRigidbody.worldCenterOfMass,
                    inputState.GrappleAimDirection);
            }

            if (motor != null)
            {
                motor.SetStrafeMode(inputState.IsStrafeMode);

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

            netMovementFlagsState.Value = new PlayerMovementFlagsState(
                motor.IsGrounded,
                motor.IsJumping,
                motor.IsFreefall,
                motor.IsFallingAfterJump,
                ladderTraversalFeature != null && ladderTraversalFeature.IsOnLadder,
                ladderTraversalFeature != null ? ladderTraversalFeature.ClimbSpeed : 0f,
                wallRunTraversalFeature != null && wallRunTraversalFeature.IsWallRunning,
                wallRunTraversalFeature != null ? wallRunTraversalFeature.WallNormal : Vector3.zero);

            netWireSwingState.Value = wireSwingFeature != null && wireSwingFeature.IsAttached
                ? new WireSwingNetworkState(true, wireSwingFeature.AnchorPoint, wireSwingFeature.RopeLength)
                : new WireSwingNetworkState(false, Vector3.zero, 0f);
        }

        private void OnWireSwingStateChanged(WireSwingNetworkState oldValue, WireSwingNetworkState newValue)
        {
            ApplyWireSwingState(newValue);
        }

        private void ApplyWireSwingState(WireSwingNetworkState state)
        {
            wireSwingFeature?.SetReplicatedState(state.IsAttached, state.AnchorPoint, state.RopeLength);
        }

        private void SyncMotorSettingsToNetwork()
        {
            var baseMotor = motor?.GetComponent<IPlayerMotor>();
            if (baseMotor == null)
                return;

            netPlayerMotorSettings.Value = PlayerMotorSettingsNetData.FromCore(baseMotor.GetSettings());

            var traversalSettings = TraversalFeatureSettings.CreateDefault();
            var traversalSettingsSyncs = motor.GetComponents<ITraversalSettingsSync>();
            for (var i = 0; i < traversalSettingsSyncs.Length; i++)
            {
                traversalSettingsSyncs[i].WriteSettings(ref traversalSettings);
            }

            netTraversalFeatureSettings.Value = TraversalFeatureSettingsNetData.FromCore(traversalSettings);
        }

        private void OnPlayerMotorSettingsChanged(PlayerMotorSettingsNetData oldValue, PlayerMotorSettingsNetData newValue)
        {
            if (motor != null)
            {
                var baseMotor = motor.GetComponent<IPlayerMotor>();
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
