using System;
using System.IO;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Network
{
    [CreateAssetMenu(fileName = "CharacterModelIdList", menuName = "SteamMultiRuntime/Character Model Id List", order = 100)]
    public class CharacterModelIdList : ScriptableObject
    {
        public enum ModelReferenceSource
        {
            Assets,
            Library
        }

        [Tooltip("Resources 配下のサブパス。例: Character")]
        public string resourceSub = "Character";

        [Tooltip("プロジェクト直下ルート。Assets または Library")]
        public ModelReferenceSource modelReferenceSource = ModelReferenceSource.Assets;

        public string[] modelIds;

        public string ResolveResourcePath(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return string.Empty;
            }

            var normalizedModelId = ToResourcesRelativePath(modelId);
            var normalizedSub = NormalizePath(resourceSub);
            var root = modelReferenceSource == ModelReferenceSource.Library ? "Library" : "Assets";

            if (!string.IsNullOrEmpty(normalizedSub) && normalizedModelId.StartsWith(normalizedSub + "/", StringComparison.Ordinal))
            {
                normalizedModelId = normalizedModelId.Substring(normalizedSub.Length + 1);
            }

            var combined = string.IsNullOrEmpty(normalizedSub)
                ? Path.Combine(root, "Resources", normalizedModelId)
                : Path.Combine(root, "Resources", normalizedSub, normalizedModelId);

            return NormalizePath(combined);
        }

        public static string ToResourcesRelativePath(string path)
        {
            var normalized = NormalizePath(path);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (TrySplitLogicalResourcesPath(normalized, out _, out var relativePath))
            {
                return relativePath;
            }

            return normalized;
        }

        public static bool TryGetLogicalSource(string path, out ModelReferenceSource source)
        {
            var normalized = NormalizePath(path);
            if (!TrySplitLogicalResourcesPath(normalized, out var root, out _))
            {
                source = default;
                return false;
            }

            if (string.Equals(root, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                source = ModelReferenceSource.Assets;
                return true;
            }

            if (string.Equals(root, "Library", StringComparison.OrdinalIgnoreCase))
            {
                source = ModelReferenceSource.Library;
                return true;
            }

            source = default;
            return false;
        }

        private static bool TrySplitLogicalResourcesPath(string normalizedPath, out string root, out string resourcesRelativePath)
        {
            root = string.Empty;
            resourcesRelativePath = string.Empty;

            if (string.IsNullOrEmpty(normalizedPath))
            {
                return false;
            }

            const string resourcesSegment = "/Resources/";
            var index = normalizedPath.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);
            if (index <= 0)
            {
                return false;
            }

            root = normalizedPath.Substring(0, index);
            if (root.Contains("/"))
            {
                return false;
            }

            resourcesRelativePath = normalizedPath.Substring(index + resourcesSegment.Length);
            return !string.IsNullOrEmpty(resourcesRelativePath);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Trim().Replace('\\', '/').Trim('/');
        }
    }
}
