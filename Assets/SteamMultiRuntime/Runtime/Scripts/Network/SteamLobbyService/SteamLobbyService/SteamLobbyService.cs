using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class SteamLobbyService : MonoBehaviour
    {
        [SerializeField] private SteamConnection steamConnection;
        [SerializeField] private SteamLobbySceneLoaderBase sceneLoader;
        [SerializeField] private SteamLobbyConnectionStatus connectionStatus;
        [SerializeField] private int defaultMaxPlayers = 4;
        [SerializeField] private bool enableLogging = false;
        [SerializeField] private bool useAdditiveClientSynchronization = true;

        private SteamLobbyManager lobbyManager;
        private SteamLobbyQualityTracker qualityTracker;
        private SteamLobbyNetworkFacade networkFacade;
        private bool isSubscribedToNetworkEvents;
        private bool isSubscribedToLobbyEvents;

        public event Action StateChanged;
        public event Action<Lobby> LobbyCreated;
        public event Action<Lobby> LobbyJoined;
        public event Action LobbyLeft;
        public event Action<IReadOnlyList<Lobby>> LobbiesRefreshed;
        public event Action<Lobby> LobbyDataChanged;
        public event Action<Lobby, Friend> LobbyMemberJoined;
        public event Action<Lobby, Friend> LobbyMemberLeft;
        public event Action<ulong> ClientDisconnected;
        public event Action<Lobby> LobbySessionClosed;
        public event Action<Lobby> LobbyHostChanged;
        public event Action<string> ErrorOccurred;

        public bool IsReady => SteamClient.IsValid;
        public bool IsInLobby => lobbyManager?.IsInLobby ?? false;
        public string LocalPlayerName => IsReady ? SteamClient.Name : "Not Connected";
        public ulong CurrentLobbyId => lobbyManager?.CurrentLobbyId ?? 0;
        public IReadOnlyList<Lobby> LobbyCache => lobbyManager?.LobbyCache ?? new List<Lobby>();
        public IReadOnlyList<string> CreatableStageSceneNames => sceneLoader != null
            ? sceneLoader.CreatableStageSceneNames
            : (IReadOnlyList<string>)Array.Empty<string>();

        private void Awake()
        {
            if (steamConnection == null)
            {
                steamConnection = GetComponent<SteamConnection>();
            }

            if (sceneLoader == null)
            {
                sceneLoader = GetComponent<SteamLobbySceneLoaderBase>();
            }

            if (connectionStatus == null)
            {
                connectionStatus = GetComponent<SteamLobbyConnectionStatus>();
            }

            if (connectionStatus == null)
            {
                connectionStatus = GetComponentInChildren<SteamLobbyConnectionStatus>(true);
            }

            if (connectionStatus == null)
            {
                connectionStatus = FindFirstObjectByType<SteamLobbyConnectionStatus>(FindObjectsInactive.Include);
            }

            networkFacade = new SteamLobbyNetworkFacade(useAdditiveClientSynchronization);

            lobbyManager = new SteamLobbyManager(steamConnection, sceneLoader);
            lobbyManager.SetDefaultMaxPlayers(defaultMaxPlayers);
            lobbyManager.SetCallbacks(
                NotifyStateChanged,
                lobby => LobbyCreated?.Invoke(lobby),
                lobby => LobbyJoined?.Invoke(lobby),
                () => LobbyLeft?.Invoke(),
                lobbies => LobbiesRefreshed?.Invoke(lobbies),
                lobby => LobbyDataChanged?.Invoke(lobby),
                (lobby, friend) => LobbyMemberJoined?.Invoke(lobby, friend),
                (lobby, friend) => LobbyMemberLeft?.Invoke(lobby, friend),
                lobby => LobbySessionClosed?.Invoke(lobby),
                lobby => LobbyHostChanged?.Invoke(lobby),
                EnsureReady,
                TryLoadLobbySceneOnEnterAsync,
                StartNetworkHost,
                StartNetworkServer,
                StartNetworkClient,
                ShutdownNetwork);

            qualityTracker = new SteamLobbyQualityTracker();
            qualityTracker.Initialize(networkFacade, useAdditiveClientSynchronization);
            qualityTracker.SetCallbacks(
                () => lobbyManager.GetCurrentLobby(),
                GetCurrentLobbyMembers,
                ApplyMemberQualitySnapshot,
                NotifyStateChanged);
        }

        private void OnEnable()
        {
            SubscribeNetworkEvents();
            SubscribeLobbyEvents();
        }

        private void OnDisable()
        {
            UnsubscribeNetworkEvents();
            UnsubscribeLobbyEvents();
        }

        private void Update()
        {
            if (!IsInLobby)
            {
                return;
            }

            var intervalSeconds = connectionStatus != null ? connectionStatus.BroadcastIntervalSeconds : 1f;
            qualityTracker.Update(Time.unscaledTime, intervalSeconds);
        }

        public async Task<bool> CreateLobbyAsync(string lobbyName, string stageSceneName = "")
        {
            Log("Creating lobby...");

            if (!EnsureReadyOrLog())
            {
                return false;
            }

            var success = await lobbyManager.CreateLobbyAsync(lobbyName, stageSceneName);
            if (success)
            {
                qualityTracker.OnNetworkHostStarted();
            }
            else
            {
                LogError("Failed to create lobby");
            }

            return success;
        }

        public async Task<bool> CreateLobbyAsServerAsync(string lobbyName, string stageSceneName = "")
        {
            Log("Creating lobby as server...");

            if (!EnsureReadyOrLog())
            {
                return false;
            }

            var success = await lobbyManager.CreateLobbyAsServerAsync(lobbyName, stageSceneName);
            if (!success)
            {
                LogError("Failed to create lobby as server");
            }

            return success;
        }

        public async Task<bool> JoinLobbyAsync(ulong lobbyId)
        {
            Log($"Joining lobby {lobbyId}...");

            if (!EnsureReadyOrLog())
            {
                return false;
            }

            var success = await lobbyManager.JoinLobbyAsync(lobbyId);
            if (!success)
            {
                LogError($"Failed to join lobby {lobbyId}");
            }

            return success;
        }

        public void LeaveLobby()
        {
            if (!IsInLobby)
            {
                Log("LeaveLobby ignored: not in lobby");
                return;
            }

            Log("Leaving lobby...");
            _ = lobbyManager.LeaveCurrentLobbyAsync();
        }

        public async Task RefreshLobbiesAsync()
        {
            if (lobbyManager == null)
            {
                return;
            }

            await lobbyManager.RefreshLobbiesAsync();
        }

        public string GetLobbyDisplayName(Lobby lobby)
        {
            return lobbyManager?.GetLobbyDisplayName(lobby) ?? string.Empty;
        }

        public bool IsHostedByLocalPlayer(Lobby lobby)
        {
            return lobbyManager != null && lobbyManager.IsHostedByLocalPlayer(lobby);
        }

        public (int memberCount, int maxMembers) GetLobbyPlayerCount(Lobby lobby)
        {
            return lobbyManager?.GetPlayerCount(lobby) ?? (lobby.MemberCount, lobby.MaxMembers);
        }

        public IReadOnlyList<string> GetCurrentLobbyMemberNames()
        {
            var members = GetCurrentLobbyMembers();
            if (members.Count == 0)
            {
                return new List<string>();
            }

            var names = new List<string>();
            foreach (var member in members)
            {
                if (connectionStatus != null)
                {
                    names.Add(connectionStatus.BuildMemberDisplayName(member.Name, member.Id, IsInLobby, lobbyManager.IsHost));
                }
                else
                {
                    names.Add(string.IsNullOrWhiteSpace(member.Name) ? "Unknown" : member.Name);
                }
            }

            return names;
        }

        public string GetConnectionStrengthText()
        {
            if (connectionStatus == null || lobbyManager == null)
            {
                return string.Empty;
            }

            return connectionStatus.GetConnectionStrengthText(IsInLobby, lobbyManager.IsHost, lobbyManager.GetHostSteamId());
        }

        private bool EnsureReady()
        {
            if (SteamClient.IsValid)
            {
                return true;
            }

            if (steamConnection != null)
            {
                steamConnection.Initialize();
            }

            return false;
        }

        private bool EnsureReadyOrLog()
        {
            if (EnsureReady())
            {
                return true;
            }

            LogError("Steam not ready");
            return false;
        }

        private async Task<bool> TryLoadLobbySceneOnEnterAsync()
        {
            if (sceneLoader == null)
            {
                return true;
            }

            return await sceneLoader.LoadLobbySceneOnEnteredAsync();
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void SubscribeNetworkEvents()
        {
            if (isSubscribedToNetworkEvents)
            {
                return;
            }

            if (networkFacade != null && networkFacade.SubscribeClientDisconnect(OnClientDisconnected))
            {
                isSubscribedToNetworkEvents = true;
            }
        }

        private void UnsubscribeNetworkEvents()
        {
            if (isSubscribedToNetworkEvents)
            {
                networkFacade?.UnsubscribeClientDisconnect(OnClientDisconnected);
                isSubscribedToNetworkEvents = false;
            }
        }

        private void SubscribeLobbyEvents()
        {
            if (isSubscribedToLobbyEvents)
            {
                return;
            }

            SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            isSubscribedToLobbyEvents = true;
        }

        private void UnsubscribeLobbyEvents()
        {
            if (!isSubscribedToLobbyEvents)
            {
                return;
            }

            SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataChanged;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
            isSubscribedToLobbyEvents = false;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsInLobby)
            {
                return;
            }

            if (networkFacade == null || !networkFacade.TryGetLocalClientId(out var localClientId))
            {
                return;
            }

            var isLocalDisconnect = clientId == localClientId;
            var isServerDisconnect = clientId == NetworkManager.ServerClientId;

            if (!isLocalDisconnect && !isServerDisconnect)
            {
                return;
            }

            ClientDisconnected?.Invoke(clientId);
            Log("Client disconnected from server, leaving lobby");
            LeaveLobby();
        }

        private void OnLobbyDataChanged(Lobby lobby)
        {
            lobbyManager.OnLobbyDataChanged(lobby);
        }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            lobbyManager.OnLobbyMemberJoined(lobby, friend);
            qualityTracker.OnMemberJoined();
        }

        private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            lobbyManager.OnLobbyMemberLeave(lobby, friend);
            qualityTracker.OnMemberLeft();
        }

        private bool StartNetworkHost()
        {
            return networkFacade != null && networkFacade.TryStartHost();
        }

        private bool StartNetworkServer()
        {
            return networkFacade != null && networkFacade.TryStartServer();
        }

        private bool StartNetworkClient(ulong hostSteamId)
        {
            return networkFacade != null && networkFacade.TryStartClient(hostSteamId);
        }

        private void ShutdownNetwork()
        {
            qualityTracker.OnNetworkShutdown();
            networkFacade?.ShutdownIfListening();
        }

        private List<Friend> GetCurrentLobbyMembers()
        {
            if (lobbyManager == null)
            {
                return new List<Friend>();
            }

            var currentLobby = lobbyManager.GetCurrentLobby();
            if (!currentLobby.HasValue)
            {
                return new List<Friend>();
            }

            return lobbyManager.GetPlayerMembers(currentLobby.Value).ToList();
        }

        private void ApplyMemberQualitySnapshot(List<SteamLobbyMemberQualityEntry> entries)
        {
            connectionStatus?.ApplySnapshot(entries);
            NotifyStateChanged();
        }

        private void Log(string message)
        {
            if (enableLogging)
            {
                Debug.Log($"[SteamLobbyService] {message}");
            }
        }

        private void LogError(string message)
        {
            ErrorOccurred?.Invoke(message);
            Debug.LogError($"[SteamLobbyService] {message}");
        }
    }
}
