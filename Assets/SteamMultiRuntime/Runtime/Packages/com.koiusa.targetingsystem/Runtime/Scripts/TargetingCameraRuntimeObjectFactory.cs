using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetingCameraRuntimeObjectFactory : MonoBehaviour
    {
        private readonly HashSet<GameObject> ownedObjects = new();

        public TargetingCameraLookAtAnchor CreateFollowTarget(Transform parent)
        {
            if (parent == null) return null;

            ownedObjects.RemoveWhere(host => host == null);
            var host = new GameObject("Targeting Camera Follow Target");
            host.transform.SetParent(parent, false);
            ownedObjects.Add(host);
            return host.AddComponent<TargetingCameraLookAtAnchor>();
        }

        public void Release(Component component)
        {
            if (component == null)
            {
                ownedObjects.RemoveWhere(host => host == null);
                return;
            }
            var host = component.gameObject;
            if (!ownedObjects.Remove(host)) return;
            Destroy(host);
        }

        private void OnDestroy()
        {
            foreach (var host in ownedObjects)
            {
                if (host != null) Destroy(host);
            }
            ownedObjects.Clear();
        }
    }
}
