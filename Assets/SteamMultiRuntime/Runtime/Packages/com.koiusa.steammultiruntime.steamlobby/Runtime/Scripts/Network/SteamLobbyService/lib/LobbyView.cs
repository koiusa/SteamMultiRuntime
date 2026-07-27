using Steamworks.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class LobbyView
    {
        private const string DropdownPopupStyleSheetPath = "UI/SteamLobby/Styles/SteamLobbyDropdownPopup";

        private readonly UIDocument uiDocument;
        private StyleSheet appliedThemeStyleSheet;
        private StyleSheet dropdownPopupStyleSheet;
        private VisualElement dropdownPopupStyleHost;
        private VisualElement pendingPanelRoot;
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
        private VisualElement createSectionHost;
        private VisualElement searchSectionHost;
        private VisualElement listSectionHost;
        private LobbyViewContext context;
        private LobbyViewPresenter presenter;
        private LobbyViewNavigationController navigation;
        private LobbyGamepadTextInputController gamepadTextInput;

        public LobbyView(UIDocument uiDocument)
        {
            this.uiDocument = uiDocument;
        }

        public bool IsRenderable => presenter != null && presenter.IsRenderable;
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
            DisposeControllers();
            var root = uiDocument.rootVisualElement;
            root.Clear();
            ApplyDropdownPopupStyle(root);

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

            createSectionHost = root.Q<VisualElement>("create-focus-group");
            searchSectionHost = root.Q<VisualElement>("search-focus-group");
            listSectionHost = root.Q<VisualElement>("list-focus-group");
            context = CreateContext();
            gamepadTextInput = new LobbyGamepadTextInputController(lobbyNameField, lobbyIdField, lobbyNameSearchField);
            navigation = new LobbyViewNavigationController(context);
            presenter = new LobbyViewPresenter(context, navigation);
            navigation.FocusInitialControl();
        }

        public void RenderServiceMissing() => presenter?.RenderServiceMissing();
        public void RenderStatus(bool isReady, string localPlayerName, bool isInLobby, ulong currentLobbyId) =>
            presenter?.RenderStatus(isReady, localPlayerName, isInLobby, currentLobbyId);
        public void SetWaitingConnection() => presenter?.SetWaitingConnection();
        public void SetInfo(string text) => presenter?.SetInfo(text);
        public void SetButtonsEnabled(bool enabled) => presenter?.SetButtonsEnabled(enabled);
        public void ShowNoLobbies(string message = "Lobby が見つかりませんでした。") => presenter?.ShowNoLobbies(message);
        public void ShowLobbies(
            System.Collections.Generic.IReadOnlyList<Lobby> lobbies,
            bool canJoin,
            System.Func<Lobby, string> getLobbyDisplayName,
            System.Func<Lobby, bool> isHostedByLocalPlayer,
            System.Func<Lobby, (int memberCount, int maxMembers)> getPlayerCount,
            ulong currentLobbyId,
            System.Action<ulong> onJoinLobby) =>
            presenter?.ShowLobbies(lobbies, canJoin, getLobbyDisplayName, isHostedByLocalPlayer, getPlayerCount, currentLobbyId, onJoinLobby);
        public void SetCreatableStageScenes(System.Collections.Generic.IReadOnlyList<string> sceneNames) => presenter?.SetCreatableStageScenes(sceneNames);
        public void ShowOnlineMembers(System.Collections.Generic.IReadOnlyList<string> memberNames, bool isInLobby, string connectionStrengthText) =>
            presenter?.ShowOnlineMembers(memberNames, isInLobby, connectionStrengthText);
        public void FocusInitialControl() => navigation?.FocusInitialControl();
        public void FocusPreviousSection() => navigation?.FocusPreviousSection();
        public void FocusNextSection() => navigation?.FocusNextSection();

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

        private void ApplyDropdownPopupStyle(VisualElement root)
        {
            dropdownPopupStyleSheet ??= Resources.Load<StyleSheet>(DropdownPopupStyleSheetPath);
            if (dropdownPopupStyleSheet == null)
            {
                Debug.LogWarning($"LobbyView: Dropdown popup stylesheet not found at '{DropdownPopupStyleSheetPath}'.");
                return;
            }

            if (root.panel != null)
            {
                AttachDropdownPopupStyle(root.panel.visualTree);
                return;
            }

            if (pendingPanelRoot != null)
            {
                pendingPanelRoot.UnregisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
            }

            pendingPanelRoot = root;
            pendingPanelRoot.RegisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
        }

        private void OnRootAttachedToPanel(AttachToPanelEvent evt)
        {
            pendingPanelRoot?.UnregisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
            pendingPanelRoot = null;
            AttachDropdownPopupStyle(evt.destinationPanel.visualTree);
        }

        private void AttachDropdownPopupStyle(VisualElement panelRoot)
        {
            if (dropdownPopupStyleHost == panelRoot)
            {
                return;
            }

            if (dropdownPopupStyleHost != null)
            {
                dropdownPopupStyleHost.styleSheets.Remove(dropdownPopupStyleSheet);
            }

            dropdownPopupStyleHost = panelRoot;
            if (!dropdownPopupStyleHost.styleSheets.Contains(dropdownPopupStyleSheet))
            {
                dropdownPopupStyleHost.styleSheets.Add(dropdownPopupStyleSheet);
            }
        }

        private LobbyViewContext CreateContext()
        {
            return new LobbyViewContext
            {
                UiDocument = uiDocument,
                ConnectionLabel = connectionLabel,
                CurrentLobbyLabel = currentLobbyLabel,
                InfoLabel = infoLabel,
                MemberConnectionStrengthLabel = memberConnectionStrengthLabel,
                OnlineMemberListView = onlineMemberListView,
                LobbyNameField = lobbyNameField,
                StageSceneField = stageSceneField,
                LobbyIdField = lobbyIdField,
                LobbyNameSearchField = lobbyNameSearchField,
                LobbyListView = lobbyListView,
                CreateButton = createButton,
                JoinByIdButton = joinByIdButton,
                SearchByNameButton = searchByNameButton,
                RefreshButton = refreshButton,
                LeaveButton = leaveButton,
                CreateSectionHost = createSectionHost,
                SearchSectionHost = searchSectionHost,
                ListSectionHost = listSectionHost
            };
        }

        public void Dispose()
        {
            DisposeControllers();
            if (pendingPanelRoot != null)
            {
                pendingPanelRoot.UnregisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
                pendingPanelRoot = null;
            }

            if (dropdownPopupStyleHost != null && dropdownPopupStyleSheet != null)
            {
                dropdownPopupStyleHost.styleSheets.Remove(dropdownPopupStyleSheet);
                dropdownPopupStyleHost = null;
            }
        }

        private void DisposeControllers()
        {
            gamepadTextInput?.Dispose();
            gamepadTextInput = null;
            presenter = null;
            navigation = null;
            context = null;
        }

        private void ClearReferences()
        {
            DisposeControllers();
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
            createSectionHost = null;
            searchSectionHost = null;
            listSectionHost = null;
        }
    }
}
