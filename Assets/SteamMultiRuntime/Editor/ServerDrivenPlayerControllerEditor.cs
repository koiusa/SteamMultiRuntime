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
        }
    }
}
