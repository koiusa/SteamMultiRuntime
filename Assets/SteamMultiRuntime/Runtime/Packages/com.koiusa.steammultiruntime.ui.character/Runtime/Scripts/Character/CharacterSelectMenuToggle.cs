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
        [SerializeField] private InputActionReference toggleAction;

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
            if (toggleAction != null)
            {
                toggleAction.action.Enable();
                toggleAction.action.performed += OnTogglePerformed;
            }
        }

        private void OnDisable()
        {
            if (toggleAction != null)
            {
                toggleAction.action.performed -= OnTogglePerformed;
                toggleAction.action.Disable();
            }
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
