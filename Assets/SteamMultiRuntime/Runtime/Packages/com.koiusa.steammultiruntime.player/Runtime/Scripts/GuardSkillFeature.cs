using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class GuardSkillFeature : ActorSkillFeature
    {
        [SerializeField, Range(0f, 1f)] private float incomingDamageScale = 0.25f;
        private IActorCombatCoordinator combat;
        protected override float ActiveDuration => float.PositiveInfinity;
        private protected override ActorSkillSlot PresentationSlot => ActorSkillSlot.Guard;

        private void Awake()
        {
            combat = GetComponent<IActorCombatCoordinator>();
        }

        protected override bool OnActivate(ActorSkillContext context)
        {
            if (combat == null) combat = GetComponent<IActorCombatCoordinator>();
            if (combat == null) return false;
            combat.SetIncomingDamageScale(GetInstanceID(), incomingDamageScale);
            return true;
        }

        protected override void OnCompleted() => EndGuard();

        protected override void OnCancelled() => EndGuard();

        private void EndGuard()
        {
            combat?.ClearIncomingDamageScale(GetInstanceID());
        }
    }
}
