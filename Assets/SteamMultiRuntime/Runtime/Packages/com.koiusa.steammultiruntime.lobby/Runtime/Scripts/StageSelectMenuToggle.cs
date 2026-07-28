using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// ステージ選択UIの表示・非表示を切り替えるコンポーネント。
    /// 共通Input Profileによるキー入力、およびUnityEvent / 外部コード呼び出しにも対応。
    /// </summary>
    [DisallowMultipleComponent]
    public class StageSelectMenuToggle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LocalStageSelectUIDocument stageSelectUiDocument;

        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private InputActionBinding toggleBinding;

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
            Hide();
        }

        public void Toggle()
        {
            if (stageSelectUiDocument == null)
            {
                return;
            }

            var isVisible = stageSelectUiDocument.gameObject.activeSelf;
            stageSelectUiDocument.gameObject.SetActive(!isVisible);
            Cursor.visible = !isVisible;
        }

        public void Show()
        {
            if (stageSelectUiDocument == null)
            {
                return;
            }

            stageSelectUiDocument.gameObject.SetActive(true);
            Cursor.visible = true;
        }

        public void Hide()
        {
            if (stageSelectUiDocument == null)
            {
                return;
            }

            stageSelectUiDocument.gameObject.SetActive(false);
            Cursor.visible = false;
        }
    }
}
