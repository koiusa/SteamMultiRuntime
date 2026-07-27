using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.UI.Common
{
    public static class CommonUiTheme
    {
        private const string ThemeResourcePath = "UI/Common/SteamMultiRuntimeTheme";
        private const string ScrollViewResourcePath = "UI/Common/SteamMultiRuntimeScrollView";
        private const string ThemeClassName = "smr-theme";

        private static StyleSheet themeStyleSheet;
        private static StyleSheet scrollViewStyleSheet;

        public static void Apply(VisualElement root)
        {
            if (root == null) return;

            themeStyleSheet ??= Resources.Load<StyleSheet>(ThemeResourcePath);
            scrollViewStyleSheet ??= Resources.Load<StyleSheet>(ScrollViewResourcePath);
            AddIfMissing(root, themeStyleSheet);
            AddIfMissing(root, scrollViewStyleSheet);
            root.AddToClassList(ThemeClassName);
        }

        private static void AddIfMissing(VisualElement root, StyleSheet styleSheet)
        {
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);
        }
    }
}
