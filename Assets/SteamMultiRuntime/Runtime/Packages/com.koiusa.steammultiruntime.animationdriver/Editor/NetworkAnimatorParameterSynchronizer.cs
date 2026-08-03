using System.Collections.Generic;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    /// <summary>
    /// Keeps NetworkAnimator's serialized parameter list aligned with its AnimatorController.
    /// Existing Synchronize selections are preserved; newly discovered parameters are enabled.
    /// </summary>
    internal static class NetworkAnimatorParameterSynchronizer
    {
        private const string MenuPath = "Tools/SteamMultiRuntime/Configuration/Animation/Sync Network Animator Parameters";

        [MenuItem(MenuPath)]
        private static void SyncAllFromMenu()
        {
            var changes = BuildChangePreview();
            if (changes.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Network Animator Parameters",
                    "No NetworkAnimator parameter changes were found.",
                    "OK");
                return;
            }

            if (!NetworkAnimatorSyncConfirmationWindow.Show(changes))
            {
                return;
            }

            var changedCount = SyncAllPrefabs();
            EditorUtility.DisplayDialog(
                "Sync Complete",
                $"Updated {changedCount} NetworkAnimator component(s).",
                "OK");
        }

        private static List<string> BuildChangePreview()
        {
            var changes = new List<string>();
            foreach (var prefabGuid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (!prefabPath.StartsWith("Assets/", System.StringComparison.Ordinal))
                {
                    continue;
                }

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (root == null)
                {
                    continue;
                }

                foreach (var networkAnimator in root.GetComponentsInChildren<NetworkAnimator>(true))
                {
                    var details = DescribeChanges(networkAnimator);
                    if (details.Count == 0)
                    {
                        continue;
                    }

                    var objectPath = AnimationUtility.CalculateTransformPath(networkAnimator.transform, root.transform);
                    changes.Add(string.IsNullOrEmpty(objectPath)
                        ? prefabPath
                        : $"{prefabPath}  [{objectPath}]");
                    foreach (var detail in details)
                    {
                        changes.Add($"    {detail}");
                    }
                }
            }

            return changes;
        }

        private static List<string> DescribeChanges(NetworkAnimator networkAnimator)
        {
            var details = new List<string>();
            var serializedObject = new SerializedObject(networkAnimator);
            var animator = serializedObject.FindProperty("m_Animator")?.objectReferenceValue as Animator;
            var controller = ResolveController(animator != null ? animator.runtimeAnimatorController : null);
            var entries = serializedObject.FindProperty("AnimatorParameterEntries")
                ?.FindPropertyRelative("ParameterEntries");
            if (controller == null || entries == null)
            {
                return details;
            }

            var existingByHash = new Dictionary<int, (string Name, int Type)>();
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                existingByHash[entry.FindPropertyRelative("NameHash").intValue] = (
                    entry.FindPropertyRelative("name").stringValue,
                    entry.FindPropertyRelative("ParameterType").intValue);
            }

            var controllerHashes = new HashSet<int>();
            var orderChanged = entries.arraySize != controller.parameters.Length;
            for (var i = 0; i < controller.parameters.Length; i++)
            {
                var parameter = controller.parameters[i];
                controllerHashes.Add(parameter.nameHash);
                if (!existingByHash.TryGetValue(parameter.nameHash, out var existing))
                {
                    details.Add($"Add: {parameter.name} ({parameter.type}, Synchronize = On)");
                    continue;
                }

                if (existing.Name != parameter.name || existing.Type != (int)parameter.type)
                {
                    details.Add($"Update: {existing.Name} -> {parameter.name} ({parameter.type})");
                }

                if (i >= entries.arraySize
                    || entries.GetArrayElementAtIndex(i).FindPropertyRelative("NameHash").intValue != parameter.nameHash)
                {
                    orderChanged = true;
                }
            }

            foreach (var existing in existingByHash)
            {
                if (!controllerHashes.Contains(existing.Key))
                {
                    details.Add($"Remove: {existing.Value.Name}");
                }
            }

            if (orderChanged && details.Count == 0)
            {
                details.Add("Reorder parameters to match the AnimatorController");
            }

            return details;
        }

        private static int SyncAllPrefabs()
        {
            var changedCount = 0;
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var prefabGuid in prefabGuids)
                {
                    var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                    if (!prefabPath.StartsWith("Assets/", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (root == null)
                    {
                        continue;
                    }

                    foreach (var networkAnimator in root.GetComponentsInChildren<NetworkAnimator>(true))
                    {
                        if (Sync(networkAnimator))
                        {
                            EditorUtility.SetDirty(networkAnimator);
                            changedCount++;
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return changedCount;
        }

        private static bool Sync(NetworkAnimator networkAnimator)
        {
            var serializedObject = new SerializedObject(networkAnimator);
            var animatorProperty = serializedObject.FindProperty("m_Animator");
            var animator = animatorProperty?.objectReferenceValue as Animator;
            var controller = ResolveController(animator != null ? animator.runtimeAnimatorController : null);
            if (controller == null)
            {
                return false;
            }

            var entries = serializedObject.FindProperty("AnimatorParameterEntries")
                ?.FindPropertyRelative("ParameterEntries");
            if (entries == null)
            {
                return false;
            }

            var synchronizeByHash = new Dictionary<int, bool>();
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var hash = entry.FindPropertyRelative("NameHash").intValue;
                var synchronize = entry.FindPropertyRelative("Synchronize").boolValue;
                synchronizeByHash[hash] = synchronize;
            }

            var parameters = controller.parameters;
            var changed = entries.arraySize != parameters.Length;
            entries.arraySize = parameters.Length;

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var entry = entries.GetArrayElementAtIndex(i);
                changed |= SetString(entry, "name", parameter.name);
                changed |= SetInt(entry, "NameHash", parameter.nameHash);
                changed |= SetInt(entry, "ParameterType", (int)parameter.type);

                var shouldSynchronize = synchronizeByHash.TryGetValue(parameter.nameHash, out var previousValue)
                    ? previousValue
                    : true;
                changed |= SetBool(entry, "Synchronize", shouldSynchronize);
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static AnimatorController ResolveController(RuntimeAnimatorController runtimeController)
        {
            if (runtimeController is AnimatorOverrideController overrideController)
            {
                runtimeController = overrideController.runtimeAnimatorController;
            }

            return runtimeController as AnimatorController;
        }

        private static bool SetString(SerializedProperty parent, string name, string value)
        {
            var property = parent.FindPropertyRelative(name);
            if (property.stringValue == value)
            {
                return false;
            }

            property.stringValue = value;
            return true;
        }

        private static bool SetInt(SerializedProperty parent, string name, int value)
        {
            var property = parent.FindPropertyRelative(name);
            if (property.intValue == value)
            {
                return false;
            }

            property.intValue = value;
            return true;
        }

        private static bool SetBool(SerializedProperty parent, string name, bool value)
        {
            var property = parent.FindPropertyRelative(name);
            if (property.boolValue == value)
            {
                return false;
            }

            property.boolValue = value;
            return true;
        }
    }

    internal sealed class NetworkAnimatorSyncConfirmationWindow : EditorWindow
    {
        private IReadOnlyList<string> changes;
        private Vector2 scrollPosition;
        private bool confirmed;

        internal static bool Show(IReadOnlyList<string> changes)
        {
            var window = CreateInstance<NetworkAnimatorSyncConfirmationWindow>();
            window.titleContent = new GUIContent("Confirm NetworkAnimator Sync");
            window.minSize = new Vector2(720f, 420f);
            window.maxSize = new Vector2(1000f, 800f);
            window.changes = changes;
            window.ShowModalUtility();
            return window.confirmed;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("NetworkAnimator Parameter Changes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The following prefab source files will be modified. Existing Synchronize settings are preserved; newly added parameters are enabled.",
                MessageType.Warning);

            EditorGUILayout.Space();
            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition, EditorStyles.helpBox))
            {
                scrollPosition = scroll.scrollPosition;
                if (changes != null)
                {
                    foreach (var change in changes)
                    {
                        EditorGUILayout.SelectableLabel(
                            change,
                            EditorStyles.label,
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(120f), GUILayout.Height(28f)))
                {
                    Close();
                }

                if (GUILayout.Button("Apply Changes", GUILayout.Width(140f), GUILayout.Height(28f)))
                {
                    confirmed = true;
                    Close();
                }
            }
        }
    }

}
