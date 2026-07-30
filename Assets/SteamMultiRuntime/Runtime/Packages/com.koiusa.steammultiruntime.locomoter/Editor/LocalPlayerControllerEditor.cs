using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(LocalPlayerController))]
    public class LocalPlayerControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "このコントローラーは ActorCompositeMotor で駆動しています。",
                MessageType.Info);

            EditorGUILayout.Space();

            var controller = (LocalPlayerController)target;
            var compositeMotor = controller.GetComponent<ActorCompositeMotor>();
            EditorGUILayout.ObjectField("ActorCompositeMotor", compositeMotor, typeof(ActorCompositeMotor), allowSceneObjects: true);

            EditorGUILayout.Space();

            DrawDefaultInspector();
        }
    }
}
