using System;
using System.Collections.Generic;
using System.IO;
using Koiusa.SteamMultiRuntime.Network;
using Koiusa.SteamMultiRuntime.Character;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    [CustomEditor(typeof(LocalRuntimeUserProfile))]
    public class LocalRuntimeUserProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty localManagerProperty;
        private SerializedProperty modelIdListProperty;
        private SerializedProperty selectedModelIndexProperty;
        private SerializedProperty applyOnEnableProperty;
        private SerializedProperty applyOnSceneLoadedProperty;

        private void OnEnable()
        {
            localManagerProperty = serializedObject.FindProperty("localManager");
            modelIdListProperty = serializedObject.FindProperty("modelIdList");
            selectedModelIndexProperty = serializedObject.FindProperty("selectedModelIndex");
            applyOnEnableProperty = serializedObject.FindProperty("applyOnEnable");
            applyOnSceneLoadedProperty = serializedObject.FindProperty("applyOnSceneLoaded");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (localManagerProperty != null)
            {
                EditorGUILayout.PropertyField(localManagerProperty);
            }
            if (modelIdListProperty != null)
            {
                EditorGUILayout.PropertyField(modelIdListProperty);
            }

            var currentIndex = selectedModelIndexProperty != null ? selectedModelIndexProperty.intValue : -1;
            var modelIdList = modelIdListProperty != null ? modelIdListProperty.objectReferenceValue as CharacterModelIdList : null;

            if (modelIdList == null)
            {
                EditorGUILayout.HelpBox("ModelIdList を設定してください", MessageType.Warning);
                if (selectedModelIndexProperty != null)
                {
                    selectedModelIndexProperty.intValue = -1;
                }
            }
            else
            {
                var names = new List<string>();
                if (modelIdList.modelIds != null)
                {
                    for (var i = 0; i < modelIdList.modelIds.Length; i++)
                    {
                        var id = modelIdList.modelIds[i];
                        names.Add($"[{i}] {id}");
                    }
                }

                var newIndex = EditorGUILayout.Popup("選択モデル", currentIndex, names.ToArray());
                if (selectedModelIndexProperty != null)
                {
                    selectedModelIndexProperty.intValue = newIndex;
                }
            }

            if (applyOnEnableProperty != null)
            {
                EditorGUILayout.PropertyField(applyOnEnableProperty);
            }
            if (applyOnSceneLoadedProperty != null)
            {
                EditorGUILayout.PropertyField(applyOnSceneLoadedProperty);
            }

            if (currentIndex >= 0 && modelIdList != null && modelIdList.modelIds != null && currentIndex < modelIdList.modelIds.Length)
            {
                var id = modelIdList.modelIds[currentIndex];
                var resolved = modelIdList.ResolveResourcePath(id);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("リソースパス情報", EditorStyles.boldLabel);
                EditorGUILayout.TextField("Model ID", id);
                EditorGUILayout.TextField("Resolved Path", resolved);

                var resourcePath = CharacterModelIdList.ToResourcesRelativePath(resolved);
                EditorGUILayout.TextField("Resources Path", resourcePath);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
