using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Koiusa.Keyconfig.Runtime;

namespace Koiusa.Keyconfig.Editor
{
    [CustomEditor(typeof(InputBindingIconResolver))]
    public sealed class InputBindingIconResolverEditor : UnityEditor.Editor
    {
        private sealed class BindingRow
        {
            public string mapName;
            public string actionName;
            public string bindingPath;
            public string deviceType;
            public string controlName;
            public string key;
        }

        private SerializedProperty inputActionAssetResolverProperty;
        private SerializedProperty customBindingsProperty;
        private int selectedMapTabIndex;

        private void OnEnable()
        {
            inputActionAssetResolverProperty = serializedObject.FindProperty("inputActionAssetResolver");
            customBindingsProperty = serializedObject.FindProperty("customBindings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((ScriptableObject)target), typeof(MonoScript), false);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(inputActionAssetResolverProperty);

            var resolver = (InputBindingIconResolver)target;
            var inputActionAsset = resolver.ResolveInputActionAsset();
            if (inputActionAsset == null)
            {
                EditorGUILayout.HelpBox("InputActionAssetResolver またはその参照先 InputActionAsset を設定してください。", MessageType.Info);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(customBindingsProperty, true);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var rows = BuildRows(inputActionAsset);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("対象バインディングが見つかりませんでした。", MessageType.Info);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(customBindingsProperty, true);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resolved Bindings", EditorStyles.boldLabel);

            var mapTabs = InputBindingIconEditorUi.BuildMapTabs(rows, row => row.mapName);
            selectedMapTabIndex = Mathf.Clamp(selectedMapTabIndex, 0, mapTabs.Length - 1);
            selectedMapTabIndex = GUILayout.Toolbar(selectedMapTabIndex, mapTabs);
            var selectedMapName = selectedMapTabIndex == 0 ? null : mapTabs[selectedMapTabIndex];

            string currentMap = null;
            string currentAction = null;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!string.IsNullOrEmpty(selectedMapName) && !string.Equals(row.mapName, selectedMapName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(currentMap, row.mapName, StringComparison.Ordinal))
                {
                    currentMap = row.mapName;
                    currentAction = null;
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(currentMap, EditorStyles.boldLabel);
                }

                if (!string.Equals(currentAction, row.actionName, StringComparison.Ordinal))
                {
                    currentAction = row.actionName;
                    EditorGUILayout.LabelField("  " + currentAction, EditorStyles.miniBoldLabel);
                }

                var currentIcon = resolver.ResolveDisplayIcon(row.deviceType, row.controlName, row.bindingPath);
                EditorGUI.BeginChangeCheck();
                var icon = (Texture2D)EditorGUILayout.ObjectField(
                    $"    <{row.deviceType}>/{row.controlName}",
                    currentIcon,
                    typeof(Texture2D),
                    false);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(resolver, "Update Binding Icon");
                    resolver.SetCustomBindingIcon(row.deviceType, row.controlName, icon);
                    EditorUtility.SetDirty(resolver);
                    serializedObject.Update();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(customBindingsProperty, true);
            serializedObject.ApplyModifiedProperties();
        }

        private static List<BindingRow> BuildRows(InputActionAsset asset)
        {
            var rows = new List<BindingRow>();
            var keySet = new HashSet<string>(StringComparer.Ordinal);

            foreach (var actionMap in asset.actionMaps)
            {
                foreach (var action in actionMap.actions)
                {
                    for (var i = 0; i < action.bindings.Count; i++)
                    {
                        var binding = action.bindings[i];
                        if (binding.isComposite || string.IsNullOrWhiteSpace(binding.effectivePath) && string.IsNullOrWhiteSpace(binding.path))
                        {
                            continue;
                        }

                        var path = string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.path : binding.effectivePath;
                        var deviceType = ExtractDeviceType(path);
                        var controlName = ExtractControlName(path);
                        if (string.IsNullOrWhiteSpace(deviceType) || string.IsNullOrWhiteSpace(controlName))
                        {
                            continue;
                        }

                        var key = BuildKey(deviceType, controlName);
                        if (!keySet.Add(key))
                        {
                            continue;
                        }

                        rows.Add(new BindingRow
                        {
                            mapName = actionMap.name,
                            actionName = action.name,
                            bindingPath = path,
                            deviceType = deviceType,
                            controlName = controlName,
                            key = key
                        });
                    }
                }
            }

            rows.Sort((a, b) =>
            {
                var mapCompare = string.Compare(a.mapName, b.mapName, StringComparison.OrdinalIgnoreCase);
                if (mapCompare != 0)
                {
                    return mapCompare;
                }

                var actionCompare = string.Compare(a.actionName, b.actionName, StringComparison.OrdinalIgnoreCase);
                if (actionCompare != 0)
                {
                    return actionCompare;
                }

                return string.Compare(a.bindingPath, b.bindingPath, StringComparison.OrdinalIgnoreCase);
            });

            return rows;
        }

        private static string ExtractDeviceType(string bindingPath)
        {
            if (string.IsNullOrWhiteSpace(bindingPath))
            {
                return string.Empty;
            }

            var start = bindingPath.IndexOf('<');
            if (start < 0)
            {
                return string.Empty;
            }

            var end = bindingPath.IndexOf('>', start + 1);
            if (end <= start + 1)
            {
                return string.Empty;
            }

            return bindingPath.Substring(start + 1, end - start - 1);
        }

        private static string ExtractControlName(string bindingPath)
        {
            if (string.IsNullOrWhiteSpace(bindingPath))
            {
                return string.Empty;
            }

            var slashIndex = bindingPath.LastIndexOf('/');
            if (slashIndex < 0 || slashIndex >= bindingPath.Length - 1)
            {
                return string.Empty;
            }

            return bindingPath.Substring(slashIndex + 1);
        }

        private static string BuildKey(string deviceType, string controlName)
        {
            var normalizedDevice = string.IsNullOrWhiteSpace(deviceType) ? string.Empty : deviceType.Trim().ToLowerInvariant();
            var normalizedControl = string.IsNullOrWhiteSpace(controlName) ? string.Empty : controlName.Trim().ToLowerInvariant();
            return normalizedDevice + "/" + normalizedControl;
        }
    }
}
