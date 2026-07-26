using System;
using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class PlayerSkillCoordinator : MonoBehaviour, IPlayerSkillCoordinator
    {
        private readonly List<IPlayerSkillFeature> skills = new List<IPlayerSkillFeature>();
        public IPlayerSkillFeature ActiveSkill { get; private set; }
        public IReadOnlyList<IPlayerSkillFeature> Skills => skills;
        public event Action<IPlayerSkillFeature> SkillStarted;
        public event Action<IPlayerSkillFeature> SkillEnded;

        private void Awake() => RefreshSkills();
        private void OnEnable() => RefreshSkills();

        private void Update()
        {
            if (ActiveSkill == null) return;
            ActiveSkill.Tick(Time.deltaTime);
            if (!ActiveSkill.IsActive)
            {
                var ended = ActiveSkill;
                ActiveSkill = null;
                SkillEnded?.Invoke(ended);
            }
        }

        public void RefreshSkills()
        {
            skills.Clear();
            GetComponents(skills);
        }

        public bool TryActivate(string skillId, Vector3 direction, GameObject target = null)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return false;
            if (ActiveSkill != null && ActiveSkill.IsActive) return false;
            var context = new PlayerSkillContext(gameObject, direction, target);
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (!string.Equals(skill.SkillId, skillId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!skill.TryActivate(context)) return false;
                ActiveSkill = skill;
                SkillStarted?.Invoke(skill);
                return true;
            }
            return false;
        }

        public void CancelActiveSkill()
        {
            if (ActiveSkill == null) return;
            var cancelled = ActiveSkill;
            ActiveSkill.Cancel();
            ActiveSkill = null;
            SkillEnded?.Invoke(cancelled);
        }

        private void OnDisable() => CancelActiveSkill();
    }
}
