using Steamworks.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class LobbyView
    {
        private readonly UIDocument uiDocument;
        private StyleSheet appliedThemeStyleSheet;
        private Label connectionLabel;
        private Label currentLobbyLabel;
        private Label infoLabel;
        private Label memberConnectionStrengthLabel;
        private ScrollView onlineMemberListView;
        private TextField lobbyNameField;
        private DropdownField stageSceneField;
        private TextField lobbyIdField;
        private TextField lobbyNameSearchField;
        private ScrollView lobbyListView;
        private Button createButton;
        private Button joinByIdButton;
        private Button searchByNameButton;
        private Button refreshButton;
        private Button leaveButton;

        public LobbyView(UIDocument uiDocument)
        {
            this.uiDocument = uiDocument;
        }

        public bool IsRenderable => connectionLabel != null && currentLobbyLabel != null && lobbyListView != null && onlineMemberListView != null && memberConnectionStrengthLabel != null;
        public string LobbyName => lobbyNameField != null ? lobbyNameField.value : string.Empty;
        public string SelectedStageSceneName => stageSceneField != null ? stageSceneField.value : string.Empty;
        public string LobbyIdRaw => lobbyIdField != null ? lobbyIdField.value : string.Empty;
        public string LobbyNameSearchRaw => lobbyNameSearchField != null ? lobbyNameSearchField.value : string.Empty;

        public void Build(
            VisualTreeAsset layoutAsset,
            StyleSheet themeStyleSheet,
            VisualTreeAsset statusPanelAsset,
            VisualTreeAsset onlineMembersPanelAsset,
            VisualTreeAsset createPanelAsset,
            VisualTreeAsset joinPanelAsset,
            VisualTreeAsset searchPanelAsset,
            VisualTreeAsset actionsPanelAsset,
            VisualTreeAsset listPanelAsset)
        {
            var root = uiDocument.rootVisualElement;
            root.Clear();

            if (appliedThemeStyleSheet != null && appliedThemeStyleSheet != themeStyleSheet)
            {
                root.styleSheets.Remove(appliedThemeStyleSheet);
                appliedThemeStyleSheet = null;
            }

            if (themeStyleSheet != null && !root.styleSheets.Contains(themeStyleSheet))
            {
                root.styleSheets.Add(themeStyleSheet);
            }

            appliedThemeStyleSheet = themeStyleSheet;

            if (layoutAsset == null)
            {
                ClearReferences();
                Debug.LogError("SteamLobbyView.uxml not found. Place it in Resources/UI/SteamLobby.");
                return;
            }

            layoutAsset.CloneTree(root);

            InjectPanel(root, "status-panel-host", statusPanelAsset);
            InjectPanel(root, "online-members-panel-host", onlineMembersPanelAsset);
            InjectPanel(root, "create-panel-host", createPanelAsset);
            InjectPanel(root, "join-panel-host", joinPanelAsset);
            InjectPanel(root, "search-panel-host", searchPanelAsset);
            InjectPanel(root, "actions-panel-host", actionsPanelAsset);
            InjectPanel(root, "list-panel-host", listPanelAsset);

            connectionLabel = root.Q<Label>("connection-label");
            currentLobbyLabel = root.Q<Label>("current-lobby-label");
            infoLabel = root.Q<Label>("info-label");
            memberConnectionStrengthLabel = root.Q<Label>("member-connection-strength-label");
            onlineMemberListView = root.Q<ScrollView>("online-member-list-view");
            lobbyNameField = root.Q<TextField>("lobby-name-field");
            stageSceneField = root.Q<DropdownField>("stage-scene-field");
            lobbyIdField = root.Q<TextField>("lobby-id-field");
            lobbyNameSearchField = root.Q<TextField>("lobby-name-search-field");
            lobbyListView = root.Q<ScrollView>("lobby-list-view");
            createButton = root.Q<Button>("create-button");
            joinByIdButton = root.Q<Button>("join-by-id-button");
            searchByNameButton = root.Q<Button>("search-by-name-button");
            refreshButton = root.Q<Button>("refresh-button");
            leaveButton = root.Q<Button>("leave-button");
        }

        public void BindActions(System.Action onCreate, System.Action onJoinById, System.Action onSearchByName, System.Action onRefresh, System.Action onLeave)
        {
            if (createButton != null)
            {
                createButton.clicked += onCreate;
            }

            if (joinByIdButton != null)
            {
                joinByIdButton.clicked += onJoinById;
            }

            if (searchByNameButton != null)
            {
                searchByNameButton.clicked += onSearchByName;
            }

            if (refreshButton != null)
            {
                refreshButton.clicked += onRefresh;
            }

            if (leaveButton != null)
            {
                leaveButton.clicked += onLeave;
            }
        }

        public void UnbindActions(System.Action onCreate, System.Action onJoinById, System.Action onSearchByName, System.Action onRefresh, System.Action onLeave)
        {
            if (createButton != null)
            {
                createButton.clicked -= onCreate;
            }

            if (joinByIdButton != null)
            {
                joinByIdButton.clicked -= onJoinById;
            }

            if (searchByNameButton != null)
            {
                searchByNameButton.clicked -= onSearchByName;
            }

            if (refreshButton != null)
            {
                refreshButton.clicked -= onRefresh;
            }

            if (leaveButton != null)
            {
                leaveButton.clicked -= onLeave;
            }
        }

        public void RenderServiceMissing()
        {
            connectionLabel.text = "Steam: SteamLobbyService not found";
            currentLobbyLabel.text = "Current Lobby: none";
            lobbyListView.Clear();
            SetButtonsEnabled(false);
        }

        public void RenderStatus(bool isReady, string localPlayerName, bool isInLobby, ulong currentLobbyId)
        {
            connectionLabel.text = isReady
                ? $"Steam: {localPlayerName}"
                : "Steam: not connected";

            currentLobbyLabel.text = isInLobby
                ? $"Current Lobby: {currentLobbyId}"
                : "Current Lobby: none";

            lobbyListView.Clear();
        }

        public void SetWaitingConnection()
        {
            if (connectionLabel != null)
            {
                connectionLabel.text = "Steam: waiting for NetworkManager/FacepunchTransport...";
            }
        }

        public void SetInfo(string text)
        {
            if (infoLabel != null)
            {
                infoLabel.text = text;
            }
        }

        public void SetButtonsEnabled(bool enabled)
        {
            createButton?.SetEnabled(enabled);
            joinByIdButton?.SetEnabled(enabled);
            searchByNameButton?.SetEnabled(enabled);
            refreshButton?.SetEnabled(enabled);
            leaveButton?.SetEnabled(enabled);
        }

        public void ShowNoLobbies(string message = "Lobby が見つかりませんでした。")
        {
            lobbyListView.Clear();
            var emptyLabel = new Label(message);
            emptyLabel.AddToClassList("muted");
            lobbyListView.Add(emptyLabel);
        }

        public void ShowLobbies(
            System.Collections.Generic.IReadOnlyList<Lobby> lobbies,
            bool canJoin,
            System.Func<Lobby, string> getLobbyDisplayName,
            System.Func<Lobby, bool> isHostedByLocalPlayer,
            ulong currentLobbyId,
            System.Action<ulong> onJoinLobby)
        {
            lobbyListView.Clear();

            foreach (var lobby in lobbies)
            {
                var row = new VisualElement();
                row.AddToClassList("lobby-row");

                var isLocalHostLobby = isHostedByLocalPlayer != null && isHostedByLocalPlayer(lobby);
                var isCurrentLobby = currentLobbyId != 0 && lobby.Id == currentLobbyId;
                var isFullLobby = lobby.MemberCount >= lobby.MaxMembers;

                var name = getLobbyDisplayName(lobby);
                var label = new Label($"{name}  {lobby.MemberCount}/{lobby.MaxMembers}  [{lobby.Id}]");
                label.AddToClassList("lobby-row-label");
                row.Add(label);

                var actions = new VisualElement();
                actions.AddToClassList("lobby-row-actions");

                if (isLocalHostLobby)
                {
                    var hostBadge = new Label("HOST");
                    hostBadge.AddToClassList("lobby-badge");
                    hostBadge.AddToClassList("host-badge");
                    actions.Add(hostBadge);
                }

                if (isCurrentLobby)
                {
                    var joinedBadge = new Label("JOINED");
                    joinedBadge.AddToClassList("lobby-badge");
                    joinedBadge.AddToClassList("joined-badge");
                    actions.Add(joinedBadge);
                }
                else if (isFullLobby)
                {
                    var fullBadge = new Label("FULL");
                    fullBadge.AddToClassList("lobby-badge");
                    fullBadge.AddToClassList("full-badge");
                    actions.Add(fullBadge);
                }

                var joinButton = new Button(() => onJoinLobby(lobby.Id))
                {
                    text = "Join"
                };
                joinButton.AddToClassList("join-button");
                joinButton.SetEnabled(canJoin && !isLocalHostLobby && !isCurrentLobby && !isFullLobby);
                actions.Add(joinButton);

                row.Add(actions);
                lobbyListView.Add(row);
            }
        }

        public void SetCreatableStageScenes(System.Collections.Generic.IReadOnlyList<string> sceneNames)
        {
            if (stageSceneField == null)
            {
                return;
            }

            stageSceneField.choices.Clear();

            if (sceneNames == null || sceneNames.Count == 0)
            {
                stageSceneField.SetEnabled(false);
                stageSceneField.value = string.Empty;
                return;
            }

            foreach (var sceneName in sceneNames)
            {
                if (!string.IsNullOrWhiteSpace(sceneName))
                {
                    stageSceneField.choices.Add(sceneName);
                }
            }

            if (stageSceneField.choices.Count == 0)
            {
                stageSceneField.SetEnabled(false);
                stageSceneField.value = string.Empty;
                return;
            }

            stageSceneField.SetEnabled(true);
            if (string.IsNullOrWhiteSpace(stageSceneField.value) || !stageSceneField.choices.Contains(stageSceneField.value))
            {
                stageSceneField.value = stageSceneField.choices[0];
            }
        }

        public void ShowOnlineMembers(System.Collections.Generic.IReadOnlyList<string> memberNames, bool isInLobby, string connectionStrengthText)
        {
            if (onlineMemberListView == null)
            {
                return;
            }

            if (memberConnectionStrengthLabel != null)
            {
                var hasConnectionText = isInLobby && !string.IsNullOrWhiteSpace(connectionStrengthText);
                memberConnectionStrengthLabel.style.display = hasConnectionText ? DisplayStyle.Flex : DisplayStyle.None;
                memberConnectionStrengthLabel.text = hasConnectionText ? connectionStrengthText : string.Empty;
            }

            onlineMemberListView.Clear();

            if (!isInLobby)
            {
                var emptyLabel = new Label("ロビー未参加");
                emptyLabel.AddToClassList("muted");
                onlineMemberListView.Add(emptyLabel);
                return;
            }

            if (memberNames == null || memberNames.Count == 0)
            {
                var emptyLabel = new Label("メンバー情報なし");
                emptyLabel.AddToClassList("muted");
                onlineMemberListView.Add(emptyLabel);
                return;
            }

            foreach (var memberName in memberNames)
            {
                var memberLabel = new Label(string.IsNullOrWhiteSpace(memberName) ? "Unknown" : memberName);
                memberLabel.AddToClassList("online-member-name");
                onlineMemberListView.Add(memberLabel);
            }
        }

        private void InjectPanel(VisualElement root, string hostName, VisualTreeAsset panelAsset)
        {
            var host = root.Q<VisualElement>(hostName);
            if (host == null)
            {
                return;
            }

            host.Clear();
            if (panelAsset != null)
            {
                panelAsset.CloneTree(host);
            }
        }

        private void ClearReferences()
        {
            connectionLabel = null;
            currentLobbyLabel = null;
            infoLabel = null;
            memberConnectionStrengthLabel = null;
            onlineMemberListView = null;
            lobbyNameField = null;
            stageSceneField = null;
            lobbyIdField = null;
            lobbyNameSearchField = null;
            lobbyListView = null;
            createButton = null;
            joinByIdButton = null;
            searchByNameButton = null;
            refreshButton = null;
            leaveButton = null;
        }
    }
}
