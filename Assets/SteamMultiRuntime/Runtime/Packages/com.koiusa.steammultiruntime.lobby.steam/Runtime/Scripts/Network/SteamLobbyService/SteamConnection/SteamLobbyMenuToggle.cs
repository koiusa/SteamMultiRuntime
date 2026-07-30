using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Koiusa.UI.Common;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class SteamLobbyMenuToggle : MonoBehaviour, IUiMenu
    {
        [Header("References")]
        [SerializeField] private SteamLobbyUiDocument lobbyUiDocument;

        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private SteamLobbyService lobbyService;
        private bool hasLobbyMembershipState;
        private bool lastIsInLobby;
        private InputActionBinding toggleBinding;

        public InputActionsConfig InputActionsConfig => inputActionsConfig;
        public bool IsVisible => lobbyUiDocument != null && lobbyUiDocument.gameObject.activeSelf;

        private void Awake()
        {
            if (lobbyUiDocument == null)
            {
                lobbyUiDocument = GetComponent<SteamLobbyUiDocument>();
            }

            if (lobbyUiDocument == null)
                Debug.LogError("SteamLobbyMenuToggle requires SteamLobbyUiDocument on itself or by explicit reference.", this);

            lobbyService = lobbyUiDocument != null
                ? lobbyUiDocument.GetComponent<SteamLobbyService>()
                : GetComponent<SteamLobbyService>();

            if (lobbyService == null)
                Debug.LogError("SteamLobbyMenuToggle requires SteamLobbyService on the configured UI root.", this);
        }

        private void OnEnable()
        {
            var action = inputActionsConfig?.FindAction("System/DebugSessionMenuToggle");
            toggleBinding = InputActionBinding.Bind(action, OnTogglePerformed);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            if (lobbyService != null)
            {
                lobbyService.StateChanged += OnLobbyStateChanged;
                lastIsInLobby = lobbyService.IsInLobby;
                hasLobbyMembershipState = true;
            }
        }

        private void OnDisable()
        {
            toggleBinding?.Dispose();
            toggleBinding = null;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            if (lobbyService != null)
            {
                lobbyService.StateChanged -= OnLobbyStateChanged;
            }
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            Toggle();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            UiMenuNavigator.CloseAll();
        }

        private void OnLobbyStateChanged()
        {
            if (lobbyService == null)
            {
                return;
            }

            var isInLobby = lobbyService.IsInLobby;
            if (hasLobbyMembershipState && isInLobby != lastIsInLobby)
            {
                Hide();
            }

            lastIsInLobby = isInLobby;
            hasLobbyMembershipState = true;
        }

        public void Toggle()
        {
            UiMenuNavigator.ToggleRoot(this);
        }

        public void Show() => UiMenuNavigator.OpenRoot(this);

        public void Hide() => UiMenuNavigator.Close(this);

        public void Activate()
        {
            if (lobbyUiDocument == null)
            {
                return;
            }

            lobbyUiDocument.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (lobbyUiDocument == null)
            {
                return;
            }

            lobbyUiDocument.gameObject.SetActive(false);
        }

        public void FocusInitial() => lobbyUiDocument?.FocusInitial();
    }
}
