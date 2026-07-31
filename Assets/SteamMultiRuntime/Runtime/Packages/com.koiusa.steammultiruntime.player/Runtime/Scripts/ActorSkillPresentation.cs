using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class ActorSkillPresentation : MonoBehaviour, IActorSkillPresentation
    {
        [Header("Animator Triggers (optional)")]
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string dashTrigger = "Dash";
        [SerializeField] private string guardParameter = "Guard";
        [SerializeField] private string healTrigger = "Heal";
        [Header("VFX Graph")]
        [SerializeField] private bool playSkillEffects = true;

        private readonly Dictionary<Animator, HashSet<int>> parameterCache = new();
        private GuardShieldVisual guardShield;
        private PlayerSkillSlot activeSkill = PlayerSkillSlot.None;
        private uint lastRemoteSequence;

        void IActorSkillPresentation.PlaySkillActivation(PlayerSkillSlot slot, uint sequence)
        {
            if (slot == PlayerSkillSlot.None) return;
            if (sequence != 0 && sequence == lastRemoteSequence) return;
            if (sequence != 0) lastRemoteSequence = sequence;

            PlayAnimatorParameter(slot);
            if (playSkillEffects && slot != PlayerSkillSlot.Guard)
                ActorSkillEffectVisual.Create(transform, slot);
        }

        void IActorSkillPresentation.SetActiveSkill(PlayerSkillSlot slot)
        {
            if (activeSkill == slot) return;
            activeSkill = slot;
            var guarding = slot == PlayerSkillSlot.Guard;
            SetAnimatorBool(guardParameter, guarding);
            EnsureGuardShield().SetGuarding(guarding);
        }

        private GuardShieldVisual EnsureGuardShield()
        {
            if (guardShield == null) guardShield = GetComponent<GuardShieldVisual>();
            if (guardShield == null) guardShield = gameObject.AddComponent<GuardShieldVisual>();
            return guardShield;
        }

        private void PlayAnimatorParameter(PlayerSkillSlot slot)
        {
            var parameter = slot switch
            {
                PlayerSkillSlot.Attack => attackTrigger,
                PlayerSkillSlot.Dash => dashTrigger,
                PlayerSkillSlot.Guard => guardParameter,
                PlayerSkillSlot.Heal => healTrigger,
                _ => string.Empty
            };
            if (slot == PlayerSkillSlot.Guard) SetAnimatorBool(parameter, true);
            else SetAnimatorTrigger(parameter);
        }

        private void SetAnimatorTrigger(string parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter)) return;
            var hash = Animator.StringToHash(parameter);
            foreach (var animator in GetComponentsInChildren<Animator>(true))
                if (HasParameter(animator, hash, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(hash);
        }

        private void SetAnimatorBool(string parameter, bool value)
        {
            if (string.IsNullOrWhiteSpace(parameter)) return;
            var hash = Animator.StringToHash(parameter);
            foreach (var animator in GetComponentsInChildren<Animator>(true))
                if (HasParameter(animator, hash, AnimatorControllerParameterType.Bool)) animator.SetBool(hash, value);
        }

        private bool HasParameter(Animator animator, int hash, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (!parameterCache.TryGetValue(animator, out var hashes))
            {
                hashes = new HashSet<int>();
                foreach (var parameter in animator.parameters)
                    hashes.Add(HashCode(parameter.nameHash, parameter.type));
                parameterCache[animator] = hashes;
            }
            return hashes.Contains(HashCode(hash, type));
        }

        private static int HashCode(int hash, AnimatorControllerParameterType type) => (hash * 397) ^ (int)type;

        private void OnDisable()
        {
            activeSkill = PlayerSkillSlot.None;
            if (guardShield != null) guardShield.SetGuarding(false);
            SetAnimatorBool(guardParameter, false);
        }
    }
}
