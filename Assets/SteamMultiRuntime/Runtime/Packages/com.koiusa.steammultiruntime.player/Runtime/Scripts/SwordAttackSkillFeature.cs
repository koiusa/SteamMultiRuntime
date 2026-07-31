using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class SwordAttackSkillFeature : ActorSkillFeature
    {
        [SerializeField, Min(0f)] private float damage = 20f;
        [SerializeField, Min(0f)] private float radius = 1.2f;
        [SerializeField, Min(0f)] private float forwardOffset = 1f;
        [SerializeField] private LayerMask targetLayers = ~0;

        private IActorCombatCoordinator combat;
        public int LastHitCount { get; private set; }

        private void Awake() => combat = GetComponent<IActorCombatCoordinator>();

        protected override bool OnActivate(ActorSkillContext context)
        {
            if (combat == null) combat = GetComponent<IActorCombatCoordinator>();
            if (combat == null) return false;
            var direction = context.Direction.sqrMagnitude > 0.0001f ? context.Direction.normalized : transform.forward;
            var center = transform.position + direction * forwardOffset;
            LastHitCount = combat.PerformAreaAttack(center, radius, damage, direction, targetLayers);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.35f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * forwardOffset, radius);
        }
    }
}
