using System.Collections.Generic;
using System;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetMarkerRegistry : MonoBehaviour
    {
        public static TargetMarkerRegistry Current { get; private set; }
        public static event Action<TargetMarkerRegistry> CurrentChanged;

        private readonly HashSet<ITargetable> targets = new HashSet<ITargetable>();

        public IEnumerable<ITargetable> Targets => targets;

        private void OnEnable()
        {
            Current = this;
            CurrentChanged?.Invoke(this);
        }

        private void OnDisable()
        {
            if (!ReferenceEquals(Current, this)) return;
            Current = null;
            CurrentChanged?.Invoke(null);
        }

        public void Register(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            targets.Add(target);
        }

        public void Unregister(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            targets.Remove(target);
        }
    }
}
