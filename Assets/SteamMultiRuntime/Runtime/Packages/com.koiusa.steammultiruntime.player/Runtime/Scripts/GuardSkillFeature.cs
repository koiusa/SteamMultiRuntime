using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class GuardSkillFeature : PlayerSkillFeature, IGuardSkillPresentation
    {
        [SerializeField, Range(0f, 1f)] private float incomingDamageScale = 0.25f;
        private IPlayerCombatCoordinator combat;
        private GuardShieldVisual shieldVisual;
        protected override float ActiveDuration => float.PositiveInfinity;

        private void Awake()
        {
            combat = GetComponent<IPlayerCombatCoordinator>();
            shieldVisual = GetComponent<GuardShieldVisual>();
            if (shieldVisual == null) shieldVisual = gameObject.AddComponent<GuardShieldVisual>();
        }

        protected override bool OnActivate(PlayerSkillContext context)
        {
            if (combat == null) combat = GetComponent<IPlayerCombatCoordinator>();
            if (combat == null) return false;
            combat.SetIncomingDamageScale(GetInstanceID(), incomingDamageScale);
            SetGuardingPresentation(true);
            return true;
        }

        public void SetGuardingPresentation(bool guarding)
        {
            if (shieldVisual == null) shieldVisual = GetComponent<GuardShieldVisual>();
            if (shieldVisual == null) shieldVisual = gameObject.AddComponent<GuardShieldVisual>();
            shieldVisual.SetGuarding(guarding);
        }

        protected override void OnCompleted() => EndGuard();

        protected override void OnCancelled() => EndGuard();

        private void EndGuard()
        {
            combat?.ClearIncomingDamageScale(GetInstanceID());
            SetGuardingPresentation(false);
        }
    }
}
