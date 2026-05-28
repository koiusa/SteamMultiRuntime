using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetMarkerRegistry : MonoBehaviour
    {
        private readonly HashSet<ITargetable> targets = new HashSet<ITargetable>();

        public IEnumerable<ITargetable> Targets => targets;

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
