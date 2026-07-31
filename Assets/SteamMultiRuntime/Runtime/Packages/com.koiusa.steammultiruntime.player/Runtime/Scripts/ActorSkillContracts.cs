using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal enum ActorSkillSlot
    {
        None = -1,
        Attack = 0,
        Dash = 1,
        Guard = 2,
        Heal = 3
    }

    internal interface IActorSkillPresentation
    {
        void PlaySkillActivation(ActorSkillSlot slot, uint sequence = 0);
        void SetActiveSkill(ActorSkillSlot slot);
    }

    public readonly struct ActorSkillContext
    {
        public ActorSkillContext(GameObject owner, Vector3 direction, GameObject target = null)
        {
            Owner = owner;
            Direction = direction;
            Target = target;
        }

        public GameObject Owner { get; }
        public Vector3 Direction { get; }
        public GameObject Target { get; }
    }

    public interface IActorSkillFeature
    {
        ActorSkillDefinition Definition { get; }
        string SkillId { get; }
        bool IsEnabled { get; }
        bool IsActive { get; }
        float CooldownRemaining { get; }
        bool CanActivate(ActorSkillContext context);
        bool TryActivate(ActorSkillContext context);
        void Tick(float deltaTime);
        void Cancel();
    }

    public interface IActorSkillCoordinator
    {
        IActorSkillFeature ActiveSkill { get; }
        bool TryActivate(string skillId, Vector3 direction, GameObject target = null);
        void CancelActiveSkill();
    }
}
