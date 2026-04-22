using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using Koiusa.SteamMultiRuntime.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class SteamLobbyUiDocument : MonoBehaviour
    {
        [SerializeField] private SteamLobbyService lobbyService;
        [SerializeField] private SteamLobbySceneLoaderBase sceneLoader;
        [SerializeField] private SteamLobbyUiAssets uiAssets;

        private UIDocument uiDocument;
        private LobbyView view;
        private Coroutine waitForSteamCoroutine;
        private bool isRefreshing;
        private string lobbyNameSearch = string.Empty;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            view = new LobbyView(uiDocument);

            if (lobbyService == null)
            {
                lobbyService = FindFirstObjectByType<SteamLobbyService>();
            }

            if (sceneLoader == null)
            {
                sceneLoader = FindFirstObjectByType<SteamLobbySceneLoaderBase>();
            }

            uiAssets?.EnsureDefaultsLoaded();
        }

        private void OnEnable()
        {
            BuildUi();

            if (lobbyService != null)
            {
                lobbyService.StateChanged += Render;
            }

            if (sceneLoader != null)
            {
                sceneLoader.LoadingFinished += OnSceneLoadingFinished;
            }

            if (uiAssets != null)
            {
                uiAssets.SetOwner(this);
                uiAssets.SubscribeSteamMatchmakingEvents();
            }

            waitForSteamCoroutine = StartCoroutine(WaitForSteamAndRefresh());
        }

        private void OnDisable()
        {
            if (waitForSteamCoroutine != null)
            {
                StopCoroutine(waitForSteamCoroutine);
                waitForSteamCoroutine = null;
            }

            if (lobbyService != null)
            {
                lobbyService.StateChanged -= Render;
            }

            if (sceneLoader != null)
            {
                sceneLoader.LoadingFinished -= OnSceneLoadingFinished;
            }

            if (uiAssets != null)
            {
                uiAssets.UnsubscribeSteamMatchmakingEvents();
                uiAssets.SetOwner(null);
            }

            view?.UnbindActions(OnCreateClicked, OnJoinByIdClicked, OnSearchByNameClicked, OnRefreshClicked, OnLeaveClicked);
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

            if (lobbyService != null)
            {
                view.SetCreatableStageScenes(lobbyService.CreatableStageSceneNames);
            }

            view.BindActions(OnCreateClicked, OnJoinByIdClicked, OnSearchByNameClicked, OnRefreshClicked, OnLeaveClicked);
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
                return;
            }

            isRefreshing = true;
            try
            {
                await lobbyService.RefreshLobbiesAsync();
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

        private IEnumerator WaitForSteamAndRefresh()
        {
            Render();

            while (isActiveAndEnabled && !SteamClient.IsValid)
            {
                view.SetWaitingConnection();
                yield return null;
            }

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            Render();
            view.SetInfo("Ready");
            _ = RefreshAsync();
            waitForSteamCoroutine = null;
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
