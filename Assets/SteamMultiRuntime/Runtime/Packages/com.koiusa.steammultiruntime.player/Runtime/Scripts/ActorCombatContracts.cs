using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal interface IActorCombatProcessGate
    {
        bool CanProcessCombat { get; }
    }

    public readonly struct ActorDamageRequest
    {
        public ActorDamageRequest(GameObject source, float amount, Vector3 point, Vector3 direction)
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

    public interface IActorHealthFeature
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
        float ApplyDamage(ActorDamageRequest request);
        float Heal(float amount);
        void RestoreFullHealth();
    }

    public interface IActorHealthNotifier : IActorHealthFeature
    {
        event Action<float, float> HealthChanged;
    }

    public interface IActorDeathNotifier : IActorHealthNotifier
    {
        event Action<ActorDamageRequest> Died;
    }

    public interface IActorDamageReceiverFeature
    {
        bool CanReceiveDamage { get; }
        float ReceiveDamage(ActorDamageRequest request);
    }

    public interface IActorAreaAttackResolver
    {
        int PerformAreaAttack(
            GameObject source,
            Vector3 center,
            float radius,
            float damage,
            Vector3 direction,
            LayerMask layers);
    }

    public interface IGuardImpactPresenter
    {
        void PlayAttackImpact(Vector3 worldPosition);
    }

    public interface IActorCombatCoordinator
    {
        IActorHealthFeature Health { get; }
        float IncomingDamageScale { get; }
        float ReceiveDamage(ActorDamageRequest request);
        float Heal(float amount);
        int PerformAreaAttack(Vector3 center, float radius, float damage, Vector3 direction, LayerMask layers);
        void SetIncomingDamageScale(int ownerId, float scale);
        void ClearIncomingDamageScale(int ownerId);
    }

    public interface IActorLifeState
    {
        bool IsDead { get; }
        event Action<bool> LifeStateChanged;
    }

    public interface IActorRespawnPresentationNotifier
    {
        event Action<Vector3, Quaternion> RespawnPresentationReady;
    }
}
