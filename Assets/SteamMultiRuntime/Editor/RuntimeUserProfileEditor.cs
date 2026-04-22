using System;
using System.Collections.Generic;
using System.IO;
using Koiusa.SteamMultiRuntime.Network;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    [CustomEditor(typeof(RuntimeUserProfile))]
    public class RuntimeUserProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty networkManagerProperty;
        private SerializedProperty modelIdListProperty;
        private SerializedProperty selectedModelIndexProperty;
        private SerializedProperty applyOnEnableProperty;
        private SerializedProperty applyOnSceneLoadedProperty;

        private void OnEnable()
        {
            networkManagerProperty = serializedObject.FindProperty("networkManager");
            modelIdListProperty = serializedObject.FindProperty("modelIdList");
            selectedModelIndexProperty = serializedObject.FindProperty("selectedModelIndex");
            applyOnEnableProperty = serializedObject.FindProperty("applyOnEnable");
            applyOnSceneLoadedProperty = serializedObject.FindProperty("applyOnSceneLoaded");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((RuntimeUserProfile)target), typeof(MonoScript), false);
            }

            EditorGUILayout.Space();
            DrawReferenceSection();
            EditorGUILayout.Space();
            DrawModelSourceSection();
            EditorGUILayout.Space();
            DrawApplyPolicySection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReferenceSection()
        {
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            if (networkManagerProperty != null)
            {
                EditorGUILayout.PropertyField(networkManagerProperty);
            }
        }

        private void DrawModelSourceSection()
        {
            EditorGUILayout.LabelField("Character Prefab Loader Source", EditorStyles.boldLabel);
            if (modelIdListProperty != null)
            {
                EditorGUILayout.PropertyField(modelIdListProperty, new GUIContent("Model ID List"));
            }

            var modelIdList = modelIdListProperty != null ? modelIdListProperty.objectReferenceValue as CharacterModelIdList : null;
            if (modelIdList == null)
            {
                EditorGUILayout.HelpBox("CharacterModelIdList を設定してください。", MessageType.Info);
                if (selectedModelIndexProperty != null)
                {
                    EditorGUILayout.PropertyField(selectedModelIndexProperty, new GUIContent("Selected Model Index"));
                }
                return;
            }

            DrawModelSelection(modelIdList);
        }

        private void DrawModelSelection(CharacterModelIdList modelIdList)
        {
            var ids = modelIdList.modelIds;
            if (ids == null || ids.Length == 0)
            {
                EditorGUILayout.HelpBox("modelIds が空です。", MessageType.Warning);
                if (selectedModelIndexProperty != null)
                {
                    EditorGUILayout.PropertyField(selectedModelIndexProperty, new GUIContent("Selected Model Index"));
                }
                return;
            }

            var currentIndex = selectedModelIndexProperty != null ? selectedModelIndexProperty.intValue : 0;
            var clampedIndex = Mathf.Clamp(currentIndex, 0, ids.Length - 1);
            if (selectedModelIndexProperty != null && currentIndex != clampedIndex)
            {
                selectedModelIndexProperty.intValue = clampedIndex;
            }

            if (selectedModelIndexProperty != null)
            {
                EditorGUILayout.PropertyField(selectedModelIndexProperty, new GUIContent("Selected Model Index"));
            }

            EditorGUILayout.LabelField($"Model List ({ids.Length})", EditorStyles.miniBoldLabel);
            for (var i = 0; i < ids.Length; i++)
            {
                var modelId = ids[i] ?? string.Empty;
                var label = i == clampedIndex ? $"[{i}] {modelId} *" : $"[{i}] {modelId}";
                var resourcePath = modelIdList.ResolveResourcePath(modelId);
                var prefabPaths = FindPrefabAssetPaths(resourcePath);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, GUILayout.MaxWidth(260f));

                    if (prefabPaths.Count == 0)
                    {
                        EditorGUILayout.LabelField("(prefab not found)", EditorStyles.miniLabel);
                    }
                    else
                    {
                        var path = prefabPaths[0];
                        EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null && GUILayout.Button("Ping", GUILayout.Width(50f)))
                        {
                            EditorGUIUtility.PingObject(prefab);
                        }
                    }
                }
            }
        }

        private void DrawApplyPolicySection()
        {
            EditorGUILayout.LabelField("Apply Timing", EditorStyles.boldLabel);
            if (applyOnEnableProperty != null)
            {
                EditorGUILayout.PropertyField(applyOnEnableProperty, new GUIContent("Apply On Enable"));
            }
            if (applyOnSceneLoadedProperty != null)
            {
                EditorGUILayout.PropertyField(applyOnSceneLoadedProperty, new GUIContent("Apply On Scene Loaded"));
            }
        }

        private static string ToRuntimeResourcePath(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                return string.Empty;
            }

            return modelId.StartsWith("Character/", StringComparison.Ordinal) ? modelId : "Character/" + modelId;
        }

        private static List<string> FindPrefabAssetPaths(string runtimeResourcePath)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(runtimeResourcePath))
            {
                return results;
            }

            var fileName = Path.GetFileName(runtimeResourcePath);
            if (string.IsNullOrEmpty(fileName))
            {
                return results;
            }

            var guids = AssetDatabase.FindAssets($"{fileName} t:Prefab");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var resourceRelativePath = ToResourceRelativePath(path);
                if (string.Equals(resourceRelativePath, runtimeResourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(path);
                }
            }

            return results;
        }

        private static string ToResourceRelativePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            const string resourcesSegment = "/Resources/";
            var normalized = assetPath.Replace('\\', '/');
            var index = normalized.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return string.Empty;
            }

            var start = index + resourcesSegment.Length;
            var relative = normalized.Substring(start);
            var extension = Path.GetExtension(relative);
            return string.IsNullOrEmpty(extension)
                ? relative
                : relative.Substring(0, relative.Length - extension.Length);
        }
    }
}
