using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class PlayerCombatCoordinator : MonoBehaviour, IPlayerCombatCoordinator
    {
        private readonly Dictionary<int, float> incomingDamageScales = new Dictionary<int, float>();
        private PlayerHitDetectionFeature hitDetection;
        private GuardShieldVisual guardShieldVisual;
        public IPlayerHealthFeature Health { get; private set; }
        public float IncomingDamageScale { get; private set; } = 1f;

        private void Awake() => RefreshFeatures();
        private void OnEnable() => RefreshFeatures();

        public void RefreshFeatures()
        {
            Health = GetComponent<IPlayerHealthFeature>();
            hitDetection = GetComponent<PlayerHitDetectionFeature>();
            guardShieldVisual = GetComponent<GuardShieldVisual>();
        }

        public float ReceiveDamage(PlayerDamageRequest request)
        {
            if (!isActiveAndEnabled || Health == null) return 0f;
            if (guardShieldVisual == null) guardShieldVisual = GetComponent<GuardShieldVisual>();
            if (IncomingDamageScale < 1f) guardShieldVisual?.PlayAttackImpact(request.Point);
            var scaled = new PlayerDamageRequest(request.Source, request.Amount * IncomingDamageScale, request.Point, request.Direction);
            return Health.ApplyDamage(scaled);
        }

        public float Heal(float amount) => isActiveAndEnabled && Health != null ? Health.Heal(amount) : 0f;

        public int PerformAreaAttack(Vector3 center, float radius, float damage, Vector3 direction, LayerMask layers)
        {
            return hitDetection != null
                ? hitDetection.PerformAreaAttack(gameObject, center, radius, damage, direction, layers)
                : 0;
        }

        public void SetIncomingDamageScale(int ownerId, float scale)
        {
            incomingDamageScales[ownerId] = Mathf.Clamp01(scale);
            RecalculateIncomingDamageScale();
        }

        public void ClearIncomingDamageScale(int ownerId)
        {
            incomingDamageScales.Remove(ownerId);
            RecalculateIncomingDamageScale();
        }

        private void RecalculateIncomingDamageScale()
        {
            IncomingDamageScale = 1f;
            foreach (var pair in incomingDamageScales) IncomingDamageScale = Mathf.Min(IncomingDamageScale, pair.Value);
        }

        private void OnDisable()
        {
            incomingDamageScales.Clear();
            IncomingDamageScale = 1f;
        }
    }
}
