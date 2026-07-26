using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

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
            toggleBinding = InputActionBinding.Bind(inputActionsConfig?.FindAction("Player/Interact"), OnTogglePerformed);
        }

        private void OnDisable()
        {
            toggleBinding?.Dispose();
            toggleBinding = null;
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            Toggle();
        }

        public void Toggle()
        {
            if (stageSelectUiDocument == null)
            {
                return;
            }

            var isVisible = stageSelectUiDocument.gameObject.activeSelf;
            stageSelectUiDocument.gameObject.SetActive(!isVisible);
        }

        public void Show()
        {
            if (stageSelectUiDocument == null)
            {
                return;
            }

            stageSelectUiDocument.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (stageSelectUiDocument == null)
            {
                return;
            }

            stageSelectUiDocument.gameObject.SetActive(false);
        }
    }
}
