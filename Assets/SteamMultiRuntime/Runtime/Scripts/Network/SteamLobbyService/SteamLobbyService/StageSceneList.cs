using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Network
{
    [CreateAssetMenu(fileName = "StageSceneList", menuName = "SteamMultiRuntime/Stage Scene List", order = 101)]
    public class StageSceneList : ScriptableObject
    {
        [Tooltip("シーン参照ルート。例: Assets/Scenes")]
        public string sceneRoot = string.Empty;

        public string[] sceneNames;

        public string ResolveSceneReference(string sceneReference)
        {
            if (string.IsNullOrWhiteSpace(sceneReference))
            {
                return string.Empty;
            }

            var normalized = sceneReference.Trim().Replace('\\', '/');
            if (normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            var normalizedRoot = NormalizeRoot(sceneRoot);
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                return normalized;
            }

            if (normalized.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return normalizedRoot + "/" + normalized;
        }

        private static string NormalizeRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return string.Empty;
            }

            return root.Trim().Replace('\\', '/').Trim('/');
        }
    }
}
