using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.TargetingSystem.Editor
{
    public static class TargetingIntegrationValidator
    {
        [MenuItem("Tools/SteamMultiRuntime/Read Only/Targeting/Validate Production Input")]
        public static void ValidateProductionInput()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(SteamMultiRuntimeTargetingInputActions)}");
            if (guids.Length == 0)
            {
                Debug.LogError("SteamMultiRuntimeTargetingInputActions asset was not found.");
                return;
            }

            var invalidAssets = new List<string>();
            SteamMultiRuntimeTargetingInputActions firstConfig = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<SteamMultiRuntimeTargetingInputActions>(path);
                if (config == null)
                {
                    invalidAssets.Add($"{path}: asset could not be loaded");
                    continue;
                }

                firstConfig ??= config;
                var serializedConfig = new SerializedObject(config);
                ValidateAction(serializedConfig, "lookActionPath", config.LookAction, path, invalidAssets);
                ValidateAction(serializedConfig, "soloLockActionPath", config.SoloLockAction, path, invalidAssets);
                ValidateAction(serializedConfig, "multiLockActionPath", config.MultiLockAction, path, invalidAssets);
                ValidateAction(serializedConfig, "clearLockActionPath", config.ClearLockAction, path, invalidAssets);
                ValidateAction(serializedConfig, "bulkLockActionPath", config.BulkLockAction, path, invalidAssets);
                ValidateAction(serializedConfig, "previousTargetActionPath", config.PreviousTargetAction, path, invalidAssets);
                ValidateAction(serializedConfig, "nextTargetActionPath", config.NextTargetAction, path, invalidAssets);
                ValidateAction(serializedConfig, "focusActionPath", config.FocusAction, path, invalidAssets);
            }

            if (invalidAssets.Count > 0)
            {
                Debug.LogError($"Targeting input validation failed:\n{string.Join("\n", invalidAssets)}");
                return;
            }

            Selection.activeObject = firstConfig;
            EditorGUIUtility.PingObject(firstConfig);
            Debug.Log($"Targeting input validation passed for {guids.Length} asset(s). Empty action paths are intentionally disabled.");
        }

        private static void ValidateAction(
            SerializedObject serializedConfig,
            string propertyName,
            object resolvedAction,
            string assetPath,
            ICollection<string> errors)
        {
            var actionPath = serializedConfig.FindProperty(propertyName)?.stringValue;
            if (!string.IsNullOrWhiteSpace(actionPath) && resolvedAction == null)
            {
                errors.Add($"{assetPath}: {actionPath}");
            }
        }
    }
}
