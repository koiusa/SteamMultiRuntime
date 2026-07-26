using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class PlayerDamageReceiverFeature : MonoBehaviour, IPlayerDamageReceiverFeature
    {
        private IPlayerCombatCoordinator combat;
        [SerializeField] private bool invincible;
        public bool CanReceiveDamage => isActiveAndEnabled && !invincible;
        public bool IsInvincible => invincible;

        private void Awake() => combat = GetComponent<IPlayerCombatCoordinator>();
        public void SetInvincible(bool value) => invincible = value;

        public float ReceiveDamage(PlayerDamageRequest request)
        {
            return CanReceiveDamage && combat != null ? combat.ReceiveDamage(request) : 0f;
        }
    }
}
