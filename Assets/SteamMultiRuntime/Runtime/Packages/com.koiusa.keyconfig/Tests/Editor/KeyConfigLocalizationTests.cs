using System;
using System.Collections.Generic;
using Koiusa.KeyConfig;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class KeyConfigLocalizationTests
    {
        private IKeyConfigLocalizer originalProvider;
        private KeyConfigLanguage originalBuiltInLocale;

        [SetUp]
        public void SetUp()
        {
            originalProvider = KeyConfigLocalization.Provider;
            originalBuiltInLocale = KeyConfigLocalization.BuiltInLocale;
        }

        [TearDown]
        public void TearDown()
        {
            KeyConfigLocalization.BuiltInLocale = originalBuiltInLocale;
            KeyConfigLocalization.Provider = originalProvider;
        }

        [Test]
        public void BuiltInLocalizer_SwitchesStandardUiBetweenEnglishAndJapanese()
        {
            var localizer = new BuiltInKeyConfigLocalizer(KeyConfigLanguage.English);
            Assert.That(localizer.Get("keyconfig.change"), Is.EqualTo("Change"));
            Assert.That(localizer.Get("keyconfig.load"), Is.EqualTo("Load"));

            localizer.Locale = KeyConfigLanguage.Japanese;

            Assert.That(localizer.Get("keyconfig.change"), Is.EqualTo("変更"));
            Assert.That(localizer.Get("keyconfig.load"), Is.EqualTo("読込"));
        }

        [Test]
        public void ProviderNotConfigured_DoesNotExposeLocalizationKeys()
        {
            KeyConfigLocalization.Provider = null;
            KeyConfigLocalization.BuiltInLocale = KeyConfigLanguage.English;

            Assert.That(KeyConfigLocalization.Get("keyconfig.change"), Is.EqualTo("Change"));
            Assert.That(KeyConfigLocalization.Get("keyconfig.future_label"), Is.EqualTo("Future Label"));
        }

        [Test]
        public void LocaleChanged_RefreshesDynamicallyCreatedNameBinding()
        {
            var localizer = new TestLocalizer();
            KeyConfigLocalization.Provider = localizer;
            var label = new Label();
            using var binding = new LocalizedTextBinding(label);
            binding.Set("Gameplay");
            Assert.That(label.text, Is.EqualTo("Gameplay"));

            localizer.UseJapanese = true;
            localizer.NotifyLocaleChanged();

            Assert.That(label.text, Is.EqualTo("ゲームプレイ"));
        }

        private sealed class TestLocalizer : IKeyConfigLocalizer
        {
            private static readonly Dictionary<string, string> Japanese = new Dictionary<string, string>
            {
                ["Gameplay"] = "ゲームプレイ"
            };

            public bool UseJapanese { get; set; }
            public event Action LocaleChanged;
            public string Get(string key, params object[] arguments) =>
                UseJapanese && Japanese.TryGetValue(key, out var value) ? value : key;
            public bool TryResolveKey(string keyOrSource, out string key)
            {
                key = keyOrSource;
                return true;
            }
            public void NotifyLocaleChanged() => LocaleChanged?.Invoke();
        }
    }
}
