using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    public interface IKeyConfigLocalizer
    {
        event Action LocaleChanged;
        string Get(string key, params object[] arguments);
        bool TryResolveKey(string keyOrSource, out string key);
    }

    public static class KeyConfigLocalization
    {
        private static readonly IKeyConfigLocalizer Fallback = new FallbackLocalizer();
        private static IKeyConfigLocalizer provider = Fallback;

        public static event Action LocaleChanged;

        public static IKeyConfigLocalizer Provider
        {
            get => provider;
            set
            {
                var next = value ?? Fallback;
                if (ReferenceEquals(provider, next)) return;
                provider.LocaleChanged -= OnProviderLocaleChanged;
                provider = next;
                provider.LocaleChanged += OnProviderLocaleChanged;
                LocaleChanged?.Invoke();
            }
        }

        public static string Get(string key, params object[] arguments) => provider.Get(key, arguments);

        public static bool TryResolveKey(string keyOrSource, out string key) =>
            provider.TryResolveKey(keyOrSource, out key);

        public static void Set(TextElement element, string key, params object[] arguments)
        {
            if (element != null) element.text = Get(key, arguments);
        }

        private static void OnProviderLocaleChanged() => LocaleChanged?.Invoke();

        private sealed class FallbackLocalizer : IKeyConfigLocalizer
        {
#pragma warning disable CS0067
            public event Action LocaleChanged;
#pragma warning restore CS0067

            public string Get(string key, params object[] arguments)
            {
                if (string.IsNullOrEmpty(key)) return string.Empty;
                if (arguments == null || arguments.Length == 0) return key;
                try { return string.Format(key, arguments); }
                catch (FormatException) { return key; }
            }

            public bool TryResolveKey(string keyOrSource, out string key)
            {
                key = keyOrSource;
                return !string.IsNullOrWhiteSpace(keyOrSource);
            }
        }
    }

    internal sealed class LocalizedTextBinding : IDisposable
    {
        private readonly TextElement element;
        private string key;
        private object[] arguments = Array.Empty<object>();

        public LocalizedTextBinding(TextElement element)
        {
            this.element = element;
            KeyConfigLocalization.LocaleChanged += Refresh;
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
                element.text = KeyConfigLocalization.Get(key, arguments);
        }

        public void Dispose() => KeyConfigLocalization.LocaleChanged -= Refresh;
    }

    internal sealed class LocalizedVisualTree : IDisposable
    {
        private readonly List<(TextElement element, string key)> entries = new();

        private LocalizedVisualTree(VisualElement root, IReadOnlyCollection<TextElement> excluded)
        {
            var excludedSet = excluded == null ? null : new HashSet<TextElement>(excluded);
            foreach (var element in root.Query<TextElement>().ToList())
            {
                if (!string.IsNullOrWhiteSpace(element.text) &&
                    (excludedSet == null || !excludedSet.Contains(element)) &&
                    KeyConfigLocalization.TryResolveKey(element.text, out var key))
                    entries.Add((element, key));
            }
            KeyConfigLocalization.LocaleChanged += Refresh;
            Refresh();
        }

        public static LocalizedVisualTree Bind(VisualElement root, params TextElement[] excluded) =>
            root == null ? null : new LocalizedVisualTree(root, excluded);

        public void Refresh()
        {
            foreach (var (element, key) in entries)
                if (element != null) element.text = KeyConfigLocalization.Get(key);
        }

        public void Dispose()
        {
            KeyConfigLocalization.LocaleChanged -= Refresh;
            entries.Clear();
        }
    }
}
