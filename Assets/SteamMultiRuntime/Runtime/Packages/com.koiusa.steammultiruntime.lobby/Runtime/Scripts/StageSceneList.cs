using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Network
{
    [CreateAssetMenu(fileName = "StageSceneList", menuName = "SteamMultiRuntime/Stage Scene List", order = 101)]
    public class StageSceneList : ScriptableObject
    {
        public enum SceneReferenceSource
        {
            Assets,
            Library
        }

        [Tooltip("参照先の優先ソース")]
        public SceneReferenceSource sceneReferenceSource = SceneReferenceSource.Assets;

        public string[] sceneNames;

        public string ResolveSceneReference(string sceneReference)
        {
            if (string.IsNullOrWhiteSpace(sceneReference))
            {
                return string.Empty;
            }

            return ResolvePreferredBuildScenePath(sceneReference, sceneReferenceSource);
        }

        private static string ResolvePreferredBuildScenePath(string sceneReference, SceneReferenceSource sourcePreference)
        {
            if (string.IsNullOrWhiteSpace(sceneReference))
            {
                return string.Empty;
            }

            var normalized = sceneReference.Replace('\\', '/').Trim();
            if (IsPreferredSourcePath(normalized, sourcePreference)
                && SceneUtility.GetBuildIndexByScenePath(normalized) >= 0)
            {
                return normalized;
            }

            var targetName = Path.GetFileNameWithoutExtension(normalized);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return string.Empty;
            }

            var buildSceneCount = SceneManager.sceneCountInBuildSettings;
            for (var i = 0; i < buildSceneCount; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (!IsPreferredSourcePath(path, sourcePreference))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static bool IsPreferredSourcePath(string path, SceneReferenceSource sourcePreference)
        {
            if (sourcePreference == SceneReferenceSource.Library)
            {
                return path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("Library/PackageCache/", StringComparison.OrdinalIgnoreCase);
            }

            return path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
