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

        private float targetDefaultWeight;
        private float targetFollowWeight;
        private IFocusMarkerContext context;
        private readonly List<InputActionReference> runtimeActionReferences = new();

        protected abstract IFocusMarkerContext ResolveContext();

        protected virtual void Awake()
        {
            if (mixingCamera == null)
            {
                mixingCamera = GetComponent<CinemachineMixingCamera>();
            }

            ConfigureCameraInputActions();

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
        }

        protected virtual void Update()
        {
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
            var controller = GetComponentInChildren<CinemachineInputAxisController>(true);
            if (controller == null || inputActionsConfig == null)
            {
                return;
            }

            var lookAction = inputActionsConfig.FindAction("Player/Look");
            var zoomAction = inputActionsConfig.FindAction("Player/CameraZoom");

            foreach (var axisController in controller.Controllers)
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
    }
}
