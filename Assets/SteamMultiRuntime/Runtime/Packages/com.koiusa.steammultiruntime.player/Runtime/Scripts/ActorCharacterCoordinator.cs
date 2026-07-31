using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorCompositeMotor))]
    [RequireComponent(typeof(ActorSkillCoordinator))]
    [RequireComponent(typeof(ActorCombatCoordinator))]
    public sealed class ActorCharacterCoordinator : MonoBehaviour
    {
        public ActorCompositeMotor Motor { get; private set; }
        public ActorSkillCoordinator Skills { get; private set; }
        public ActorCombatCoordinator Combat { get; private set; }

        private void Awake() => RefreshComponents();

        public void RefreshComponents()
        {
            Motor = GetComponent<ActorCompositeMotor>();
            Skills = GetComponent<ActorSkillCoordinator>();
            Combat = GetComponent<ActorCombatCoordinator>();
            Skills?.RefreshSkills();
            Combat?.RefreshFeatures();
        }

        public bool TryActivateSkill(string skillId, Vector3 direction, GameObject target = null)
        {
            return Combat?.Health?.IsAlive == true
                && Skills != null
                && Skills.TryActivate(skillId, direction, target);
        }

        public bool TryActivateSkill(ActorSkillDefinition definition, Vector3 direction, GameObject target = null)
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
