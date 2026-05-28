using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [RequireComponent(typeof(PlayerCompositeMotor))]
    public class ServerDrivenPlayerController : NetworkBehaviour, IPlayerController
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

        private Rigidbody targetRigidbody;
        private InputActionPlayerInputSource baseInputSource;
        private PlayerCompositeMotor motor;
        private int jumpToken;
        private int lastConsumedJumpToken;
        private bool isStrafeMode;
        private bool hasInitializedSettings;

        private readonly NetworkVariable<PlayerInputSyncState> netInputState = new NetworkVariable<PlayerInputSyncState>(
            new PlayerInputSyncState(Vector3.zero, Vector2.zero, 0, false), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Settings Sync (Server -> All Clients)
        private readonly NetworkVariable<PlayerMotorSettings> netPlayerMotorSettings = new NetworkVariable<PlayerMotorSettings>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<TraversalFeatureSettings> netTraversalMotorSettings = new NetworkVariable<TraversalFeatureSettings>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<PlayerKinematicState> netKinematicState = new NetworkVariable<PlayerKinematicState>(
            new PlayerKinematicState(Vector3.zero, Quaternion.identity, 0f, 0f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<PlayerMovementFlagsState> netMovementFlagsState = new NetworkVariable<PlayerMovementFlagsState>(
            new PlayerMovementFlagsState(true, false, false, false), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool UseLocalMotorState => IsSpawned && IsServer;

        public bool IsGrounded => UseLocalMotorState && motor != null ? motor.IsGrounded : netMovementFlagsState.Value.IsGrounded;
        public bool IsJumping => UseLocalMotorState && motor != null ? motor.IsJumping : netMovementFlagsState.Value.IsJumping;
        public bool IsFreefall => UseLocalMotorState && motor != null ? motor.IsFreefall : netMovementFlagsState.Value.IsFreefall;
        public bool IsFallingAfterJump => UseLocalMotorState && motor != null ? motor.IsFallingAfterJump : netMovementFlagsState.Value.IsFallingAfterJump;
        public bool IsStrafeMode => UseLocalMotorState ? isStrafeMode : netInputState.Value.IsStrafeMode;
        public Vector3 InheritedGroundVelocity => UseLocalMotorState && motor != null ? motor.InheritedGroundVelocity : Vector3.zero;
        public Vector2 MoveInput => netInputState.Value.MoveInput;
        public Vector3 MoveDirection => netInputState.Value.MoveDirection;
        public float HorizontalVelocity => UseLocalMotorState && motor != null ? motor.HorizontalVelocity : netKinematicState.Value.HorizontalVelocity;
        public float VerticalVelocity => UseLocalMotorState && motor != null ? motor.VerticalVelocity : netKinematicState.Value.VerticalVelocity;
        public float MaxMoveSpeed => 5f;

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody>();
            targetRigidbody.freezeRotation = true;
            targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            motor = GetComponent<PlayerCompositeMotor>();
            if (motor == null)
            {
                motor = gameObject.AddComponent<PlayerCompositeMotor>();
            }

            if (inputActionsProfile == null)
            {
                Debug.LogError("PlayerInputActionsProfile is not assigned.", this);
                enabled = false;
                return;
            }

            baseInputSource = new InputActionPlayerInputSource(
                inputActionsProfile.MoveAction,
                inputActionsProfile.JumpAction,
                inputActionsProfile.StrafeToggleAction);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (targetRigidbody != null)
            {
                targetRigidbody.isKinematic = false;
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
                netTraversalMotorSettings.OnValueChanged += OnTraversalMotorSettingsChanged;
            }

            if (IsOwner)
            {
                if (cameraTransform == null && Camera.main != null)
                {
                    cameraTransform = Camera.main.transform;
                }

                baseInputSource?.Enable();
            }
            else
            {
                SyncTransformFromNetwork();
            }
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from settings changes
            if (!IsServer)
            {
                netPlayerMotorSettings.OnValueChanged -= OnPlayerMotorSettingsChanged;
                netTraversalMotorSettings.OnValueChanged -= OnTraversalMotorSettingsChanged;
            }

            baseInputSource?.Disable();
            motor?.ResetState();
            base.OnNetworkDespawn();
        }

        private void OnEnable()
        {
            if (IsSpawned && IsOwner)
            {
                baseInputSource?.Enable();
            }
        }

        private void OnDisable()
        {
            baseInputSource?.Disable();
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

        private void LateUpdate()
        {
            if (!IsSpawned || IsServer)
            {
                return;
            }

            SyncTransformFromNetwork();
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || motor == null)
            {
                return;
            }

            if (IsServer)
            {
                TickServerPhysics();
            }
            else if (IsOwner)
            {
                TickClientPhysics();
            }
        }

        private void ReadAndSendInput()
        {
            if (baseInputSource == null)
            {
                var emptyInputState = netInputState.Value;
                emptyInputState.MoveDirection = Vector3.zero;
                emptyInputState.MoveInput = Vector2.zero;
                netInputState.Value = emptyInputState;
                return;
            }

            var inputState = baseInputSource.ReadState();
            var moveInput = inputState.Move;

            Transform referenceTransform = cameraTransform != null ? cameraTransform : transform;
            var moveDirection = PlayerMotor.GetMoveDirection(referenceTransform, moveInput);

            if (inputState.JumpPressed)
            {
                jumpToken++;
            }

            isStrafeMode = baseInputSource.GetStrafeMode();
            netInputState.Value = new PlayerInputSyncState(moveDirection, moveInput, jumpToken, isStrafeMode);
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
            }

            motor.Tick(moveDirection, jumpThisFrame);

            var kinematicState = netKinematicState.Value;
            if (targetRigidbody != null)
            {
                kinematicState.Position = targetRigidbody.position;
                kinematicState.Rotation = targetRigidbody.rotation;
            }

            kinematicState.HorizontalVelocity = motor.HorizontalVelocity;
            kinematicState.VerticalVelocity = motor.VerticalVelocity;
            netKinematicState.Value = kinematicState;

            netMovementFlagsState.Value = new PlayerMovementFlagsState(
                motor.IsGrounded,
                motor.IsJumping,
                motor.IsFreefall,
                motor.IsFallingAfterJump);
        }

        private void SyncTransformFromNetwork()
        {
            if (targetRigidbody != null)
            {
                var kinematicState = netKinematicState.Value;
                targetRigidbody.position = kinematicState.Position;
                targetRigidbody.rotation = kinematicState.Rotation;
            }
        }

        private void TickClientPhysics()
        {
            var moveDirection = netInputState.Value.MoveDirection;

            var jumpThisFrame = jumpToken != lastConsumedJumpToken;
            if (jumpThisFrame)
            {
                lastConsumedJumpToken = jumpToken;
            }

            if (motor != null)
            {
                var baseMotor = motor.GetComponent<IPlayerMotor>();
                if (baseMotor != null)
                {
                    // Settings are applied via OnValueChanged callbacks, not every frame
                    baseMotor.SetStrafeMode(isStrafeMode);
                }
            }

            motor.Tick(moveDirection, jumpThisFrame);
        }

        private void OnCollisionEnter(Collision collision)
        {
            motor?.OnCollisionEnter(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            motor?.OnCollisionStay(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            motor?.OnCollisionExit(collision);
        }

        private void SyncMotorSettingsToNetwork()
        {
            var baseMotor = motor?.GetComponent<IPlayerMotor>();
            if (baseMotor == null)
                return;

            netPlayerMotorSettings.Value = baseMotor.GetSettings();

            var traversalSettings = TraversalFeatureSettings.CreateDefault();
            var traversalSettingsSyncs = motor.GetComponents<ITraversalSettingsSync>();
            for (var i = 0; i < traversalSettingsSyncs.Length; i++)
            {
                traversalSettingsSyncs[i].WriteSettings(ref traversalSettings);
            }

            netTraversalMotorSettings.Value = traversalSettings;
        }

        private void OnPlayerMotorSettingsChanged(PlayerMotorSettings oldValue, PlayerMotorSettings newValue)
        {
            if (motor != null)
            {
                var baseMotor = motor.GetComponent<IPlayerMotor>();
                if (baseMotor != null)
                {
                    baseMotor.UpdateSettingsFromStruct(newValue);
                }
            }
        }

        private void OnTraversalMotorSettingsChanged(TraversalFeatureSettings oldValue, TraversalFeatureSettings newValue)
        {
            if (motor == null)
            {
                return;
            }

            var traversalSettingsTargets = motor.GetComponents<ITraversalSettingsSync>();
            for (var i = 0; i < traversalSettingsTargets.Length; i++)
            {
                traversalSettingsTargets[i].ReadSettings(newValue);
            }
        }
    }
}
