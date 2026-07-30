using Koiusa.Input;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterCoordinator))]
    public sealed class PlayerSkillInputController : MonoBehaviour
    {
        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private PlayerSkillDefinition attackSkill;
        [SerializeField] private PlayerSkillDefinition dashSkill;
        [SerializeField] private PlayerSkillDefinition guardSkill;
        [SerializeField] private PlayerSkillDefinition healSkill;
        [SerializeField] private Transform directionReference;

        private PlayerCharacterCoordinator coordinator;
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
            coordinator = GetComponent<PlayerCharacterCoordinator>();
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

        private bool TryActivate(PlayerSkillDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) return false;
            if (coordinator == null) coordinator = GetComponent<PlayerCharacterCoordinator>();
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
