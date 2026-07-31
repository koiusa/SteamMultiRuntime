using System;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealthFeature))]
    public sealed class NetworkActorCombatState : NetworkBehaviour, IActorCombatProcessGate,
        IActorRespawnPresentationNotifier
    {
        private readonly NetworkVariable<float> currentHealth = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<RespawnPresentationState> respawnPresentation =
            new NetworkVariable<RespawnPresentationState>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private ActorHealthFeature health;
        private ActorRespawnFeature respawn;

        public float CurrentHealth => currentHealth.Value;
        public event Action<Vector3, Quaternion> RespawnPresentationReady;
        bool IActorCombatProcessGate.CanProcessCombat => IsSpawned && IsServer;

        private void Awake()
        {
            health = GetComponent<ActorHealthFeature>();
            health?.EnsureInitialized();
            respawn = GetComponent<ActorRespawnFeature>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            currentHealth.OnValueChanged += OnCurrentHealthChanged;
            respawnPresentation.OnValueChanged += OnRespawnPresentationChanged;

            if (IsServer)
            {
                health.HealthChanged += OnServerHealthChanged;
                if (respawn != null) respawn.RespawnPresentationReady += OnServerRespawnPresentationReady;
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
            respawnPresentation.OnValueChanged -= OnRespawnPresentationChanged;
            if (health != null) health.HealthChanged -= OnServerHealthChanged;
            if (respawn != null) respawn.RespawnPresentationReady -= OnServerRespawnPresentationReady;
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

        private void OnServerRespawnPresentationReady(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;
            var previous = respawnPresentation.Value;
            respawnPresentation.Value = new RespawnPresentationState(
                position,
                rotation,
                previous.Sequence + 1u);
        }

        private void OnRespawnPresentationChanged(
            RespawnPresentationState previous,
            RespawnPresentationState current)
        {
            if (current.Sequence == 0u || current.Sequence == previous.Sequence) return;
            if (!IsServer) transform.SetPositionAndRotation(current.Position, current.Rotation);
            RespawnPresentationReady?.Invoke(current.Position, current.Rotation);
        }

        private struct RespawnPresentationState : INetworkSerializable, IEquatable<RespawnPresentationState>
        {
            public RespawnPresentationState(Vector3 position, Quaternion rotation, uint sequence)
            {
                Position = position;
                Rotation = rotation;
                Sequence = sequence;
            }

            public Vector3 Position;
            public Quaternion Rotation;
            public uint Sequence;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Rotation);
                serializer.SerializeValue(ref Sequence);
            }

            public bool Equals(RespawnPresentationState other)
            {
                return Position.Equals(other.Position)
                    && Rotation.Equals(other.Rotation)
                    && Sequence == other.Sequence;
            }
        }
    }
}
