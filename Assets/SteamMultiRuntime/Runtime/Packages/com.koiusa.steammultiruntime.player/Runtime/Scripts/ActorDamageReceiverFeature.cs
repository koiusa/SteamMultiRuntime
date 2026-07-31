using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class ActorDamageReceiverFeature : MonoBehaviour, IActorDamageReceiverFeature
    {
        private IActorCombatCoordinator combat;
        [SerializeField] private bool invincible;
        public bool CanReceiveDamage => isActiveAndEnabled && !invincible;
        public bool IsInvincible => invincible;

        private void Awake() => combat = GetComponent<IActorCombatCoordinator>();
        public void SetInvincible(bool value) => invincible = value;

        public float ReceiveDamage(ActorDamageRequest request)
        {
            return CanReceiveDamage && combat != null ? combat.ReceiveDamage(request) : 0f;
        }
    }
}
