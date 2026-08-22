using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class KeyConfigPanelSettingsTests
    {
        private const string PanelSettingsRelativePath =
            "Runtime/Resources/UI/KeyConfig/KeyConfig Panel Settings.asset";

        [Test]
        public void PackagedPanelSettings_HasThemeAndJapaneseFontFallback()
        {
            var assetPath = Array.Find(
                AssetDatabase.FindAssets("t:PanelSettings KeyConfig"),
                guid => AssetDatabase.GUIDToAssetPath(guid).EndsWith(PanelSettingsRelativePath, StringComparison.Ordinal));
            assetPath = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.GUIDToAssetPath(assetPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(assetPath);
            Assert.That(panelSettings, Is.Not.Null, $"PanelSettings was not found at {assetPath}.");

            var serializedSettings = new SerializedObject(panelSettings);
            var theme = serializedSettings.FindProperty("themeUss")?.objectReferenceValue;
            var textSettings = serializedSettings.FindProperty("textSettings")?.objectReferenceValue as PanelTextSettings;

            Assert.That(theme, Is.Not.Null, "Theme Style Sheet must be package-local and resolvable.");
            Assert.That(textSettings, Is.Not.Null, "UITK Text Settings must be assigned.");
            Assert.That(textSettings.fallbackFontAssets, Is.Not.Null.And.Not.Empty,
                "UITK Text Settings must provide a Japanese-capable fallback font.");
            Assert.That(textSettings.fallbackFontAssets[0], Is.Not.Null);
            var runtimeFont = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(
                textSettings.fallbackFontAssets[0].sourceFontFile);
            Assert.That(runtimeFont, Is.Not.Null);
            try
            {
                Assert.That(runtimeFont.TryAddCharacters("日本", out var missing), Is.True,
                    $"The packaged fallback font must generate Japanese glyphs. Missing: {missing}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeFont);
            }
        }
    }
}
