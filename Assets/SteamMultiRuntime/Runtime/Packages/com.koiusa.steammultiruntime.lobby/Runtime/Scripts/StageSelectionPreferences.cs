using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Persists the most recently selected stage across UI rebuilds and sessions.</summary>
    public static class StageSelectionPreferences
    {
        private const string PlayerPrefsKey = "SteamMultiRuntime.SelectedStageScene";

        public static string SelectedStageName => PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);

        public static void Save(string stageName)
        {
            if (string.IsNullOrWhiteSpace(stageName))
            {
                return;
            }

            PlayerPrefs.SetString(PlayerPrefsKey, stageName);
            PlayerPrefs.Save();
        }
    }
}
