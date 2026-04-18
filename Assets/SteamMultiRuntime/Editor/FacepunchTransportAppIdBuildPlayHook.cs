using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Editor
{
    [InitializeOnLoad]
    public static class FacepunchTransportAppIdPlayHook
    {
        static FacepunchTransportAppIdPlayHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            if (!FacepunchTransportAppIdEditorUtility.TryResolveAppId(out var appId))
            {
                return;
            }

            FacepunchTransportAppIdEditorUtility.ApplyToOpenScenes(appId);
        }
    }

    public class FacepunchTransportAppIdBuildHook : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static readonly List<SceneBackup> BuildBackups = new List<SceneBackup>();

        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildBackups.Clear();

            if (!FacepunchTransportAppIdEditorUtility.TryResolveAppId(out var appId))
            {
                Debug.LogWarning("[FacepunchTransportAppIdBuildHook] Skipped AppID apply. Local AppID not found.");
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var buildScene in EditorBuildSettings.scenes)
                {
                    if (!buildScene.enabled)
                    {
                        continue;
                    }

                    var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                    var changes = FacepunchTransportAppIdEditorUtility.ApplyToScene(scene, appId);
                    if (changes.Count == 0)
                    {
                        continue;
                    }

                    BuildBackups.Add(new SceneBackup
                    {
                        ScenePath = buildScene.path,
                        Entries = changes
                    });

                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (BuildBackups.Count == 0)
            {
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (var i = 0; i < BuildBackups.Count; i++)
                {
                    var backup = BuildBackups[i];
                    var scene = EditorSceneManager.OpenScene(backup.ScenePath, OpenSceneMode.Single);

                    var restored = false;
                    for (var j = 0; j < backup.Entries.Count; j++)
                    {
                        var entry = backup.Entries[j];
                        if (!GlobalObjectId.TryParse(entry.GlobalObjectId, out var globalObjectId))
                        {
                            continue;
                        }

                        var target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
                        if (!(target is Component component))
                        {
                            continue;
                        }

                        if (FacepunchTransportAppIdEditorUtility.TrySetAppId(component, entry.OriginalValue))
                        {
                            restored = true;
                        }
                    }

                    if (restored)
                    {
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }
            finally
            {
                BuildBackups.Clear();
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        [Serializable]
        private class SceneBackup
        {
            public string ScenePath;
            public List<FacepunchTransportAppIdEditorUtility.AppIdBackupEntry> Entries;
        }
    }

    public static class FacepunchTransportAppIdEditorUtility
    {
        public const string LocalAppIdFilePath = "UserSettings/steam_appid.local.txt";

        private static readonly string[] EnvironmentVariableNames =
        {
            "STEAM_APP_ID",
            "FACEPUNCH_STEAM_APP_ID"
        };

        private static readonly string[] CandidatePropertyNames =
        {
            "steamAppId",
            "steamAppID",
            "SteamAppId",
            "SteamAppID",
            "appId",
            "AppId"
        };

        public static void ApplyToOpenScenes(uint appId)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ApplyToScene(scene, appId);
            }
        }

        public static List<AppIdBackupEntry> ApplyToScene(Scene scene, uint appId)
        {
            var backups = new List<AppIdBackupEntry>();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return backups;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var components = roots[i].GetComponentsInChildren<Component>(true);
                for (var j = 0; j < components.Length; j++)
                {
                    var component = components[j];
                    if (component == null || !IsFacepunchTransportComponent(component))
                    {
                        continue;
                    }

                    if (!TryGetAppId(component, out var currentAppId))
                    {
                        continue;
                    }

                    if (currentAppId == appId)
                    {
                        continue;
                    }

                    var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
                    if (TrySetAppId(component, appId))
                    {
                        backups.Add(new AppIdBackupEntry
                        {
                            GlobalObjectId = globalObjectId,
                            OriginalValue = currentAppId
                        });
                    }
                }
            }

            return backups;
        }

        public static bool TryResolveAppId(out uint appId)
        {
            for (var i = 0; i < EnvironmentVariableNames.Length; i++)
            {
                var value = Environment.GetEnvironmentVariable(EnvironmentVariableNames[i]);
                if (TryParseAppId(value, out appId))
                {
                    return true;
                }
            }

            return TryReadLocalAppId(out appId);
        }

        public static bool TryReadLocalAppId(out uint appId)
        {
            var fullPath = GetLocalAppIdFullPath();
            if (File.Exists(fullPath))
            {
                var value = File.ReadAllText(fullPath).Trim();
                if (TryParseAppId(value, out appId))
                {
                    return true;
                }
            }

            appId = 0;
            return false;
        }

        public static string GetLocalAppIdFullPath()
        {
            return Path.GetFullPath(LocalAppIdFilePath);
        }

        public static void SaveLocalAppId(uint appId)
        {
            var fullPath = GetLocalAppIdFullPath();
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, appId.ToString());
            AssetDatabase.Refresh();
        }

        public static void DeleteLocalAppId()
        {
            var fullPath = GetLocalAppIdFullPath();
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                AssetDatabase.Refresh();
            }
        }

        public static bool TrySetAppId(Component component, uint appId)
        {
            var serializedObject = new SerializedObject(component);
            for (var i = 0; i < CandidatePropertyNames.Length; i++)
            {
                var property = serializedObject.FindProperty(CandidatePropertyNames[i]);
                if (property != null && property.propertyType == SerializedPropertyType.Integer)
                {
                    property.intValue = (int)appId;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    return true;
                }
            }

            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.propertyType != SerializedPropertyType.Integer)
                    {
                        continue;
                    }

                    if (iterator.name.IndexOf("appid", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    iterator.intValue = (int)appId;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    return true;
                }
                while (iterator.NextVisible(false));
            }

            return false;
        }

        private static bool TryGetAppId(Component component, out uint appId)
        {
            var serializedObject = new SerializedObject(component);
            for (var i = 0; i < CandidatePropertyNames.Length; i++)
            {
                var property = serializedObject.FindProperty(CandidatePropertyNames[i]);
                if (property != null && property.propertyType == SerializedPropertyType.Integer)
                {
                    appId = (uint)Mathf.Max(0, property.intValue);
                    return true;
                }
            }

            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.propertyType == SerializedPropertyType.Integer &&
                        iterator.name.IndexOf("appid", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        appId = (uint)Mathf.Max(0, iterator.intValue);
                        return true;
                    }
                }
                while (iterator.NextVisible(false));
            }

            appId = 0;
            return false;
        }

        private static bool IsFacepunchTransportComponent(Component component)
        {
            var fullName = component.GetType().FullName;
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return false;
            }

            return fullName.IndexOf("FacepunchTransport", StringComparison.Ordinal) >= 0;
        }

        private static bool TryParseAppId(string value, out uint appId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                appId = 0;
                return false;
            }

            return uint.TryParse(value.Trim(), out appId) && appId > 0;
        }

        [Serializable]
        public class AppIdBackupEntry
        {
            public string GlobalObjectId;
            public uint OriginalValue;
        }
    }
}
