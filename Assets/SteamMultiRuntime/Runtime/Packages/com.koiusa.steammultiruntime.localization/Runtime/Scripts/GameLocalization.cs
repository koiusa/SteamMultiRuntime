using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.Localization
{
    /// <summary>Shared access point for the project's "UI" string table.</summary>
    public static class GameLocalization
    {
        public const string TableName = "SteamMultiRuntime UI";
        public const string PlayerPrefsKey = "game.locale";

        public static event Action LocaleChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // A UPM package cannot ship a host project's active settings. Until the
            // installer has generated them, keep the UI usable with its source text.
            if (!LocalizationSettings.HasSettings) return;
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            LocalizationSettings.InitializationOperation.Completed += operation =>
            {
                if (operation.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    return;
                RestoreLocale();
                LocaleChanged?.Invoke();
            };
        }

        public static string Get(string key, params object[] arguments)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (!LocalizationSettings.HasSettings)
                return FormatFallback(key, arguments);

            // GetLocalizedString is synchronous and internally uses WaitForCompletion.
            // Never call it while Addressables/Localization is still initializing;
            // doing so once per UI element can stall the Unity main thread.
            var initialization = LocalizationSettings.InitializationOperation;
            if (!initialization.IsDone ||
                initialization.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ||
                LocalizationSettings.SelectedLocale == null)
                return FormatFallback(key, arguments);
            try
            {
                var value = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key, arguments);
                return string.IsNullOrEmpty(value) ? FormatFallback(key, arguments) : value;
            }
            catch (Exception)
            {
                return FormatFallback(key, arguments);
            }
        }

        public static bool SelectLocale(string code)
        {
            if (!LocalizationSettings.HasSettings) return false;
            var initialization = LocalizationSettings.InitializationOperation;
            if (!initialization.IsDone ||
                initialization.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                return false;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null) return false;
            LocalizationSettings.SelectedLocale = locale;
            return true;
        }

        public static void Set(TextElement element, string key, params object[] arguments)
        {
            if (element != null) element.text = Get(key, arguments);
        }

        private static void RestoreLocale()
        {
            var saved = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(saved)) SelectLocale(saved);
        }

        private static string FormatFallback(string key, object[] arguments)
        {
            var fallback = UiLocalizationCatalog.GetJapaneseFallback(key);
            if (arguments == null || arguments.Length == 0) return fallback;
            try { return string.Format(fallback, arguments); }
            catch (FormatException) { return fallback; }
        }

        private static void OnSelectedLocaleChanged(Locale locale)
        {
            if (locale != null)
            {
                PlayerPrefs.SetString(PlayerPrefsKey, locale.Identifier.Code);
                PlayerPrefs.Save();
            }
            LocaleChanged?.Invoke();
        }
    }

    /// <summary>Keeps a dynamic UI Toolkit text property synchronized with locale changes.</summary>
    public sealed class LocalizedTextBinding : IDisposable
    {
        private readonly TextElement element;
        private string key;
        private object[] arguments = Array.Empty<object>();

        public LocalizedTextBinding(TextElement element)
        {
            this.element = element;
            GameLocalization.LocaleChanged += Refresh;
        }

        public void Set(string localizationKey, params object[] formatArguments)
        {
            key = localizationKey;
            arguments = formatArguments ?? Array.Empty<object>();
            Refresh();
        }

        public void Clear()
        {
            key = string.Empty;
            arguments = Array.Empty<object>();
            if (element != null) element.text = string.Empty;
        }

        public void Refresh()
        {
            if (element != null && !string.IsNullOrEmpty(key))
                element.text = GameLocalization.Get(key, arguments);
        }

        public void Dispose() => GameLocalization.LocaleChanged -= Refresh;
    }

    /// <summary>Localizes static Label/Button text in a UI Toolkit tree and refreshes it on locale changes.</summary>
    public sealed class LocalizedVisualTree : IDisposable
    {
        private readonly List<(TextElement element, string key)> entries = new();

        private LocalizedVisualTree(VisualElement root, IReadOnlyCollection<TextElement> excluded)
        {
            var excludedSet = excluded == null ? null : new HashSet<TextElement>(excluded);
            foreach (var element in root.Query<TextElement>().ToList())
            {
                if (!string.IsNullOrWhiteSpace(element.text) && (excludedSet == null || !excludedSet.Contains(element)))
                    entries.Add((element, UiLocalizationCatalog.ResolveKey(element.text)));
            }
            GameLocalization.LocaleChanged += Refresh;
            Refresh();
        }

        public static LocalizedVisualTree Bind(VisualElement root, params TextElement[] excluded) =>
            root == null ? null : new LocalizedVisualTree(root, excluded);

        public void Refresh()
        {
            foreach (var (element, key) in entries)
                if (element != null) element.text = GameLocalization.Get(key);
        }

        public void Dispose()
        {
            GameLocalization.LocaleChanged -= Refresh;
            entries.Clear();
        }
    }
}
