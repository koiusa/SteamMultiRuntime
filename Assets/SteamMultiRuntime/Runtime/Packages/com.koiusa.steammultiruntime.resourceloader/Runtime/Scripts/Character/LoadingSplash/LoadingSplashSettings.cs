using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [CreateAssetMenu(menuName = "SteamMultiRuntime/Loading Splash Settings", fileName = "LoadingSplashSettings")]
    public sealed class LoadingSplashSettings : ScriptableObject
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
