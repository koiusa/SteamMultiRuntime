using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RegistryTargetCandidateSource : MonoBehaviour, ITargetCandidateSource
    {
        [SerializeField] private TargetMarkerRegistry registry;

        public void Collect(in TargetingContext context, List<ITargetable> results)
        {
            var resolvedRegistry = registry != null ? registry : TargetMarkerRegistry.Current;
            if (resolvedRegistry == null || results == null)
            {
                return;
            }

            foreach (var target in resolvedRegistry.Targets)
            {
                if (target != null && target.IsTargetable && target.Root != context.Owner)
                {
                    results.Add(target);
                }
            }
        }
    }
}
