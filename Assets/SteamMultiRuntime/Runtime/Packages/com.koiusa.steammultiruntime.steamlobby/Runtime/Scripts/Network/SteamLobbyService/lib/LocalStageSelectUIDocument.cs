using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TNRD;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Koiusa.SteamMultiRuntime.Network;

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
        private string previouslyLoadedStageName = string.Empty;

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
            // 優先順: 同じGameObject → 子要素 → SteamLobbySceneLoader → SteamLobbyDedicatedServer → LocalSceneFlowLoader
            var loader = GetComponent<ISteamLobbySceneLoader>()
                ?? GetComponentInChildren<ISteamLobbySceneLoader>(true)
                ?? FindFirstObjectByType<SteamLobbySceneLoader>() as ISteamLobbySceneLoader
                ?? FindFirstObjectByType<Network.SteamLobbyDedicatedServer>(FindObjectsInactive.Include) as ISteamLobbySceneLoader
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

            // StageSelectUIを初期化
            stageSelectUI = new StageSelectUI(uiDocument);
            stageSelectUI.Build("stage-scene-field");
            stageSelectUI.StageSelected += OnStageSelected;

            // ステージ一覧をUIに反映
            if (SceneLoader != null)
            {
                stageSelectUI.PopulateStageScenes(SceneLoader.CreatableStageSceneNames);
            }
            else
            {
                Debug.LogWarning("LocalStageSelectUIDocument: SceneLoader is null. No stages will be populated.");
            }
        }

        private void UnbindUI()
        {
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
                // 前のシーンをアンロード
                if (!string.IsNullOrEmpty(previouslyLoadedStageName))
                {
                    await UnloadPreviousSceneAsync();
                }

                // 新しいシーンを読み込む
                await LoadStageSceneAsync(stageName);
                previouslyLoadedStageName = stageName;
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

        private async Task UnloadPreviousSceneAsync()
        {
            if (string.IsNullOrEmpty(previouslyLoadedStageName))
            {
                return;
            }

            var scene = SceneManager.GetSceneByName(previouslyLoadedStageName);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"LocalStageSelectUIDocument: Scene '{previouslyLoadedStageName}' not found to unload.");
                return;
            }

            var asyncOp = SceneManager.UnloadSceneAsync(scene);
            if (asyncOp == null)
            {
                Debug.LogWarning($"LocalStageSelectUIDocument: Failed to start unloading scene '{previouslyLoadedStageName}'");
                return;
            }

            while (!asyncOp.isDone)
            {
                await Task.Yield();
            }

            Debug.Log($"LocalStageSelectUIDocument: Scene '{previouslyLoadedStageName}' unloaded successfully.");
        }

        private async Task LoadStageSceneAsync(string stageName)
        {
            // シーンを追加的にロードする
            var asyncOp = SceneManager.LoadSceneAsync(stageName, LoadSceneMode.Additive);

            if (asyncOp == null)
            {
                throw new InvalidOperationException($"Failed to start loading scene '{stageName}'");
            }

            // シーンロード完了を待機
            while (!asyncOp.isDone)
            {
                await Task.Yield();
            }

            var loadedScene = SceneManager.GetSceneByName(stageName);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                Debug.LogWarning($"LocalStageSelectUIDocument: Loaded scene '{stageName}' could not be set as active.");
                return;
            }

            // ロードしたシーンのカメラを無効化する
            SceneLoadUtility.ApplyLoadedSceneCameraSettings(loadedScene, disableCamerasInLoadedScenes: true);

            // ロードしたシーンをアクティブに設定する
            // Instantiate はアクティブシーンに生成するため、NPCなどが正しいシーンに配置される
            SceneManager.SetActiveScene(loadedScene);
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
