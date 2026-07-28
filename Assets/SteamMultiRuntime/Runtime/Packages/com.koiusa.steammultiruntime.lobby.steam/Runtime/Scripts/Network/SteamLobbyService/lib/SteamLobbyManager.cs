using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using Lobby = Steamworks.Data.Lobby;
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
        private readonly HashSet<ulong> departedLobbyIds = new HashSet<ulong>();
        private readonly SemaphoreSlim lobbyRefreshGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim lobbyTransitionGate = new SemaphoreSlim(1, 1);
        private readonly LobbyState lobbyState = new LobbyState();
        private readonly SteamConnection steamConnection;
        private readonly ISteamLobbySceneLoader sceneLoader;
        private readonly ISteamLobbyTransitionScope transitionScope;
        private readonly INetworkSessionController networkSession;
        private readonly ILobbySceneTransitionController sceneTransitionController;
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

        public SteamLobbyManager(SteamConnection steamConnection, ISteamLobbySceneLoader sceneLoader, INetworkSessionController networkSession)
        {
            this.steamConnection = steamConnection;
            this.sceneLoader = sceneLoader;
            this.transitionScope = sceneLoader as ISteamLobbyTransitionScope;
            this.networkSession = networkSession;
            this.sceneTransitionController = sceneLoader as ILobbySceneTransitionController;
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
            Func<Task<bool>> tryLoadLobbySceneAsync)
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
        }

        public async Task<bool> CreateLobbyAsync(string lobbyName, string stageSceneName)
        {
            await lobbyTransitionGate.WaitAsync();
            transitionScope?.BeginLobbyTransitionScope();

            try
            {
                var previousSceneName = IsInLobby ? sceneLoader?.LobbySceneName : string.Empty;
                if (IsInLobby)
                {
                    await LeaveCurrentLobbyInternalAsync(shutdownNetwork: true, refreshLobbies: false);
                }

                ApplySelectedStageScene(stageSceneName);

                var maxPlayers = Mathf.Max(2, defaultMaxPlayers);
                var lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);

                if (!lobby.HasValue)
                {
                    return false;
                }

                networkSession?.TryActivateBootstrapScene((sceneLoader as ISceneLoadContext)?.DefaultSceneName);
                if (networkSession == null || !networkSession.TryStartHost())
                {
                    await FailAndLeaveLobbyAsync(lobby.Value, unloadLobbyScene: true);
                    return false;
                }

                if (!await networkSession.SwitchStageSceneAsync(sceneLoader?.LobbySceneName, string.Empty)
                    || !await TrySwitchLobbySceneAsync(string.Empty))
                {
                    await FailAndLeaveLobbyAsync(lobby.Value, unloadLobbyScene: true);
                    return false;
                }

                ConfigureLobby(lobby.Value, lobbyName, stageSceneName);
                BeginLobbyEntry(lobby.Value, SteamClient.SteamId);
                onLobbyCreated?.Invoke(lobby.Value);
                await RefreshLobbiesAsync();
                return true;
            }
            finally
            {
                transitionScope?.EndLobbyTransitionScope();
                lobbyTransitionGate.Release();
            }
        }

        public async Task<bool> CreateLobbyAsServerAsync(string lobbyName, string stageSceneName)
        {
            await lobbyTransitionGate.WaitAsync();
            transitionScope?.BeginLobbyTransitionScope();

            try
            {
                var previousSceneName = IsInLobby ? sceneLoader?.LobbySceneName : string.Empty;
                if (IsInLobby)
                {
                    await LeaveCurrentLobbyInternalAsync(shutdownNetwork: true, refreshLobbies: false);
                }

                ApplySelectedStageScene(stageSceneName);

                // +1 to account for the dedicated server itself occupying one Steam lobby slot
                var maxPlayers = Mathf.Max(2, defaultMaxPlayers) + 1;
                var lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);

                if (!lobby.HasValue)
                {
                    return false;
                }

                networkSession?.TryActivateBootstrapScene((sceneLoader as ISceneLoadContext)?.DefaultSceneName);
                if (networkSession == null || !networkSession.TryStartServer())
                {
                    await FailAndLeaveLobbyAsync(lobby.Value, unloadLobbyScene: true);
                    return false;
                }

                if (!await networkSession.SwitchStageSceneAsync(sceneLoader?.LobbySceneName, string.Empty)
                    || !await TrySwitchLobbySceneAsync(string.Empty))
                {
                    await FailAndLeaveLobbyAsync(lobby.Value, unloadLobbyScene: true);
                    return false;
                }

                ConfigureLobbyAsServer(lobby.Value, lobbyName, stageSceneName);
                BeginLobbyEntry(lobby.Value, SteamClient.SteamId);
                onLobbyCreated?.Invoke(lobby.Value);
                await RefreshLobbiesAsync();
                return true;
            }
            finally
            {
                transitionScope?.EndLobbyTransitionScope();
                lobbyTransitionGate.Release();
            }
        }

        public async Task<bool> JoinLobbyAsync(ulong lobbyId)
        {
            if (IsInLobby && CurrentLobbyId == lobbyId)
            {
                NotifyStateChanged();
                return true;
            }

            await lobbyTransitionGate.WaitAsync();
            transitionScope?.BeginLobbyTransitionScope();

            try
            {
                var previousSceneName = IsInLobby ? sceneLoader?.LobbySceneName : string.Empty;
                if (IsInLobby)
                {
                    await LeaveCurrentLobbyInternalAsync(shutdownNetwork: true, refreshLobbies: false);
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
                    await FailAndLeaveLobbyAsync(lobby.Value, unloadLobbyScene: false);
                    return false;
                }

                networkSession?.TryActivateBootstrapScene((sceneLoader as ISceneLoadContext)?.DefaultSceneName);
                if (networkSession == null || !networkSession.TryStartClient(hostSteamId))
                {
                    await FailAndLeaveLobbyAsync(lobby.Value, unloadLobbyScene: false);
                    return false;
                }

                if (!await networkSession.WaitForClientStageSceneAsync(sceneLoader?.LobbySceneName)
                    || !await TrySwitchLobbySceneAsync(string.Empty))
                {
                    await FailAndLeaveLobbyAsync(lobby.Value, unloadLobbyScene: false);
                    return false;
                }

                BeginLobbyEntry(lobby.Value, hostSteamId);
                onLobbyJoined?.Invoke(lobby.Value);
                await RefreshLobbiesAsync();
                return true;
            }
            finally
            {
                transitionScope?.EndLobbyTransitionScope();
                lobbyTransitionGate.Release();
            }
        }

        public async Task LeaveCurrentLobbyAsync()
        {
            await lobbyTransitionGate.WaitAsync();
            transitionScope?.BeginLobbyTransitionScope();
            try
            {
                await LeaveCurrentLobbyInternalAsync(shutdownNetwork: true, refreshLobbies: true);
            }
            finally
            {
                transitionScope?.EndLobbyTransitionScope();
                lobbyTransitionGate.Release();
            }
        }

        public async Task RecoverFromHostLossAsync()
        {
            await lobbyTransitionGate.WaitAsync();
            transitionScope?.BeginLobbyTransitionScope();
            try
            {
                if (!lobbyState.CurrentLobby.HasValue || lobbyState.IsHost)
                {
                    return;
                }

                await LeaveCurrentLobbyInternalAsync(
                    shutdownNetwork: true,
                    refreshLobbies: true);
            }
            finally
            {
                transitionScope?.EndLobbyTransitionScope();
                lobbyTransitionGate.Release();
            }
        }

        /// <summary>
        /// アプリ終了時用。終了フレームでは非同期のシーン処理を完了できないため、
        /// Steam ロビーを閉じて退出する操作だけを同期的に確定する。
        /// </summary>
        public void LeaveCurrentLobbyImmediately()
        {
            if (!lobbyState.CurrentLobby.HasValue)
            {
                return;
            }

            var currentLobby = lobbyState.CurrentLobby.Value;
            var wasHost = lobbyState.IsHost;
            lobbyState.Clear();

            if (wasHost)
            {
                CloseLobby(currentLobby);
            }

            currentLobby.Leave();
        }

        public async Task<bool> ChangeHostedLobbyStageAsync(string stageSceneName)
        {
            await lobbyTransitionGate.WaitAsync();
            transitionScope?.BeginLobbyTransitionScope();
            try
            {
                return await ChangeHostedLobbyStageCoreAsync(stageSceneName);
            }
            finally
            {
                transitionScope?.EndLobbyTransitionScope();
                lobbyTransitionGate.Release();
            }
        }

        public async Task RefreshLobbiesAsync()
        {
            await lobbyRefreshGate.WaitAsync();
            try
            {
                var snapshot = new List<Lobby>();
                if (onEnsureReady == null || !onEnsureReady.Invoke())
                {
                    PublishLobbyCache();
                    return;
                }

                var lobbies = await SteamMatchmaking.LobbyList.RequestAsync();
                if (lobbies == null)
                {
                    // A transient Steam query failure must not erase the last valid
                    // result and make every lobby disappear from the UI.
                    PublishLobbyCache();
                    return;
                }

                snapshot.AddRange(lobbies);

                // A newly public lobby can take time to enter Steam's search index. The
                // current lobby is authoritative local state, so never make its display
                // depend on when the search backend happens to return it.
                MergeCurrentLobby(snapshot);
                ReplaceLobbyCache(snapshot);
                PublishLobbyCache();
            }
            finally
            {
                lobbyRefreshGate.Release();
            }
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

            lobbyState.CurrentLobby = lobby;
            UpsertLobbyCache(lobby);
            onLobbyDataChanged?.Invoke(lobby);
            NotifyStateChanged();
            if (!lobbyState.IsHost)
            {
                var nextStageSceneName = lobby.GetData(LobbyDataKeys.StageScene);
                if (!string.IsNullOrWhiteSpace(nextStageSceneName)
                    && !SceneLoadUtility.AreSameSceneReference(sceneLoader?.LobbySceneName, nextStageSceneName))
                {
                    ApplySelectedStageScene(nextStageSceneName);
                }
            }
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

            // Steam can report the host leaving before the lobby owner change or the
            // closed session metadata reaches the remaining members. Compare against
            // the host captured on entry so a deleted host lobby cannot remain active
            // locally just because the Lobby snapshot in this callback is stale.
            if (!lobbyState.IsHost
                && lobbyState.HostSteamId != 0
                && friend.Id == lobbyState.HostSteamId)
            {
                onLobbyHostChanged?.Invoke(lobby);
                _ = RecoverFromHostLossAsync();
                return;
            }

            ValidateCurrentLobbyState(lobby);
        }

        public Lobby? GetCurrentLobby() => lobbyState.CurrentLobby;

        public ulong GetHostSteamId() => lobbyState.HostSteamId;

        private async Task LeaveCurrentLobbyInternalAsync(
            bool shutdownNetwork,
            bool refreshLobbies,
            bool preserveScene = false)
        {
            if (!lobbyState.CurrentLobby.HasValue)
            {
                return;
            }

            var currentLobby = lobbyState.CurrentLobby;
            var wasHost = lobbyState.IsHost;
            var hasLeftSteamLobby = false;
            var previousLobbySceneName = sceneLoader != null ? sceneLoader.LobbySceneName : string.Empty;

            lobbyState.Clear();
            if (currentLobby.HasValue)
            {
                departedLobbyIds.Add(currentLobby.Value.Id);
                lobbyCache.RemoveAll(lobby => lobby.Id == currentLobby.Value.Id);
            }
            PublishLobbyCache();

            // A departing client does not need to remain in the Steam lobby while
            // network shutdown and scene recovery are running. Leave immediately so
            // membership does not linger during asynchronous scene operations.
            if (!wasHost && currentLobby.HasValue)
            {
                currentLobby.Value.Leave();
                hasLeftSteamLobby = true;
            }

            // Notify clients before Netcode unloads their synchronized scene. This
            // gives them a deterministic event to show transition UI first.
            if (wasHost && currentLobby.HasValue)
            {
                if (networkSession != null)
                {
                    await networkSession.NotifyClientsSessionEndingAsync();
                }
                CloseLobby(currentLobby.Value);
            }

            try
            {
                // On clients, NetworkManager shutdown can unload synchronized scenes.
                // Make the local default scene active before shutdown so there is no
                // frame where the lobby scene is gone but its replacement is absent.
                if (!wasHost && !preserveScene && sceneTransitionController != null)
                {
                    await sceneTransitionController.PrepareForLobbyExitAsync();
                }

                if (shutdownNetwork && networkSession != null)
                {
                    if (wasHost && !string.IsNullOrWhiteSpace(previousLobbySceneName))
                    {
                        await networkSession.UnloadStageSceneAsync(previousLobbySceneName);
                    }

                    await networkSession.StopAsync();
                }

                if (!preserveScene && sceneLoader != null)
                {
                    await sceneLoader.HandleLobbyLeftAsync(previousLobbySceneName);
                }

            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                onLobbyLeft?.Invoke();

                if (!hasLeftSteamLobby && currentLobby.HasValue)
                {
                    currentLobby.Value.Leave();
                }

                if (refreshLobbies)
                {
                    await RefreshLobbiesAsync();
                }
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
                _ = RecoverFromHostLossAsync();
                return;
            }

            if (HasHostChanged(lobby))
            {
                onLobbyHostChanged?.Invoke(lobby);
                _ = RecoverFromHostLossAsync();
            }
        }

        private void BeginLobbyEntry(Lobby lobby, ulong hostSteamId)
        {
            departedLobbyIds.Remove(lobby.Id);
            lobbyState.CurrentLobby = lobby;
            lobbyState.HostSteamId = hostSteamId;

            // Publish the known lobby synchronously before asking Steam for a remote
            // list. This gives the UI a deterministic state even during propagation.
            UpsertLobbyCache(lobby);
            PublishLobbyCache();
        }

        private void MergeCurrentLobby(List<Lobby> snapshot)
        {
            if (!lobbyState.CurrentLobby.HasValue)
            {
                return;
            }

            var current = lobbyState.CurrentLobby.Value;
            snapshot.RemoveAll(lobby => lobby.Id == current.Id);
            snapshot.Insert(0, current);
        }

        private void ReplaceLobbyCache(List<Lobby> snapshot)
        {
            lobbyCache.Clear();
            lobbyCache.AddRange(snapshot.Where(lobby =>
                !departedLobbyIds.Contains(lobby.Id)
                && !IsLobbySessionClosed(lobby)));
        }

        private void UpsertLobbyCache(Lobby lobby)
        {
            var index = lobbyCache.FindIndex(cached => cached.Id == lobby.Id);
            if (index >= 0)
            {
                lobbyCache[index] = lobby;
                return;
            }

            lobbyCache.Insert(0, lobby);
        }

        private void PublishLobbyCache()
        {
            NotifyStateChanged();
            onLobbiesRefreshed?.Invoke(LobbyCache);
        }

        private async Task<bool> TryLoadLobbySceneOnEnterAsync()
        {
            return await (onTryLoadLobbySceneAsync?.Invoke() ?? Task.FromResult(true));
        }

        private async Task<bool> TrySwitchLobbySceneAsync(string previousSceneName)
        {
            if (sceneTransitionController != null)
            {
                return await sceneTransitionController.SwitchLobbySceneAsync(previousSceneName);
            }

            return await TryLoadLobbySceneOnEnterAsync();
        }

        private async Task<bool> ChangeHostedLobbyStageCoreAsync(string stageSceneName)
        {
            if (!lobbyState.CurrentLobby.HasValue || !lobbyState.IsHost)
            {
                return false;
            }

            var previousSceneName = sceneLoader?.LobbySceneName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(stageSceneName)
                && SceneLoadUtility.AreSameSceneReference(previousSceneName, stageSceneName))
            {
                return true;
            }

            ApplySelectedStageScene(stageSceneName);
            if (networkSession == null
                || !await networkSession.SwitchStageSceneAsync(sceneLoader?.LobbySceneName, previousSceneName))
            {
                sceneLoader?.SetLobbySceneName(previousSceneName);
                return false;
            }

            if (!await TrySwitchLobbySceneAsync(previousSceneName))
            {
                sceneLoader?.SetLobbySceneName(previousSceneName);
                return false;
            }

            var lobby = lobbyState.CurrentLobby.Value;
            lobby.SetData(LobbyDataKeys.StageScene, stageSceneName);
            lobbyState.CurrentLobby = lobby;
            UpsertLobbyCache(lobby);
            PublishLobbyCache();
            return true;
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

        private async Task FailAndLeaveLobbyAsync(Lobby lobby, bool unloadLobbyScene)
        {
            lobby.Leave();
            if (networkSession != null)
            {
                await networkSession.StopAsync();
            }

            if (unloadLobbyScene)
            {
                if (sceneLoader != null)
                {
                    await sceneLoader.HandleLobbyLeftAsync(sceneLoader.LobbySceneName);
                }
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
