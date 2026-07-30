using Koiusa.Input;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterCoordinator))]
    public sealed class NetworkPlayerSkillController : NetworkBehaviour
    {
        private const int NoActiveSkill = -1;
        private const float MinimumDirectionSqrMagnitude = 0.0001f;

        [SerializeField] private InputActionsConfig inputActionsConfig;
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
        private PlayerSkillInputBindings inputBindings;
        private bool guardStartedByInput;
        private GuardShieldVisual guardShieldVisual;

        public int ActiveSkillIndex => activeSkillIndex.Value;
        public uint ActivationSequence => activationSequence.Value;

        private void Awake()
        {
            coordinator = GetComponent<PlayerCharacterCoordinator>();
            guardShieldVisual = GetComponent<GuardShieldVisual>();
            if (guardShieldVisual == null) guardShieldVisual = gameObject.AddComponent<GuardShieldVisual>();
            CreateInputBindings();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            activeSkillIndex.OnValueChanged += OnActiveSkillIndexChanged;
            guardShieldVisual?.SetGuarding(activeSkillIndex.Value == 2);
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
            guardShieldVisual?.SetGuarding(false);
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
            guardShieldVisual?.SetGuarding(newValue == 2);
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
                () => RequestActivate(0, attackSkill),
                () => RequestActivate(1, dashSkill),
                () => guardStartedByInput = RequestActivate(2, guardSkill),
                CancelInputGuard,
                () => RequestActivate(3, healSkill));
        }

        private void ReleaseInput()
        {
            inputBindings?.Dispose();
            CancelInputGuard();
        }

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
