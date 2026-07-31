using System;
using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class ActorSkillCoordinator : MonoBehaviour, IActorSkillCoordinator
    {
        private readonly List<IActorSkillFeature> skills = new List<IActorSkillFeature>();
        private IActorSkillPresentation presentation;
        public IActorSkillFeature ActiveSkill { get; private set; }
        public IReadOnlyList<IActorSkillFeature> Skills => skills;
        public event Action<IActorSkillFeature> SkillStarted;
        public event Action<IActorSkillFeature> SkillEnded;

        private void Awake()
        {
            RefreshSkills();
            presentation = GetComponent<IActorSkillPresentation>();
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
                presentation?.SetActiveSkill(ActorSkillSlot.None);
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
            var context = new ActorSkillContext(gameObject, direction, target);
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
            presentation?.SetActiveSkill(ActorSkillSlot.None);
            SkillEnded?.Invoke(cancelled);
        }

        private void OnDisable() => CancelActiveSkill();

        private static ActorSkillSlot GetSkillSlot(IActorSkillFeature skill)
        {
            return skill switch
            {
                SwordAttackSkillFeature => ActorSkillSlot.Attack,
                DashSkillFeature => ActorSkillSlot.Dash,
                GuardSkillFeature => ActorSkillSlot.Guard,
                HealSkillFeature => ActorSkillSlot.Heal,
                _ => ActorSkillSlot.None
            };
        }
    }
}
