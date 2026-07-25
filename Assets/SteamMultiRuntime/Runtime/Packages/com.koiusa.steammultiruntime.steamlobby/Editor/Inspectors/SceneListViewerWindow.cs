using System.Collections.Generic;
using System.IO;
using Koiusa.SteamMultiRuntime.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    public class SceneListViewerWindow : EditorWindow
    {
        private readonly List<StageSceneListInfo> stageSceneLists = new List<StageSceneListInfo>();
        private readonly Dictionary<string, List<string>> scenePathMap = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, bool> foldoutStateByPath = new Dictionary<string, bool>();
        private Vector2 scroll;

        private struct StageSceneListInfo
        {
            public StageSceneList asset;
            public string path;
            public string[] sceneNames;
        }

        [MenuItem("Tools/SteamMultiRuntime/Read Only/Scene List Viewer")]
        public static void Open()
        {
            var window = GetWindow<SceneListViewerWindow>("Scene List");
            window.minSize = new Vector2(760f, 360f);
            window.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(100f)))
                {
                    Refresh();
                }
            }

            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawStageSceneListSection();
            EditorGUILayout.EndScrollView();
        }

        private void Refresh()
        {
            stageSceneLists.Clear();
            scenePathMap.Clear();

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (var sceneGuid in sceneGuids)
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                var sceneName = Path.GetFileNameWithoutExtension(scenePath);
                if (string.IsNullOrEmpty(sceneName))
                {
                    continue;
                }

                if (!scenePathMap.TryGetValue(sceneName, out var paths))
                {
                    paths = new List<string>();
                    scenePathMap[sceneName] = paths;
                }

                paths.Add(scenePath);
            }

            var stageSceneListGuids = AssetDatabase.FindAssets("t:StageSceneList");
            foreach (var guid in stageSceneListGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<StageSceneList>(path);
                if (asset == null)
                {
                    continue;
                }

                stageSceneLists.Add(new StageSceneListInfo
                {
                    asset = asset,
                    path = path,
                    sceneNames = asset.sceneNames
                });

                if (!foldoutStateByPath.ContainsKey(path))
                {
                    foldoutStateByPath[path] = true;
                }
            }

            stageSceneLists.Sort((a, b) => string.CompareOrdinal(a.asset != null ? a.asset.name : string.Empty, b.asset != null ? b.asset.name : string.Empty));
        }

        private void DrawStageSceneListSection()
        {
            EditorGUILayout.LabelField($"StageSceneList ({stageSceneLists.Count})", EditorStyles.boldLabel);

            if (stageSceneLists.Count == 0)
            {
                EditorGUILayout.HelpBox("StageSceneList アセットが見つかりません。", MessageType.Info);
                return;
            }

            foreach (var info in stageSceneLists)
            {
                EditorGUILayout.BeginVertical("box");

                var key = info.path ?? string.Empty;
                var title = info.asset != null ? info.asset.name : "(Missing)";
                foldoutStateByPath[key] = EditorGUILayout.Foldout(foldoutStateByPath.TryGetValue(key, out var expanded) ? expanded : true, title, true);

                if (foldoutStateByPath[key])
                {
                    EditorGUI.indentLevel++;

                    DrawLabeledPathRow("SO Path", info.path, null);

                    var names = info.sceneNames;
                    if (names == null || names.Length == 0)
                    {
                        EditorGUILayout.HelpBox("sceneNames が空です。", MessageType.None);
                    }
                    else
                    {
                        for (var i = 0; i < names.Length; i++)
                        {
                            var sceneRef = names[i] ?? string.Empty;
                            var resolvedSceneRef = info.asset != null ? info.asset.ResolveSceneReference(sceneRef) : sceneRef;

                            EditorGUILayout.Space(2f);
                            EditorGUILayout.LabelField($"Scene {i}", EditorStyles.miniBoldLabel);
                            DrawLabeledPathRow("Scene Ref", sceneRef, null);

                            var scenePaths = ResolveScenePaths(resolvedSceneRef);
                            if (scenePaths.Count > 0)
                            {
                                for (var j = 0; j < scenePaths.Count; j++)
                                {
                                    DrawLabeledPathRow("Scene Path", scenePaths[j], scenePaths[j]);
                                }
                            }
                            else
                            {
                                EditorGUILayout.HelpBox("対応するシーンパスが見つかりません。", MessageType.Warning);
                            }
                        }
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawLabeledPathRow(string label, string value, string assetPathForActions)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (!string.IsNullOrEmpty(assetPathForActions))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPathForActions);
                    if (obj != null && GUILayout.Button("Ping", GUILayout.Width(50f)))
                    {
                        EditorGUIUtility.PingObject(obj);
                    }

                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPathForActions);
                    if (sceneAsset != null && GUILayout.Button("Open", GUILayout.Width(50f)))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            EditorSceneManager.OpenScene(assetPathForActions);
                        }
                    }
                }
            }
        }

        private List<string> ResolveScenePaths(string sceneReference)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(sceneReference))
            {
                return results;
            }

            var normalized = sceneReference.Replace('\\', '/').Trim();
            if (normalized.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
            {
                results.Add(normalized);
                return results;
            }

            if (scenePathMap.TryGetValue(normalized, out var scenePaths) && scenePaths != null && scenePaths.Count > 0)
            {
                results.AddRange(scenePaths);
                return results;
            }

            var sceneName = Path.GetFileNameWithoutExtension(normalized);
            if (!string.IsNullOrEmpty(sceneName) && scenePathMap.TryGetValue(sceneName, out var byName) && byName != null)
            {
                results.AddRange(byName);
            }

            return results;
        }
    }
}
