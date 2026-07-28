using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.TargetingSystem.Editor
{
    [CustomEditor(typeof(SteamMultiRuntimeTargetingInputActions))]
    public sealed class SteamMultiRuntimeTargetingInputActionsEditor : UnityEditor.Editor
    {
        private static readonly string[] ActionPathProperties =
        {
            "lookActionPath",
            "soloLockActionPath",
            "multiLockActionPath",
            "clearLockActionPath",
            "bulkLockActionPath",
            "previousTargetActionPath",
            "nextTargetActionPath",
            "focusActionPath"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            var config = (SteamMultiRuntimeTargetingInputActions)target;
            var resolvedActions = new[]
            {
                config.LookAction,
                config.SoloLockAction,
                config.MultiLockAction,
                config.ClearLockAction,
                config.BulkLockAction,
                config.PreviousTargetAction,
                config.NextTargetAction,
                config.FocusAction
            };

            var missingActions = new List<string>();
            for (var i = 0; i < ActionPathProperties.Length; i++)
            {
                var path = serializedObject.FindProperty(ActionPathProperties[i])?.stringValue;
                if (!string.IsNullOrWhiteSpace(path) && resolvedActions[i] == null)
                {
                    missingActions.Add(path);
                }
            }

            if (missingActions.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"共有Input Actions設定に存在しないAction Pathがあります:\n{string.Join("\n", missingActions)}",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "設定済みのAction Pathはすべて解決できています。空欄の操作は無効です。",
                    MessageType.Info);
            }
        }
    }
}
