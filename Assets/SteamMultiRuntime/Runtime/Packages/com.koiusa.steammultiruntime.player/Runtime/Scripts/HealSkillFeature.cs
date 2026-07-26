using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class HealSkillFeature : PlayerSkillFeature
    {
        [SerializeField, Min(0f)] private float amount = 25f;
        private IPlayerCombatCoordinator combat;
        public float LastHealedAmount { get; private set; }

        private void Awake() => combat = GetComponent<IPlayerCombatCoordinator>();

        public override bool CanActivate(PlayerSkillContext context)
        {
            if (!base.CanActivate(context)) return false;
            if (combat == null) combat = GetComponent<IPlayerCombatCoordinator>();
            return combat?.Health != null && combat.Health.IsAlive && combat.Health.CurrentHealth < combat.Health.MaxHealth;
        }

        protected override bool OnActivate(PlayerSkillContext context)
        {
            LastHealedAmount = combat != null ? combat.Heal(amount) : 0f;
            return LastHealedAmount > 0f;
        }
    }
}
