using Koiusa.TargetingSystem.Runtime;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.TargetingSystem
{
    [DisallowMultipleComponent]
    public sealed class PlayerTargetingOwner : MonoBehaviour
    {
        [SerializeField] private TargetingController controller;
        [SerializeField] private TargetingCommandInput input;

        private ILocalPlayerOwnershipNotifier ownership;
        private bool lastActive;
        private bool hasAppliedState;

        private void Awake()
        {
            ResolveReferences();
            ApplyOwnership();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (ownership != null)
            {
                ownership.OwnershipChanged += OnOwnershipChanged;
            }
            ApplyOwnership();
        }

        private void OnDisable()
        {
            if (ownership != null)
            {
                ownership.OwnershipChanged -= OnOwnershipChanged;
            }
            LocalTargetingControllerRegistry.Unregister(controller);
        }

        private void ResolveReferences()
        {
            controller ??= GetComponent<TargetingController>();
            input ??= GetComponent<TargetingCommandInput>();

            ownership = null;
            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is ILocalPlayerOwnershipNotifier source)
                {
                    ownership = source;
                    break;
                }
            }
        }

        private void ApplyOwnership()
        {
            var active = ownership != null && ownership.IsOwnershipResolved && ownership.IsLocalOwner;
            if (hasAppliedState && active == lastActive) return;

            hasAppliedState = true;
            lastActive = active;
            if (controller != null) controller.enabled = active;
            if (input != null) input.enabled = active;
            if (active)
                LocalTargetingControllerRegistry.Register(controller);
            else
                LocalTargetingControllerRegistry.Unregister(controller);
        }

        private void OnOwnershipChanged()
        {
            ApplyOwnership();
        }
    }
}
