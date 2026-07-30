using Unity.Cinemachine;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetingCameraPresenter : MonoBehaviour
    {
        [SerializeField] private TargetingController controller;
        [SerializeField] private CinemachineMixingCamera mixingCamera;
        [SerializeField] private CinemachineCamera freeCamera;
        [SerializeField] private CinemachineCamera singleCamera;
        [SerializeField] private CinemachineCamera multiCamera;
        [SerializeField] private CinemachineTargetGroup multiTargetGroup;
        [SerializeField] private Transform fallbackLookAt;
        [SerializeField] private TargetingCameraGroupPresenter groupPresenter;
        [SerializeField] private TargetingCameraFramingMode framingMode = TargetingCameraFramingMode.PrimaryCentered;
        [SerializeField, Min(0f)] private float targetWeight = 1f;
        [SerializeField, Min(0f)] private float targetRadius = 0.5f;

        private TargetingMode targetMode;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<TargetingController>();
            if (mixingCamera == null) mixingCamera = GetComponent<CinemachineMixingCamera>();
            ConfigureGroupPresenter();
        }

        private void OnEnable()
        {
            SubscribeController();
        }

        private void OnDisable()
        {
            UnsubscribeController();
            groupPresenter?.Present(TargetingState.Empty);
        }

        public void SetController(TargetingController value)
        {
            if (controller == value) return;
            UnsubscribeController();
            controller = value;
            if (isActiveAndEnabled) SubscribeController();
        }

        private void OnStateChanged(TargetingStateChange change) => Present(change.Current);

        private void SubscribeController()
        {
            if (controller == null)
            {
                Present(TargetingState.Empty);
                return;
            }

            controller.StateChanged -= OnStateChanged;
            controller.StateChanged += OnStateChanged;
            Present(controller.State);
        }

        private void UnsubscribeController()
        {
            if (controller != null) controller.StateChanged -= OnStateChanged;
        }

        private void Present(TargetingState state)
        {
            var previousMode = targetMode;
            targetMode = state.Mode;
            groupPresenter?.SetPlayerAnchor(fallbackLookAt);
            groupPresenter?.Present(state);
            MatchIncomingCamera(previousMode, state.Mode);
            SetWeight(freeCamera, state.Mode == TargetingMode.None ? 1f : 0f);
            SetWeight(singleCamera, state.Mode == TargetingMode.Single ? 1f : 0f);
            SetWeight(multiCamera, state.Mode == TargetingMode.Multi ? 1f : 0f);
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
            TargetingMode.None => freeCamera,
            TargetingMode.Single => singleCamera,
            TargetingMode.Multi => multiCamera,
            _ => null
        };

        private void ConfigureGroupPresenter()
        {
            if (groupPresenter == null)
            {
                groupPresenter = GetComponent<TargetingCameraGroupPresenter>();
            }
            if (groupPresenter == null)
            {
                Debug.LogWarning(
                    "Targeting camera requires a preconfigured TargetingCameraGroupPresenter.",
                    this);
                return;
            }
            groupPresenter.Configure(singleCamera, multiCamera, multiTargetGroup, framingMode, targetWeight, targetRadius);
        }

        private void SetWeight(CinemachineCamera camera, float weight)
        {
            if (mixingCamera != null && camera != null) mixingCamera.SetWeight(camera, weight);
        }
    }
}
