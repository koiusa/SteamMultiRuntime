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
        private VisualElement createSectionHost;
        private VisualElement searchSectionHost;
        private VisualElement listSectionHost;
        private readonly System.Collections.Generic.List<VisualElement> createControls = new System.Collections.Generic.List<VisualElement>();
        private readonly System.Collections.Generic.List<VisualElement> searchControls = new System.Collections.Generic.List<VisualElement>();
        private readonly System.Collections.Generic.List<VisualElement> lobbyRows = new System.Collections.Generic.List<VisualElement>();
        private VisualElement lastCreateControl;
        private VisualElement lastSearchControl;
        private VisualElement lastLobbyRow;
        private FocusSection activeFocusSection = FocusSection.Create;

        private enum FocusSection
        {
            Create,
            Search,
            LobbyList
        }

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
            if (lobbyListView != null)
            {
                lobbyListView.focusable = true;
                lobbyListView.tabIndex = -1;
                lobbyListView.RegisterCallback<FocusInEvent>(_ => SetActiveFocusSection(FocusSection.LobbyList));
            }
            createButton = root.Q<Button>("create-button");
            joinByIdButton = root.Q<Button>("join-by-id-button");
            searchByNameButton = root.Q<Button>("search-by-name-button");
            refreshButton = root.Q<Button>("refresh-button");
            leaveButton = root.Q<Button>("leave-button");

            createSectionHost = root.Q<VisualElement>("create-focus-group");
            searchSectionHost = root.Q<VisualElement>("search-focus-group");
            listSectionHost = root.Q<VisualElement>("list-focus-group");
            ConfigureFocusSections(root);

            FocusInitialControl();
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
            lobbyRows.Clear();
            lastLobbyRow = null;
            var emptyLabel = new Label(message);
            emptyLabel.AddToClassList("muted");
            lobbyListView.Add(emptyLabel);

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
            var rememberedLobbyId = lastLobbyRow?.userData is ulong lobbyIdValue ? lobbyIdValue : 0;
            lobbyListView.Clear();
            lobbyRows.Clear();
            lastLobbyRow = null;

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

                var name = getLobbyDisplayName(lobby);
                var label = new Label($"{name}  {memberCount}/{maxMembers}  [{lobby.Id}]");
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
                joinButton.SetEnabled(canJoinLobby);
                // The row is the gamepad/keyboard target. The button remains
                // clickable by pointer without creating a duplicate focus stop.
                joinButton.focusable = false;
                actions.Add(joinButton);

                row.Add(actions);
                lobbyListView.Add(row);

                if (canJoinLobby)
                {
                    var lobbyId = lobby.Id;
                    row.RegisterCallback<FocusInEvent>(_ => OnLobbyRowFocused(row));
                    row.RegisterCallback<NavigationSubmitEvent>(evt =>
                    {
                        evt.PreventDefault();
                        evt.StopPropagation();
                        onJoinLobby?.Invoke(lobbyId);
                    });
                    lobbyRows.Add(row);

                    if (lobby.Id == rememberedLobbyId)
                        lastLobbyRow = row;
                }
            }

            RestoreFocusAfterListRebuild();
        }

        public void FocusInitialControl()
        {
            var root = uiDocument.rootVisualElement;
            root.schedule.Execute(() =>
            {
                if (HasValidFocus(root))
                    return;

                FocusCreateSection();
            });
        }

        public void FocusPreviousSection()
        {
            switch (activeFocusSection)
            {
                case FocusSection.Create:
                    FocusLobbySection();
                    break;
                case FocusSection.Search:
                    FocusCreateSection();
                    break;
                default:
                    FocusSearchSection();
                    break;
            }
        }

        public void FocusNextSection()
        {
            switch (activeFocusSection)
            {
                case FocusSection.Create:
                    FocusSearchSection();
                    break;
                case FocusSection.Search:
                    FocusLobbySection();
                    break;
                default:
                    FocusCreateSection();
                    break;
            }
        }

        private void ConfigureFocusSections(VisualElement root)
        {
            root.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove);
            root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove);

            createControls.Clear();
            AddCreateControl(lobbyNameField);
            AddCreateControl(stageSceneField);
            AddCreateControl(createButton);
            lastCreateControl = stageSceneField;

            searchControls.Clear();
            AddSearchControl(lobbyIdField);
            AddSearchControl(joinByIdButton);
            AddSearchControl(lobbyNameSearchField);
            AddSearchControl(searchByNameButton);
            AddSearchControl(refreshButton);
            AddSearchControl(leaveButton);

            SetActiveFocusSection(FocusSection.Create);
        }

        private void AddCreateControl(VisualElement control)
        {
            if (control == null)
                return;

            createControls.Add(control);
            control.RegisterCallback<FocusInEvent>(_ =>
            {
                lastCreateControl = control;
                SetActiveFocusSection(FocusSection.Create);
            });
        }

        private void AddSearchControl(VisualElement control)
        {
            if (control == null)
                return;

            searchControls.Add(control);
            control.RegisterCallback<FocusInEvent>(_ =>
            {
                lastSearchControl = control;
                SetActiveFocusSection(FocusSection.Search);
            });
        }

        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            var root = uiDocument.rootVisualElement;
            var focused = root.focusController?.focusedElement as VisualElement;
            if (focused == null)
                return;

            var searchControl = FindContaining(focused, searchControls);
            var createControl = FindContaining(focused, createControls);
            var lobbyRow = FindContaining(focused, lobbyRows);
            var isInsideLobbyList = lobbyListView != null &&
                                    (focused == lobbyListView || lobbyListView.Contains(focused));

            if ((evt.direction == NavigationMoveEvent.Direction.Up || evt.direction == NavigationMoveEvent.Direction.Down) &&
                createControl != null)
            {
                FocusAdjacent(createControls, createControl, evt.direction == NavigationMoveEvent.Direction.Down ? 1 : -1);
                evt.PreventDefault();
                evt.StopPropagation();
            }
            else if ((evt.direction == NavigationMoveEvent.Direction.Up || evt.direction == NavigationMoveEvent.Direction.Down) &&
                searchControl != null)
            {
                FocusAdjacent(searchControls, searchControl, evt.direction == NavigationMoveEvent.Direction.Down ? 1 : -1);
                evt.PreventDefault();
                evt.StopPropagation();
            }
            else if ((evt.direction == NavigationMoveEvent.Direction.Up || evt.direction == NavigationMoveEvent.Direction.Down) &&
                     lobbyRow != null)
            {
                FocusAdjacent(lobbyRows, lobbyRow, evt.direction == NavigationMoveEvent.Direction.Down ? 1 : -1);
                evt.PreventDefault();
                evt.StopPropagation();
            }
            else if ((evt.direction == NavigationMoveEvent.Direction.Up || evt.direction == NavigationMoveEvent.Direction.Down) &&
                     isInsideLobbyList)
            {
                var target = evt.direction == NavigationMoveEvent.Direction.Down
                    ? lobbyRows.Find(IsFocusable)
                    : lobbyRows.FindLast(IsFocusable);

                if (target != null)
                {
                    target.Focus();
                    ScrollToLobbyRow(target);
                }

                // Keep navigation inside the list even when it has no
                // joinable rows. Section changes are handled by LB/RB.
                evt.PreventDefault();
                evt.StopPropagation();
            }
            else if (evt.direction == NavigationMoveEvent.Direction.Right &&
                     searchControl != null &&
                     searchControl is not TextField)
            {
                if (FocusLobbySection())
                {
                    evt.PreventDefault();
                    evt.StopPropagation();
                }
            }
            else if (evt.direction == NavigationMoveEvent.Direction.Left && lobbyRow != null)
            {
                FocusSearchSection();
                evt.PreventDefault();
                evt.StopPropagation();
            }
        }

        private void OnLobbyRowFocused(VisualElement row)
        {
            lastLobbyRow = row;
            SetActiveFocusSection(FocusSection.LobbyList);
            ScrollToLobbyRow(row);
        }

        private void FocusSearchSection()
        {
            var target = IsFocusable(lastSearchControl)
                ? lastSearchControl
                : searchControls.Find(IsFocusable);

            if (target == null)
                return;

            SetActiveFocusSection(FocusSection.Search);
            target.Focus();
        }

        private void FocusCreateSection()
        {
            var target = IsFocusable(lastCreateControl)
                ? lastCreateControl
                : createControls.Find(IsFocusable);

            if (target == null)
                return;

            SetActiveFocusSection(FocusSection.Create);
            target.Focus();
        }

        private bool FocusLobbySection()
        {
            var target = IsFocusable(lastLobbyRow)
                ? lastLobbyRow
                : lobbyRows.Find(IsFocusable);

            if (target == null)
                target = lobbyListView;

            if (!IsFocusable(target))
                return false;

            SetActiveFocusSection(FocusSection.LobbyList);
            target.Focus();
            ScrollToLobbyRow(target);
            return true;
        }

        private void ScrollToLobbyRow(VisualElement target)
        {
            if (lobbyListView == null ||
                target == null ||
                target == lobbyListView ||
                !lobbyListView.contentContainer.Contains(target))
                return;

            lobbyListView.ScrollTo(target);
        }

        private void SetActiveFocusSection(FocusSection section)
        {
            activeFocusSection = section;
            var createIsActive = section == FocusSection.Create;
            var searchIsActive = section == FocusSection.Search;
            createSectionHost?.EnableInClassList("lobby-focus-group--active", createIsActive);
            searchSectionHost?.EnableInClassList("lobby-focus-group--active", searchIsActive);
            listSectionHost?.EnableInClassList("lobby-focus-group--active", section == FocusSection.LobbyList);
        }

        private static VisualElement FindContaining(VisualElement focused, System.Collections.Generic.List<VisualElement> elements)
        {
            return elements.Find(element => element == focused || element.Contains(focused));
        }

        private static void FocusAdjacent(
            System.Collections.Generic.List<VisualElement> elements,
            VisualElement current,
            int direction)
        {
            if (elements.Count == 0)
                return;

            var currentIndex = elements.IndexOf(current);
            for (var offset = 1; offset <= elements.Count; offset++)
            {
                var index = (currentIndex + direction * offset + elements.Count) % elements.Count;
                if (!IsFocusable(elements[index]))
                    continue;

                elements[index].Focus();
                return;
            }
        }

        private static bool IsFocusable(VisualElement element)
        {
            return element != null && element.panel != null && element.enabledInHierarchy && element.focusable;
        }

        private void RestoreFocusAfterListRebuild()
        {
            var root = uiDocument.rootVisualElement;
            root.schedule.Execute(() =>
            {
                if (!HasValidFocus(root))
                {
                    switch (activeFocusSection)
                    {
                        case FocusSection.Create:
                            FocusCreateSection();
                            break;
                        case FocusSection.LobbyList:
                            if (!FocusLobbySection())
                                FocusCreateSection();
                            break;
                        default:
                            FocusSearchSection();
                            break;
                    }
                }
            });
        }

        private static bool HasValidFocus(VisualElement root)
        {
            var focused = root?.focusController?.focusedElement as VisualElement;
            return focused != null && focused.panel != null && root.Contains(focused);
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
            createSectionHost = null;
            searchSectionHost = null;
            listSectionHost = null;
            createControls.Clear();
            searchControls.Clear();
            lobbyRows.Clear();
            lastCreateControl = null;
            lastSearchControl = null;
            lastLobbyRow = null;
        }
    }
}
