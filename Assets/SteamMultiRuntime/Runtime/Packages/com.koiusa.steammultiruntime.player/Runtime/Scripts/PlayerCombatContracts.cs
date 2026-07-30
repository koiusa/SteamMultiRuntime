using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal interface IPlayerCombatProcessGate
    {
        bool CanProcessCombat { get; }
    }

    public readonly struct PlayerDamageRequest
    {
        public PlayerDamageRequest(GameObject source, float amount, Vector3 point, Vector3 direction)
        {
            Source = source;
            Amount = Mathf.Max(0f, amount);
            Point = point;
            Direction = direction;
        }

        public GameObject Source { get; }
        public float Amount { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
    }

    public interface IPlayerHealthFeature
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
        float ApplyDamage(PlayerDamageRequest request);
        float Heal(float amount);
    }

    public interface IPlayerDamageReceiverFeature
    {
        bool CanReceiveDamage { get; }
        float ReceiveDamage(PlayerDamageRequest request);
    }

    public interface IPlayerCombatCoordinator
    {
        IPlayerHealthFeature Health { get; }
        float IncomingDamageScale { get; }
        float ReceiveDamage(PlayerDamageRequest request);
        float Heal(float amount);
        int PerformAreaAttack(Vector3 center, float radius, float damage, Vector3 direction, LayerMask layers);
        void SetIncomingDamageScale(int ownerId, float scale);
        void ClearIncomingDamageScale(int ownerId);
    }
}
