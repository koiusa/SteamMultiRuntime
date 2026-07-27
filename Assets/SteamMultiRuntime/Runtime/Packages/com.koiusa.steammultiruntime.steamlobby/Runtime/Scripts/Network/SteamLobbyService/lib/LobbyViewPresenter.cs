using Steamworks.Data;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class LobbyViewPresenter
    {
        private readonly LobbyViewContext context;
        private readonly LobbyViewNavigationController navigation;

        public LobbyViewPresenter(LobbyViewContext context, LobbyViewNavigationController navigation)
        {
            this.context = context;
            this.navigation = navigation;
        }

        public bool IsRenderable => context.ConnectionLabel != null && context.CurrentLobbyLabel != null &&
            context.LobbyListView != null && context.OnlineMemberListView != null && context.MemberConnectionStrengthLabel != null;

        public void RenderServiceMissing()
        {
            context.ConnectionLabel.text = "Steam: SteamLobbyService not found";
            context.CurrentLobbyLabel.text = "Current Lobby: none";
            context.LobbyListView.Clear();
            SetButtonsEnabled(false);
        }

        public void RenderStatus(bool isReady, string localPlayerName, bool isInLobby, ulong currentLobbyId)
        {
            context.ConnectionLabel.text = isReady ? $"Steam: {localPlayerName}" : "Steam: not connected";
            context.CurrentLobbyLabel.text = isInLobby ? $"Current Lobby: {currentLobbyId}" : "Current Lobby: none";
            context.LobbyListView.Clear();
        }

        public void SetWaitingConnection()
        {
            if (context.ConnectionLabel != null)
                context.ConnectionLabel.text = "Steam: waiting for NetworkManager/FacepunchTransport...";
        }

        public void SetInfo(string text)
        {
            if (context.InfoLabel != null)
                context.InfoLabel.text = text;
        }

        public void SetButtonsEnabled(bool enabled)
        {
            context.CreateButton?.SetEnabled(enabled);
            context.JoinByIdButton?.SetEnabled(enabled);
            context.SearchByNameButton?.SetEnabled(enabled);
            context.RefreshButton?.SetEnabled(enabled);
            context.LeaveButton?.SetEnabled(enabled);
        }

        public void ShowNoLobbies(string message = "Lobby が見つかりませんでした。")
        {
            context.LobbyListView.Clear();
            navigation.ClearLobbyRows();
            var emptyLabel = new Label(message);
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
                    AddLobbyBadge(actions, "HOST", "host-badge");
                if (isCurrentLobby)
                    AddLobbyBadge(actions, "JOINED", "joined-badge");
                else if (isFullLobby)
                    AddLobbyBadge(actions, "FULL", "full-badge");

                var joinButton = new Button(() => onJoinLobby(lobby.Id)) { text = "Join", focusable = false };
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
                var emptyLabel = new Label(isInLobby ? "メンバー情報なし" : "ロビー未参加");
                emptyLabel.AddToClassList("muted");
                context.OnlineMemberListView.Add(emptyLabel);
                return;
            }

            foreach (var memberName in memberNames)
            {
                var memberLabel = new Label(string.IsNullOrWhiteSpace(memberName) ? "Unknown" : memberName);
                memberLabel.AddToClassList("online-member-name");
                context.OnlineMemberListView.Add(memberLabel);
            }
        }
    }
}




