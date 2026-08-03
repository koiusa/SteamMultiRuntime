using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    internal sealed class NpcCrowdBackendWindow : EditorWindow
    {
        private sealed class PrefabEntry
        {
            public GameObject Prefab;
            public string Path;
            public bool UseCrowdSimulation;
            public bool EnableNpcRigidbodyCollisions;
            public bool IsEditable;
            public int ControllerCount;
        }

        private const string MenuPath = "Tools/SteamMultiRuntime/Configuration/NPC/Crowd Simulation";
        private readonly List<PrefabEntry> entries = new();
        private Vector2 scrollPosition;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<NpcCrowdBackendWindow>("NPC Crowd ON/OFF");
            window.minSize = new Vector2(680f, 260f);
            window.RefreshPrefabList();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += OnProjectChanged;
            RefreshPrefabList();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "AssetsとPackagesからNpcNavMeshControllerを持つプレファブを表示します。チェックの変更はプレファブへ直ちに保存され、次回のPlay開始時に反映されます。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"NPC Prefabs: {entries.Count}", EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
                    RefreshPrefabList();
            }

            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Crowd", GUILayout.Width(48f));
                GUILayout.Label("NPC Collision", GUILayout.Width(92f));
                GUILayout.Label("Prefab", GUILayout.Width(210f));
                GUILayout.Label("Asset Path");
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (var entry in entries)
                DrawPrefabRow(entry);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabRow(PrefabEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var canEdit = entry.IsEditable && !EditorApplication.isPlayingOrWillChangePlaymode;
                using (new EditorGUI.DisabledScope(!canEdit))
                {
                    EditorGUI.BeginChangeCheck();
                    var enabled = EditorGUILayout.Toggle(entry.UseCrowdSimulation, GUILayout.Width(48f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (ApplyToPrefab(
                                entry,
                                "useCrowdSimulation",
                                enabled,
                                "NPC Crowd simulation"))
                            entry.UseCrowdSimulation = enabled;
                    }
                }

                using (new EditorGUI.DisabledScope(!canEdit || entry.UseCrowdSimulation))
                {
                    EditorGUI.BeginChangeCheck();
                    var collisionsEnabled = EditorGUILayout.Toggle(
                        entry.EnableNpcRigidbodyCollisions,
                        GUILayout.Width(92f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (ApplyToPrefab(
                                entry,
                                "enableNpcRigidbodyCollisions",
                                collisionsEnabled,
                                "NPC Rigidbody collisions"))
                            entry.EnableNpcRigidbodyCollisions = collisionsEnabled;
                    }
                }

                EditorGUILayout.ObjectField(entry.Prefab, typeof(GameObject), false, GUILayout.Width(210f));

                var suffix = entry.IsEditable ? string.Empty : "  (read only)";
                if (entry.ControllerCount > 1)
                    suffix += $"  ({entry.ControllerCount} controllers)";
                EditorGUILayout.SelectableLabel(
                    entry.Path + suffix,
                    EditorStyles.miniLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void OnProjectChanged()
        {
            RefreshPrefabList();
            Repaint();
        }

        private void RefreshPrefabList()
        {
            entries.Clear();

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                var controllers = prefab.GetComponentsInChildren<NpcNavMeshController>(true);
                if (controllers.Length == 0)
                    continue;

                var serializedController = new SerializedObject(controllers[0]);
                var crowdProperty = serializedController.FindProperty("useCrowdSimulation");
                var collisionProperty = serializedController.FindProperty("enableNpcRigidbodyCollisions");
                if (crowdProperty == null || collisionProperty == null)
                    continue;

                entries.Add(new PrefabEntry
                {
                    Prefab = prefab,
                    Path = path,
                    UseCrowdSimulation = crowdProperty.boolValue,
                    EnableNpcRigidbodyCollisions = collisionProperty.boolValue,
                    IsEditable = IsEditablePrefab(path),
                    ControllerCount = controllers.Length
                });
            }

            entries.Sort((left, right) =>
                string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsEditablePrefab(string path)
        {
            if (!AssetDatabase.IsOpenForEdit(path, StatusQueryOptions.UseCachedIfPossible))
                return false;

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            return package == null ||
                   package.source == UnityEditor.PackageManager.PackageSource.Embedded ||
                   package.source == UnityEditor.PackageManager.PackageSource.Local;
        }

        private static bool ApplyToPrefab(
            PrefabEntry entry,
            string propertyName,
            bool enabled,
            string displayName)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(entry.Path);
                var controllers = root.GetComponentsInChildren<NpcNavMeshController>(true);
                foreach (var controller in controllers)
                {
                    var serializedController = new SerializedObject(controller);
                    var property = serializedController.FindProperty(propertyName);
                    if (property == null)
                        continue;

                    property.boolValue = enabled;
                    serializedController.ApplyModifiedPropertiesWithoutUndo();
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, entry.Path) == null)
                    throw new InvalidOperationException("プレファブを保存できませんでした。");

                Debug.Log(
                    $"[RuntimeTools] {entry.Prefab.name}: {displayName}=" +
                    $"{(enabled ? "ON" : "OFF")} (applies on next Play)",
                    entry.Prefab);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "NPC Crowd ON/OFF",
                    $"{entry.Path} の設定を保存できませんでした。\n\n{exception.Message}",
                    "OK");
                return false;
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
