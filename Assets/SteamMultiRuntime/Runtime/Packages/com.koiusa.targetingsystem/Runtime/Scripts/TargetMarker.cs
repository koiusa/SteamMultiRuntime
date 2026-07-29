using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    public sealed class TargetMarker : MonoBehaviour, ITargetable, ITargetableLifetime
    {
        [SerializeField] private Transform aimPoint;
        [SerializeField] private float priority;
        [SerializeField] private bool isTargetable = true;
        [SerializeField] private TargetMarkerRegistry registry;

        public Transform Root => transform;
        public Transform AimPoint => aimPoint != null ? aimPoint : transform;
        public bool IsTargetable => isTargetable && isActiveAndEnabled;
        public float Priority => priority;
        public event System.Action Invalidated;

        public TargetMarkerRegistry Registry
        {
            get => registry;
            set
            {
                if (ReferenceEquals(registry, value))
                {
                    return;
                }

                if (isActiveAndEnabled && registry != null)
                {
                    registry.Unregister(this);
                }

                registry = value;

                if (isActiveAndEnabled && registry != null)
                {
                    registry.Register(this);
                }
            }
        }

        private void OnEnable()
        {
            TargetMarkerRegistry.CurrentChanged += OnCurrentRegistryChanged;
            ResolveRegistry()?.Register(this);
        }

        private void OnDisable()
        {
            TargetMarkerRegistry.CurrentChanged -= OnCurrentRegistryChanged;
            ResolveRegistry()?.Unregister(this);
            Invalidated?.Invoke();
        }

        private TargetMarkerRegistry ResolveRegistry() => registry != null ? registry : TargetMarkerRegistry.Current;

        private void OnCurrentRegistryChanged(TargetMarkerRegistry current)
        {
            if (registry == null) current?.Register(this);
        }

        public void SetTargetable(bool value)
        {
            if (isTargetable == value)
            {
                return;
            }

            isTargetable = value;
            if (!value)
            {
                Invalidated?.Invoke();
            }
        }

    }
}
