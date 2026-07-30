using Koiusa.TargetingSystem.Runtime;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.TargetingSystem
{
    [DisallowMultipleComponent]
    public sealed class LocalTargetingIndicatorPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject indicatorObject;
        [SerializeField] private TargetIndicatorController indicator;

        public void Configure(GameObject indicatorRoot, TargetIndicatorController indicatorController)
        {
            indicatorObject = indicatorRoot;
            indicator = indicatorController;
        }

        private void OnEnable() => RestoreRuntime();

        private void OnDisable()
        {
            LocalTargetingControllerRegistry.CurrentChanged -= OnControllerChanged;
            SetController(null);
        }

        private void RestoreRuntime()
        {
            LocalTargetingControllerRegistry.CurrentChanged -= OnControllerChanged;
            LocalTargetingControllerRegistry.CurrentChanged += OnControllerChanged;
            OnControllerChanged(LocalTargetingControllerRegistry.Current);
        }

        private void OnControllerChanged(TargetingController controller) => SetController(controller);

        private void SetController(TargetingController controller)
        {
            if (controller == null)
            {
                indicator?.SetController(null);
                if (indicatorObject != null)
                    indicatorObject.SetActive(false);
                return;
            }

            if (indicator == null || indicatorObject == null)
            {
                Debug.LogWarning("[Targeting] Target Indicator UI is not configured on Targeting System.", this);
                return;
            }

            indicator.SetController(controller);
            indicatorObject.SetActive(true);
        }
    }
}
