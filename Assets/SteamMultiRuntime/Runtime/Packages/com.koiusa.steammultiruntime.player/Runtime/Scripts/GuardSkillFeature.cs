using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class GuardSkillFeature : PlayerSkillFeature
    {
        [SerializeField, Range(0f, 1f)] private float incomingDamageScale = 0.25f;
        private IPlayerCombatCoordinator combat;
        protected override float ActiveDuration => float.PositiveInfinity;

        private void Awake() => combat = GetComponent<IPlayerCombatCoordinator>();

        protected override bool OnActivate(PlayerSkillContext context)
        {
            if (combat == null) combat = GetComponent<IPlayerCombatCoordinator>();
            if (combat == null) return false;
            combat.SetIncomingDamageScale(GetInstanceID(), incomingDamageScale);
            return true;
        }

        protected override void OnCompleted() => combat?.ClearIncomingDamageScale(GetInstanceID());
        protected override void OnCancelled() => combat?.ClearIncomingDamageScale(GetInstanceID());
    }
}
