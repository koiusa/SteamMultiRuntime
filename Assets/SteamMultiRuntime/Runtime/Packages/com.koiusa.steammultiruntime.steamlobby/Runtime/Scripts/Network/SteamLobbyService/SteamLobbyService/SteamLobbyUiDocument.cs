using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using Koiusa.Input;
using Koiusa.SteamMultiRuntime.Network;
using TNRD;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class SteamLobbyUiDocument : MonoBehaviour
    {
        [SerializeField] private SteamLobbyService lobbyService;
        [SerializeField] private SteamConnection steamConnection;
        [SerializeField] private SerializableInterface<ISteamLobbySceneLoader> sceneLoader;
        [SerializeField] private SteamLobbyUiAssets uiAssets;

        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private UIDocument uiDocument;
        private LobbyView view;
        private ISteamLobbySceneLoader SceneLoader => sceneLoader != null ? sceneLoader.Value : null;
        private bool isRefreshing;
        private bool refreshRequested;
        private string lobbyNameSearch = string.Empty;
        private InputActionBinding previousSectionBinding;
        private InputActionBinding nextSectionBinding;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            view = new LobbyView(uiDocument);

            if (inputActionsConfig == null)
            {
                var menuToggle = GetComponent<SteamLobbyMenuToggle>()
                    ?? FindFirstObjectByType<SteamLobbyMenuToggle>(FindObjectsInactive.Include);
                inputActionsConfig = menuToggle?.InputActionsConfig;
            }

            if (lobbyService == null)
            {
                lobbyService = FindFirstObjectByType<SteamLobbyService>();
            }

            if (steamConnection == null)
            {
                steamConnection = FindFirstObjectByType<SteamConnection>(FindObjectsInactive.Include);
            }

            if (SceneLoader == null)
            {
                var loader = GetComponent<ISteamLobbySceneLoader>()
                    ?? GetComponentInChildren<ISteamLobbySceneLoader>(true)
                    ?? FindFirstObjectByType<SteamLobbySceneLoader>(FindObjectsInactive.Include) as ISteamLobbySceneLoader
                    ?? FindFirstObjectByType<Koiusa.SteamMultiRuntime.Network.SteamLobbyDedicatedServer>(FindObjectsInactive.Include) as ISteamLobbySceneLoader
                    ?? FindFirstObjectByType<LocalSceneFlowLoader>(FindObjectsInactive.Include) as ISteamLobbySceneLoader;
                if (loader != null)
                {
                    sceneLoader = new SerializableInterface<ISteamLobbySceneLoader>(loader);
                }
            }

            uiAssets?.EnsureDefaultsLoaded();
        }

        private void OnEnable()
        {
            BuildUi();
            previousSectionBinding = InputActionBinding.Bind(
                inputActionsConfig?.FindAction("UI/PreviousSection"),
                OnPreviousSectionPerformed);
            nextSectionBinding = InputActionBinding.Bind(
                inputActionsConfig?.FindAction("UI/NextSection"),
                OnNextSectionPerformed);

            if (lobbyService != null)
            {
                lobbyService.StateChanged += Render;
            }

            if (SceneLoader != null)
            {
                SceneLoader.LoadingFinished += OnSceneLoadingFinished;
            }

            if (uiAssets != null)
            {
                uiAssets.SetOwner(this);
                uiAssets.SubscribeSteamMatchmakingEvents();
            }

            if (steamConnection != null)
            {
                steamConnection.Initialized += OnSteamInitialized;
            }

            Render();
            if (SteamClient.IsValid)
            {
                OnSteamInitialized();
            }
            else
            {
                view.SetWaitingConnection();
            }
        }

        private void OnDisable()
        {
            previousSectionBinding?.Dispose();
            previousSectionBinding = null;
            nextSectionBinding?.Dispose();
            nextSectionBinding = null;

            if (steamConnection != null)
            {
                steamConnection.Initialized -= OnSteamInitialized;
            }

            if (lobbyService != null)
            {
                lobbyService.StateChanged -= Render;
            }

            if (SceneLoader != null)
            {
                SceneLoader.LoadingFinished -= OnSceneLoadingFinished;
            }

            if (uiAssets != null)
            {
                uiAssets.UnsubscribeSteamMatchmakingEvents();
                uiAssets.SetOwner(null);
            }

            view?.UnbindActions(OnCreateClicked, OnJoinByIdClicked, OnSearchByNameClicked, OnRefreshClicked, OnLeaveClicked);
        }

        private void OnPreviousSectionPerformed(InputAction.CallbackContext context)
        {
            view?.FocusPreviousSection();
        }

        private void OnNextSectionPerformed(InputAction.CallbackContext context)
        {
            view?.FocusNextSection();
        }

        private void BuildUi()
        {
            if (uiAssets == null)
            {
                Debug.LogError("SteamLobbyUiAssets is not assigned.", this);
                return;
            }

            if (!uiAssets.TryBuild(view, this))
            {
                return;
            }

            if (SceneLoader != null)
            {
                view.SetCreatableStageScenes(SceneLoader.CreatableStageSceneNames);
            }

            view.BindActions(OnCreateClicked, OnJoinByIdClicked, OnSearchByNameClicked, OnRefreshClicked, OnLeaveClicked);
            view.FocusInitialControl();
        }

        private void OnCreateClicked()
        {
            _ = CreateLobbyAsync();
        }

        private void OnJoinByIdClicked()
        {
            _ = JoinByIdAsync();
        }

        private void OnSearchByNameClicked()
        {
            lobbyNameSearch = view.LobbyNameSearchRaw?.Trim() ?? string.Empty;
            Render();
        }

        private void OnRefreshClicked()
        {
            _ = RefreshAsync();
        }

        private void OnLeaveClicked()
        {
            lobbyService?.LeaveLobby();
        }

        private async Task CreateLobbyAsync()
        {
            if (lobbyService == null)
            {
                return;
            }

            if (lobbyService.IsInLobby && lobbyService.IsHost)
            {
                var changed = await lobbyService.ChangeStageAsync(view.SelectedStageSceneName);
                view.SetInfo(changed ? "Stage changed." : "Stage change failed.");
                return;
            }

            var created = await lobbyService.CreateLobbyAsync(view.LobbyName, view.SelectedStageSceneName);
            view.SetInfo(created ? "Lobby created." : "Lobby create failed.");
        }

        private async Task JoinByIdAsync()
        {
            if (lobbyService == null)
            {
                return;
            }

            if (!ulong.TryParse(view.LobbyIdRaw, out var lobbyId))
            {
                view.SetInfo("Lobby ID が不正です。");
                return;
            }

            var joined = await lobbyService.JoinLobbyAsync(lobbyId);
            view.SetInfo(joined ? "Lobby joined." : "Lobby join failed.");
        }

        private async Task RefreshAsync()
        {
            if (lobbyService == null)
            {
                Render();
                return;
            }

            if (isRefreshing)
            {
                refreshRequested = true;
                return;
            }

            isRefreshing = true;
            try
            {
                do
                {
                    refreshRequested = false;
                    await lobbyService.RefreshLobbiesAsync();
                }
                while (refreshRequested && isActiveAndEnabled);
            }
            finally
            {
                isRefreshing = false;
            }
        }

        private void Render()
        {
            if (!view.IsRenderable)
            {
                return;
            }

            if (lobbyService == null)
            {
                view.RenderServiceMissing();
                return;
            }

            var isReady = lobbyService.IsReady;
            var connectionStrengthText = isReady && lobbyService.IsInLobby
                ? lobbyService.GetConnectionStrengthText()
                : string.Empty;

            view.RenderStatus(isReady, lobbyService.LocalPlayerName, lobbyService.IsInLobby, lobbyService.CurrentLobbyId);
            view.ShowOnlineMembers(lobbyService.GetCurrentLobbyMemberNames(), lobbyService.IsInLobby, connectionStrengthText);

            if (!isReady)
            {
                view.SetInfo("NetworkManager/FacepunchTransport の初期化待ち");
            }

            view.SetButtonsEnabled(isReady);

            if (lobbyService.LobbyCache.Count == 0)
            {
                view.ShowNoLobbies();
                return;
            }

            var filteredLobbies = FilterLobbiesByName(lobbyService.LobbyCache);
            if (filteredLobbies.Count == 0)
            {
                view.ShowNoLobbies("該当するロビー名が見つかりませんでした。");
                return;
            }

            view.ShowLobbies(
                filteredLobbies,
                isReady,
                lobbyService.GetLobbyDisplayName,
                lobbyService.IsHostedByLocalPlayer,
                lobbyService.GetLobbyPlayerCount,
                lobbyService.CurrentLobbyId,
                JoinLobbyFromList);
        }

        private void JoinLobbyFromList(ulong lobbyId)
        {
            if (lobbyService == null)
            {
                return;
            }

            _ = lobbyService.JoinLobbyAsync(lobbyId);
        }

        internal void RequestRefresh()
        {
            _ = RefreshAsync();
        }

        private void OnSteamInitialized()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Render();
            view.SetInfo("Ready");
            _ = RefreshAsync();
        }

        private void OnSceneLoadingFinished()
        {
            _ = RefreshAsync();
        }

        private IReadOnlyList<Lobby> FilterLobbiesByName(IReadOnlyList<Lobby> lobbies)
        {
            if (string.IsNullOrWhiteSpace(lobbyNameSearch))
            {
                return lobbies;
            }

            var filtered = new List<Lobby>();
            for (var i = 0; i < lobbies.Count; i++)
            {
                var lobby = lobbies[i];
                var displayName = lobbyService.GetLobbyDisplayName(lobby);
                if (!string.IsNullOrWhiteSpace(displayName) &&
                    displayName.IndexOf(lobbyNameSearch, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filtered.Add(lobby);
                }
            }

            return filtered;
        }
    }
}
