using Koiusa.Input;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorCharacterCoordinator))]
    public sealed class NetworkPlayerSkillController : NetworkBehaviour
    {
        private const float MinimumDirectionSqrMagnitude = 0.0001f;

        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private ActorSkillDefinition attackSkill;
        [SerializeField] private ActorSkillDefinition dashSkill;
        [SerializeField] private ActorSkillDefinition guardSkill;
        [SerializeField] private ActorSkillDefinition healSkill;
        [SerializeField] private Transform directionReference;

        private readonly NetworkVariable<int> activeSkillIndex = new NetworkVariable<int>(
            (int)ActorSkillSlot.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> lastActivatedSkillIndex = new NetworkVariable<int>(
            (int)ActorSkillSlot.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> activationSequence = new NetworkVariable<uint>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private ActorCharacterCoordinator coordinator;
        private PlayerSkillInputBindings inputBindings;
        private bool guardStartedByInput;
        private IActorSkillPresentation presentation;

        public int ActiveSkillIndex => activeSkillIndex.Value;
        public uint ActivationSequence => activationSequence.Value;

        private void Awake()
        {
            coordinator = GetComponent<ActorCharacterCoordinator>();
            presentation = GetComponent<IActorSkillPresentation>();
            CreateInputBindings();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            activeSkillIndex.OnValueChanged += OnActiveSkillIndexChanged;
            activationSequence.OnValueChanged += OnActivationSequenceChanged;
            if (!IsServer) presentation?.SetActiveSkill((ActorSkillSlot)activeSkillIndex.Value);
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
            if (!IsServer) presentation?.SetActiveSkill(ActorSkillSlot.None);
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
            if (!IsServer) presentation?.SetActiveSkill((ActorSkillSlot)newValue);
        }

        private void OnActivationSequenceChanged(uint previousValue, uint newValue)
        {
            if (!IsServer && newValue != 0)
                presentation?.PlaySkillActivation((ActorSkillSlot)lastActivatedSkillIndex.Value, newValue);
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
                () => RequestActivate(ActorSkillSlot.Attack, attackSkill),
                () => RequestActivate(ActorSkillSlot.Dash, dashSkill),
                () => guardStartedByInput = RequestActivate(ActorSkillSlot.Guard, guardSkill),
                CancelInputGuard,
                () => RequestActivate(ActorSkillSlot.Heal, healSkill));
        }

        private void ReleaseInput()
        {
            inputBindings?.Dispose();
            CancelInputGuard();
        }

        private bool RequestActivate(ActorSkillSlot skillSlot, ActorSkillDefinition definition)
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
            ActivateOnServer((ActorSkillSlot)skillIndex, direction);
        }

        private bool ActivateOnServer(ActorSkillSlot skillSlot, Vector3 direction)
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

        private void OnServerSkillStarted(IActorSkillFeature skill)
        {
            var slot = GetDefinitionSlot(skill.Definition);
            activeSkillIndex.Value = (int)slot;
            lastActivatedSkillIndex.Value = (int)slot;
            activationSequence.Value++;
        }

        private void OnServerSkillEnded(IActorSkillFeature skill)
        {
            if (activeSkillIndex.Value == (int)GetDefinitionSlot(skill.Definition))
                activeSkillIndex.Value = (int)ActorSkillSlot.None;
        }

        private ActorSkillDefinition GetDefinition(ActorSkillSlot slot)
        {
            return slot switch
            {
                ActorSkillSlot.Attack => attackSkill,
                ActorSkillSlot.Dash => dashSkill,
                ActorSkillSlot.Guard => guardSkill,
                ActorSkillSlot.Heal => healSkill,
                _ => null
            };
        }

        private ActorSkillSlot GetDefinitionSlot(ActorSkillDefinition definition)
        {
            if (definition == attackSkill) return ActorSkillSlot.Attack;
            if (definition == dashSkill) return ActorSkillSlot.Dash;
            if (definition == guardSkill) return ActorSkillSlot.Guard;
            if (definition == healSkill) return ActorSkillSlot.Heal;
            return ActorSkillSlot.None;
        }
    }
}
