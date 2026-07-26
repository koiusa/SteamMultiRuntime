using System;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Assigns distinct Unity component icons to traversal features and actions
    /// so that a GameObject with many components remains easy to scan.
    /// </summary>
    [InitializeOnLoad]
    internal static class TraversalComponentHeader
    {
        private static readonly Texture2D FeatureIcon = CreateFeatureIcon();
        private static readonly Texture2D ActionIcon = CreateActionIcon();
        private static readonly Texture2D SkillIcon = CreateSkillIcon();

        static TraversalComponentHeader()
        {
            EditorApplication.delayCall += ApplyIconsToLoadedComponents;
            EditorApplication.hierarchyChanged -= ApplyIconsToLoadedComponents;
            EditorApplication.hierarchyChanged += ApplyIconsToLoadedComponents;

            // This also covers prefab-mode and locked Inspector targets which may
            // not be returned by a hierarchy scan at initialization time.
            Editor.finishedDefaultHeaderGUI -= ApplyIconToInspectedComponent;
            Editor.finishedDefaultHeaderGUI += ApplyIconToInspectedComponent;
        }

        private static void ApplyIconToInspectedComponent(UnityEditor.Editor editor)
        {
            if (editor.target is MonoBehaviour component)
            {
                ApplyIcon(component);
            }
        }

        private static void ApplyIconsToLoadedComponents()
        {
            foreach (var component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                ApplyIcon(component);
            }
        }

        private static void ApplyIcon(MonoBehaviour component)
        {
            if (component == null || !TryGetIcon(component.GetType(), out var icon))
            {
                return;
            }

            if (EditorGUIUtility.GetIconForObject(component) != icon)
            {
                EditorGUIUtility.SetIconForObject(component, icon);
            }
        }

        private static bool TryGetIcon(Type componentType, out Texture2D icon)
        {
            // Limit the convention to this package so unrelated *Action components
            // (for example Input System components) do not receive a traversal badge.
            if (componentType.Namespace != typeof(PlayerCompositeMotor).Namespace)
            {
                icon = null;
                return false;
            }

            if (componentType.Name.EndsWith("SkillFeature", StringComparison.Ordinal))
            {
                icon = SkillIcon;
                return true;
            }

            if (componentType.Name.EndsWith("Feature", StringComparison.Ordinal))
            {
                icon = FeatureIcon;
                return true;
            }

            if (componentType.Name.EndsWith("Action", StringComparison.Ordinal))
            {
                icon = ActionIcon;
                return true;
            }

            icon = null;
            return false;
        }

        private static Texture2D CreateFeatureIcon()
        {
            var blue = new Color32(48, 156, 255, 255);
            return CreateIcon("Traversal Feature", (x, y) =>
            {
                // Gear silhouette: shared systems/configuration at a glance.
                var dx = x - 7.5f;
                var dy = y - 7.5f;
                var radiusSquared = dx * dx + dy * dy;
                var ring = radiusSquared >= 8f && radiusSquared <= 30f;
                var teeth = (x >= 6 && x <= 9 && (y <= 3 || y >= 12)) ||
                            (y >= 6 && y <= 9 && (x <= 3 || x >= 12));
                return ring || teeth ? blue : default;
            });
        }

        private static Texture2D CreateActionIcon()
        {
            var orange = new Color32(255, 151, 35, 255);
            return CreateIcon("Traversal Action", (x, y) =>
            {
                // Bold lightning bolt: an executable movement/action.
                var upper = y >= 7 && x >= 7 - (y - 7) / 2 && x <= 10;
                var lower = y <= 8 && x >= 5 && x <= 8 - (7 - y) / 2;
                var bridge = y >= 6 && y <= 9 && x >= 4 && x <= 11;
                return upper || lower || bridge ? orange : default;
            });
        }

        private static Texture2D CreateSkillIcon()
        {
            var purple = new Color32(190, 92, 255, 255);
            return CreateIcon("Player Skill", (x, y) =>
            {
                // Four-point star: a player-triggered special ability.
                var dx = Mathf.Abs(x - 7.5f);
                var dy = Mathf.Abs(y - 7.5f);
                return dx + dy <= 6f && (dx <= 2.5f || dy <= 2.5f) ? purple : default;
            });
        }

        private static Texture2D CreateIcon(string name, Func<int, int, Color32> drawPixel)
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                pixels[y * size + x] = drawPixel(x, y);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
