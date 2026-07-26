using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public abstract class PlayerSkillFeature : MonoBehaviour, IPlayerSkillFeature
    {
        [SerializeField] private string skillId;
        [SerializeField, Min(0f)] private float cooldown = 1f;
        [SerializeField, Min(0f)] private float activeDuration = 0.2f;

        private float cooldownEndsAt;
        private float activeEndsAt;

        public string SkillId => string.IsNullOrWhiteSpace(skillId) ? GetType().Name : skillId;
        public bool IsEnabled => isActiveAndEnabled;
        public bool IsActive { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, cooldownEndsAt - Time.time);
        protected PlayerSkillContext Context { get; private set; }
        protected virtual float ActiveDuration => activeDuration;

        public virtual bool CanActivate(PlayerSkillContext context)
        {
            return IsEnabled && !IsActive && CooldownRemaining <= 0f;
        }

        public bool TryActivate(PlayerSkillContext context)
        {
            if (!CanActivate(context)) return false;
            Context = context;
            if (!OnActivate(context)) return false;
            IsActive = true;
            activeEndsAt = Time.time + ActiveDuration;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            OnSkillTick(deltaTime);
            if (ActiveDuration <= 0f || Time.time >= activeEndsAt) Complete();
        }

        public void Cancel()
        {
            if (!IsActive) return;
            IsActive = false;
            cooldownEndsAt = Time.time + cooldown;
            OnCancelled();
        }

        protected void Complete()
        {
            if (!IsActive) return;
            IsActive = false;
            cooldownEndsAt = Time.time + cooldown;
            OnCompleted();
        }

        protected abstract bool OnActivate(PlayerSkillContext context);
        protected virtual void OnSkillTick(float deltaTime) { }
        protected virtual void OnCompleted() { }
        protected virtual void OnCancelled() { }

        protected virtual void OnDisable()
        {
            Cancel();
        }
    }
}
