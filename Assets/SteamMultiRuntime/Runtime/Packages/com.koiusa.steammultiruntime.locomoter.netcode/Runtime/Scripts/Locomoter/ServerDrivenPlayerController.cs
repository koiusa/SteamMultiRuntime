using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [RequireComponent(typeof(PlayerCompositeMotor))]
    public class ServerDrivenPlayerController : NetworkBehaviour, IPlayerController, IPlayerLadderState, IPlayerWallRunState
    {
        private sealed class InputActionPlayerInputSource : IPlayerInputSource
        {
            private readonly InputActionReference moveAction;
            private readonly InputActionReference jumpAction;
            private readonly InputActionReference strafeToggleAction;
            private int jumpToken;
            private bool isStrafeMode;
            private bool isEnabled;

            public InputActionPlayerInputSource(InputActionReference moveAction, InputActionReference jumpAction, InputActionReference strafeToggleAction)
            {
                this.moveAction = moveAction;
                this.jumpAction = jumpAction;
                this.strafeToggleAction = strafeToggleAction;
            }

            public void Enable()
            {
                if (isEnabled) return;
                isEnabled = true;

                if (moveAction != null)
                {
                    moveAction.action.Enable();
                }

                if (jumpAction != null)
                {
                    jumpAction.action.Enable();
                    jumpAction.action.performed += OnJumpPerformed;
                }

                if (strafeToggleAction != null)
                {
                    strafeToggleAction.action.Enable();
                    strafeToggleAction.action.performed += OnStrafeTogglePerformed;
                }
            }

            public void Disable()
            {
                if (!isEnabled) return;
                isEnabled = false;

                if (strafeToggleAction != null)
                {
                    strafeToggleAction.action.performed -= OnStrafeTogglePerformed;
                    strafeToggleAction.action.Disable();
                }

                if (jumpAction != null)
                {
                    jumpAction.action.performed -= OnJumpPerformed;
                    jumpAction.action.Disable();
                }

                if (moveAction != null)
                {
                    moveAction.action.Disable();
                }

                jumpToken = 0;
                isStrafeMode = false;
            }

            public PlayerInputState ReadState()
            {
                var move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
                var jumpPressed = jumpToken;
                jumpToken = 0;
                return new PlayerInputState(move, jumpPressed > 0);
            }

            public bool GetStrafeMode() => isStrafeMode;

            private void OnJumpPerformed(InputAction.CallbackContext context)
            {
                jumpToken++;
            }

            private void OnStrafeTogglePerformed(InputAction.CallbackContext context)
            {
                isStrafeMode = !isStrafeMode;
            }
        }

        [Header("Input")]
        [SerializeField] private PlayerInputActionsProfile inputActionsProfile;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Network State")]
        [SerializeField, Min(0.02f)] private float stateSyncInterval = 0.05f;

        private Rigidbody targetRigidbody;
        private InputActionPlayerInputSource baseInputSource;
        private IPlayerInputSource activeInputSource;
        private Transform injectedInputReferenceTransform;
        private PlayerCompositeMotor motor;
        private IPlayerMoveInputReceiver moveInputReceiver;
        private ILadderTraversalFeature ladderTraversalFeature;
        private IWallRunTraversalFeature wallRunTraversalFeature;
        private PhysicsPresentationSmoother presentationSmoother;
        private int jumpToken;
        private int lastConsumedJumpToken;
        private bool isStrafeMode;
        private bool hasInitializedSettings;
        private PlayerInputSyncState localInputState;
        private float nextInputSendTime;
        private int lastSentJumpToken = -1;
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

        public void SetInputProfile(PlayerInputActionsProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("InputProfile cannot be null.", this);
                return;
            }

            inputActionsProfile = profile;

            // Reinitialize input source if already created
            activeInputSource?.Disable();

            baseInputSource = new InputActionPlayerInputSource(
                profile.MoveAction,
                profile.JumpAction,
                profile.StrafeToggleAction);
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
            presentationSmoother.Initialize(targetRigidbody, this);

            motor = GetComponent<PlayerCompositeMotor>();
            if (motor == null)
            {
                motor = gameObject.AddComponent<PlayerCompositeMotor>();
            }

            moveInputReceiver = motor as IPlayerMoveInputReceiver;
            ladderTraversalFeature = GetComponent<ILadderTraversalFeature>();
            wallRunTraversalFeature = GetComponent<IWallRunTraversalFeature>();

            if (inputActionsProfile == null)
            {
                return;
            }

            baseInputSource = new InputActionPlayerInputSource(
                inputActionsProfile.MoveAction,
                inputActionsProfile.JumpAction,
                inputActionsProfile.StrafeToggleAction);
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

            isStrafeMode = activeInputSource is InputActionPlayerInputSource playerInput
                && playerInput.GetStrafeMode();
            SubmitInput(new PlayerInputSyncState(
                moveDirection,
                moveInput,
                referenceTransform.rotation,
                jumpToken,
                isStrafeMode));
        }

        private void SubmitInput(PlayerInputSyncState inputState)
        {
            localInputState = inputState;

            var jumpChanged = inputState.JumpToken != lastSentJumpToken;
            if (!jumpChanged && Time.unscaledTime < nextInputSendTime)
                return;

            var tickRate = NetworkManager != null
                ? Mathf.Max(1, NetworkManager.NetworkConfig.TickRate)
                : 30;
            nextInputSendTime = Time.unscaledTime + 1f / tickRate;
            lastSentJumpToken = inputState.JumpToken;

            if (IsServer)
                netInputState.Value = inputState;
            else
                SubmitInputServerRpc(inputState);
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SubmitInputServerRpc(PlayerInputSyncState inputState)
        {
            netInputState.Value = inputState;
        }

        private void TickServerPhysics()
        {
            var inputState = netInputState.Value;
            var moveDirection = inputState.MoveDirection;

            var jumpThisFrame = inputState.JumpToken != lastConsumedJumpToken;
            if (jumpThisFrame)
            {
                lastConsumedJumpToken = inputState.JumpToken;
            }

            if (motor != null)
            {
                var baseMotor = motor.GetComponent<IPlayerMotor>();
                if (baseMotor != null)
                {
                    // Server uses local motor settings, no need to apply from network
                    baseMotor.SetStrafeMode(inputState.IsStrafeMode);
                }

                moveInputReceiver?.SetMoveInput(inputState.MoveInput);
                moveInputReceiver?.SetMoveReferenceRotation(inputState.MoveReferenceRotation);
            }

            motor.Tick(moveDirection, jumpThisFrame);

            if (Time.unscaledTime < nextStateSyncTime)
                return;

            nextStateSyncTime = Time.unscaledTime + Mathf.Max(0.02f, stateSyncInterval);

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
                    baseMotor.UpdateSettingsFromStruct(newValue.ToCore(currentSettings.GroundLayer));
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
