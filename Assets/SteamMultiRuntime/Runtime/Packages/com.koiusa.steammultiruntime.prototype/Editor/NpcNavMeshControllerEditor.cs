using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(NpcNavMeshController))]
    public sealed class NpcNavMeshControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "NPC機能は同じGameObject上のコンポーネントで構成されます。" +
                "必要な機能だけを追加し、不要な機能はコンポーネントを削除してください。",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.HelpBox(
                "Use Crowd SimulationとEnable Npc Rigidbody Collisionsの変更は" +
                "次回のPlay開始時に反映されます。" +
                "Play中のBackend切り替えには対応していません。",
                MessageType.None);

            var gameObject = ((NpcNavMeshController)target).gameObject;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Attached NPC Features", EditorStyles.boldLabel);
            DrawComponentRow<NpcNavMeshMovementModule>(gameObject, "Movement");
            DrawComponentRow<NpcNavMeshSpeedModule>(gameObject, "Speed");
            DrawComponentRow<NpcNavMeshSteeringModule>(gameObject, "Steering");
            DrawComponentRow<NpcNavMeshAvoidanceModule>(gameObject, "Avoidance");
            DrawComponentRow<NpcNavMeshJumpModule>(gameObject, "Jump");
        }

        private static void DrawComponentRow<T>(GameObject gameObject, string label) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(label, component, typeof(T), true);
                if (component == null && GUILayout.Button("Add", GUILayout.Width(50f)))
                {
                    component = Undo.AddComponent<T>(gameObject);
                    EditorGUIUtility.PingObject(component);
                    MarkDirty(gameObject);
                }
                else if (component != null && GUILayout.Button("Select", GUILayout.Width(50f)))
                {
                    Selection.activeObject = component;
                    EditorGUIUtility.PingObject(component);
                }
            }
        }

        private static void MarkDirty(GameObject gameObject)
        {
            EditorUtility.SetDirty(gameObject);
            if (!Application.isPlaying && gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
}
