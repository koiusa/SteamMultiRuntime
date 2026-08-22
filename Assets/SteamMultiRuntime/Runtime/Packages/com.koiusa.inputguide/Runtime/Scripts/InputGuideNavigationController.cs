using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.InputGuide
{
    /// <summary>Owns optional Input Actions for compact operation-map navigation.</summary>
    [DisallowMultipleComponent]
    public sealed class InputGuideNavigationController : MonoBehaviour
    {
        [SerializeField] private InputGuideOverlay overlay;
        [SerializeField] private string previousMapAction;
        [SerializeField] private string nextMapAction;
        [SerializeField] private string scrollAction;
        [SerializeField, Min(1f)] private float scrollStep = 90f;

        private InputActionBinding previousMapBinding;
        private InputActionBinding nextMapBinding;
        private InputActionBinding scrollBinding;
        private InputGuideOverlay subscribedOverlay;

        public string[] GetAvailableActionPaths()
        {
            return overlay != null ? overlay.GetAvailableActionPaths() : System.Array.Empty<string>();
        }

        public string[] GetAvailableScrollActionPaths()
        {
            return overlay != null
                ? overlay.GetAvailableVector2ActionPaths()
                : System.Array.Empty<string>();
        }

        private void OnEnable() => RefreshBindings();

        private void OnDisable()
        {
            if (subscribedOverlay != null)
            {
                subscribedOverlay.ConfiguredInputActionsChanged -= RefreshBindings;
                subscribedOverlay = null;
            }

            ReleaseBindings();
        }

        public void RefreshBindings()
        {
            if (!ReferenceEquals(subscribedOverlay, overlay))
            {
                if (subscribedOverlay != null)
                {
                    subscribedOverlay.ConfiguredInputActionsChanged -= RefreshBindings;
                }

                subscribedOverlay = overlay;
                if (subscribedOverlay != null)
                {
                    subscribedOverlay.ConfiguredInputActionsChanged += RefreshBindings;
                }
            }

            ReleaseBindings();
            previousMapBinding = InputActionBinding.Bind(
                overlay?.FindConfiguredAction(previousMapAction), OnPreviousMapPerformed);
            nextMapBinding = InputActionBinding.Bind(
                overlay?.FindConfiguredAction(nextMapAction), OnNextMapPerformed);
            scrollBinding = InputActionBinding.Bind(
                overlay?.FindConfiguredAction(scrollAction), OnScrollPerformed);
        }

        private void ReleaseBindings()
        {
            previousMapBinding?.Dispose();
            previousMapBinding = null;
            nextMapBinding?.Dispose();
            nextMapBinding = null;
            scrollBinding?.Dispose();
            scrollBinding = null;
        }

        private void OnPreviousMapPerformed(InputAction.CallbackContext context) =>
            overlay?.SelectPreviousMapTab();

        private void OnNextMapPerformed(InputAction.CallbackContext context) =>
            overlay?.SelectNextMapTab();

        private void OnScrollPerformed(InputAction.CallbackContext context)
        {
            overlay?.ScrollOperationList(context.ReadValue<Vector2>().y, scrollStep);
        }
    }
}
