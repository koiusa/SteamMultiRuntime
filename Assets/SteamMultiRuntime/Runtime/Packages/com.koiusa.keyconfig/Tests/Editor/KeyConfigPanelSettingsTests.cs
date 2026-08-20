using System.IO;
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
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(KeyConfigPanelSettingsTests).Assembly);
            Assert.That(package, Is.Not.Null, "The test assembly must belong to com.koiusa.keyconfig.");

            var assetPath = Path.Combine(package.assetPath, PanelSettingsRelativePath).Replace('\\', '/');
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
            Assert.That(textSettings.fallbackFontAssets[0].TryAddCharacters("日本", out var missing), Is.True,
                $"The packaged fallback font must generate Japanese glyphs. Missing: {missing}");
        }
    }
}
