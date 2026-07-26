using System.Collections.Generic;
using Koiusa.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    public abstract class CameraMixerWeightControllerBase : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineMixingCamera mixingCamera;

        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        [Header("Weight Index")]
        [SerializeField, Min(0)] private int defaultCameraIndex = 0;
        [SerializeField, Min(0)] private int followCameraIndex = 1;

        [Header("Transition")]
        [SerializeField, Min(0f)] private float transitionSpeed = 10f;

        [Header("Camera Collision")]
        [SerializeField] private bool enableCameraCollision = true;
        [SerializeField] private LayerMask cameraCollisionLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f)] private float cameraCollisionRadius = 0.45f;
        [SerializeField, Min(0.01f)] private float minimumDistanceFromTarget = 0.5f;
        [SerializeField, Min(0f)] private float minimumOcclusionTime = 0.08f;
        [SerializeField, Range(0f, 2f)] private float collisionSmoothingTime = 0.25f;
        [SerializeField, Min(0f)] private float collisionDamping = 0.4f;
        [SerializeField, Min(0f)] private float collisionRecoveryDamping = 0.7f;

        private float targetDefaultWeight;
        private float targetFollowWeight;
        private IFocusMarkerContext context;
        private CinemachineInputAxisController inputAxisController;
        private InputAction grappleAction;
        private readonly List<InputActionReference> runtimeActionReferences = new();
        private readonly List<CameraCollisionState> cameraCollisionStates = new();
        private bool appliedCameraCollision;

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

            context = ResolveContext();
        }

        protected virtual void OnDestroy()
        {
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
                inputAxisController.enabled = grappleAction == null || !grappleAction.IsPressed();
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

            mixingCamera.SetWeight(defaultCameraIndex, nextDefault);
            mixingCamera.SetWeight(followCameraIndex, nextFollow);
        }

        private void OnContextStateChanged()
        {
            RefreshTargetWeight(false);
        }

        protected void RefreshTargetWeight(bool immediate)
        {
            var isActive = context != null && context.IsActive;

            targetDefaultWeight = isActive ? 0f : 1f;
            targetFollowWeight = isActive ? 1f : 0f;

            if (!immediate || mixingCamera == null)
            {
                return;
            }

            mixingCamera.SetWeight(defaultCameraIndex, targetDefaultWeight);
            mixingCamera.SetWeight(followCameraIndex, targetFollowWeight);
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
                var addedDeoccluder = deoccluder == null;
                if (deoccluder == null)
                {
                    deoccluder = camera.gameObject.AddComponent<CinemachineDeoccluder>();
                }

                var decollider = camera.GetComponent<CinemachineDecollider>();
                var addedDecollider = decollider == null;
                if (decollider == null)
                {
                    decollider = camera.gameObject.AddComponent<CinemachineDecollider>();
                }

                cameraCollisionStates.Add(new CameraCollisionState
                {
                    Deoccluder = deoccluder,
                    AddedDeoccluder = addedDeoccluder,
                    DeoccluderEnabled = deoccluder.enabled,
                    CollideAgainst = deoccluder.CollideAgainst,
                    MinimumDistanceFromTarget = deoccluder.MinimumDistanceFromTarget,
                    AvoidObstacles = deoccluder.AvoidObstacles,
                    Decollider = decollider,
                    AddedDecollider = addedDecollider,
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
