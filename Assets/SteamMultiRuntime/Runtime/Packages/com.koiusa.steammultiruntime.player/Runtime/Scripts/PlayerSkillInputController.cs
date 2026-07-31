using Koiusa.Input;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorCharacterCoordinator))]
    public sealed class PlayerSkillInputController : MonoBehaviour
    {
        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private ActorSkillDefinition attackSkill;
        [SerializeField] private ActorSkillDefinition dashSkill;
        [SerializeField] private ActorSkillDefinition guardSkill;
        [SerializeField] private ActorSkillDefinition healSkill;
        [SerializeField] private Transform directionReference;

        private ActorCharacterCoordinator coordinator;
        private PlayerSkillInputBindings inputBindings;
        private bool guardStartedByInput;

        public void SetInputConfig(InputActionsConfig config)
        {
            if (ReferenceEquals(inputActionsConfig, config)) return;
            inputBindings?.Dispose();
            inputActionsConfig = config;
            CreateInputBindings();
            if (isActiveAndEnabled) inputBindings.Acquire();
        }

        private void Awake()
        {
            coordinator = GetComponent<ActorCharacterCoordinator>();
            CreateInputBindings();
        }

        private void OnEnable() => inputBindings?.Acquire();

        private void OnDisable()
        {
            inputBindings?.Dispose();
            CancelInputGuard();
        }

        private void CreateInputBindings()
        {
            inputBindings = new PlayerSkillInputBindings(
                inputActionsConfig,
                () => TryActivate(attackSkill),
                () => TryActivate(dashSkill),
                () => guardStartedByInput = TryActivate(guardSkill),
                CancelInputGuard,
                () => TryActivate(healSkill));
        }

        private bool TryActivate(ActorSkillDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) return false;
            if (coordinator == null) coordinator = GetComponent<ActorCharacterCoordinator>();
            var reference = directionReference != null ? directionReference : transform;
            return coordinator != null && coordinator.TryActivateSkill(definition, reference.forward);
        }

        private void CancelInputGuard()
        {
            if (!guardStartedByInput) return;
            guardStartedByInput = false;
            if (coordinator == null) return;
            var activeSkill = coordinator.Skills?.ActiveSkill;
            if (guardSkill == null) return;
            if (activeSkill != null && activeSkill.Definition == guardSkill)
            {
                coordinator.Skills.CancelActiveSkill();
            }
        }
    }
}
