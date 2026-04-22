using System;
using System.Collections.Generic;
using System.IO;
using Koiusa.SteamMultiRuntime;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    [CustomEditor(typeof(SteamLobbySceneLoader))]
    public class SteamLobbySceneLoaderEditor : UnityEditor.Editor
    {
        private SerializedProperty lobbyServiceProperty;
        private SerializedProperty sceneCatalogProperty;
        private SerializedProperty defaultSceneNameProperty;
        private SerializedProperty stageSceneListProperty;
        private SerializedProperty defaultScenePolicyProperty;
        private SerializedProperty loadOnStartProperty;
        private SerializedProperty unloadOnLobbyEnteredProperty;
        private SerializedProperty loadOnLobbyLeftProperty;
        private SerializedProperty lobbyScenePolicyProperty;
        private SerializedProperty loadOnEnteredProperty;
        private SerializedProperty unloadOnLeftProperty;
        private SerializedProperty disableCamerasProperty;

        private void OnEnable()
        {
            lobbyServiceProperty = serializedObject.FindProperty("lobbyService");
            sceneCatalogProperty = serializedObject.FindProperty("sceneCatalog");
            defaultSceneNameProperty = sceneCatalogProperty != null ? sceneCatalogProperty.FindPropertyRelative("defaultSceneName") : null;
            stageSceneListProperty = sceneCatalogProperty != null ? sceneCatalogProperty.FindPropertyRelative("stageSceneList") : null;

            defaultScenePolicyProperty = serializedObject.FindProperty("defaultScenePolicy");
            loadOnStartProperty = defaultScenePolicyProperty != null ? defaultScenePolicyProperty.FindPropertyRelative("loadOnStart") : null;
            unloadOnLobbyEnteredProperty = defaultScenePolicyProperty != null ? defaultScenePolicyProperty.FindPropertyRelative("unloadOnLobbyEntered") : null;
            loadOnLobbyLeftProperty = defaultScenePolicyProperty != null ? defaultScenePolicyProperty.FindPropertyRelative("loadOnLobbyLeft") : null;

            lobbyScenePolicyProperty = serializedObject.FindProperty("lobbyScenePolicy");
            loadOnEnteredProperty = lobbyScenePolicyProperty != null ? lobbyScenePolicyProperty.FindPropertyRelative("loadOnEntered") : null;
            unloadOnLeftProperty = lobbyScenePolicyProperty != null ? lobbyScenePolicyProperty.FindPropertyRelative("unloadOnLeft") : null;

            disableCamerasProperty = serializedObject.FindProperty("disableCamerasInLoadedScenes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((SteamLobbySceneLoader)target), typeof(MonoScript), false);
            }

            EditorGUILayout.Space();
            DrawReferencesSection();
            EditorGUILayout.Space();
            DrawSceneCatalogSection();
            EditorGUILayout.Space();
            DrawPolicySection();
            EditorGUILayout.Space();
            DrawRuntimeSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReferencesSection()
        {
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            if (lobbyServiceProperty != null)
            {
                EditorGUILayout.PropertyField(lobbyServiceProperty);
            }
        }

        private void DrawSceneCatalogSection()
        {
            EditorGUILayout.LabelField("Scene Catalog", EditorStyles.boldLabel);

            DrawDefaultSceneSelector();

            if (stageSceneListProperty != null)
            {
                EditorGUILayout.PropertyField(stageSceneListProperty, new GUIContent("Stage Scene List"));
                DrawStageSceneListPreview();
            }
        }

        private void DrawDefaultSceneSelector()
        {
            if (defaultSceneNameProperty == null)
            {
                return;
            }

            var options = GetBuildSceneNameOptions();
            var current = defaultSceneNameProperty.stringValue ?? string.Empty;
            var selectedIndex = Mathf.Max(0, options.IndexOf(current));

            EditorGUILayout.LabelField("Default Scene", EditorStyles.miniBoldLabel);

            if (options.Count > 1)
            {
                var nextIndex = EditorGUILayout.Popup("From Build Settings", selectedIndex, options.ToArray());
                if (nextIndex != selectedIndex)
                {
                    defaultSceneNameProperty.stringValue = nextIndex == 0 ? string.Empty : options[nextIndex];
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Build Settings にシーンがありません。", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(defaultSceneNameProperty, new GUIContent("Scene Name"));

            var sceneName = defaultSceneNameProperty.stringValue;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            var status = GetBuildSceneStatus(sceneName);
            if (!status.exists)
            {
                EditorGUILayout.HelpBox("指定した Default Scene は Build Settings に見つかりません。", MessageType.Warning);
            }
            else if (!status.enabled)
            {
                EditorGUILayout.HelpBox("指定した Default Scene は Build Settings にありますが無効です。", MessageType.Warning);
            }
        }

        private void DrawStageSceneListPreview()
        {
            var listObject = stageSceneListProperty != null ? stageSceneListProperty.objectReferenceValue : null;
            if (listObject == null)
            {
                EditorGUILayout.HelpBox("StageSceneList を設定してください。", MessageType.Info);
                return;
            }

            var loader = target as SteamLobbySceneLoader;
            var names = loader != null ? loader.CreatableStageSceneNames : null;
            if (names == null || names.Count == 0)
            {
                EditorGUILayout.HelpBox("StageSceneList の sceneNames が空です。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField($"Stage Scenes ({names.Count})", EditorStyles.miniBoldLabel);
            for (var i = 0; i < names.Count; i++)
            {
                var sceneName = names[i] ?? string.Empty;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"[{i}] {sceneName}", GUILayout.MaxWidth(260f));
                    var path = ResolveScenePath(sceneName);
                    if (string.IsNullOrEmpty(path))
                    {
                        EditorGUILayout.LabelField("(path not found)", EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                        if (sceneAsset != null && GUILayout.Button("Ping", GUILayout.Width(50f)))
                        {
                            EditorGUIUtility.PingObject(sceneAsset);
                        }
                    }
                }
            }
        }

        private void DrawPolicySection()
        {
            EditorGUILayout.LabelField("Policies", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Default Scene", EditorStyles.miniBoldLabel);
            if (loadOnStartProperty != null)
            {
                EditorGUILayout.PropertyField(loadOnStartProperty, new GUIContent("Load On Start"));
            }
            if (unloadOnLobbyEnteredProperty != null)
            {
                EditorGUILayout.PropertyField(unloadOnLobbyEnteredProperty, new GUIContent("Unload On Lobby Entered"));
            }
            if (loadOnLobbyLeftProperty != null)
            {
                EditorGUILayout.PropertyField(loadOnLobbyLeftProperty, new GUIContent("Load On Lobby Left"));
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Lobby Scene", EditorStyles.miniBoldLabel);
            if (loadOnEnteredProperty != null)
            {
                EditorGUILayout.PropertyField(loadOnEnteredProperty, new GUIContent("Load On Entered"));
            }
            if (unloadOnLeftProperty != null)
            {
                EditorGUILayout.PropertyField(unloadOnLeftProperty, new GUIContent("Unload On Left"));
            }

            if (disableCamerasProperty != null)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.PropertyField(disableCamerasProperty, new GUIContent("Disable Cameras In Loaded Scenes"));
            }
        }

        private void DrawRuntimeSection()
        {
            var loader = target as SteamLobbySceneLoader;
            if (loader == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Lobby Scene Name", string.IsNullOrWhiteSpace(loader.LobbySceneName) ? "(none)" : loader.LobbySceneName);
            EditorGUILayout.LabelField("Direct Transition", loader.IsDirectLobbyTransitionInProgress ? "In Progress" : "Idle");
        }

        private static List<string> GetBuildSceneNameOptions()
        {
            var options = new List<string> { "(None)" };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                var path = scenes[i].path;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name) || seen.Contains(name))
                {
                    continue;
                }

                seen.Add(name);
                options.Add(name);
            }

            return options;
        }

        private static string ResolveScenePath(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return string.Empty;
            }

            var guids = AssetDatabase.FindAssets($"{sceneName} t:Scene");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), sceneName, StringComparison.Ordinal))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static (bool exists, bool enabled) GetBuildSceneStatus(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return (false, false);
            }

            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                var path = scenes[i].path;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(name, sceneName, StringComparison.Ordinal))
                {
                    continue;
                }

                return (true, scenes[i].enabled);
            }

            return (false, false);
        }
    }
}
