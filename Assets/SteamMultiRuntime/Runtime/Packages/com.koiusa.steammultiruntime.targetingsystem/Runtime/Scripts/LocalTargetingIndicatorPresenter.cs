using Koiusa.TargetingSystem.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.TargetingSystem
{
    [DisallowMultipleComponent]
    public sealed class LocalTargetingIndicatorPresenter : MonoBehaviour
    {
        private const string PanelSettingsPath = "UI/TargetIndicator Panel Settings";
        private const string VisualTreePath = "UI/TargetIndicator";

        private static LocalTargetingIndicatorPresenter instance;
        private GameObject indicatorObject;
        private TargetIndicatorController indicator;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || instance != null)
                return;

            instance = FindFirstObjectByType<LocalTargetingIndicatorPresenter>(FindObjectsInactive.Include);
            if (instance != null)
            {
                instance.RestoreRuntime();
                return;
            }

            var host = new GameObject("Local Target Indicator Presenter");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<LocalTargetingIndicatorPresenter>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnEnable() => RestoreRuntime();

        private void OnDisable()
        {
            LocalTargetingControllerRegistry.CurrentChanged -= OnControllerChanged;
            SetController(null);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void RestoreRuntime()
        {
            LocalTargetingControllerRegistry.CurrentChanged -= OnControllerChanged;
            LocalTargetingControllerRegistry.CurrentChanged += OnControllerChanged;
            OnControllerChanged(LocalTargetingControllerRegistry.Current);
        }

        private void OnControllerChanged(TargetingController controller) => SetController(controller);

        private void SetController(TargetingController controller)
        {
            if (controller == null)
            {
                indicator?.SetController(null);
                if (indicatorObject != null)
                    indicatorObject.SetActive(false);
                return;
            }

            EnsureIndicator();
            indicator.SetController(controller);
            indicatorObject.SetActive(true);
        }

        private void EnsureIndicator()
        {
            if (indicator != null)
                return;

            indicatorObject = new GameObject("Target Indicator UI");
            indicatorObject.transform.SetParent(transform, false);
            indicatorObject.SetActive(false);

            var document = indicatorObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>(VisualTreePath);
            document.sortingOrder = short.MaxValue - 2;

            var theme = indicatorObject.AddComponent<TargetIndicatorThemeProvider>();
            theme.Configure(
                Resources.Load<VisualTreeAsset>(VisualTreePath),
                Resources.Load<StyleSheet>(VisualTreePath));

            indicator = indicatorObject.AddComponent<TargetIndicatorController>();
        }
    }
}
