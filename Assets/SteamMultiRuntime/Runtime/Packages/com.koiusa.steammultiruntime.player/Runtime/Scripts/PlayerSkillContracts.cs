using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal enum PlayerSkillSlot
    {
        None = -1,
        Attack = 0,
        Dash = 1,
        Guard = 2,
        Heal = 3
    }

    internal interface IPlayerSkillPresentation
    {
        void PlaySkillActivation(PlayerSkillSlot slot, uint sequence = 0);
        void SetActiveSkill(PlayerSkillSlot slot);
    }

    public readonly struct PlayerSkillContext
    {
        public PlayerSkillContext(GameObject owner, Vector3 direction, GameObject target = null)
        {
            Owner = owner;
            Direction = direction;
            Target = target;
        }

        public GameObject Owner { get; }
        public Vector3 Direction { get; }
        public GameObject Target { get; }
    }

    public interface IPlayerSkillFeature
    {
        PlayerSkillDefinition Definition { get; }
        string SkillId { get; }
        bool IsEnabled { get; }
        bool IsActive { get; }
        float CooldownRemaining { get; }
        bool CanActivate(PlayerSkillContext context);
        bool TryActivate(PlayerSkillContext context);
        void Tick(float deltaTime);
        void Cancel();
    }

    public interface IPlayerSkillCoordinator
    {
        IPlayerSkillFeature ActiveSkill { get; }
        bool TryActivate(string skillId, Vector3 direction, GameObject target = null);
        void CancelActiveSkill();
    }
}
