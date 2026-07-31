using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class ActorHitDetectionFeature : MonoBehaviour
    {
        private readonly Collider[] hits = new Collider[32];
        private readonly HashSet<IActorDamageReceiverFeature> damaged = new HashSet<IActorDamageReceiverFeature>();

        public int PerformAreaAttack(GameObject source, Vector3 center, float radius, float damage, Vector3 direction, LayerMask layers)
        {
            damaged.Clear();
            var count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0f, radius), hits, layers, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var receiver = hits[i] != null ? hits[i].GetComponentInParent<IActorDamageReceiverFeature>() : null;
                if (receiver == null || !receiver.CanReceiveDamage) continue;
                if (receiver is Component component && component.gameObject == source) continue;
                if (!damaged.Add(receiver)) continue;
                receiver.ReceiveDamage(new ActorDamageRequest(source, damage, center, direction));
            }
            return damaged.Count;
        }
    }
}
