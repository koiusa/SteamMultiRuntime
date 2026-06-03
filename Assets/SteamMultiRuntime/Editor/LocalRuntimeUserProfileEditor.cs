using System;
using System.Collections.Generic;
using System.IO;
using Koiusa.SteamMultiRuntime.Network;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    [CustomEditor(typeof(LocalRuntimeUserProfile))]
    public class LocalRuntimeUserProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty localManagerProperty;
        private SerializedProperty localPlayerObjectProperty;
        private SerializedProperty modelIdListProperty;
        private SerializedProperty selectedModelIndexProperty;
        private SerializedProperty applyOnEnableProperty;
        private SerializedProperty applyOnSceneLoadedProperty;

        private void OnEnable()
        {
            localManagerProperty = serializedObject.FindProperty("localManager");
            localPlayerObjectProperty = serializedObject.FindProperty("localPlayerObject");
            modelIdListProperty = serializedObject.FindProperty("modelIdList");
            selectedModelIndexProperty = serializedObject.FindProperty("selectedModelIndex");
            applyOnEnableProperty = serializedObject.FindProperty("applyOnEnable");
            applyOnSceneLoadedProperty = serializedObject.FindProperty("applyOnSceneLoaded");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(localManagerProperty);
            EditorGUILayout.PropertyField(localPlayerObjectProperty);
            EditorGUILayout.PropertyField(modelIdListProperty);

            var currentIndex = selectedModelIndexProperty.intValue;
            var modelIdList = modelIdListProperty.objectReferenceValue as CharacterModelIdList;

            if (modelIdList == null)
            {
                EditorGUILayout.HelpBox("ModelIdList を設定してください", MessageType.Warning);
                selectedModelIndexProperty.intValue = -1;
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
                selectedModelIndexProperty.intValue = newIndex;
            }

            EditorGUILayout.PropertyField(applyOnEnableProperty);
            EditorGUILayout.PropertyField(applyOnSceneLoadedProperty);

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
