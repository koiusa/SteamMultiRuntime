using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Editor
{
    /// <summary>Edits the scene list stored in a Unity 6 Build Profile.</summary>
    public sealed class BuildProfileSceneEditorWindow : EditorWindow
    {
        private enum DuplicateScenePolicy
        {
            KeepExisting,
            OverwriteExisting
        }

        private const string BuildProfileTypeName = "UnityEditor.Build.Profile.BuildProfile";
        private const string RecentBuildProfileKeyPrefix = "Koiusa.SteamMultiRuntime.BuildProfileSceneEditor.Recent.";
        private const string RecentPresetKeyPrefix = "Koiusa.SteamMultiRuntime.BuildProfileSceneEditor.RecentPreset.";

        private static readonly Type BuildProfileType =
            typeof(UnityEditor.Editor).Assembly.GetType(BuildProfileTypeName);

        [SerializeField] private UnityEngine.Object buildProfile;
        [SerializeField] private BuildProfileScenePreset preset;
        [SerializeField] private DuplicateScenePolicy duplicateScenePolicy = DuplicateScenePolicy.KeepExisting;

        private SerializedObject serializedProfile;
        private SerializedProperty overrideSceneListProperty;
        private SerializedProperty scenesProperty;
        private ReorderableList sceneList;
        private Vector2 scrollPosition;

        [MenuItem("Tools/SteamMultiRuntime/Build/Build Profile Scenes")]
        private static void Open()
        {
            var window = GetWindow<BuildProfileSceneEditorWindow>("Build Profile Scenes");
            window.minSize = new Vector2(520f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            if (!TryLoadRecentBuildProfile())
            {
                TryUseSelectedBuildProfile();
            }

            TryLoadRecentPreset();
            RebuildSerializedProfile();
        }

        private void OnSelectionChange()
        {
            if (TryUseSelectedBuildProfile())
            {
                RebuildSerializedProfile();
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Build Profile Scene Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build Profile のシーン一覧を編集します。変更は選択中のアセットへ直ちに保存されます。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var nextProfile = EditorGUILayout.ObjectField(
                    "Build Profile",
                    buildProfile,
                    BuildProfileType ?? typeof(UnityEngine.Object),
                    false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (nextProfile != null && !IsBuildProfile(nextProfile))
                    {
                        EditorUtility.DisplayDialog("Build Profile Scenes", "BuildProfile アセットを指定してください。", "OK");
                    }
                    else
                    {
                        buildProfile = nextProfile;
                        RememberBuildProfile();
                        RebuildSerializedProfile();
                    }
                }

                if (GUILayout.Button("Create", GUILayout.Width(72f)))
                {
                    ShowCreateBuildProfileMenu();
                }
            }

            if (buildProfile == null)
            {
                EditorGUILayout.HelpBox("BuildProfile アセットを指定するか、Project ウィンドウで選択してください。", MessageType.Warning);
                if (GUILayout.Button("Find Build Profiles"))
                {
                    ShowBuildProfilePicker();
                }

                return;
            }

            if (serializedProfile == null || serializedProfile.targetObject == null)
            {
                RebuildSerializedProfile();
            }

            if (scenesProperty == null)
            {
                EditorGUILayout.HelpBox("この BuildProfile からシーン一覧を取得できませんでした。Unity のバージョンを確認してください。", MessageType.Error);
                return;
            }

            DrawPresetSection();

            serializedProfile.Update();
            if (overrideSceneListProperty != null)
            {
                EditorGUILayout.PropertyField(
                    overrideSceneListProperty,
                    new GUIContent(
                        "BuildProfile固有のシーン一覧を使う",
                        "有効にすると、共通のBuild Settingsではなく、このBuildProfileのシーン一覧を使用します。"));
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scroll.scrollPosition;
                sceneList.DoLayoutList();
            }

            DrawActions();

            if (serializedProfile.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(buildProfile);
            }
        }

        private void DrawPresetSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var nextPreset = (BuildProfileScenePreset)EditorGUILayout.ObjectField(
                "Scene Preset", preset, typeof(BuildProfileScenePreset), false);
            if (EditorGUI.EndChangeCheck())
            {
                preset = nextPreset;
                RememberPreset();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(preset == null))
                {
                    if (GUILayout.Button("Apply Preset"))
                    {
                        ApplyPreset();
                    }

                    if (GUILayout.Button("Save Profile To Preset"))
                    {
                        SaveToPreset();
                    }
                }

                if (GUILayout.Button("Create Preset"))
                {
                    CreatePresetFromProfile();
                }
            }
        }

        private void ApplyPreset()
        {
            if (preset == null)
            {
                return;
            }

            RecordUndo("Apply Build Profile Scene Preset");
            serializedProfile.Update();
            scenesProperty.ClearArray();
            var unresolved = new List<string>();
            var appliedScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in preset.Scenes)
            {
                var resolvedPath = ResolvePresetScenePath(entry);
                if (string.IsNullOrEmpty(resolvedPath))
                {
                    unresolved.Add(string.IsNullOrEmpty(entry.Path) ? entry.Guid : entry.Path);
                    continue;
                }

                if (!appliedScenes.Add(GetSceneIdentity(resolvedPath)))
                {
                    continue;
                }

                scenesProperty.arraySize++;
                var item = scenesProperty.GetArrayElementAtIndex(scenesProperty.arraySize - 1);
                item.FindPropertyRelative("m_enabled").boolValue = entry.Enabled;
                item.FindPropertyRelative("m_path").stringValue = resolvedPath;
            }

            if (overrideSceneListProperty != null)
            {
                overrideSceneListProperty.boolValue = true;
            }

            ApplyAndSave();
            if (unresolved.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Build Profile Scenes",
                    $"{unresolved.Count} 個のシーンを解決できなかったため、追加しませんでした。\n\n{string.Join("\n", unresolved)}",
                    "OK");
            }
        }

        private void SaveToPreset()
        {
            if (preset == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(preset, "Save Build Profile Scene Preset");
            serializedProfile.Update();
            preset.SetScenes(ReadProfileScenes());
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
        }

        private void CreatePresetFromProfile()
        {
            var defaultName = buildProfile != null ? $"{buildProfile.name}_Scenes.asset" : "BuildProfileScenePreset.asset";
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Build Profile Scene Preset",
                defaultName,
                "asset",
                "プリセットの保存先を指定してください。",
                "Assets/Settings/Build Profiles");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var created = CreateInstance<BuildProfileScenePreset>();
            serializedProfile.Update();
            created.SetScenes(ReadProfileScenes());
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            preset = created;
            RememberPreset();
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        private List<BuildProfileScenePreset.SceneEntry> ReadProfileScenes()
        {
            var entries = new List<BuildProfileScenePreset.SceneEntry>(scenesProperty.arraySize);
            for (var i = 0; i < scenesProperty.arraySize; i++)
            {
                var item = scenesProperty.GetArrayElementAtIndex(i);
                var path = item.FindPropertyRelative("m_path").stringValue;
                var resolvedPath = ResolveExistingScenePath(path);
                entries.Add(new BuildProfileScenePreset.SceneEntry(
                    string.IsNullOrEmpty(resolvedPath) ? string.Empty : AssetDatabase.AssetPathToGUID(resolvedPath),
                    string.IsNullOrEmpty(resolvedPath) ? path : resolvedPath,
                    item.FindPropertyRelative("m_enabled").boolValue));
            }

            return entries;
        }

        private static string ResolvePresetScenePath(BuildProfileScenePreset.SceneEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.Guid))
            {
                var pathFromGuid = AssetDatabase.GUIDToAssetPath(entry.Guid);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(pathFromGuid) != null)
                {
                    return pathFromGuid;
                }
            }

            return ResolveExistingScenePath(entry.Path);
        }

        private static string ResolveExistingScenePath(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                return path;
            }

            var sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(sceneName))
            {
                return string.Empty;
            }

            var matches = AssetDatabase.FindAssets($"{sceneName} t:Scene");
            foreach (var match in matches)
            {
                var candidate = AssetDatabase.GUIDToAssetPath(match);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(candidate), sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private void DrawActions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add / Replace", EditorStyles.boldLabel);
            duplicateScenePolicy = (DuplicateScenePolicy)EditorGUILayout.Popup(
                new GUIContent("同名シーンが見つかった場合"),
                (int)duplicateScenePolicy,
                new[]
                {
                    new GUIContent("既存を優先"),
                    new GUIContent("見つけたSampleを優先")
                });
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Open Scenes"))
                {
                    AddScenes(GetOpenScenePaths(), false);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Sample Scenes"))
                {
                    AddScenes(GetProjectSampleScenePaths(), false);
                }
            }

        }

        private void RebuildSerializedProfile()
        {
            serializedProfile = null;
            overrideSceneListProperty = null;
            scenesProperty = null;
            sceneList = null;

            if (!IsBuildProfile(buildProfile))
            {
                buildProfile = null;
                return;
            }

            serializedProfile = new SerializedObject(buildProfile);
            overrideSceneListProperty = serializedProfile.FindProperty("m_OverrideGlobalSceneList");
            scenesProperty = serializedProfile.FindProperty("m_Scenes");
            if (scenesProperty == null || !scenesProperty.isArray)
            {
                return;
            }

            sceneList = new ReorderableList(serializedProfile, scenesProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"Scenes ({scenesProperty.arraySize})"),
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
                drawElementCallback = DrawSceneElement,
                onAddCallback = _ => AddEmptyScene(),
                onRemoveCallback = list => RemoveScene(list.index)
            };
        }

        private void DrawSceneElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var item = scenesProperty.GetArrayElementAtIndex(index);
            var enabledProperty = item.FindPropertyRelative("m_enabled");
            var pathProperty = item.FindPropertyRelative("m_path");
            rect.y += 2f;

            var toggleRect = new Rect(rect.x, rect.y, 18f, EditorGUIUtility.singleLineHeight);
            var objectRect = new Rect(rect.x + 22f, rect.y, rect.width - 22f, EditorGUIUtility.singleLineHeight);
            enabledProperty.boolValue = EditorGUI.Toggle(toggleRect, enabledProperty.boolValue);

            var current = AssetDatabase.LoadAssetAtPath<SceneAsset>(pathProperty.stringValue);
            var next = (SceneAsset)EditorGUI.ObjectField(objectRect, current, typeof(SceneAsset), false);
            if (next != current)
            {
                pathProperty.stringValue = next == null ? string.Empty : AssetDatabase.GetAssetPath(next);
            }
        }

        private void AddEmptyScene()
        {
            RecordUndo("Add Build Profile Scene");
            scenesProperty.arraySize++;
            var item = scenesProperty.GetArrayElementAtIndex(scenesProperty.arraySize - 1);
            item.FindPropertyRelative("m_enabled").boolValue = true;
            item.FindPropertyRelative("m_path").stringValue = string.Empty;
            ApplyAndSave();
        }

        private void RemoveScene(int index)
        {
            if (index < 0 || index >= scenesProperty.arraySize)
            {
                return;
            }

            RecordUndo("Remove Build Profile Scene");
            scenesProperty.DeleteArrayElementAtIndex(index);
            ApplyAndSave();
        }

        private void AddScenes(IReadOnlyList<string> paths, bool replace)
        {
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Build Profile Scenes", "対象のシーンがありません。", "OK");
                return;
            }

            RecordUndo(replace ? "Replace Build Profile Scenes" : "Add Build Profile Scenes");
            serializedProfile.Update();
            if (replace)
            {
                scenesProperty.ClearArray();
            }
            else
            {
                RemoveDuplicateScenes();
            }

            var existing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < scenesProperty.arraySize; i++)
            {
                var existingPath = scenesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("m_path").stringValue;
                RegisterSceneKeys(existing, existingPath, i);
            }

            RegisterPresetAliases(existing);

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var existingIndex = FindExistingSceneIndex(existing, path);
                if (existingIndex >= 0)
                {
                    if (duplicateScenePolicy == DuplicateScenePolicy.OverwriteExisting)
                    {
                        var existingItem = scenesProperty.GetArrayElementAtIndex(existingIndex);
                        existingItem.FindPropertyRelative("m_enabled").boolValue = true;
                        existingItem.FindPropertyRelative("m_path").stringValue = path;
                    }

                    continue;
                }

                scenesProperty.arraySize++;
                var item = scenesProperty.GetArrayElementAtIndex(scenesProperty.arraySize - 1);
                item.FindPropertyRelative("m_enabled").boolValue = true;
                item.FindPropertyRelative("m_path").stringValue = path;
                RegisterSceneKeys(existing, path, scenesProperty.arraySize - 1);
            }

            // Sanitize the completed list as well. Serialized arrays can copy the previous
            // element when expanded, so the final result is the source of truth.
            RemoveDuplicateScenes();

            if (overrideSceneListProperty != null)
            {
                overrideSceneListProperty.boolValue = true;
            }

            ApplyAndSave();
        }

        private void RecordUndo(string label)
        {
            Undo.RegisterCompleteObjectUndo(buildProfile, label);
        }

        private void ApplyAndSave()
        {
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(buildProfile);
            AssetDatabase.SaveAssetIfDirty(buildProfile);
            Repaint();
        }

        private static string GetSceneIdentity(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            // Imported package samples can have a different GUID and versioned path.
            // Scene loading also expects scene names to be unique, so use the file name as identity.
            var sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrEmpty(sceneName) ? path : sceneName;
        }

        private static void RegisterSceneKeys(Dictionary<string, int> scenes, string path, int index)
        {
            var identity = GetSceneIdentity(path);
            if (!string.IsNullOrEmpty(identity))
            {
                scenes["name:" + identity] = index;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
            {
                scenes["guid:" + guid] = index;
            }
        }

        private static int FindExistingSceneIndex(Dictionary<string, int> scenes, string path)
        {
            var identity = GetSceneIdentity(path);
            if (!string.IsNullOrEmpty(identity) && scenes.TryGetValue("name:" + identity, out var index))
            {
                return index;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid) && scenes.TryGetValue("guid:" + guid, out index) ? index : -1;
        }

        private void RegisterPresetAliases(Dictionary<string, int> scenes)
        {
            if (preset == null)
            {
                return;
            }

            foreach (var entry in preset.Scenes)
            {
                var resolvedPath = ResolvePresetScenePath(entry);
                var index = FindExistingSceneIndex(scenes, resolvedPath);
                if (index < 0)
                {
                    index = FindExistingSceneIndex(scenes, entry.Path);
                }

                if (index < 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.Guid))
                {
                    scenes["guid:" + entry.Guid] = index;
                }

                RegisterSceneKeys(scenes, resolvedPath, index);
                RegisterSceneKeys(scenes, entry.Path, index);
            }
        }

        private void RemoveDuplicateScenes()
        {
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < scenesProperty.arraySize; i++)
            {
                var path = scenesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("m_path").stringValue;
                var identity = GetSceneIdentity(path);
                if (string.IsNullOrEmpty(identity) || identities.Add(identity))
                {
                    continue;
                }

                scenesProperty.DeleteArrayElementAtIndex(i);
                i--;
            }
        }

        private bool TryUseSelectedBuildProfile()
        {
            if (!IsBuildProfile(Selection.activeObject))
            {
                return false;
            }

            buildProfile = Selection.activeObject;
            RememberBuildProfile();
            return true;
        }

        private bool TryLoadRecentBuildProfile()
        {
            var guid = EditorPrefs.GetString(GetRecentBuildProfileKey(), string.Empty);
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            var profile = AssetDatabase.LoadMainAssetAtPath(path);
            if (!IsBuildProfile(profile))
            {
                EditorPrefs.DeleteKey(GetRecentBuildProfileKey());
                return false;
            }

            buildProfile = profile;
            return true;
        }

        private void RememberBuildProfile()
        {
            var key = GetRecentBuildProfileKey();
            if (buildProfile == null)
            {
                EditorPrefs.DeleteKey(key);
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(buildProfile));
            if (!string.IsNullOrEmpty(guid))
            {
                EditorPrefs.SetString(key, guid);
            }
        }

        private static string GetRecentBuildProfileKey()
        {
            return RecentBuildProfileKeyPrefix + Hash128.Compute(Application.dataPath);
        }

        private void TryLoadRecentPreset()
        {
            var key = GetRecentPresetKey();
            var guid = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            preset = AssetDatabase.LoadAssetAtPath<BuildProfileScenePreset>(path);
            if (preset == null)
            {
                EditorPrefs.DeleteKey(key);
            }
        }

        private void RememberPreset()
        {
            var key = GetRecentPresetKey();
            if (preset == null)
            {
                EditorPrefs.DeleteKey(key);
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(preset));
            if (!string.IsNullOrEmpty(guid))
            {
                EditorPrefs.SetString(key, guid);
            }
        }

        private static string GetRecentPresetKey()
        {
            return RecentPresetKeyPrefix + Hash128.Compute(Application.dataPath);
        }

        private static bool IsBuildProfile(UnityEngine.Object candidate)
        {
            return candidate != null && candidate.GetType().FullName == BuildProfileTypeName;
        }

        private static void AddUniquePath(List<string> paths, string path)
        {
            if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
            {
                paths.Add(path);
            }
        }

        private static List<string> GetOpenScenePaths()
        {
            var paths = new List<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var path = SceneManager.GetSceneAt(i).path;
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        private List<string> GetProjectSampleScenePaths()
        {
            var paths = new List<string>();

            // A preset is the authoritative sample list. Resolve its GUIDs to the
            // imported, versioned sample paths and feed those paths to AddScenes,
            // just like Add Open Scenes does.
            if (preset != null)
            {
                foreach (var entry in preset.Scenes)
                {
                    AddUniquePath(paths, ResolvePresetScenePath(entry));
                }

                return paths;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("/Samples/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddUniquePath(paths, path);
                }
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static void ShowBuildProfilePicker()
        {
            var profiles = FindBuildProfiles();
            if (profiles.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Build Profile Scenes",
                    "BuildProfile アセットが見つかりません。File > Build Profiles から作成してください。",
                    "OK");
                return;
            }

            if (profiles.Count == 1)
            {
                SelectAndPing(profiles[0]);
                return;
            }

            var menu = new GenericMenu();
            foreach (var profile in profiles)
            {
                var capturedProfile = profile;
                var path = AssetDatabase.GetAssetPath(profile);
                menu.AddItem(new GUIContent($"{profile.name}  ({path})"), false, () => SelectAndPing(capturedProfile));
            }

            menu.ShowAsContext();
        }

        private static List<UnityEngine.Object> FindBuildProfiles()
        {
            var profiles = new List<UnityEngine.Object>();
            foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (IsBuildProfile(asset))
                {
                    profiles.Add(asset);
                }
            }

            profiles.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return profiles;
        }

        private static void SelectAndPing(UnityEngine.Object profile)
        {
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        private void ShowCreateBuildProfileMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("Windows/Windows x64"),
                false,
                () => CreateAndUseBuildProfile(BuildTarget.StandaloneWindows64, false));
            menu.AddItem(
                new GUIContent("macOS/macOS Apple Silicon"),
                false,
                () => CreateAndUseBuildProfile(BuildTarget.StandaloneOSX, true));
            menu.ShowAsContext();
        }

        private void CreateAndUseBuildProfile(BuildTarget target, bool appleSilicon)
        {
            var createdProfile = CreateBuildProfile(target, appleSilicon);
            if (createdProfile == null)
            {
                return;
            }

            buildProfile = createdProfile;
            RememberBuildProfile();
            RebuildSerializedProfile();
            Repaint();
        }

        private static UnityEngine.Object CreateBuildProfile(BuildTarget target, bool appleSilicon)
        {
            if (BuildProfileType == null)
            {
                EditorUtility.DisplayDialog("Build Profile Scenes", "BuildProfile型を取得できませんでした。", "OK");
                return null;
            }

            var defaultName = appleSilicon ? "macOS_AppleSilicon" : "Windows_x64";
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Build Profile",
                defaultName,
                "asset",
                $"{target} 用のBuildProfileを保存します。",
                "Assets");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var factory = BuildProfileType.GetMethod(
                "CreateInstance",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(BuildTarget), typeof(StandaloneBuildSubtarget) },
                null);
            if (factory == null)
            {
                EditorUtility.DisplayDialog("Build Profile Scenes", "このUnityバージョンではBuildProfileを作成できません。", "OK");
                return null;
            }

            try
            {
                var profile = factory.Invoke(
                    null,
                    new object[] { target, StandaloneBuildSubtarget.Player }) as UnityEngine.Object;
                if (profile == null)
                {
                    throw new InvalidOperationException("BuildProfileの生成結果がnullです。");
                }

                if (appleSilicon)
                {
                    SetAppleSiliconArchitecture(profile);
                }

                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
                SelectAndPing(profile);
                return profile;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Build Profile Scenes",
                    $"BuildProfileの作成に失敗しました。\n{exception.GetBaseException().Message}",
                    "OK");
                return null;
            }
        }

        private static void SetAppleSiliconArchitecture(UnityEngine.Object profile)
        {
            var serializedProfile = new SerializedObject(profile);
            var platformSettings = serializedProfile.FindProperty("m_PlatformBuildProfile");
            var architecture = platformSettings?.FindPropertyRelative("m_Architecture");
            if (architecture == null)
            {
                throw new InvalidOperationException("macOS BuildProfileのArchitecture設定を取得できませんでした。");
            }

            // Unity macOS architecture: 0 = Intel, 1 = ARM64, 2 = Universal.
            architecture.intValue = 1;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
