using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal static class NpcConventionalCollisionRegistry
    {
        private sealed class Entry
        {
            internal NpcNavMeshController Controller;
            internal Collider[] SolidColliders;
        }

        private static readonly List<Entry> Entries = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Entries.Clear();

        internal static void Register(NpcNavMeshController controller, Rigidbody body)
        {
            if (controller == null || body == null || Find(controller) >= 0)
                return;
            RemoveDestroyedEntries();
            var colliders = controller.GetComponentsInChildren<Collider>(true);
            var solidColliders = new List<Collider>(colliders.Length);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider != null && !collider.isTrigger && collider.attachedRigidbody == body)
                    solidColliders.Add(collider);
            }
            var entry = new Entry { Controller = controller, SolidColliders = solidColliders.ToArray() };
            for (var i = 0; i < Entries.Count; i++)
                SetIgnored(entry.SolidColliders, Entries[i].SolidColliders, true);
            Entries.Add(entry);
        }

        internal static void Refresh(NpcNavMeshController controller, Rigidbody body)
        {
            if (controller == null || body == null)
                return;
            Unregister(controller);
            Register(controller, body);
        }

        internal static void Unregister(NpcNavMeshController controller)
        {
            var index = Find(controller);
            if (index < 0)
                return;
            var entry = Entries[index];
            for (var i = 0; i < Entries.Count; i++)
                if (i != index)
                    SetIgnored(entry.SolidColliders, Entries[i].SolidColliders, false);
            Entries.RemoveAt(index);
        }

        private static int Find(NpcNavMeshController controller)
        {
            for (var i = 0; i < Entries.Count; i++)
                if (Entries[i].Controller == controller)
                    return i;
            return -1;
        }

        private static void RemoveDestroyedEntries()
        {
            for (var i = Entries.Count - 1; i >= 0; i--)
                if (Entries[i].Controller == null)
                    Entries.RemoveAt(i);
        }

        private static void SetIgnored(Collider[] left, Collider[] right, bool ignored)
        {
            for (var leftIndex = 0; leftIndex < left.Length; leftIndex++)
            {
                var leftCollider = left[leftIndex];
                if (leftCollider == null)
                    continue;
                for (var rightIndex = 0; rightIndex < right.Length; rightIndex++)
                {
                    var rightCollider = right[rightIndex];
                    if (rightCollider != null)
                        Physics.IgnoreCollision(leftCollider, rightCollider, ignored);
                }
            }
        }
    }
}
