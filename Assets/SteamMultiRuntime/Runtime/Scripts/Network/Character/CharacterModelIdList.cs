using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Network
{
    [CreateAssetMenu(fileName = "CharacterModelIdList", menuName = "SteamMultiRuntime/Character Model Id List", order = 100)]
    public class CharacterModelIdList : ScriptableObject
    {
        [Tooltip("Resources からのルートパス。例: Character または Character/Project")]
        public string resourceRoot = "Character";

        public string[] modelIds;

        public string ResolveResourcePath(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return string.Empty;
            }

            var normalizedModelId = modelId.Trim().Replace('\\', '/');
            if (normalizedModelId.StartsWith("Character/", StringComparison.Ordinal))
            {
                return normalizedModelId;
            }

            var normalizedRoot = NormalizeResourceRoot(resourceRoot);
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                return normalizedModelId;
            }

            if (normalizedModelId.StartsWith(normalizedRoot + "/", StringComparison.Ordinal))
            {
                return normalizedModelId;
            }

            return normalizedRoot + "/" + normalizedModelId;
        }

        private static string NormalizeResourceRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return string.Empty;
            }

            return root.Trim().Replace('\\', '/').Trim('/');
        }
    }
}
