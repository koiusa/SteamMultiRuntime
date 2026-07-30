using UnityEngine;

namespace Koiusa.SteamMultiRuntime.TargetingSystem
{
    [DisallowMultipleComponent]
    public sealed class LocalTargetingCameraConnector : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour consumerSource;
        private ILocalTargetingCameraConsumer consumer;

        private void Awake()
        {
            ResolveConsumer();
        }

        private void OnEnable()
        {
            LocalTargetingControllerRegistry.CurrentChanged += OnControllerChanged;
            OnControllerChanged(LocalTargetingControllerRegistry.Current);
        }

        private void OnDisable()
        {
            LocalTargetingControllerRegistry.CurrentChanged -= OnControllerChanged;
            consumer?.SetTargetingController(null);
        }

        private void OnControllerChanged(Koiusa.TargetingSystem.Runtime.TargetingController controller)
        {
            consumer?.SetTargetingController(controller);
        }

        private void ResolveConsumer()
        {
            consumer = consumerSource as ILocalTargetingCameraConsumer;
            if (consumer != null) return;

            var components = GetComponentsInParent<MonoBehaviour>(true);
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is ILocalTargetingCameraConsumer source)
                {
                    consumer = source;
                    consumerSource = source as MonoBehaviour;
                    return;
                }
            }
        }
    }
}
