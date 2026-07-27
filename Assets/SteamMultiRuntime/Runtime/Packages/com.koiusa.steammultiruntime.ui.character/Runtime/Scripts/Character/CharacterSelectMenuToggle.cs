using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class CharacterSelectMenuToggle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterSelectUiDocument characterSelectUiDocument;

        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private InputActionBinding toggleBinding;

        private void Awake()
        {
            if (characterSelectUiDocument == null)
            {
                characterSelectUiDocument = GetComponent<CharacterSelectUiDocument>();
            }

            if (characterSelectUiDocument == null)
            {
                characterSelectUiDocument = FindFirstObjectByType<CharacterSelectUiDocument>(FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            toggleBinding = InputActionBinding.Bind(inputActionsConfig?.FindAction("UI/CharacterMenuToggle"), OnTogglePerformed);
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
            if (characterSelectUiDocument == null)
            {
                return;
            }

            var isVisible = characterSelectUiDocument.gameObject.activeSelf;
            characterSelectUiDocument.gameObject.SetActive(!isVisible);
            Cursor.visible = !isVisible;
        }

        public void Show()
        {
            if (characterSelectUiDocument == null)
            {
                return;
            }

            characterSelectUiDocument.gameObject.SetActive(true);
            Cursor.visible = true;
        }

        public void Hide()
        {
            if (characterSelectUiDocument == null)
            {
                return;
            }

            characterSelectUiDocument.gameObject.SetActive(false);
            Cursor.visible = false;
        }
    }
}
