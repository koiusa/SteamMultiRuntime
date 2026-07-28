using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterCoordinator))]
    public sealed class PlayerSkillInputController : MonoBehaviour
    {
        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private string attackActionPath = "Combat/Attack";
        [SerializeField] private string dashActionPath = "Player/Dash";
        [SerializeField] private string guardActionPath = "Player/Guard";
        [SerializeField] private string healActionPath = "Player/Heal";
        [SerializeField] private PlayerSkillDefinition attackSkill;
        [SerializeField] private PlayerSkillDefinition dashSkill;
        [SerializeField] private PlayerSkillDefinition guardSkill;
        [SerializeField] private PlayerSkillDefinition healSkill;
        [SerializeField] private Transform directionReference;

        private PlayerCharacterCoordinator coordinator;
        private InputAction attackAction;
        private InputAction dashAction;
        private InputAction guardAction;
        private InputAction healAction;
        private InputActionLease attackLease;
        private InputActionLease dashLease;
        private InputActionLease guardLease;
        private InputActionLease healLease;
        private bool guardStartedByInput;

        public void SetInputConfig(InputActionsConfig config)
        {
            if (ReferenceEquals(inputActionsConfig, config)) return;
            ReleaseInput();
            inputActionsConfig = config;
            ResolveInput();
            if (isActiveAndEnabled) AcquireInput();
        }

        private void Awake()
        {
            coordinator = GetComponent<PlayerCharacterCoordinator>();
            ResolveInput();
        }

        private void OnEnable() => AcquireInput();

        private void OnDisable() => ReleaseInput();

        private void ResolveInput()
        {
            attackAction = inputActionsConfig?.FindAction(attackActionPath);
            dashAction = inputActionsConfig?.FindAction(dashActionPath);
            guardAction = inputActionsConfig?.FindAction(guardActionPath);
            healAction = inputActionsConfig?.FindAction(healActionPath);
        }

        private void AcquireInput()
        {
            if (attackAction == null && dashAction == null && guardAction == null && healAction == null) ResolveInput();
            if (attackAction != null && attackLease == null)
            {
                attackAction.performed += OnAttackPerformed;
                attackLease = InputActionLease.Acquire(attackAction);
            }
            if (dashAction != null && dashLease == null)
            {
                dashAction.performed += OnDashPerformed;
                dashLease = InputActionLease.Acquire(dashAction);
            }
            if (guardAction != null && guardLease == null)
            {
                guardAction.performed += OnGuardPerformed;
                guardAction.canceled += OnGuardCanceled;
                guardLease = InputActionLease.Acquire(guardAction);
            }
            if (healAction != null && healLease == null)
            {
                healAction.performed += OnHealPerformed;
                healLease = InputActionLease.Acquire(healAction);
            }
        }

        private void ReleaseInput()
        {
            if (attackAction != null) attackAction.performed -= OnAttackPerformed;
            if (dashAction != null) dashAction.performed -= OnDashPerformed;
            if (guardAction != null)
            {
                guardAction.performed -= OnGuardPerformed;
                guardAction.canceled -= OnGuardCanceled;
            }
            if (healAction != null) healAction.performed -= OnHealPerformed;
            CancelInputGuard();
            attackLease?.Dispose();
            dashLease?.Dispose();
            guardLease?.Dispose();
            healLease?.Dispose();
            attackLease = null;
            dashLease = null;
            guardLease = null;
            healLease = null;
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            TryActivate(attackSkill);
        }

        private void OnDashPerformed(InputAction.CallbackContext context)
        {
            TryActivate(dashSkill);
        }

        private void OnGuardPerformed(InputAction.CallbackContext context)
        {
            guardStartedByInput = TryActivate(guardSkill);
        }

        private void OnGuardCanceled(InputAction.CallbackContext context)
        {
            CancelInputGuard();
        }

        private void OnHealPerformed(InputAction.CallbackContext context)
        {
            TryActivate(healSkill);
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
