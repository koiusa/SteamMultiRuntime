using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.EditorTools
{
    public static class UiDocumentSortingOrderValidator
    {
        private static readonly string[] SearchRoots =
        {
            "Assets/SteamMultiRuntime/Runtime/Resources",
            "Assets/SteamMultiRuntime/Samples"
        };

        private readonly struct OrderBand
        {
            public OrderBand(string role, int minimum, int maximum, params string[] nameTokens)
            {
                Role = role;
                Minimum = minimum;
                Maximum = maximum;
                NameTokens = nameTokens;
            }

            public string Role { get; }
            public int Minimum { get; }
            public int Maximum { get; }
            public string[] NameTokens { get; }
        }

        private static readonly OrderBand[] Bands =
        {
            new("Loading / blocking", 110, 119, "Loading", "Splash", "Dialog"),
            new("Child / modal menu", 100, 109, "CharacterSelect", "KeyConfig"),
            new("Root menu", 90, 99, "PauseMenu"),
            new("Session menu", 80, 89, "StageSelector", "StageSelect", "SteamLobby", "LobbyView"),
            new("Debug UI", 50, 79, "Debug"),
            new("HUD / overlay", 0, 49, "Hud", "HUD", "Overlay", "Compass", "PlayerName")
        };

        [MenuItem("Tools/SteamMultiRuntime/Read Only/UI/Validate UIDocument Sorting Orders")]
        public static void ValidateAll()
        {
            var issues = CollectIssues();
            if (issues.Count == 0)
            {
                Debug.Log("[UI Sorting Order] Validation passed.");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("UI Sorting Order", "Validation passed.", "OK");
                return;
            }

            foreach (var issue in issues)
                Debug.LogError(issue.Message, issue.Context);

            var summary = $"{issues.Count} UIDocument sorting-order issue(s) found. See Console for asset paths.";
            Debug.LogError($"[UI Sorting Order] {summary}");
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("UI Sorting Order", summary, "OK");
        }

        internal static List<ValidationIssue> CollectIssues()
        {
            var issues = new List<ValidationIssue>();
            var guids = AssetDatabase.FindAssets("t:Prefab", SearchRoots);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("/Thirdparty/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    foreach (var document in root.GetComponentsInChildren<UIDocument>(true))
                        ValidateDocument(path, document, issues);
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return issues;
        }

        private static void ValidateDocument(string path, UIDocument document, ICollection<ValidationIssue> issues)
        {
            if (!TryResolveBand(document.gameObject.name, out var band))
                return;

            var order = document.sortingOrder;
            if (order >= band.Minimum && order <= band.Maximum)
                return;

            issues.Add(new ValidationIssue(
                $"[UI Sorting Order] {path} :: {GetHierarchyPath(document.transform)} " +
                $"is {order:0.##}; {band.Role} requires {band.Minimum}-{band.Maximum}.",
                document));
        }

        private static bool TryResolveBand(string objectName, out OrderBand band)
        {
            foreach (var candidate in Bands)
            {
                foreach (var token in candidate.NameTokens)
                {
                    if (objectName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    band = candidate;
                    return true;
                }
            }

            band = default;
            return false;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        internal readonly struct ValidationIssue
        {
            public ValidationIssue(string message, UnityEngine.Object context)
            {
                Message = message;
                Context = context;
            }

            public string Message { get; }
            public UnityEngine.Object Context { get; }
        }
    }
}
