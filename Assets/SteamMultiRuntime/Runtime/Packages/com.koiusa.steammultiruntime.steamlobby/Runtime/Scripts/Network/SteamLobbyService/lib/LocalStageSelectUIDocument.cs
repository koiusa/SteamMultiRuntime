using System;
using System.Threading.Tasks;
using TNRD;
using UnityEngine;
using UnityEngine.UIElements;
using Koiusa.SteamMultiRuntime.Network;
using Koiusa.SteamMultiRuntime.Localization;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Local版ステージ選択UIドキュメント
    /// UIToolkitを使用してステージを選択・ロードする
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class LocalStageSelectUIDocument : MonoBehaviour, ILoadingSplashEventSource
    {
        [SerializeField] private StageSelectUiAssets uiAssets;
        [SerializeField] private SerializableInterface<ISteamLobbySceneLoader> sceneLoader;

        private UIDocument uiDocument;
        private StageSelectUI stageSelectUI;
        private bool isLoading;
        private LocalizedVisualTree localizedTree;

        public event Action LoadingStarted;
        public event Action LoadingFinished;

        private ISteamLobbySceneLoader SceneLoader => sceneLoader?.Value;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            ResolveSceneLoader();
            BuildUI();
        }

        private void OnDisable()
        {
            UnbindUI();
        }

        private void ResolveSceneLoader()
        {
            if (SceneLoader != null)
            {
                return;
            }

            // ISteamLobbySceneLoaderを実装するローダーを探す
            // 優先順: 同じGameObject → 子要素 → LocalSceneFlowLoader
            var loader = GetComponent<ISteamLobbySceneLoader>()
                ?? GetComponentInChildren<ISteamLobbySceneLoader>(true)
                ?? FindFirstObjectByType<LocalSceneFlowLoader>(FindObjectsInactive.Include) as ISteamLobbySceneLoader;

            if (loader == null)
            {
                Debug.LogWarning("LocalStageSelectUIDocument: ISteamLobbySceneLoader not found. Stage selection will not be available.");
                return;
            }

            sceneLoader = new SerializableInterface<ISteamLobbySceneLoader>(loader);
            Debug.Log($"LocalStageSelectUIDocument: Found {loader.GetType().Name} as SceneLoader");
        }

        private void BuildUI()
        {
            if (uiDocument == null)
            {
                Debug.LogError("LocalStageSelectUIDocument: UIDocument is not assigned.");
                return;
            }

            // UIAssetsを確保
            if (uiAssets == null)
            {
                uiAssets = StageSelectUiAssets.GetDefault();
            }

            if (uiAssets == null)
            {
                Debug.LogWarning("LocalStageSelectUIDocument: StageSelectUiAssets not found. UI will not be rendered.");
                return;
            }

            // デフォルトアセットを読み込み
            uiAssets.EnsureDefaultsLoaded();

            // UXMLをアセットから取得
            var layoutAsset = uiAssets.LayoutAsset;
            if (layoutAsset == null)
            {
                Debug.LogWarning("LocalStageSelectUIDocument: Layout asset not available. UI will not be rendered.");
                return;
            }

            var root = uiDocument.rootVisualElement;
            root.Clear();
            layoutAsset.CloneTree(root);

            // Themeを適用
            uiAssets.ApplyToDocument(uiDocument);
            localizedTree?.Dispose();
            localizedTree = LocalizedVisualTree.Bind(uiDocument.rootVisualElement);

            // StageSelectUIを初期化
            stageSelectUI = new StageSelectUI(uiDocument);
            stageSelectUI.Build("stage-scene-field");

            // ステージ一覧をUIに反映（StageSelected購読前に行うことで初期値セット時のイベント発火を防ぐ）
            if (SceneLoader != null)
            {
                stageSelectUI.PopulateStageScenes(SceneLoader.CreatableStageSceneNames);
            }
            else
            {
                Debug.LogWarning("LocalStageSelectUIDocument: SceneLoader is null. No stages will be populated.");
            }

            stageSelectUI.StageSelected += OnStageSelected;
            stageSelectUI.Focus();
        }

        private void UnbindUI()
        {
            localizedTree?.Dispose();
            localizedTree = null;
            if (stageSelectUI != null)
            {
                stageSelectUI.StageSelected -= OnStageSelected;
                stageSelectUI.Cleanup();
            }
        }

        private async void OnStageSelected(string stageName)
        {
            if (isLoading)
            {
                Debug.LogWarning("LocalStageSelectUIDocument: Scene is already loading.");
                return;
            }

            if (SceneLoader == null)
            {
                Debug.LogError("LocalStageSelectUIDocument: SceneLoader is not available.");
                return;
            }

            isLoading = true;
            LoadingStarted?.Invoke();

            try
            {
                var loaded = await SceneLoadUtility.SwitchPresentationSceneAsync(
                    stageName,
                    SceneLoader.CreatableStageSceneNames,
                    true,
                    this,
                    nameof(LocalStageSelectUIDocument));
                if (!loaded)
                {
                    Debug.LogError($"LocalStageSelectUIDocument: Failed to load stage '{stageName}'.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"LocalStageSelectUIDocument: Failed to load stage '{stageName}': {ex.Message}");
            }
            finally
            {
                isLoading = false;
                LoadingFinished?.Invoke();
            }
        }

        /// <summary>
        /// 現在選択されているステージ名を取得
        /// </summary>
        public string GetSelectedStageName() => stageSelectUI?.SelectedStageName ?? string.Empty;

        /// <summary>
        /// 指定されたステージを選択
        /// </summary>
        public bool TrySelectStage(string stageName) => stageSelectUI?.TrySelectStage(stageName) ?? false;
    }
}
