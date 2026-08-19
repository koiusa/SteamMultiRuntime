using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Koiusa.UI.Core;

namespace Koiusa.SteamMultiRuntime.Character.UI
{
    public static class CharacterSelectMenuRegistry
    {
        public static CharacterSelectMenuToggle Current { get; private set; }

        public static void Register(CharacterSelectMenuToggle menu)
        {
            if (menu == null || Current == menu)
                return;

            if (Current != null)
            {
                Debug.LogError("Multiple active CharacterSelectMenuToggle instances are not supported.", menu);
                return;
            }

            Current = menu;
        }

        public static void Unregister(CharacterSelectMenuToggle menu)
        {
            if (Current == menu)
                Current = null;
        }
    }

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
                Debug.LogError("CharacterSelectMenuToggle requires CharacterSelectUiDocument on itself or by explicit reference.", this);

            characterSelectUiDocument?.ConfigureInputActions(inputActionsConfig);
            characterSelectUiDocument?.ConfigureClose(() => UiMenuNavigator.Back(this));
        }

        private void OnEnable()
        {
            CharacterSelectMenuRegistry.Register(this);
            toggleBinding = InputActionBinding.Bind(inputActionsConfig?.FindAction("UI/CharacterMenuToggle"), OnTogglePerformed);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            CharacterSelectMenuRegistry.Unregister(this);
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
