using System;
using System.Threading;
using System.Threading.Tasks;
using Koiusa.Input;
using TNRD;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("sceneLoader")]
        [SerializeField] private SerializableInterface<IStageSceneCatalog> stageSceneCatalog;
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private UIDocument uiDocument;
        private StageSelectUI stageSelectUI;
        private bool isLoading;
        private LocalizedVisualTree localizedTree;
        private UiNavigationInputSession inputSession;
        private Action closeRequested;
        private CancellationTokenSource enableCancellation;

        public event Action LoadingStarted;
        public event Action LoadingFinished;

        private IStageSceneCatalog StageSceneCatalog => stageSceneCatalog?.Value;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            enableCancellation?.Dispose();
            enableCancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ResolveSceneLoader();
            BuildUI();
        }

        private void OnDisable()
        {
            enableCancellation?.Cancel();
            enableCancellation?.Dispose();
            enableCancellation = null;
            inputSession?.Dispose();
            inputSession = null;
            UnbindUI();
        }

        public void ConfigureInputActions(InputActionsConfig config)
        {
            inputActionsConfig = config;
        }

        public void ConfigureClose(Action close) => closeRequested = close;

        public void FocusInitial() => stageSelectUI?.Focus();

        private void ResolveSceneLoader()
        {
            if (StageSceneCatalog != null)
            {
                return;
            }

            // IStageSceneCatalogを実装するローダーを探す
            // 優先順: 同じGameObject → 子要素 → LocalSceneFlowLoader
            var loader = GetComponent<IStageSceneCatalog>()
                ?? GetComponentInChildren<IStageSceneCatalog>(true);

            if (loader == null)
            {
                Debug.LogWarning("LocalStageSelectUIDocument: IStageSceneCatalog not found. Stage selection will not be available.");
                return;
            }

            stageSceneCatalog = new SerializableInterface<IStageSceneCatalog>(loader);
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
            if (StageSceneCatalog != null)
            {
                stageSelectUI.PopulateStageScenes(StageSceneCatalog.CreatableStageSceneNames);
            }
            else
            {
                Debug.LogWarning("LocalStageSelectUIDocument: StageSceneCatalog is null. No stages will be populated.");
            }

            stageSelectUI.StageSelected += OnStageSelected;
            stageSelectUI.Focus();
            inputSession = new UiNavigationInputSession(
                inputActionsConfig,
                stageSelectUI.MoveSelection,
                stageSelectUI.SubmitSelection,
                Close,
                root);
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

            if (StageSceneCatalog == null)
            {
                Debug.LogError("LocalStageSelectUIDocument: StageSceneCatalog is not available.");
                return;
            }

            isLoading = true;
            LoadingStarted?.Invoke();

            try
            {
                var cancellationToken = enableCancellation?.Token ?? destroyCancellationToken;
                var loaded = await SceneLoadUtility.SwitchPresentationSceneAsync(
                    stageName,
                    StageSceneCatalog.CreatableStageSceneNames,
                    true,
                    this,
                    nameof(LocalStageSelectUIDocument),
                    cancellationToken);
                if (!loaded)
                {
                    Debug.LogError($"LocalStageSelectUIDocument: Failed to load stage '{stageName}'.");
                }
            }
            catch (OperationCanceledException)
            {
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

        private void Close()
        {
            if (closeRequested != null) closeRequested.Invoke();
            else gameObject.SetActive(false);
        }
    }
}
