using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [CreateAssetMenu(menuName = "SteamMultiRuntime/Steam Lobby Loading Splash Settings", fileName = "SteamLobbyLoadingSplashSettings")]
    public sealed class SteamLobbyLoadingSplashSettings : ScriptableObject
    {
        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private VisualTreeAsset splashLayoutAsset;
        [SerializeField] private Texture2D splashImageTexture;
        [SerializeField] private string splashMessage = "Loading...";

        public PanelSettings PanelSettings => panelSettings;
        public VisualTreeAsset SplashLayoutAsset => splashLayoutAsset;
        public Texture2D SplashImageTexture => splashImageTexture;
        public string SplashMessage => string.IsNullOrWhiteSpace(splashMessage) ? "Loading..." : splashMessage;
    }
}
