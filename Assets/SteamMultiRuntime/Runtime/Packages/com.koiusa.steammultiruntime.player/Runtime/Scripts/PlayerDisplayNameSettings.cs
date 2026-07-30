using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerIdentitySource
    {
        bool IsAvailable { get; }
        ulong? PlayerId { get; }
        string DisplayName { get; }
    }

    public interface IPlayerDisplayNameSource : IPlayerIdentitySource
    {
    }

    public interface IPlayerDisplayNameNotifier
    {
        event Action DisplayNameChanged;
    }

    public static class PlayerDisplayNameSettings
    {
        private const string PlayerPrefsKey = "SteamMultiRuntime.PlayerDisplayName";
        public const int MaxLength = 24;
        private static Func<string> platformDisplayNameResolver;

        public static Func<string> PlatformDisplayNameResolver
        {
            get => platformDisplayNameResolver;
            set
            {
                if (platformDisplayNameResolver == value)
                    return;

                platformDisplayNameResolver = value;
                DisplayNameChanged?.Invoke();
            }
        }
        public static event Action DisplayNameChanged;

        public static string CustomDisplayName => PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);

        public static void SetCustomDisplayName(string displayName)
        {
            var sanitized = Sanitize(displayName);
            if (string.IsNullOrEmpty(sanitized))
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
            else
                PlayerPrefs.SetString(PlayerPrefsKey, sanitized);

            PlayerPrefs.Save();
            DisplayNameChanged?.Invoke();
        }

        public static string ResolveLocalDisplayName()
        {
            var customName = Sanitize(CustomDisplayName);
            if (!string.IsNullOrEmpty(customName))
                return customName;

            var platformName = Sanitize(PlatformDisplayNameResolver?.Invoke());
            return string.IsNullOrEmpty(platformName) ? "Player" : platformName;
        }

        public static string Sanitize(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return string.Empty;

            var trimmed = displayName.Trim().Replace("<", string.Empty).Replace(">", string.Empty);
            return trimmed.Length <= MaxLength ? trimmed : trimmed.Substring(0, MaxLength);
        }
    }
}
