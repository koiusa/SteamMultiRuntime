using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealthFeature))]
    public sealed class NetworkPlayerCombatState : NetworkBehaviour, IPlayerCombatProcessGate
    {
        private readonly NetworkVariable<float> currentHealth = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private PlayerHealthFeature health;

        public float CurrentHealth => currentHealth.Value;
        bool IPlayerCombatProcessGate.CanProcessCombat => IsSpawned && IsServer;

        private void Awake()
        {
            health = GetComponent<PlayerHealthFeature>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            currentHealth.OnValueChanged += OnCurrentHealthChanged;

            if (IsServer)
            {
                health.HealthChanged += OnServerHealthChanged;
                currentHealth.Value = health.CurrentHealth;
            }
            else
            {
                health.ApplyReplicatedHealth(currentHealth.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            currentHealth.OnValueChanged -= OnCurrentHealthChanged;
            if (health != null) health.HealthChanged -= OnServerHealthChanged;
            base.OnNetworkDespawn();
        }

        private void OnServerHealthChanged(float value, float maxHealth)
        {
            if (IsServer) currentHealth.Value = value;
        }

        private void OnCurrentHealthChanged(float previousValue, float newValue)
        {
            if (!IsServer && health != null) health.ApplyReplicatedHealth(newValue);
        }
    }
}
