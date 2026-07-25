using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class SteamLobbyMenuToggle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SteamLobbyUiDocument lobbyUiDocument;

        [Header("Input")]
        [SerializeField] private InputActionAssetProfile inputProfile;

        private SteamLobbyService lobbyService;
        private bool hasLobbyMembershipState;
        private bool lastIsInLobby;
        private InputActionBinding toggleBinding;

        private void Awake()
        {
            if (lobbyUiDocument == null)
            {
                lobbyUiDocument = GetComponent<SteamLobbyUiDocument>();
            }

            if (lobbyUiDocument == null)
            {
                lobbyUiDocument = FindFirstObjectByType<SteamLobbyUiDocument>(FindObjectsInactive.Include);
            }

            lobbyService = lobbyUiDocument != null
                ? lobbyUiDocument.GetComponent<SteamLobbyService>()
                : GetComponent<SteamLobbyService>();

            if (lobbyService == null)
            {
                lobbyService = FindFirstObjectByType<SteamLobbyService>();
            }
        }

        private void OnEnable()
        {
            toggleBinding = InputActionBinding.Bind(inputProfile?.FindAction("Player/Interact"), OnTogglePerformed);

            if (lobbyService != null)
            {
                lobbyService.StateChanged += OnLobbyStateChanged;
                hasLobbyMembershipState = false;
            }
        }

        private void OnDisable()
        {
            toggleBinding?.Dispose();
            toggleBinding = null;

            if (lobbyService != null)
            {
                lobbyService.StateChanged -= OnLobbyStateChanged;
            }
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            Toggle();
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
            if (lobbyUiDocument == null)
            {
                return;
            }

            var isVisible = lobbyUiDocument.gameObject.activeSelf;
            lobbyUiDocument.gameObject.SetActive(!isVisible);
        }

        public void Show()
        {
            if (lobbyUiDocument == null)
            {
                return;
            }

            lobbyUiDocument.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (lobbyUiDocument == null)
            {
                return;
            }

            lobbyUiDocument.gameObject.SetActive(false);
        }
    }
}
