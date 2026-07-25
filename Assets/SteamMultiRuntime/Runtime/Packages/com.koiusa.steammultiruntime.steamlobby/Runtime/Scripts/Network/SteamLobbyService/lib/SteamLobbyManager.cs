using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal class SteamLobbyManager
    {
        private static class LobbyDataKeys
        {
            public const string Name = "name";
            public const string Version = "version";
            public const string HostSteamId = "hostSteamId";
            public const string SessionState = "sessionState";
            public const string StageScene = "stageScene";
            public const string IsDedicatedServer = "isDedicatedServer";
        }

        private static class LobbySessionStates
        {
            public const string Open = "open";
            public const string Closed = "closed";
        }

        private sealed class LobbyState
        {
            public Lobby? CurrentLobby;
            public ulong HostSteamId;

            public void Clear()
            {
                CurrentLobby = null;
                HostSteamId = 0;
            }

            public bool IsHost => SteamClient.IsValid && HostSteamId == SteamClient.SteamId;
        }

        private readonly List<Lobby> lobbyCache = new List<Lobby>();
        private readonly LobbyState lobbyState = new LobbyState();
        private readonly SteamConnection steamConnection;
        private readonly ISteamLobbySceneLoader sceneLoader;
        private readonly ISteamLobbyTransitionScope transitionScope;
        private int defaultMaxPlayers = 4;
        private Action onStateChanged;
        private Action<Lobby> onLobbyCreated;
        private Action<Lobby> onLobbyJoined;
        private Action onLobbyLeft;
        private Action<IReadOnlyList<Lobby>> onLobbiesRefreshed;
        private Action<Lobby> onLobbyDataChanged;
        private Action<Lobby, Friend> onLobbyMemberJoined;
        private Action<Lobby, Friend> onLobbyMemberLeft;
        private Action<Lobby> onLobbySessionClosed;
        private Action<Lobby> onLobbyHostChanged;
        private Func<bool> onEnsureReady;
        private Func<Task<bool>> onTryLoadLobbySceneAsync;
        private Func<bool> onStartNetworkHost;
        private Func<bool> onStartNetworkServer;
        private Func<ulong, bool> onStartNetworkClient;
        private Action onShutdownNetwork;

        public bool IsInLobby => lobbyState.CurrentLobby.HasValue;
        public ulong CurrentLobbyId => lobbyState.CurrentLobby?.Id ?? 0;
        public IReadOnlyList<Lobby> LobbyCache => lobbyCache;
        public bool IsHost => lobbyState.IsHost;

        public bool IsDedicatedServerLobby(Lobby lobby)
        {
            return lobby.GetData(LobbyDataKeys.IsDedicatedServer) == "1";
        }

        public IEnumerable<Friend> GetPlayerMembers(Lobby lobby)
        {
            if (!IsDedicatedServerLobby(lobby))
            {
                return lobby.Members;
            }

            var ownerSteamId = lobby.Owner.Id;
            return lobby.Members.Where(m => m.Id != ownerSteamId);
        }

        public (int memberCount, int maxMembers) GetPlayerCount(Lobby lobby)
        {
            var offset = IsDedicatedServerLobby(lobby) ? 1 : 0;
            return (Mathf.Max(0, lobby.MemberCount - offset), Mathf.Max(1, lobby.MaxMembers - offset));
        }

        public SteamLobbyManager(SteamConnection steamConnection, ISteamLobbySceneLoader sceneLoader)
        {
            this.steamConnection = steamConnection;
            this.sceneLoader = sceneLoader;
            this.transitionScope = sceneLoader as ISteamLobbyTransitionScope;
        }

        public void SetDefaultMaxPlayers(int maxPlayers)
        {
            defaultMaxPlayers = maxPlayers;
        }

        public void SetCallbacks(
            Action stateChanged,
            Action<Lobby> lobbyCreated,
            Action<Lobby> lobbyJoined,
            Action lobbyLeft,
            Action<IReadOnlyList<Lobby>> lobbiesRefreshed,
            Action<Lobby> lobbyDataChanged,
            Action<Lobby, Friend> lobbyMemberJoined,
            Action<Lobby, Friend> lobbyMemberLeft,
            Action<Lobby> lobbySessionClosed,
            Action<Lobby> lobbyHostChanged,
            Func<bool> ensureReady,
            Func<Task<bool>> tryLoadLobbySceneAsync,
            Func<bool> startNetworkHost,
            Func<bool> startNetworkServer,
            Func<ulong, bool> startNetworkClient,
            Action shutdownNetwork)
        {
            onStateChanged = stateChanged;
            onLobbyCreated = lobbyCreated;
            onLobbyJoined = lobbyJoined;
            onLobbyLeft = lobbyLeft;
            onLobbiesRefreshed = lobbiesRefreshed;
            onLobbyDataChanged = lobbyDataChanged;
            onLobbyMemberJoined = lobbyMemberJoined;
            onLobbyMemberLeft = lobbyMemberLeft;
            onLobbySessionClosed = lobbySessionClosed;
            onLobbyHostChanged = lobbyHostChanged;
            onEnsureReady = ensureReady;
            onTryLoadLobbySceneAsync = tryLoadLobbySceneAsync;
            onStartNetworkHost = startNetworkHost;
            onStartNetworkServer = startNetworkServer;
            onStartNetworkClient = startNetworkClient;
            onShutdownNetwork = shutdownNetwork;
        }

        public async Task<bool> CreateLobbyAsync(string lobbyName, string stageSceneName)
        {
            transitionScope?.BeginLobbyTransitionScope();

            try
            {
                if (IsInLobby)
                {
                    await LeaveCurrentLobbyInternalAsync(shutdownNetwork: true, refreshLobbies: false);
                    await Task.Yield();
                }

                ApplySelectedStageScene(stageSceneName);

                var maxPlayers = Mathf.Max(2, defaultMaxPlayers);
                var lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);

                if (!lobby.HasValue)
                {
                    return false;
                }

                if (!await TryLoadLobbySceneOnEnterAsync())
                {
                    FailAndLeaveLobby(lobby.Value, unloadLobbyScene: false);
                    return false;
                }

                if (!onStartNetworkHost?.Invoke() ?? false)
                {
                    FailAndLeaveLobby(lobby.Value, unloadLobbyScene: true);
                    return false;
                }

                ConfigureLobby(lobby.Value, lobbyName, stageSceneName);
                onLobbyCreated?.Invoke(lobby.Value);
                await CompleteLobbyEntryAsync(lobby.Value, SteamClient.SteamId);
                return true;
            }
            finally
            {
                transitionScope?.EndLobbyTransitionScope();
            }
        }

        public async Task<bool> CreateLobbyAsServerAsync(string lobbyName, string stageSceneName)
        {
            ApplySelectedStageScene(stageSceneName);

            // +1 to account for the dedicated server itself occupying one Steam lobby slot
            var maxPlayers = Mathf.Max(2, defaultMaxPlayers) + 1;
            var lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);

            if (!lobby.HasValue)
            {
                return false;
            }

            if (!await TryLoadLobbySceneOnEnterAsync())
            {
                FailAndLeaveLobby(lobby.Value, unloadLobbyScene: false);
                return false;
            }

            if (!onStartNetworkServer?.Invoke() ?? false)
            {
                FailAndLeaveLobby(lobby.Value, unloadLobbyScene: true);
                return false;
            }

            ConfigureLobbyAsServer(lobby.Value, lobbyName, stageSceneName);
            onLobbyCreated?.Invoke(lobby.Value);
            await CompleteLobbyEntryAsync(lobby.Value, SteamClient.SteamId);
            return true;
        }

        public async Task<bool> JoinLobbyAsync(ulong lobbyId)
        {
            if (IsInLobby && CurrentLobbyId == lobbyId)
            {
                NotifyStateChanged();
                return true;
            }

            transitionScope?.BeginLobbyTransitionScope();

            try
            {
                if (IsInLobby)
                {
                    await LeaveCurrentLobbyInternalAsync(shutdownNetwork: true, refreshLobbies: false);
                    await Task.Yield();
                }

                var lobby = await SteamMatchmaking.JoinLobbyAsync((SteamId)lobbyId);

                if (!lobby.HasValue)
                {
                    return false;
                }

                ApplyStageSceneFromLobby(lobby.Value);

                var hostSteamId = ResolveLobbyHostSteamId(lobby.Value);

                if (!IsValidClientTarget(hostSteamId))
                {
                    FailAndLeaveLobby(lobby.Value, unloadLobbyScene: false);
                    return false;
                }

                if (!onStartNetworkClient?.Invoke(hostSteamId) ?? false)
                {
                    FailAndLeaveLobby(lobby.Value, unloadLobbyScene: false);
                    return false;
                }

                if (!await TryLoadLobbySceneOnEnterAsync())
                {
                    // Don't fail, scene loading errors shouldn't prevent join
                }

                onLobbyJoined?.Invoke(lobby.Value);
                await CompleteLobbyEntryAsync(lobby.Value, hostSteamId);
                return true;
            }
            finally
            {
                transitionScope?.EndLobbyTransitionScope();
            }
        }

        public async Task LeaveCurrentLobbyAsync()
        {
            await LeaveCurrentLobbyInternalAsync(shutdownNetwork: true, refreshLobbies: true);
        }

        public async Task RefreshLobbiesAsync()
        {
            lobbyCache.Clear();

            if (onEnsureReady != null && onEnsureReady.Invoke())
            {
                var lobbies = await SteamMatchmaking.LobbyList.RequestAsync();
                if (lobbies != null)
                {
                    // Request lobby metadata for each lobby so GetData() is populated
                    foreach (var lobby in lobbies)
                    {
                        lobby.Refresh();
                    }
                    // Wait for Steam to deliver lobby data callbacks
                    await Task.Delay(500);
                    lobbyCache.AddRange(lobbies);
                }
            }

            NotifyStateChanged();
            onLobbiesRefreshed?.Invoke(LobbyCache);
        }

        public string GetLobbyDisplayName(Lobby lobby)
        {
            var name = lobby.GetData(LobbyDataKeys.Name);
            return !string.IsNullOrWhiteSpace(name) ? name : $"Lobby {lobby.Id}";
        }

        public bool IsHostedByLocalPlayer(Lobby lobby)
        {
            if (!SteamClient.IsValid)
            {
                return false;
            }

            var hostSteamId = ResolveLobbyHostSteamId(lobby);
            return hostSteamId != 0 && hostSteamId == SteamClient.SteamId;
        }

        public void OnLobbyDataChanged(Lobby lobby)
        {
            if (!IsCurrentLobby(lobby))
            {
                return;
            }

            onLobbyDataChanged?.Invoke(lobby);
            NotifyStateChanged();
            ValidateCurrentLobbyState(lobby);
        }

        public void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            if (!IsCurrentLobby(lobby))
            {
                return;
            }

            onLobbyMemberJoined?.Invoke(lobby, friend);
            NotifyStateChanged();
        }

        public void OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            if (!IsCurrentLobby(lobby))
            {
                return;
            }

            onLobbyMemberLeft?.Invoke(lobby, friend);
            NotifyStateChanged();
            ValidateCurrentLobbyState(lobby);
        }

        public Lobby? GetCurrentLobby() => lobbyState.CurrentLobby;

        public ulong GetHostSteamId() => lobbyState.HostSteamId;

        private async Task LeaveCurrentLobbyInternalAsync(bool shutdownNetwork, bool refreshLobbies)
        {
            if (!lobbyState.CurrentLobby.HasValue)
            {
                return;
            }

            var currentLobby = lobbyState.CurrentLobby;
            var wasHost = lobbyState.IsHost;
            var previousLobbySceneName = sceneLoader != null ? sceneLoader.LobbySceneName : string.Empty;

            lobbyState.Clear();
            NotifyStateChanged();

            if (sceneLoader != null)
            {
                await sceneLoader.HandleLobbyLeftAsync(previousLobbySceneName);
            }
            onLobbyLeft?.Invoke();

            if (wasHost && currentLobby.HasValue)
            {
                CloseLobby(currentLobby.Value);
            }

            if (shutdownNetwork)
            {
                onShutdownNetwork?.Invoke();
            }

            if (currentLobby.HasValue)
            {
                currentLobby.Value.Leave();
            }

            if (refreshLobbies)
            {
                await RefreshLobbiesAsync();
            }
        }

        private void ValidateCurrentLobbyState(Lobby lobby)
        {
            if (!lobbyState.CurrentLobby.HasValue || lobbyState.IsHost)
            {
                return;
            }

            if (IsLobbySessionClosed(lobby))
            {
                onLobbySessionClosed?.Invoke(lobby);
                _ = LeaveCurrentLobbyInternalAsync(shutdownNetwork: false, refreshLobbies: true);
                return;
            }

            if (HasHostChanged(lobby))
            {
                onLobbyHostChanged?.Invoke(lobby);
                _ = LeaveCurrentLobbyInternalAsync(shutdownNetwork: false, refreshLobbies: true);
            }
        }

        private async Task CompleteLobbyEntryAsync(Lobby lobby, ulong hostSteamId)
        {
            lobbyState.CurrentLobby = lobby;
            lobbyState.HostSteamId = hostSteamId;

            NotifyStateChanged();
            await RefreshLobbiesAsync();
        }

        private async Task<bool> TryLoadLobbySceneOnEnterAsync()
        {
            return await (onTryLoadLobbySceneAsync?.Invoke() ?? Task.FromResult(true));
        }

        private void NotifyStateChanged()
        {
            onStateChanged?.Invoke();
        }

        private void ConfigureLobbyAsServer(Lobby lobby, string lobbyName, string stageSceneName)
        {
            ConfigureLobby(lobby, lobbyName, stageSceneName);
            lobby.SetData(LobbyDataKeys.IsDedicatedServer, "1");
        }

        private void ConfigureLobby(Lobby lobby, string lobbyName, string stageSceneName)
        {
            lobby.SetPublic();
            lobby.SetJoinable(true);
            lobby.SetData(LobbyDataKeys.Name, string.IsNullOrWhiteSpace(lobbyName) ? $"{SteamClient.Name}'s Lobby" : lobbyName);
            lobby.SetData(LobbyDataKeys.Version, Application.version);
            lobby.SetData(LobbyDataKeys.HostSteamId, SteamClient.SteamId.ToString());
            lobby.SetData(LobbyDataKeys.SessionState, LobbySessionStates.Open);

            if (!string.IsNullOrWhiteSpace(stageSceneName))
            {
                lobby.SetData(LobbyDataKeys.StageScene, stageSceneName);
            }
        }

        private void CloseLobby(Lobby lobby)
        {
            lobby.SetJoinable(false);
            lobby.SetPrivate();
            lobby.SetData(LobbyDataKeys.SessionState, LobbySessionStates.Closed);
        }

        private void FailAndLeaveLobby(Lobby lobby, bool unloadLobbyScene)
        {
            lobby.Leave();
            if (unloadLobbyScene)
            {
                sceneLoader?.UnloadLobbySceneOnLeft();
            }
        }

        private void ApplySelectedStageScene(string stageSceneName)
        {
            if (sceneLoader == null || string.IsNullOrWhiteSpace(stageSceneName))
            {
                return;
            }

            sceneLoader.SetLobbySceneName(stageSceneName);
        }

        private void ApplyStageSceneFromLobby(Lobby lobby)
        {
            if (sceneLoader == null)
            {
                return;
            }

            var stageSceneName = lobby.GetData(LobbyDataKeys.StageScene);
            if (string.IsNullOrWhiteSpace(stageSceneName))
            {
                return;
            }

            sceneLoader.SetLobbySceneName(stageSceneName);
        }

        private bool IsCurrentLobby(Lobby lobby)
        {
            return lobbyState.CurrentLobby.HasValue && lobbyState.CurrentLobby.Value.Id == lobby.Id;
        }

        private bool IsValidClientTarget(ulong hostSteamId)
        {
            return hostSteamId != 0 && hostSteamId != SteamClient.SteamId;
        }

        private bool IsLobbySessionClosed(Lobby lobby)
        {
            return lobby.GetData(LobbyDataKeys.SessionState) == "closed";
        }

        private bool HasHostChanged(Lobby lobby)
        {
            return lobbyState.HostSteamId != 0 && lobby.Owner.Id != lobbyState.HostSteamId;
        }

        private ulong ResolveLobbyHostSteamId(Lobby lobby)
        {
            var hostSteamIdRaw = lobby.GetData(LobbyDataKeys.HostSteamId);
            if (ulong.TryParse(hostSteamIdRaw, out var hostSteamId))
            {
                return hostSteamId;
            }

            return lobby.Owner.Id;
        }
    }
}
