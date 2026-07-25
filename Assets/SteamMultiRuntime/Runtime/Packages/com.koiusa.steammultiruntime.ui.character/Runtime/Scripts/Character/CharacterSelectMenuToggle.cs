using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

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
            toggleBinding = InputActionBinding.Bind(inputActionsConfig?.FindAction("Player/Previous"), OnTogglePerformed);
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
            if (characterSelectUiDocument == null)
            {
                return;
            }

            var isVisible = characterSelectUiDocument.gameObject.activeSelf;
            characterSelectUiDocument.gameObject.SetActive(!isVisible);
        }

        public void Show()
        {
            if (characterSelectUiDocument == null)
            {
                return;
            }

            characterSelectUiDocument.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (characterSelectUiDocument == null)
            {
                return;
            }

            characterSelectUiDocument.gameObject.SetActive(false);
        }
    }
}
