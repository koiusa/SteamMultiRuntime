using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(ServerDrivenPlayerController))]
    public class ServerDrivenPlayerControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "このコントローラーは PlayerCompositeMotor で駆動しています。",
                MessageType.Info);

            EditorGUILayout.Space();

            var controller = (ServerDrivenPlayerController)target;
            var compositeMotor = controller.GetComponent<PlayerCompositeMotor>();
            EditorGUILayout.ObjectField("PlayerCompositeMotor", compositeMotor, typeof(PlayerCompositeMotor), allowSceneObjects: true);

            EditorGUILayout.Space();

            DrawDefaultInspector();

            var controlMode = serializedObject.FindProperty("controlMode");
            if (controlMode != null)
            {
                EditorGUILayout.Space();
                var mode = (NetworkControlMode)controlMode.enumValueIndex;
                var message = mode == NetworkControlMode.ServerNpc
                    ? "Server NPC: 疑似入力はサーバー内だけで使用し、補助状態は5Hzで同期します。"
                    : "Player: 入力状態を配信し、補助状態は20Hzで同期します。";
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
        }
    }
}
