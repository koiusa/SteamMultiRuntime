using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Persistent switches exposed by the SteamMultiRuntime editor Tools menu.</summary>
    public static class RuntimeToolSettings
    {
        private const string FrameRateLoggingKey = "Koiusa.SteamMultiRuntime.Tools.FrameRateLogging";

        public static bool FrameRateLoggingEnabled
        {
            get => PlayerPrefs.GetInt(FrameRateLoggingKey, 0) != 0;
            set => SetBoolean(FrameRateLoggingKey, value);
        }

        private static void SetBoolean(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
