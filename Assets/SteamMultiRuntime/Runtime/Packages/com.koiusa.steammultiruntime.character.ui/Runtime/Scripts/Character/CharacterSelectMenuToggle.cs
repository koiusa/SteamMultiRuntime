using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Koiusa.UI.Common;

namespace Koiusa.SteamMultiRuntime.Character.UI
{
    [DisallowMultipleComponent]
    public class CharacterSelectMenuToggle : MonoBehaviour, IUiMenu
    {
        [Header("References")]
        [SerializeField] private CharacterSelectUiDocument characterSelectUiDocument;

        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private InputActionBinding toggleBinding;

        public bool IsVisible => characterSelectUiDocument != null && characterSelectUiDocument.gameObject.activeSelf;

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

            characterSelectUiDocument?.ConfigureInputActions(inputActionsConfig);
            characterSelectUiDocument?.ConfigureClose(() => UiMenuNavigator.Back(this));
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
            if (characterSelectUiDocument == null)
            {
                return;
            }

            characterSelectUiDocument.ConfigureInputActions(inputActionsConfig);
            characterSelectUiDocument.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (characterSelectUiDocument == null)
            {
                return;
            }

            characterSelectUiDocument.gameObject.SetActive(false);
        }

        public void FocusInitial() => characterSelectUiDocument?.FocusInitial();
    }
}
