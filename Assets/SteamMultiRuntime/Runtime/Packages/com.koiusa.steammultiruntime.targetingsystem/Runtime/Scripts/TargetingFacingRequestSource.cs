using Koiusa.TargetingSystem.Runtime;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.TargetingSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TargetingController))]
    public sealed class TargetingFacingRequestSource : MonoBehaviour, IPlayerFacingRequestSource
    {
        private TargetingController controller;
        private Transform primaryAimPoint;

        private void Awake()
        {
            controller = GetComponent<TargetingController>();
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                controller = GetComponent<TargetingController>();
            }

            controller.StateChanged -= OnStateChanged;
            controller.StateChanged += OnStateChanged;
            ApplyState(controller.State);
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.StateChanged -= OnStateChanged;
            }

            primaryAimPoint = null;
        }

        public bool TryGetFacingRequest(Vector3 origin, bool _, out PlayerFacingRequest request)
        {
            if (primaryAimPoint == null)
            {
                request = default;
                return false;
            }

            request = new PlayerFacingRequest(
                primaryAimPoint.position - origin,
                PlayerFacingPriority.Targeting);
            return request.IsValid;
        }

        private void OnStateChanged(TargetingStateChange change)
        {
            ApplyState(change.Current);
        }

        private void ApplyState(TargetingState state)
        {
            var primaryTarget = state.Mode == TargetingMode.Multi ? state.PrimaryTarget : null;
            primaryAimPoint = primaryTarget?.AimPoint != null ? primaryTarget.AimPoint : primaryTarget?.Root;
        }
    }
}
