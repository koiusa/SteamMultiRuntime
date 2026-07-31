using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(ActorSkillCoordinator))]
    public sealed class ActorSkillCoordinatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var coordinator = (ActorSkillCoordinator)target;
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Attached Skill Features", EditorStyles.boldLabel);
            coordinator.RefreshSkills();
            foreach (var skill in coordinator.Skills)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var component = skill as Component;
                    EditorGUILayout.ObjectField(skill.SkillId, component, component != null ? component.GetType() : typeof(MonoBehaviour), true);
                    using (new EditorGUI.DisabledScope(!Application.isPlaying || !skill.CanActivate(
                        new ActorSkillContext(coordinator.gameObject, coordinator.transform.forward))))
                    {
                        if (GUILayout.Button("Activate", GUILayout.Width(64f)))
                            coordinator.TryActivate(skill.SkillId, coordinator.transform.forward);
                    }
                }
            }

            if (Application.isPlaying && coordinator.ActiveSkill != null)
            {
                EditorGUILayout.LabelField("Active", coordinator.ActiveSkill.SkillId);
                if (GUILayout.Button("Cancel Active Skill")) coordinator.CancelActiveSkill();
                Repaint();
            }
        }
    }
}
