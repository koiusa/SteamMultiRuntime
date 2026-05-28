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

        private InputBindingIconResolver iconResolver;
        private Vector2 scrollPosition;

        [MenuItem("Tools/KeyConfig/Input Binding Icon Window")]
        private static void Open()
        {
            var window = GetWindow<InputBindingIconBindingWindow>("KeyConfig Icon Binding");
            window.minSize = new Vector2(620f, 380f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Input Binding Icon Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            iconResolver = (InputBindingIconResolver)EditorGUILayout.ObjectField(
                "Icon Resolver",
                iconResolver,
                typeof(InputBindingIconResolver),
                false);

            if (iconResolver == null)
            {
                EditorGUILayout.HelpBox("InputBindingIconResolver を設定してください。", MessageType.Info);
                return;
            }

            var inputActionAsset = iconResolver.ResolveInputActionAsset();
            if (inputActionAsset == null)
            {
                EditorGUILayout.HelpBox("Resolver 側の InputActionAssetResolver と InputActionAsset を設定してください。", MessageType.Warning);
                return;
            }

            var rows = BuildRows(inputActionAsset, iconResolver);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("対象バインディングが見つかりません。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            string currentCategory = null;
            string currentMap = null;
            string currentAction = null;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

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

                var currentIcon = FindCustomIcon(iconResolver, row.key);
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
                            category = BuildCategory(deviceType),
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
                    var key = BuildKey(custom.deviceType, custom.controlName);
                    if (string.IsNullOrWhiteSpace(key) || !keySet.Add(key))
                    {
                        continue;
                    }

                    rows.Add(new BindingRow
                    {
                        category = BuildCategory(custom.deviceType),
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

        private static Texture2D FindCustomIcon(InputBindingIconResolver resolver, string key)
        {
            if (resolver == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var bindings = resolver.CustomBindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (!string.Equals(BuildKey(binding.deviceType, binding.controlName), key, StringComparison.Ordinal))
                {
                    continue;
                }

                return binding.icon;
            }

            return null;
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

        private static string BuildCategory(string deviceType)
        {
            if (string.IsNullOrWhiteSpace(deviceType))
            {
                return "Other";
            }

            var lower = deviceType.Trim().ToLowerInvariant();
            if (lower.Contains("keyboard"))
            {
                return "Keyboard";
            }

            if (lower.Contains("mouse"))
            {
                return "Mouse";
            }

            if (lower.Contains("gamepad") || lower.Contains("controller") || lower.Contains("joystick") || lower.Contains("steam"))
            {
                return "Gamepad";
            }

            return "Other";
        }

        private static string BuildKey(string deviceType, string controlName)
        {
            var normalizedDevice = string.IsNullOrWhiteSpace(deviceType) ? string.Empty : deviceType.Trim().ToLowerInvariant();
            var normalizedControl = string.IsNullOrWhiteSpace(controlName) ? string.Empty : controlName.Trim().ToLowerInvariant();
            return normalizedDevice + "/" + normalizedControl;
        }
    }
}
