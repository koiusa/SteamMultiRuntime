using System.Collections.Generic;
using Koiusa.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Koiusa.TargetingSystem.Runtime;
using Koiusa.SteamMultiRuntime.TargetingSystem;

namespace Koiusa.SteamMultiRuntime
{
    public abstract class CameraMixerWeightControllerBase : MonoBehaviour, ILocalTargetingCameraConsumer
    {
        [Header("References")]
        [SerializeField] private CinemachineMixingCamera mixingCamera;

        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        [Header("Weight Index")]
        [SerializeField, Min(0)] private int defaultCameraIndex = 0;
        [SerializeField, Min(0)] private int followCameraIndex = 1;
        [SerializeField, Min(0)] private int singleTargetCameraIndex = 2;
        [SerializeField, Min(0)] private int multiTargetCameraIndex = 3;

        [Header("Targeting")]
        [SerializeField] private CinemachineCamera followCamera;
        [SerializeField] private CinemachineCamera singleTargetCamera;
        [SerializeField] private CinemachineCamera multiTargetCamera;
        [SerializeField] private CinemachineTargetGroup multiTargetGroup;
        [SerializeField] private TargetingCameraGroupPresenter targetingGroupPresenter;
        [SerializeField] private TargetingCameraFramingMode targetingFramingMode = TargetingCameraFramingMode.PrimaryCentered;
        [SerializeField, Min(0f)] private float targetingGroupWeight = 1f;
        [SerializeField, Min(0f)] private float targetingGroupRadius = 0.5f;

        [Header("Transition")]
        [SerializeField, Min(0f)] private float transitionSpeed = 10f;

        [Header("Camera Collision")]
        [SerializeField] private bool enableCameraCollision;
        [SerializeField] private LayerMask cameraCollisionLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f)] private float cameraCollisionRadius = 0.45f;
        [SerializeField, Min(0.01f)] private float minimumDistanceFromTarget = 0.5f;
        [SerializeField, Min(0f)] private float minimumOcclusionTime = 0.08f;
        [SerializeField, Range(0f, 2f)] private float collisionSmoothingTime = 0.25f;
        [SerializeField, Min(0f)] private float collisionDamping = 0.4f;
        [SerializeField, Min(0f)] private float collisionRecoveryDamping = 0.7f;

        private float targetDefaultWeight;
        private float targetFollowWeight;
        private float targetSingleWeight;
        private float targetMultiWeight;
        private IFocusMarkerContext context;
        private CinemachineInputAxisController inputAxisController;
        private InputAction grappleAction;
        private IActorTraversalCoordinator traversalCoordinator;
        private GameObject traversalPlayerObject;
        private readonly List<InputActionReference> runtimeActionReferences = new();
        private readonly List<CameraCollisionState> cameraCollisionStates = new();
        private bool appliedCameraCollision;
        private TargetingController targetingController;
        private TargetingMode targetingMode;

        private sealed class CameraCollisionState
        {
            public CinemachineDeoccluder Deoccluder;
            public bool AddedDeoccluder;
            public bool DeoccluderEnabled;
            public LayerMask CollideAgainst;
            public float MinimumDistanceFromTarget;
            public CinemachineDeoccluder.ObstacleAvoidance AvoidObstacles;
            public CinemachineDecollider Decollider;
            public bool AddedDecollider;
            public bool DecolliderEnabled;
            public float CameraRadius;
            public CinemachineDecollider.DecollisionSettings Decollision;
            public CinemachineDecollider.TerrainSettings TerrainResolution;
        }

        protected abstract IFocusMarkerContext ResolveContext();

        protected virtual void Awake()
        {
            if (mixingCamera == null)
            {
                mixingCamera = GetComponent<CinemachineMixingCamera>();
            }

            ConfigureCameraInputActions();
            ConfigureCameraCollision();
            ConfigureTargetingGroupPresenter();

            context = ResolveContext();
        }

        protected virtual void OnDestroy()
        {
            SetTargetingController(null);
            foreach (var actionReference in runtimeActionReferences)
            {
                if (actionReference != null)
                {
                    Destroy(actionReference);
                }
            }

            runtimeActionReferences.Clear();
        }

        protected virtual void OnEnable()
        {
            context = ResolveContext();
            if (context != null)
            {
                context.StateChanged += OnContextStateChanged;
            }

            if (targetingController != null)
            {
                ApplyTargetingState(targetingController.State);
            }
            RefreshTargetWeight(true);
        }

        protected virtual void OnDisable()
        {
            if (context != null)
            {
                context.StateChanged -= OnContextStateChanged;
            }

            if (inputAxisController != null) inputAxisController.enabled = true;
        }

        protected virtual void Update()
        {
            if (enableCameraCollision != appliedCameraCollision)
            {
                ConfigureCameraCollision();
            }

            if (inputAxisController != null)
            {
                inputAxisController.enabled = grappleAction == null
                    || !grappleAction.IsPressed()
                    || IsWireAttached();
            }

            if (mixingCamera == null)
            {
                return;
            }

            var t = transitionSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

            var nextDefault = Mathf.Lerp(mixingCamera.GetWeight(defaultCameraIndex), targetDefaultWeight, t);
            var nextFollow = Mathf.Lerp(mixingCamera.GetWeight(followCameraIndex), targetFollowWeight, t);
            var nextSingle = Mathf.Lerp(mixingCamera.GetWeight(singleTargetCameraIndex), targetSingleWeight, t);
            var nextMulti = Mathf.Lerp(mixingCamera.GetWeight(multiTargetCameraIndex), targetMultiWeight, t);

            mixingCamera.SetWeight(defaultCameraIndex, nextDefault);
            mixingCamera.SetWeight(followCameraIndex, nextFollow);
            mixingCamera.SetWeight(singleTargetCameraIndex, nextSingle);
            mixingCamera.SetWeight(multiTargetCameraIndex, nextMulti);
        }

        private bool IsWireAttached()
        {
            var playerObject = context?.PlayerObject;
            if (playerObject == null)
            {
                traversalPlayerObject = null;
                traversalCoordinator = null;
                return false;
            }

            if (traversalPlayerObject != playerObject)
            {
                traversalPlayerObject = playerObject;
                traversalCoordinator = playerObject.GetComponentInChildren<IActorTraversalCoordinator>(true);
            }

            return traversalCoordinator != null && traversalCoordinator.IsWireAttached;
        }

        private void OnContextStateChanged()
        {
            if (targetingController != null)
            {
                ApplyTargetingState(targetingController.State);
                return;
            }

            targetingGroupPresenter?.SetPlayerAnchor(ResolvePlayerAimPoint());
            RefreshTargetWeight(false);
        }

        protected void RefreshTargetWeight(bool immediate)
        {
            var isActive = context != null && context.IsActive;

            targetDefaultWeight = isActive ? 0f : 1f;
            targetFollowWeight = isActive && targetingMode == TargetingMode.None ? 1f : 0f;
            targetSingleWeight = isActive && targetingMode == TargetingMode.Single ? 1f : 0f;
            targetMultiWeight = isActive && targetingMode == TargetingMode.Multi ? 1f : 0f;

            if (!immediate || mixingCamera == null)
            {
                return;
            }

            mixingCamera.SetWeight(defaultCameraIndex, targetDefaultWeight);
            mixingCamera.SetWeight(followCameraIndex, targetFollowWeight);
            mixingCamera.SetWeight(singleTargetCameraIndex, targetSingleWeight);
            mixingCamera.SetWeight(multiTargetCameraIndex, targetMultiWeight);
        }

        public void SetTargetingController(TargetingController controller)
        {
            if (targetingController == controller) return;
            if (targetingController != null) targetingController.StateChanged -= OnTargetingStateChanged;
            targetingController = controller;
            if (targetingController != null)
            {
                targetingController.StateChanged += OnTargetingStateChanged;
                ApplyTargetingState(targetingController.State);
            }
            else
            {
                ApplyTargetingState(TargetingState.Empty);
            }
        }

        private void OnTargetingStateChanged(TargetingStateChange change)
        {
            ApplyTargetingState(change.Current);
        }

        private void ApplyTargetingState(TargetingState state)
        {
            var previousMode = targetingMode;
            targetingMode = state.Mode;
            targetingGroupPresenter?.SetPlayerAnchor(ResolvePlayerAimPoint());
            targetingGroupPresenter?.Present(state);
            MatchIncomingCamera(previousMode, state.Mode);
            RefreshTargetWeight(false);
        }

        private void MatchIncomingCamera(TargetingMode previousMode, TargetingMode nextMode)
        {
            if (previousMode == nextMode) return;

            var source = ResolveModeCamera(previousMode);
            var destination = ResolveModeCamera(nextMode);
            if (source == null || destination == null) return;

            var sourceState = source.State;
            destination.ForceCameraPosition(sourceState.GetFinalPosition(), sourceState.GetFinalOrientation());
        }

        private CinemachineCamera ResolveModeCamera(TargetingMode mode) => mode switch
        {
            TargetingMode.None => followCamera,
            TargetingMode.Single => singleTargetCamera,
            TargetingMode.Multi => multiTargetCamera,
            _ => null
        };

        private Transform ResolvePlayerAimPoint()
        {
            var playerObject = context?.PlayerObject;
            if (playerObject == null) return null;
            var cameraTrackMarker = playerObject.GetComponentInChildren<CameraTrackMarker>(true);
            return cameraTrackMarker != null ? cameraTrackMarker.transform : playerObject.transform;
        }

        private void ConfigureTargetingGroupPresenter()
        {
            if (targetingGroupPresenter == null)
            {
                targetingGroupPresenter = GetComponent<TargetingCameraGroupPresenter>();
            }
            if (targetingGroupPresenter == null)
            {
                Debug.LogWarning(
                    "Targeting camera requires a preconfigured TargetingCameraGroupPresenter.",
                    this);
                return;
            }
            targetingGroupPresenter.Configure(
                singleTargetCamera,
                multiTargetCamera,
                multiTargetGroup,
                targetingFramingMode,
                targetingGroupWeight,
                targetingGroupRadius);
        }

        private void ConfigureCameraInputActions()
        {
            inputAxisController = GetComponentInChildren<CinemachineInputAxisController>(true);
            if (inputAxisController == null || inputActionsConfig == null)
            {
                return;
            }

            var lookAction = inputActionsConfig.FindAction("Player/Look");
            var zoomAction = inputActionsConfig.FindAction("Player/CameraZoom");
            grappleAction = inputActionsConfig.FindAction("Player/Grapple");

            foreach (var axisController in inputAxisController.Controllers)
            {
                var action = axisController.Name switch
                {
                    "Look Orbit X" => lookAction,
                    "Look Orbit Y" => lookAction,
                    "Orbit Scale" => zoomAction,
                    _ => null
                };

                if (action == null)
                {
                    continue;
                }

                var actionReference = InputActionReference.Create(action);
                runtimeActionReferences.Add(actionReference);
                axisController.Input.InputAction = actionReference;
            }
        }

        private void ConfigureCameraCollision()
        {
            if (!enableCameraCollision)
            {
                RestoreCameraCollision();
                return;
            }

            if (cameraCollisionStates.Count > 0)
            {
                ApplyCameraCollisionSettings();
                appliedCameraCollision = true;
                return;
            }

            foreach (var camera in GetComponentsInChildren<CinemachineCamera>(true))
            {
                var deoccluder = camera.GetComponent<CinemachineDeoccluder>();
                if (deoccluder == null)
                {
                    Debug.LogWarning($"{camera.name} requires a preconfigured CinemachineDeoccluder.", camera);
                    continue;
                }

                var decollider = camera.GetComponent<CinemachineDecollider>();
                if (decollider == null)
                {
                    Debug.LogWarning($"{camera.name} requires a preconfigured CinemachineDecollider.", camera);
                    continue;
                }

                cameraCollisionStates.Add(new CameraCollisionState
                {
                    Deoccluder = deoccluder,
                    AddedDeoccluder = false,
                    DeoccluderEnabled = deoccluder.enabled,
                    CollideAgainst = deoccluder.CollideAgainst,
                    MinimumDistanceFromTarget = deoccluder.MinimumDistanceFromTarget,
                    AvoidObstacles = deoccluder.AvoidObstacles,
                    Decollider = decollider,
                    AddedDecollider = false,
                    DecolliderEnabled = decollider.enabled,
                    CameraRadius = decollider.CameraRadius,
                    Decollision = decollider.Decollision,
                    TerrainResolution = decollider.TerrainResolution
                });
            }

            ApplyCameraCollisionSettings();
            appliedCameraCollision = true;
        }

        private void ApplyCameraCollisionSettings()
        {
            foreach (var state in cameraCollisionStates)
            {
                if (state.Deoccluder == null || state.Decollider == null) continue;
                state.Deoccluder.enabled = true;
                state.Deoccluder.CollideAgainst = cameraCollisionLayers;
                state.Deoccluder.MinimumDistanceFromTarget = minimumDistanceFromTarget;
                state.Deoccluder.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
                {
                    Enabled = true, DistanceLimit = 0f, MinimumOcclusionTime = minimumOcclusionTime,
                    CameraRadius = cameraCollisionRadius,
                    UseFollowTarget = new CinemachineDeoccluder.ObstacleAvoidance.FollowTargetSettings { Enabled = true, YOffset = 0f },
                    Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward,
                    MaximumEffort = 4, SmoothingTime = collisionSmoothingTime,
                    Damping = collisionRecoveryDamping, DampingWhenOccluded = collisionDamping
                };

                state.Decollider.enabled = true;
                state.Decollider.CameraRadius = cameraCollisionRadius;
                state.Decollider.Decollision = new CinemachineDecollider.DecollisionSettings
                {
                    Enabled = true, ObstacleLayers = cameraCollisionLayers,
                    UseFollowTarget = new CinemachineDecollider.DecollisionSettings.FollowTargetSettings { Enabled = true, YOffset = 0f },
                    Damping = collisionDamping, SmoothingTime = collisionSmoothingTime
                };
                state.Decollider.TerrainResolution = new CinemachineDecollider.TerrainSettings { Enabled = false };
            }
        }

        private void RestoreCameraCollision()
        {
            foreach (var state in cameraCollisionStates)
            {
                if (state.Deoccluder != null)
                {
                    state.Deoccluder.enabled = state.AddedDeoccluder ? false : state.DeoccluderEnabled;
                    if (!state.AddedDeoccluder)
                    {
                        state.Deoccluder.CollideAgainst = state.CollideAgainst;
                        state.Deoccluder.MinimumDistanceFromTarget = state.MinimumDistanceFromTarget;
                        state.Deoccluder.AvoidObstacles = state.AvoidObstacles;
                    }
                }

                if (state.Decollider != null)
                {
                    state.Decollider.enabled = state.AddedDecollider ? false : state.DecolliderEnabled;
                    if (!state.AddedDecollider)
                    {
                        state.Decollider.CameraRadius = state.CameraRadius;
                        state.Decollider.Decollision = state.Decollision;
                        state.Decollider.TerrainResolution = state.TerrainResolution;
                    }
                }
            }

            appliedCameraCollision = false;
        }
    }
}
