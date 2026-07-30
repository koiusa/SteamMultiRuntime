using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(PlayerCharacterCoordinator))]
    public sealed class PlayerCharacterCoordinatorEditor : UnityEditor.Editor
    {
        private bool movementExpanded = true;
        private bool skillsExpanded = true;
        private bool combatExpanded = true;

        public override void OnInspectorGUI()
        {
            var coordinator = (PlayerCharacterCoordinator)target;
            var owner = coordinator.gameObject;

            EditorGUILayout.HelpBox(
                "PlayerのMovement、Skill、Combatを論理階層で管理します。Featureは同じGameObjectへ個別に追加・削除できます。",
                MessageType.Info);

            DrawMovement(owner);
            DrawSkills(owner);
            DrawCombat(owner);

            if (Application.isPlaying && GUILayout.Button("Refresh Components")) coordinator.RefreshComponents();
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private void DrawMovement(GameObject owner)
        {
            movementExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(movementExpanded, "Player Composite Motor");
            if (movementExpanded)
            {
                EditorGUI.indentLevel++;
                DrawReadOnly<ActorCompositeMotor>(owner, "Composite Motor");
                DrawReadOnly<ActorMotor>(owner, "Player Motor");
                DrawReadOnly<ActorTraversalCoordinator>(owner, "Traversal Coordinator");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSkills(GameObject owner)
        {
            skillsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(skillsExpanded, "Player Skill Coordinator");
            if (skillsExpanded)
            {
                EditorGUI.indentLevel++;
                DrawReadOnly<PlayerSkillCoordinator>(owner, "Coordinator");
                DrawOptional<PlayerSkillPresentation>(owner, "Skill Presentation");
                DrawSkillInput(owner);
                DrawOptional<DashSkillFeature>(owner, "Dash Skill");
                DrawOptional<SwordAttackSkillFeature>(owner, "Sword Attack Skill");
                DrawOptional<GuardSkillFeature>(owner, "Guard Skill");
                DrawOptional<HealSkillFeature>(owner, "Heal Skill");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawCombat(GameObject owner)
        {
            combatExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(combatExpanded, "Player Combat Coordinator");
            if (combatExpanded)
            {
                EditorGUI.indentLevel++;
                DrawReadOnly<PlayerCombatCoordinator>(owner, "Coordinator");
                DrawOptional<PlayerHealthFeature>(owner, "Health Feature");
                DrawOptional<PlayerDamageReceiverFeature>(owner, "Damage Receiver Feature");
                DrawOptional<PlayerHitDetectionFeature>(owner, "Hit Detection Feature");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawReadOnly<T>(GameObject owner, string label) where T : Component
        {
            EditorGUILayout.ObjectField(label, owner.GetComponent<T>(), typeof(T), true);
        }

        private static void DrawOptional<T>(GameObject owner, string label) where T : Component
        {
            var component = owner.GetComponent<T>();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(label, component, typeof(T), true);
                if (component == null && GUILayout.Button("Add", GUILayout.Width(48f)))
                {
                    Undo.AddComponent<T>(owner);
                    MarkDirty(owner);
                }
            }
        }

        private static void DrawSkillInput(GameObject owner)
        {
            var localInput = owner.GetComponent<PlayerSkillInputController>();
            var usesLocalInput = owner.GetComponent<LocalPlayerController>() != null;
            if (!usesLocalInput)
            {
                EditorGUILayout.LabelField("Skill Input Controller", "Provided by an optional integration package");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "Skill Input Controller",
                    localInput != null ? localInput.GetType().Name : "Not added");

                var buttonLabel = localInput == null ? "Add" : "Configure";
                if (!GUILayout.Button(buttonLabel, GUILayout.Width(72f))) return;
                if (localInput == null) Undo.AddComponent<PlayerSkillInputController>(owner);
                PlayerSkillInputEditorUtility.ConfigureSkillInput(owner);
                MarkDirty(owner);
            }
        }

        private static void MarkDirty(GameObject owner)
        {
            EditorUtility.SetDirty(owner);
            if (!Application.isPlaying && owner.scene.IsValid()) EditorSceneManager.MarkSceneDirty(owner.scene);
        }
    }
}
