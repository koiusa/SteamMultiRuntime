using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(PlayerInputActionsProfile))]
    public sealed class PlayerInputActionsProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "プレイヤーゲームプレイ入力の唯一の設定箇所です。ControllerやTraversal Featureには個別のInputActionを設定しません。",
                MessageType.Info);

            DrawDefaultInspector();

            var profile = (PlayerInputActionsProfile)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resolved Actions", EditorStyles.boldLabel);
            DrawResolvedAction("Move", profile.MoveInputAction, required: true);
            DrawResolvedAction("Jump", profile.JumpInputAction, required: true);
            DrawResolvedAction("Strafe Toggle", profile.StrafeToggleInputAction, required: false);
            DrawResolvedAction("Grapple", profile.GrappleInputAction, required: false);
            DrawResolvedAction("Reel", profile.ReelInputAction, required: false);

            if (profile.MoveInputAction == null || profile.JumpInputAction == null)
            {
                EditorGUILayout.HelpBox(
                    "必須Actionが解決できません。明示参照を設定するか、同じInputActionAssetに Player/Move と Player/Jump を作成してください。",
                    MessageType.Error);
            }
        }

        private static void DrawResolvedAction(string label, InputAction action, bool required)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(label, action != null ? $"{action.actionMap?.name}/{action.name}" : "Not Found");
            }

            if (required && action == null)
            {
                EditorGUILayout.HelpBox($"{label} Action is required.", MessageType.Error);
            }
        }
    }
}
