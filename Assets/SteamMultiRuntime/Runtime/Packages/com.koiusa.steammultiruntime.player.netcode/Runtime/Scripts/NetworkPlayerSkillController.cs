using Koiusa.Input;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterCoordinator))]
    public sealed class NetworkPlayerSkillController : NetworkBehaviour
    {
        private const int NoActiveSkill = -1;

        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private string attackActionPath = "Player/Attack";
        [SerializeField] private string dashActionPath = "Player/Dash";
        [SerializeField] private string guardActionPath = "Player/Guard";
        [SerializeField] private string healActionPath = "Player/Heal";
        [SerializeField] private PlayerSkillDefinition attackSkill;
        [SerializeField] private PlayerSkillDefinition dashSkill;
        [SerializeField] private PlayerSkillDefinition guardSkill;
        [SerializeField] private PlayerSkillDefinition healSkill;
        [SerializeField] private Transform directionReference;

        private readonly NetworkVariable<int> activeSkillIndex = new NetworkVariable<int>(
            NoActiveSkill, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> activationSequence = new NetworkVariable<uint>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        public int ActiveSkillIndex => activeSkillIndex.Value;
        public uint ActivationSequence => activationSequence.Value;

        private void Awake()
        {
            coordinator = GetComponent<PlayerCharacterCoordinator>();
            ResolveInput();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && coordinator?.Skills != null)
            {
                coordinator.Skills.SkillStarted += OnServerSkillStarted;
                coordinator.Skills.SkillEnded += OnServerSkillEnded;
            }
            if (IsOwner) AcquireInput();
        }

        public override void OnNetworkDespawn()
        {
            ReleaseInput();
            if (coordinator?.Skills != null)
            {
                coordinator.Skills.SkillStarted -= OnServerSkillStarted;
                coordinator.Skills.SkillEnded -= OnServerSkillEnded;
            }
            base.OnNetworkDespawn();
        }

        private void OnEnable()
        {
            if (IsSpawned && IsOwner) AcquireInput();
        }

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
            Acquire(attackAction, ref attackLease, OnAttackPerformed);
            Acquire(dashAction, ref dashLease, OnDashPerformed);
            Acquire(healAction, ref healLease, OnHealPerformed);
            if (guardAction != null && guardLease == null)
            {
                guardAction.performed += OnGuardPerformed;
                guardAction.canceled += OnGuardCanceled;
                guardLease = InputActionLease.Acquire(guardAction);
            }
        }

        private static void Acquire(
            InputAction action,
            ref InputActionLease lease,
            System.Action<InputAction.CallbackContext> callback)
        {
            if (action == null || lease != null) return;
            action.performed += callback;
            lease = InputActionLease.Acquire(action);
        }

        private void ReleaseInput()
        {
            Release(attackAction, ref attackLease, OnAttackPerformed);
            Release(dashAction, ref dashLease, OnDashPerformed);
            Release(healAction, ref healLease, OnHealPerformed);
            if (guardAction != null)
            {
                guardAction.performed -= OnGuardPerformed;
                guardAction.canceled -= OnGuardCanceled;
            }
            if (guardLease != null)
            {
                guardLease.Dispose();
                guardLease = null;
            }
            CancelInputGuard();
        }

        private static void Release(
            InputAction action,
            ref InputActionLease lease,
            System.Action<InputAction.CallbackContext> callback)
        {
            if (action != null) action.performed -= callback;
            lease?.Dispose();
            lease = null;
        }

        private void OnAttackPerformed(InputAction.CallbackContext context) => RequestActivate(0, attackSkill);
        private void OnDashPerformed(InputAction.CallbackContext context) => RequestActivate(1, dashSkill);
        private void OnGuardPerformed(InputAction.CallbackContext context)
        {
            guardStartedByInput = RequestActivate(2, guardSkill);
        }
        private void OnGuardCanceled(InputAction.CallbackContext context) => CancelInputGuard();
        private void OnHealPerformed(InputAction.CallbackContext context) => RequestActivate(3, healSkill);

        private bool RequestActivate(int skillIndex, PlayerSkillDefinition definition)
        {
            if (!IsSpawned || !IsOwner || definition == null || string.IsNullOrWhiteSpace(definition.Id)) return false;
            var reference = directionReference != null ? directionReference : transform;
            var direction = reference.forward;
            if (IsServer) return ActivateOnServer(skillIndex, direction);
            ActivateSkillServerRpc(skillIndex, direction);
            return true;
        }

        [ServerRpc]
        private void ActivateSkillServerRpc(int skillIndex, Vector3 direction)
        {
            ActivateOnServer(skillIndex, direction);
        }

        private bool ActivateOnServer(int skillIndex, Vector3 direction)
        {
            var definition = GetDefinition(skillIndex);
            return IsServer
                && definition != null
                && coordinator != null
                && coordinator.TryActivateSkill(definition, direction);
        }

        private void CancelInputGuard()
        {
            if (!guardStartedByInput) return;
            guardStartedByInput = false;
            if (IsServer) CancelGuardOnServer();
            else if (IsSpawned && IsOwner) CancelGuardServerRpc();
        }

        [ServerRpc]
        private void CancelGuardServerRpc() => CancelGuardOnServer();

        private void CancelGuardOnServer()
        {
            if (!IsServer || guardSkill == null || coordinator?.Skills?.ActiveSkill == null) return;
            if (coordinator.Skills.ActiveSkill.Definition == guardSkill)
                coordinator.Skills.CancelActiveSkill();
        }

        private void OnServerSkillStarted(IPlayerSkillFeature skill)
        {
            activeSkillIndex.Value = GetDefinitionIndex(skill.Definition);
            activationSequence.Value++;
        }

        private void OnServerSkillEnded(IPlayerSkillFeature skill)
        {
            if (activeSkillIndex.Value == GetDefinitionIndex(skill.Definition))
                activeSkillIndex.Value = NoActiveSkill;
        }

        private PlayerSkillDefinition GetDefinition(int index)
        {
            return index switch
            {
                0 => attackSkill,
                1 => dashSkill,
                2 => guardSkill,
                3 => healSkill,
                _ => null
            };
        }

        private int GetDefinitionIndex(PlayerSkillDefinition definition)
        {
            if (definition == attackSkill) return 0;
            if (definition == dashSkill) return 1;
            if (definition == guardSkill) return 2;
            if (definition == healSkill) return 3;
            return NoActiveSkill;
        }
    }
}
