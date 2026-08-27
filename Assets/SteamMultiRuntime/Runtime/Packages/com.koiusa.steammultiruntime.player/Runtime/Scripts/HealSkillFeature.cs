using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class HealSkillFeature : ActorSkillFeature
    {
        [SerializeField, Min(0f)] private float amount = 25f;
        private IActorCombatCoordinator combat;
        public float LastHealedAmount { get; private set; }
        private protected override ActorSkillSlot PresentationSlot => ActorSkillSlot.Heal;

        private void Awake() => combat = GetComponent<IActorCombatCoordinator>();

        public override bool CanActivate(ActorSkillContext context)
        {
            if (!base.CanActivate(context)) return false;
            if (combat == null) combat = GetComponent<IActorCombatCoordinator>();
            return combat?.Health != null && combat.Health.IsAlive && combat.Health.CurrentHealth < combat.Health.MaxHealth;
        }

        protected override bool OnActivate(ActorSkillContext context)
        {
            LastHealedAmount = combat != null ? combat.Heal(amount) : 0f;
            return LastHealedAmount > 0f;
        }
    }
}
