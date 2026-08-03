using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    internal static class RuntimeToolSettingsMenu
    {
        private const string FrameRateMenuPath =
            "Tools/SteamMultiRuntime/Diagnostics/FPS Logging";

        [MenuItem(FrameRateMenuPath)]
        private static void ToggleFrameRateLogging()
        {
            RuntimeToolSettings.FrameRateLoggingEnabled = !RuntimeToolSettings.FrameRateLoggingEnabled;
            RuntimeFrameRateLogging.Refresh();
            Debug.Log($"[RuntimeTools] FPS logging={(RuntimeToolSettings.FrameRateLoggingEnabled ? "ON" : "OFF")}");
        }

        [MenuItem(FrameRateMenuPath, true)]
        private static bool ValidateFrameRateLogging()
        {
            Menu.SetChecked(FrameRateMenuPath, RuntimeToolSettings.FrameRateLoggingEnabled);
            return true;
        }

    }
}
