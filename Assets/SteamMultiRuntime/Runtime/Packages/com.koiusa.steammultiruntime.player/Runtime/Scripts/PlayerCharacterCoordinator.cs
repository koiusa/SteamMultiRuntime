using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCompositeMotor))]
    [RequireComponent(typeof(PlayerSkillCoordinator))]
    [RequireComponent(typeof(PlayerCombatCoordinator))]
    public sealed class PlayerCharacterCoordinator : MonoBehaviour
    {
        public PlayerCompositeMotor Motor { get; private set; }
        public PlayerSkillCoordinator Skills { get; private set; }
        public PlayerCombatCoordinator Combat { get; private set; }

        private void Awake() => RefreshComponents();

        public void RefreshComponents()
        {
            Motor = GetComponent<PlayerCompositeMotor>();
            Skills = GetComponent<PlayerSkillCoordinator>();
            Combat = GetComponent<PlayerCombatCoordinator>();
            Skills?.RefreshSkills();
            Combat?.RefreshFeatures();
        }

        public bool TryActivateSkill(string skillId, Vector3 direction, GameObject target = null)
        {
            return Combat?.Health?.IsAlive == true
                && Skills != null
                && Skills.TryActivate(skillId, direction, target);
        }

        public bool TryActivateSkill(PlayerSkillDefinition definition, Vector3 direction, GameObject target = null)
        {
            return definition != null && TryActivateSkill(definition.Id, direction, target);
        }

        public void ResetState()
        {
            Skills?.CancelActiveSkill();
            Motor?.ResetState();
        }
    }
}
