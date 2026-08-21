using System;
using System.Collections.Generic;
using System.IO;
using Koiusa.SteamMultiRuntime.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Koiusa.SteamMultiRuntime.Localization.Editor
{
    public static class LocalizationTableInstaller
    {
        // Generated into the consuming project. UPM package contents can be read-only.
        private const string Root = "Assets/SteamMultiRuntimeGenerated/Localization";
        private const string TableName = GameLocalization.TableName;

        [MenuItem("Tools/SteamMultiRuntime/Maintenance/Localization/Install or Update Tables")]
        public static void Install()
        {
            if (!ValidateCatalog(out var error))
            {
                Debug.LogError("Localization catalog is invalid: " + error);
                return;
            }

            EnsureAssetFolder(Root);
            EnsureLocalizationSettings();
            var japanese = GetOrCreateLocale("ja", "Japanese");
            var english = GetOrCreateLocale("en", "English");
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName) ??
                             LocalizationEditorSettings.CreateStringTableCollection(TableName, Root);

            var japaneseResult = UpdateTable(collection, japanese, useJapanese: true);
            var englishResult = UpdateTable(collection, english, useJapanese: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            Debug.Log(
                $"Localization: installed {UiLocalizationCatalog.Entries.Count} stable UI keys for Japanese and English. " +
                $"ja: {japaneseResult.added} added / {japaneseResult.updated} updated, " +
                $"en: {englishResult.added} added / {englishResult.updated} updated. " +
                "Addressables entries were updated without replacing the consuming project's settings.");
        }

        public static void InstallBatch()
        {
            Install();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Tools/SteamMultiRuntime/Validation/Localization/Validate Installation")]
        public static void ValidateInstallation()
        {
            if (!ValidateCatalog(out var error))
            {
                Debug.LogError("Localization catalog is invalid: " + error);
                return;
            }

            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            var japanese = LocalizationEditorSettings.GetLocale("ja");
            var english = LocalizationEditorSettings.GetLocale("en");
            var missing = new List<string>();

            if (settings == null) missing.Add("active Localization Settings");
            if (japanese == null) missing.Add("ja locale");
            if (english == null) missing.Add("en locale");
            if (collection == null) missing.Add($"'{TableName}' string table collection");

            if (collection != null)
            {
                ValidateTable(collection.GetTable("ja") as StringTable, "ja", missing);
                ValidateTable(collection.GetTable("en") as StringTable, "en", missing);
            }

            if (missing.Count == 0)
                Debug.Log("Localization installation is valid.");
            else
                Debug.LogError("Localization installation is incomplete:\n- " + string.Join("\n- ", missing));
        }

        private static void EnsureLocalizationSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "SteamMultiRuntime Localization Settings";
            AssetDatabase.CreateAsset(settings, Root + "/LocalizationSettings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }

        private static Locale GetOrCreateLocale(string code, string displayName)
        {
            var locale = LocalizationEditorSettings.GetLocale(code);
            if (locale != null) return locale;
            locale = Locale.CreateLocale(code);
            locale.name = displayName + " (" + code + ")";
            AssetDatabase.CreateAsset(locale, $"{Root}/{code}.asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static (int added, int updated) UpdateTable(
            StringTableCollection collection,
            Locale locale,
            bool useJapanese)
        {
            var table = collection.GetTable(locale.Identifier) as StringTable;
            if (table == null)
                table = collection.AddNewTable(locale.Identifier) as StringTable;

            var added = 0;
            var updated = 0;
            foreach (var entry in UiLocalizationCatalog.Entries)
            {
                var expected = useJapanese ? entry.Japanese : entry.English;
                var tableEntry = table.GetEntry(entry.Key);
                if (tableEntry == null)
                {
                    table.AddEntry(entry.Key, expected);
                    added++;
                }
                else if (!string.Equals(tableEntry.Value, expected, StringComparison.Ordinal))
                {
                    tableEntry.Value = expected;
                    updated++;
                }
            }

            LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            return (added, updated);
        }

        private static bool ValidateCatalog(out string error)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in UiLocalizationCatalog.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || !keys.Add(entry.Key))
                {
                    error = $"Empty or duplicate key '{entry.Key}'.";
                    return false;
                }
                if (string.IsNullOrEmpty(entry.Japanese) || string.IsNullOrEmpty(entry.English))
                {
                    error = $"Key '{entry.Key}' has an empty translation.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static void ValidateTable(StringTable table, string localeCode, ICollection<string> missing)
        {
            if (table == null)
            {
                missing.Add($"{localeCode} UI table");
                return;
            }
            foreach (var entry in UiLocalizationCatalog.Entries)
            {
                var tableEntry = table.GetEntry(entry.Key);
                if (tableEntry == null)
                    missing.Add($"{localeCode}:{entry.Key}");
                else
                {
                    var expected = localeCode == "ja" ? entry.Japanese : entry.English;
                    if (!string.Equals(tableEntry.Value, expected, StringComparison.Ordinal))
                        missing.Add($"{localeCode}:{entry.Key} has stale text");
                }
            }
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
