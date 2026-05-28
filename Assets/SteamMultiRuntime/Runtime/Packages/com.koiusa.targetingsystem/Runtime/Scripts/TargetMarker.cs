using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    public sealed class TargetMarker : MonoBehaviour, ITargetable
    {
        [SerializeField] private Transform aimPoint;
        [SerializeField] private float priority;
        [SerializeField] private bool isTargetable = true;
        [SerializeField] private TargetMarkerRegistry registry;

        public Transform Root => transform;
        public Transform AimPoint => aimPoint != null ? aimPoint : transform;
        public bool IsTargetable => isTargetable && isActiveAndEnabled;
        public float Priority => priority;

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
            Registry?.Register(this);
        }

        private void OnDisable()
        {
            Registry?.Unregister(this);
        }

        public void SetTargetable(bool value)
        {
            isTargetable = value;
        }

    }
}
