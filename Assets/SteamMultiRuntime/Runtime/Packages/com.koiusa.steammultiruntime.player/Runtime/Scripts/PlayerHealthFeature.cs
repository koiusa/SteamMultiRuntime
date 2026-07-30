using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealthFeature : MonoBehaviour, IPlayerHealthFeature
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool restoreOnEnable = true;
        [SerializeField] private float currentHealth;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0f;
        public event Action<float, float> HealthChanged;
        public event Action<PlayerDamageRequest> Died;

        private void Awake()
        {
            if (restoreOnEnable || currentHealth <= 0f) currentHealth = maxHealth;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        public float ApplyDamage(PlayerDamageRequest request)
        {
            if (!isActiveAndEnabled || !IsAlive || request.Amount <= 0f) return 0f;
            var previous = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - request.Amount);
            var applied = previous - currentHealth;
            HealthChanged?.Invoke(currentHealth, maxHealth);
            if (previous > 0f && currentHealth <= 0f) Died?.Invoke(request);
            return applied;
        }

        public float Heal(float amount)
        {
            if (!isActiveAndEnabled || !IsAlive || amount <= 0f) return 0f;
            var previous = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            var applied = currentHealth - previous;
            if (applied > 0f) HealthChanged?.Invoke(currentHealth, maxHealth);
            return applied;
        }

        internal void ApplyReplicatedHealth(float value)
        {
            var replicated = Mathf.Clamp(value, 0f, maxHealth);
            if (Mathf.Approximately(currentHealth, replicated)) return;
            currentHealth = replicated;
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
