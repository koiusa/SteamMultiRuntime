using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// ステージ選択UI用アセット管理
    /// UXML/USSなどのUIアセットをScriptableObjectで一元管理
    /// </summary>
    [CreateAssetMenu(menuName = "SteamMultiRuntime/Stage Select UI Assets", fileName = "StageSelectUiAssets")]
    public sealed class StageSelectUiAssets : ScriptableObject
    {
        private const string DefaultLayoutResourcePath = "UI/StageSelect/LocalStageSelect";
        private const string DefaultThemeStyleSheetResourcePath = "UI/StageSelect/LocalStageSelectTheme";

        [Header("Layout")]
        [SerializeField] private VisualTreeAsset layoutAsset;

        [Header("Theme")]
        [SerializeField] private StyleSheet themeStyleSheet;

        public VisualTreeAsset LayoutAsset => layoutAsset;
        public StyleSheet ThemeStyleSheet => themeStyleSheet;

        /// <summary>
        /// デフォルトアセットがまだ設定されていない場合、Resourcesから自動読み込み
        /// </summary>
        public void EnsureDefaultsLoaded()
        {
            if (layoutAsset == null)
            {
                layoutAsset = Resources.Load<VisualTreeAsset>(DefaultLayoutResourcePath);
                if (layoutAsset == null)
                {
                    Debug.LogWarning($"StageSelectUiAssets: Failed to load default layout from '{DefaultLayoutResourcePath}'");
                }
            }

            if (themeStyleSheet == null)
            {
                themeStyleSheet = Resources.Load<StyleSheet>(DefaultThemeStyleSheetResourcePath);
                if (themeStyleSheet == null)
                {
                    Debug.LogWarning($"StageSelectUiAssets: Failed to load default theme from '{DefaultThemeStyleSheetResourcePath}'");
                }
            }
        }

        /// <summary>
        /// UIDocumentに対してUIアセットを適用
        /// </summary>
        public void ApplyToDocument(UIDocument uiDocument)
        {
            if (uiDocument == null)
            {
                Debug.LogError("StageSelectUiAssets: UIDocument is null");
                return;
            }

            EnsureDefaultsLoaded();

            var root = uiDocument.rootVisualElement;
            root.Clear();

            // レイアウトを適用
            if (layoutAsset != null)
            {
                layoutAsset.CloneTree(root);
            }
            else
            {
                Debug.LogError("StageSelectUiAssets: Layout asset is not available");
                return;
            }

            // テーマスタイルシートを適用
            if (themeStyleSheet != null && !root.styleSheets.Contains(themeStyleSheet))
            {
                root.styleSheets.Add(themeStyleSheet);
            }
        }

        #region Factory Methods

        /// <summary>
        /// デフォルトのStageSelectUiAssetsをResources/UI/StageSelect/から取得
        /// </summary>
        public static StageSelectUiAssets GetDefault()
        {
            var asset = Resources.Load<StageSelectUiAssets>("UI/StageSelect/StageSelectUiAssets");
            if (asset == null)
            {
                Debug.LogWarning("StageSelectUiAssets: Default asset not found in Resources. Creating temporary instance.");
                asset = CreateInstance<StageSelectUiAssets>();
                asset.EnsureDefaultsLoaded();
            }
            return asset;
        }

        #endregion
    }
}
