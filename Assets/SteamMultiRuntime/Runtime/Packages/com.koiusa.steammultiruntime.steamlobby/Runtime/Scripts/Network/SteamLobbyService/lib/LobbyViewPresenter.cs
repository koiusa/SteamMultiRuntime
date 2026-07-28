using Steamworks.Data;
using UnityEngine.UIElements;
using Koiusa.SteamMultiRuntime.Localization;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class LobbyViewPresenter : System.IDisposable
    {
        private readonly LobbyViewContext context;
        private readonly LobbyViewNavigationController navigation;
        private readonly LocalizedTextBinding connectionBinding;
        private readonly LocalizedTextBinding currentLobbyBinding;
        private readonly LocalizedTextBinding infoBinding;

        public LobbyViewPresenter(LobbyViewContext context, LobbyViewNavigationController navigation)
        {
            this.context = context;
            this.navigation = navigation;
            connectionBinding = new LocalizedTextBinding(context.ConnectionLabel);
            currentLobbyBinding = new LocalizedTextBinding(context.CurrentLobbyLabel);
            infoBinding = new LocalizedTextBinding(context.InfoLabel);
        }

        public bool IsRenderable => context.ConnectionLabel != null && context.CurrentLobbyLabel != null &&
            context.LobbyListView != null && context.OnlineMemberListView != null && context.MemberConnectionStrengthLabel != null;

        public void RenderServiceMissing()
        {
            connectionBinding.Set("lobby.service_missing");
            currentLobbyBinding.Set("lobby.current_none");
            context.LobbyListView.Clear();
            SetButtonsEnabled(false);
        }

        public void RenderStatus(bool isReady, string localPlayerName, bool isInLobby, ulong currentLobbyId)
        {
            if (isReady) connectionBinding.Set("lobby.steam_user", localPlayerName);
            else connectionBinding.Set("lobby.not_connected");
            if (isInLobby) currentLobbyBinding.Set("lobby.current", currentLobbyId);
            else currentLobbyBinding.Set("lobby.current_none");
            context.LobbyListView.Clear();
        }

        public void SetWaitingConnection()
        {
            if (context.ConnectionLabel != null)
                connectionBinding.Set("lobby.steam_waiting");
        }

        public void SetInfo(string text)
        {
            if (context.InfoLabel != null)
                infoBinding.Set(text);
        }

        public void SetButtonsEnabled(bool enabled)
        {
            context.CreateButton?.SetEnabled(enabled);
            context.JoinByIdButton?.SetEnabled(enabled);
            context.SearchByNameButton?.SetEnabled(enabled);
            context.RefreshButton?.SetEnabled(enabled);
            context.LeaveButton?.SetEnabled(enabled);
        }

        public void ShowNoLobbies(string messageKey = "lobby.not_found")
        {
            context.LobbyListView.Clear();
            navigation.ClearLobbyRows();
            var emptyLabel = new Label(GameLocalization.Get(messageKey));
            emptyLabel.AddToClassList("muted");
            context.LobbyListView.Add(emptyLabel);
        }

        public void ShowLobbies(
            System.Collections.Generic.IReadOnlyList<Lobby> lobbies,
            bool canJoin,
            System.Func<Lobby, string> getLobbyDisplayName,
            System.Func<Lobby, bool> isHostedByLocalPlayer,
            System.Func<Lobby, (int memberCount, int maxMembers)> getPlayerCount,
            ulong currentLobbyId,
            System.Action<ulong> onJoinLobby)
        {
            var rememberedLobbyId = navigation.RememberedLobbyId;
            context.LobbyListView.Clear();
            navigation.ClearLobbyRows();

            foreach (var lobby in lobbies)
            {
                var row = new VisualElement();
                row.AddToClassList("lobby-row");
                var isLocalHostLobby = isHostedByLocalPlayer != null && isHostedByLocalPlayer(lobby);
                var isCurrentLobby = currentLobbyId != 0 && lobby.Id == currentLobbyId;
                var (memberCount, maxMembers) = getPlayerCount != null ? getPlayerCount(lobby) : (lobby.MemberCount, lobby.MaxMembers);
                var isFullLobby = memberCount >= maxMembers;
                var canJoinLobby = canJoin && !isLocalHostLobby && !isCurrentLobby && !isFullLobby;

                row.focusable = canJoinLobby;
                row.tabIndex = canJoinLobby ? 0 : -1;
                row.userData = lobby.Id;
                var label = new Label($"{getLobbyDisplayName(lobby)}  {memberCount}/{maxMembers}  [{lobby.Id}]");
                label.AddToClassList("lobby-row-label");
                row.Add(label);

                var actions = new VisualElement();
                actions.AddToClassList("lobby-row-actions");
                if (isLocalHostLobby)
                    AddLobbyBadge(actions, GameLocalization.Get("lobby.host"), "host-badge");
                if (isCurrentLobby)
                    AddLobbyBadge(actions, GameLocalization.Get("lobby.joined"), "joined-badge");
                else if (isFullLobby)
                    AddLobbyBadge(actions, GameLocalization.Get("lobby.full"), "full-badge");

                var joinButton = new Button(() => onJoinLobby(lobby.Id)) { text = GameLocalization.Get("lobby.join"), focusable = false };
                joinButton.AddToClassList("join-button");
                joinButton.SetEnabled(canJoinLobby);
                actions.Add(joinButton);
                row.Add(actions);
                context.LobbyListView.Add(row);

                if (!canJoinLobby)
                    continue;

                var lobbyId = lobby.Id;
                navigation.RegisterLobbyRow(row, lobbyId, onJoinLobby);
                if (lobby.Id == rememberedLobbyId)
                    // Registration order already preserves this row for focus restoration.
                    navigation.SetRememberedLobbyRow(row);
            }

            navigation.RestoreFocusAfterListRebuild();
        }

        private static void AddLobbyBadge(VisualElement actions, string text, string className)
        {
            var badge = new Label(text);
            badge.AddToClassList("lobby-badge");
            badge.AddToClassList(className);
            actions.Add(badge);
        }

        public void SetCreatableStageScenes(System.Collections.Generic.IReadOnlyList<string> sceneNames)
        {
            if (context.StageSceneField == null)
                return;

            context.StageSceneField.choices.Clear();
            if (sceneNames != null)
            {
                foreach (var sceneName in sceneNames)
                {
                    if (!string.IsNullOrWhiteSpace(sceneName))
                        context.StageSceneField.choices.Add(sceneName);
                }
            }

            var hasScenes = context.StageSceneField.choices.Count > 0;
            context.StageSceneField.SetEnabled(hasScenes);
            if (!hasScenes)
                context.StageSceneField.value = string.Empty;
            else if (string.IsNullOrWhiteSpace(context.StageSceneField.value) || !context.StageSceneField.choices.Contains(context.StageSceneField.value))
                context.StageSceneField.value = context.StageSceneField.choices[0];
        }

        public void ShowOnlineMembers(System.Collections.Generic.IReadOnlyList<string> memberNames, bool isInLobby, string connectionStrengthText)
        {
            if (context.OnlineMemberListView == null)
                return;

            if (context.MemberConnectionStrengthLabel != null)
            {
                var visible = isInLobby && !string.IsNullOrWhiteSpace(connectionStrengthText);
                context.MemberConnectionStrengthLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                context.MemberConnectionStrengthLabel.text = visible ? connectionStrengthText : string.Empty;
            }

            context.OnlineMemberListView.Clear();
            if (!isInLobby || memberNames == null || memberNames.Count == 0)
            {
                var emptyLabel = new Label(GameLocalization.Get(isInLobby ? "lobby.no_member_info" : "lobby.not_joined"));
                emptyLabel.AddToClassList("muted");
                context.OnlineMemberListView.Add(emptyLabel);
                return;
            }

            foreach (var memberName in memberNames)
            {
                var memberLabel = new Label(string.IsNullOrWhiteSpace(memberName) ? GameLocalization.Get("common.unknown") : memberName);
                memberLabel.AddToClassList("online-member-name");
                context.OnlineMemberListView.Add(memberLabel);
            }
        }

        public void Dispose()
        {
            connectionBinding.Dispose();
            currentLobbyBinding.Dispose();
            infoBinding.Dispose();
        }
    }
}




