using Koiusa.Input;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterCoordinator))]
    public sealed class NetworkPlayerSkillController : NetworkBehaviour
    {
        private const float MinimumDirectionSqrMagnitude = 0.0001f;

        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private PlayerSkillDefinition attackSkill;
        [SerializeField] private PlayerSkillDefinition dashSkill;
        [SerializeField] private PlayerSkillDefinition guardSkill;
        [SerializeField] private PlayerSkillDefinition healSkill;
        [SerializeField] private Transform directionReference;

        private readonly NetworkVariable<int> activeSkillIndex = new NetworkVariable<int>(
            (int)PlayerSkillSlot.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> lastActivatedSkillIndex = new NetworkVariable<int>(
            (int)PlayerSkillSlot.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> activationSequence = new NetworkVariable<uint>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private PlayerCharacterCoordinator coordinator;
        private PlayerSkillInputBindings inputBindings;
        private bool guardStartedByInput;
        private IPlayerSkillPresentation presentation;

        public int ActiveSkillIndex => activeSkillIndex.Value;
        public uint ActivationSequence => activationSequence.Value;

        private void Awake()
        {
            coordinator = GetComponent<PlayerCharacterCoordinator>();
            presentation = GetComponent<IPlayerSkillPresentation>();
            CreateInputBindings();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            activeSkillIndex.OnValueChanged += OnActiveSkillIndexChanged;
            activationSequence.OnValueChanged += OnActivationSequenceChanged;
            if (!IsServer) presentation?.SetActiveSkill((PlayerSkillSlot)activeSkillIndex.Value);
            if (IsServer && coordinator?.Skills != null)
            {
                coordinator.Skills.SkillStarted += OnServerSkillStarted;
                coordinator.Skills.SkillEnded += OnServerSkillEnded;
            }
            if (IsOwner) inputBindings.Acquire();
        }

        public override void OnNetworkDespawn()
        {
            activeSkillIndex.OnValueChanged -= OnActiveSkillIndexChanged;
            activationSequence.OnValueChanged -= OnActivationSequenceChanged;
            if (!IsServer) presentation?.SetActiveSkill(PlayerSkillSlot.None);
            ReleaseInput();
            if (coordinator?.Skills != null)
            {
                coordinator.Skills.SkillStarted -= OnServerSkillStarted;
                coordinator.Skills.SkillEnded -= OnServerSkillEnded;
            }
            base.OnNetworkDespawn();
        }

        private void OnActiveSkillIndexChanged(int previousValue, int newValue)
        {
            if (!IsServer) presentation?.SetActiveSkill((PlayerSkillSlot)newValue);
        }

        private void OnActivationSequenceChanged(uint previousValue, uint newValue)
        {
            if (!IsServer && newValue != 0)
                presentation?.PlaySkillActivation((PlayerSkillSlot)lastActivatedSkillIndex.Value, newValue);
        }

        private void OnEnable()
        {
            if (IsSpawned && IsOwner) inputBindings?.Acquire();
        }

        private void OnDisable() => ReleaseInput();

        private void CreateInputBindings()
        {
            inputBindings = new PlayerSkillInputBindings(
                inputActionsConfig,
                () => RequestActivate(PlayerSkillSlot.Attack, attackSkill),
                () => RequestActivate(PlayerSkillSlot.Dash, dashSkill),
                () => guardStartedByInput = RequestActivate(PlayerSkillSlot.Guard, guardSkill),
                CancelInputGuard,
                () => RequestActivate(PlayerSkillSlot.Heal, healSkill));
        }

        private void ReleaseInput()
        {
            inputBindings?.Dispose();
            CancelInputGuard();
        }

        private bool RequestActivate(PlayerSkillSlot skillSlot, PlayerSkillDefinition definition)
        {
            if (!IsSpawned || !IsOwner || definition == null || string.IsNullOrWhiteSpace(definition.Id)) return false;
            var reference = directionReference != null ? directionReference : transform;
            var direction = reference.forward;
            if (IsServer) return ActivateOnServer(skillSlot, direction);
            ActivateSkillServerRpc((int)skillSlot, direction);
            return true;
        }

        [ServerRpc]
        private void ActivateSkillServerRpc(int skillIndex, Vector3 direction)
        {
            ActivateOnServer((PlayerSkillSlot)skillIndex, direction);
        }

        private bool ActivateOnServer(PlayerSkillSlot skillSlot, Vector3 direction)
        {
            var definition = GetDefinition(skillSlot);
            return IsServer
                && definition != null
                && coordinator != null
                && TryNormalizeDirection(direction, out var normalizedDirection)
                && coordinator.TryActivateSkill(definition, normalizedDirection);
        }

        internal static bool TryNormalizeDirection(Vector3 direction, out Vector3 normalizedDirection)
        {
            normalizedDirection = Vector3.zero;
            if (!IsFinite(direction.x) || !IsFinite(direction.y) || !IsFinite(direction.z))
                return false;

            var sqrMagnitude = direction.sqrMagnitude;
            if (float.IsNaN(sqrMagnitude) || float.IsInfinity(sqrMagnitude))
                return false;

            if (sqrMagnitude <= MinimumDirectionSqrMagnitude)
                return true;

            normalizedDirection = direction / Mathf.Sqrt(sqrMagnitude);
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
            var slot = GetDefinitionSlot(skill.Definition);
            activeSkillIndex.Value = (int)slot;
            lastActivatedSkillIndex.Value = (int)slot;
            activationSequence.Value++;
        }

        private void OnServerSkillEnded(IPlayerSkillFeature skill)
        {
            if (activeSkillIndex.Value == (int)GetDefinitionSlot(skill.Definition))
                activeSkillIndex.Value = (int)PlayerSkillSlot.None;
        }

        private PlayerSkillDefinition GetDefinition(PlayerSkillSlot slot)
        {
            return slot switch
            {
                PlayerSkillSlot.Attack => attackSkill,
                PlayerSkillSlot.Dash => dashSkill,
                PlayerSkillSlot.Guard => guardSkill,
                PlayerSkillSlot.Heal => healSkill,
                _ => null
            };
        }

        private PlayerSkillSlot GetDefinitionSlot(PlayerSkillDefinition definition)
        {
            if (definition == attackSkill) return PlayerSkillSlot.Attack;
            if (definition == dashSkill) return PlayerSkillSlot.Dash;
            if (definition == guardSkill) return PlayerSkillSlot.Guard;
            if (definition == healSkill) return PlayerSkillSlot.Heal;
            return PlayerSkillSlot.None;
        }
    }
}
