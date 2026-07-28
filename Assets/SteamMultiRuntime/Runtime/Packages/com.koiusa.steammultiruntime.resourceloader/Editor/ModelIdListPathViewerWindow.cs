using System;
using System.Collections.Generic;
using System.IO;
using Koiusa.SteamMultiRuntime.Character;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    public class ModelIdListPathViewerWindow : EditorWindow
    {
        private readonly List<CharacterModelIdList> assets = new List<CharacterModelIdList>();
        private readonly Dictionary<string, bool> foldoutStateByPath = new Dictionary<string, bool>();
        private Vector2 scroll;

        [MenuItem("Tools/SteamMultiRuntime/Read Only/ModelIdList Path Viewer")]
        public static void Open()
        {
            var window = GetWindow<ModelIdListPathViewerWindow>("ModelIdList Paths");
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
            DrawAssetList();
            EditorGUILayout.EndScrollView();
        }

        private void Refresh()
        {
            assets.Clear();

            var guids = AssetDatabase.FindAssets("t:CharacterModelIdList");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<CharacterModelIdList>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                    if (!foldoutStateByPath.ContainsKey(path))
                    {
                        foldoutStateByPath[path] = true;
                    }
                }
            }

            assets.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
        }

        private void DrawAssetList()
        {
            EditorGUILayout.LabelField($"CharacterModelIdList Assets ({assets.Count})", EditorStyles.boldLabel);

            if (assets.Count == 0)
            {
                EditorGUILayout.HelpBox("CharacterModelIdList アセットが見つかりません。", MessageType.Info);
                return;
            }

            foreach (var asset in assets)
            {
                DrawPathRow(asset, asset, AssetDatabase.GetAssetPath(asset));
            }
        }

        private void DrawPathRow(UnityEngine.Object owner, CharacterModelIdList list, string path)
        {
            EditorGUILayout.BeginVertical("box");

            var key = path ?? string.Empty;
            var title = owner != null ? owner.name : "(Missing)";
            foldoutStateByPath[key] = EditorGUILayout.Foldout(foldoutStateByPath.TryGetValue(key, out var expanded) ? expanded : true, title, true);

            if (foldoutStateByPath[key])
            {
                EditorGUI.indentLevel++;

                var source = GetSourceLabel(path);
                EditorGUILayout.LabelField($"Source: {source}", EditorStyles.miniLabel);
                DrawLabeledPathRow("SO Path", path, path);

                DrawModelIdEntries(list);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawModelIdEntries(CharacterModelIdList list)
        {
            var ids = list != null ? list.modelIds : null;
            if (ids == null || ids.Length == 0)
            {
                EditorGUILayout.HelpBox("modelIds が空です。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField($"modelIds ({ids.Length})", EditorStyles.miniBoldLabel);
            for (var i = 0; i < ids.Length; i++)
            {
                var modelId = ids[i] ?? string.Empty;
                var resourcePath = list != null ? list.ResolveResourcePath(modelId) : modelId;
                var prefabPaths = FindPrefabAssetPaths(resourcePath);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField($"Model {i}", EditorStyles.miniBoldLabel);
                DrawLabeledPathRow("Model ID", modelId, null);
                DrawLabeledPathRow("Resource Path", resourcePath, null);

                if (prefabPaths.Count == 0)
                {
                    EditorGUILayout.HelpBox("対応するPrefabが見つかりません。", MessageType.Warning);
                }
                else
                {
                    foreach (var prefabPath in prefabPaths)
                    {
                        DrawLabeledPathRow("Prefab Path", prefabPath, prefabPath);
                    }
                }
            }
        }

        private static void DrawLabeledPathRow(string label, string value, string assetPathForPing)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (!string.IsNullOrEmpty(assetPathForPing))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPathForPing);
                    if (obj != null && GUILayout.Button("Ping", GUILayout.Width(50f)))
                    {
                        EditorGUIUtility.PingObject(obj);
                    }
                }
            }
        }

        private static string ToRuntimeResourcePath(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                return string.Empty;
            if (modelId.StartsWith("Character/", StringComparison.Ordinal))
                return modelId;
            return "Character/" + modelId;
        }

        private enum ResourceSourceKind
        {
            Any,
            Assets,
            Library
        }

        private static List<string> FindPrefabAssetPaths(string runtimeResourcePath)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(runtimeResourcePath))
                return results;

            var normalizedRuntimePath = NormalizePath(runtimeResourcePath);
            var expectedSource = GetExpectedSource(normalizedRuntimePath);
            var runtimeRelativePath = CharacterModelIdList.ToResourcesRelativePath(normalizedRuntimePath);
            var fileName = Path.GetFileName(runtimeRelativePath);
            if (string.IsNullOrEmpty(fileName))
                return results;

            var guids = AssetDatabase.FindAssets($"{fileName} t:Prefab");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                var candidateSource = GetAssetSource(path);
                if (expectedSource != ResourceSourceKind.Any && expectedSource != candidateSource)
                    continue;

                var resourceRelative = ToResourceRelativePath(path);
                if (string.Equals(resourceRelative, runtimeRelativePath, StringComparison.OrdinalIgnoreCase))
                    results.Add(path);
            }

            return results;
        }

        private static ResourceSourceKind GetExpectedSource(string runtimeResourcePath)
        {
            if (CharacterModelIdList.TryGetLogicalSource(runtimeResourcePath, out var source))
            {
                return source == CharacterModelIdList.ModelReferenceSource.Library
                    ? ResourceSourceKind.Library
                    : ResourceSourceKind.Assets;
            }

            return ResourceSourceKind.Any;
        }

        private static ResourceSourceKind GetAssetSource(string assetPath)
        {
            var normalized = NormalizePath(assetPath);
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return ResourceSourceKind.Assets;
            if (normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Library/PackageCache/", StringComparison.OrdinalIgnoreCase))
                return ResourceSourceKind.Library;
            return ResourceSourceKind.Any;
        }

        private static string ToResourceRelativePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            const string resourcesSegment = "/Resources/";
            var normalized = assetPath.Replace('\\', '/');
            var index = normalized.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return string.Empty;

            var start = index + resourcesSegment.Length;
            var relative = normalized.Substring(start);
            var ext = Path.GetExtension(relative);
            return string.IsNullOrEmpty(ext)
                ? NormalizePath(relative)
                : NormalizePath(relative.Substring(0, relative.Length - ext.Length));
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            return path.Trim().Replace('\\', '/').Trim('/');
        }

        private static string GetSourceLabel(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "None";
            if (path.StartsWith("Assets/"))
                return "Project";
            if (path.StartsWith("Packages/"))
                return "Package";
            return "Other";
        }
    }
}
