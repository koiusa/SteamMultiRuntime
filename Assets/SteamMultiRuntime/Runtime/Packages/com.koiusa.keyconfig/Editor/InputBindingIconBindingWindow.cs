using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Koiusa.Keyconfig.Runtime;

namespace Koiusa.Keyconfig.Editor
{
    public sealed class InputBindingIconBindingWindow : EditorWindow
    {
        private sealed class BindingRow
        {
            public string category;
            public string mapName;
            public string actionName;
            public string bindingPath;
            public string deviceType;
            public string controlName;
            public string key;
        }

        private const string LastResolverPathEditorPrefsKey = "Koiusa.Keyconfig.InputBindingIconBindingWindow.LastResolverPath";

        private InputBindingIconResolver iconResolver;
        private Vector2 scrollPosition;
        private int selectedMapTabIndex;

        [MenuItem("Tools/KeyConfig/Input Binding Icon Window")]
        private static void Open()
        {
            var window = GetWindow<InputBindingIconBindingWindow>("KeyConfig Icon Binding");
            window.minSize = new Vector2(620f, 380f);
        }

        private void OnEnable()
        {
            var lastResolverPath = EditorPrefs.GetString(LastResolverPathEditorPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(lastResolverPath))
            {
                return;
            }

            iconResolver = AssetDatabase.LoadAssetAtPath<InputBindingIconResolver>(lastResolverPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Input Binding Icon Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            iconResolver = (InputBindingIconResolver)EditorGUILayout.ObjectField(
                "Icon Resolver",
                iconResolver,
                typeof(InputBindingIconResolver),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                SaveLastResolver();
            }

            if (iconResolver == null)
            {
                EditorGUILayout.HelpBox("InputBindingIconResolver を設定してください。", MessageType.Info);
                return;
            }

            var inputActionAsset = iconResolver.ResolveInputActionAsset();
            if (inputActionAsset == null)
            {
                EditorGUILayout.HelpBox("KeyConfigInputActionsConfigとInputActionAssetを設定してください。", MessageType.Warning);
                return;
            }

            var rows = BuildRows(inputActionAsset, iconResolver);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("対象バインディングが見つかりません。", MessageType.Info);
                return;
            }

            var mapTabs = InputBindingIconEditorUi.BuildMapTabs(rows, row => row.mapName);
            selectedMapTabIndex = Mathf.Clamp(selectedMapTabIndex, 0, mapTabs.Length - 1);
            selectedMapTabIndex = GUILayout.Toolbar(selectedMapTabIndex, mapTabs);
            var selectedMapName = selectedMapTabIndex == 0 ? null : mapTabs[selectedMapTabIndex];

            EditorGUILayout.Space();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            string currentCategory = null;
            string currentMap = null;
            string currentAction = null;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!string.IsNullOrEmpty(selectedMapName) && !string.Equals(row.mapName, selectedMapName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(currentCategory, row.category, StringComparison.Ordinal))
                {
                    currentCategory = row.category;
                    currentMap = null;
                    currentAction = null;
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(currentCategory, EditorStyles.boldLabel);
                }

                if (!string.Equals(currentMap, row.mapName, StringComparison.Ordinal))
                {
                    currentMap = row.mapName;
                    currentAction = null;
                    EditorGUILayout.LabelField("  " + currentMap, EditorStyles.boldLabel);
                }

                if (!string.Equals(currentAction, row.actionName, StringComparison.Ordinal))
                {
                    currentAction = row.actionName;
                    EditorGUILayout.LabelField("    " + currentAction, EditorStyles.miniBoldLabel);
                }

                var currentIcon = iconResolver.ResolveDisplayIcon(row.deviceType, row.controlName, row.bindingPath);
                EditorGUI.BeginChangeCheck();
                var newIcon = (Texture2D)EditorGUILayout.ObjectField(
                    $"      <{row.deviceType}>/{row.controlName}",
                    currentIcon,
                    typeof(Texture2D),
                    false);

                if (!EditorGUI.EndChangeCheck())
                {
                    continue;
                }

                Undo.RecordObject(iconResolver, "Update Binding Icon");
                iconResolver.SetCustomBindingIcon(row.deviceType, row.controlName, newIcon);
                EditorUtility.SetDirty(iconResolver);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Save"))
            {
                AssetDatabase.SaveAssets();
            }
        }

        private static List<BindingRow> BuildRows(InputActionAsset asset, InputBindingIconResolver resolver)
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
                        var deviceType = InputBindingIconEditorUi.ExtractDeviceType(path);
                        var controlName = InputBindingIconEditorUi.ExtractControlName(path);
                        if (string.IsNullOrWhiteSpace(deviceType) || string.IsNullOrWhiteSpace(controlName))
                        {
                            continue;
                        }

                        var key = InputBindingIconEditorUi.BuildKey(deviceType, controlName);
                        if (!keySet.Add(key))
                        {
                            continue;
                        }

                        rows.Add(new BindingRow
                        {
                            category = InputBindingIconEditorUi.BuildCategory(deviceType),
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

            if (resolver != null)
            {
                var customBindings = resolver.CustomBindings;
                for (var i = 0; i < customBindings.Count; i++)
                {
                    var custom = customBindings[i];
                    var key = InputBindingIconEditorUi.BuildKey(custom.deviceType, custom.controlName);
                    if (string.IsNullOrWhiteSpace(key) || !keySet.Add(key))
                    {
                        continue;
                    }

                    rows.Add(new BindingRow
                    {
                        category = InputBindingIconEditorUi.BuildCategory(custom.deviceType),
                        mapName = "(Assigned Only)",
                        actionName = "(No InputAction)",
                        bindingPath = string.Empty,
                        deviceType = custom.deviceType,
                        controlName = custom.controlName,
                        key = key
                    });
                }
            }

            rows.Sort((a, b) =>
            {
                var categoryCompare = string.Compare(a.category, b.category, StringComparison.OrdinalIgnoreCase);
                if (categoryCompare != 0)
                {
                    return categoryCompare;
                }

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

                return string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase);
            });

            return rows;
        }

        private void SaveLastResolver()
        {
            if (iconResolver == null)
            {
                EditorPrefs.DeleteKey(LastResolverPathEditorPrefsKey);
                return;
            }

            var path = AssetDatabase.GetAssetPath(iconResolver);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            EditorPrefs.SetString(LastResolverPathEditorPrefsKey, path);
        }

    }
}
