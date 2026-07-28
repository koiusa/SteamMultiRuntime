using System;
using Koiusa.Keyconfig.Runtime;
using Koiusa.SteamMultiRuntime.Localization;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Keyconfig
{
    public sealed class SteamMultiRuntimeKeyConfigLocalizer : IKeyConfigLocalizer
    {
        public event Action LocaleChanged
        {
            add => GameLocalization.LocaleChanged += value;
            remove => GameLocalization.LocaleChanged -= value;
        }

        public string Get(string key, params object[] arguments) => GameLocalization.Get(key, arguments);

        public bool TryResolveKey(string keyOrSource, out string key) =>
            UiLocalizationCatalog.TryResolveKey(keyOrSource, out key);
    }

    internal static class SteamMultiRuntimeKeyConfigBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            KeyConfigLocalization.Provider = new SteamMultiRuntimeKeyConfigLocalizer();
        }
    }
}
