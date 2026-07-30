using System;
using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class PlayerSkillCoordinator : MonoBehaviour, IPlayerSkillCoordinator
    {
        private readonly List<IPlayerSkillFeature> skills = new List<IPlayerSkillFeature>();
        private IPlayerSkillPresentation presentation;
        public IPlayerSkillFeature ActiveSkill { get; private set; }
        public IReadOnlyList<IPlayerSkillFeature> Skills => skills;
        public event Action<IPlayerSkillFeature> SkillStarted;
        public event Action<IPlayerSkillFeature> SkillEnded;

        private void Awake()
        {
            RefreshSkills();
            presentation = GetComponent<IPlayerSkillPresentation>();
        }
        private void OnEnable() => RefreshSkills();

        private void Update()
        {
            if (ActiveSkill == null) return;
            ActiveSkill.Tick(Time.deltaTime);
            if (!ActiveSkill.IsActive)
            {
                var ended = ActiveSkill;
                ActiveSkill = null;
                presentation?.SetActiveSkill(PlayerSkillSlot.None);
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
                presentation?.PlaySkillActivation(GetSkillSlot(skill));
                presentation?.SetActiveSkill(GetSkillSlot(skill));
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
            presentation?.SetActiveSkill(PlayerSkillSlot.None);
            SkillEnded?.Invoke(cancelled);
        }

        private void OnDisable() => CancelActiveSkill();

        private static PlayerSkillSlot GetSkillSlot(IPlayerSkillFeature skill)
        {
            return skill switch
            {
                SwordAttackSkillFeature => PlayerSkillSlot.Attack,
                DashSkillFeature => PlayerSkillSlot.Dash,
                GuardSkillFeature => PlayerSkillSlot.Guard,
                HealSkillFeature => PlayerSkillSlot.Heal,
                _ => PlayerSkillSlot.None
            };
        }
    }
}
