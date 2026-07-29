using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Koiusa.UI.Common;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// ステージ選択UIの表示・非表示を切り替えるコンポーネント。
    /// 共通Input Profileによるキー入力、およびUnityEvent / 外部コード呼び出しにも対応。
    /// </summary>
    [DisallowMultipleComponent]
    public class StageSelectMenuToggle : MonoBehaviour, IUiMenu
    {
        [Header("References")]
        [SerializeField] private LocalStageSelectUIDocument stageSelectUiDocument;

        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private InputActionBinding toggleBinding;

        public bool IsVisible => stageSelectUiDocument != null && stageSelectUiDocument.gameObject.activeSelf;

        private void Awake()
        {
            if (stageSelectUiDocument == null)
            {
                stageSelectUiDocument = GetComponent<LocalStageSelectUIDocument>();
            }

            if (stageSelectUiDocument == null)
            {
                stageSelectUiDocument = FindFirstObjectByType<LocalStageSelectUIDocument>(FindObjectsInactive.Include);
            }

            stageSelectUiDocument?.ConfigureInputActions(inputActionsConfig);
            stageSelectUiDocument?.ConfigureClose(() => UiMenuNavigator.Back(this));
        }

        private void OnEnable()
        {
            var action = inputActionsConfig?.FindAction("System/DebugSessionMenuToggle");
            toggleBinding = InputActionBinding.Bind(action, OnTogglePerformed);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            toggleBinding?.Dispose();
            toggleBinding = null;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            Toggle();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            UiMenuNavigator.CloseAll();
        }

        public void Toggle()
        {
            UiMenuNavigator.ToggleRoot(this);
        }

        public void Show() => UiMenuNavigator.OpenRoot(this);

        public void Hide() => UiMenuNavigator.Close(this);

        public void Activate()
        {
            if (stageSelectUiDocument == null)
            {
                return;
            }

            stageSelectUiDocument.ConfigureInputActions(inputActionsConfig);
            stageSelectUiDocument.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (stageSelectUiDocument == null)
            {
                return;
            }

            stageSelectUiDocument.gameObject.SetActive(false);
        }

        public void FocusInitial() => stageSelectUiDocument?.FocusInitial();
    }
}
