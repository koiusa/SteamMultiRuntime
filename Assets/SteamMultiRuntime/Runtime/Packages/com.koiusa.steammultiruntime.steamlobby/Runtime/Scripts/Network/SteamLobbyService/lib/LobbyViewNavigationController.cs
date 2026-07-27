using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class LobbyViewNavigationController
    {
        private readonly LobbyViewContext context;
        private readonly System.Collections.Generic.List<VisualElement> lobbyRows = new System.Collections.Generic.List<VisualElement>();
        private readonly System.Collections.Generic.List<VisualElement> createControls = new System.Collections.Generic.List<VisualElement>();
        private readonly System.Collections.Generic.List<VisualElement> searchControls = new System.Collections.Generic.List<VisualElement>();
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

        public LobbyViewNavigationController(LobbyViewContext context)
        {
            this.context = context;
            ConfigureFocusSections();
            if (context.LobbyListView != null)
            {
                context.LobbyListView.focusable = true;
                context.LobbyListView.tabIndex = -1;
                context.LobbyListView.RegisterCallback<FocusInEvent>(_ => SetActiveFocusSection(FocusSection.LobbyList));
            }
        }

        public ulong RememberedLobbyId => lastLobbyRow?.userData is ulong lobbyId ? lobbyId : 0;

        public void ClearLobbyRows()
        {
            lobbyRows.Clear();
            lastLobbyRow = null;
        }

        public void RegisterLobbyRow(VisualElement row, ulong lobbyId, System.Action<ulong> onJoinLobby)
        {
            row.RegisterCallback<FocusInEvent>(_ => OnLobbyRowFocused(row));
            row.RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                evt.PreventDefault();
                evt.StopPropagation();
                onJoinLobby?.Invoke(lobbyId);
            });
            lobbyRows.Add(row);
        }

        public void SetRememberedLobbyRow(VisualElement row)
        {
            lastLobbyRow = row;
        }

        public void FocusInitialControl()
        {
            var root = context.UiDocument.rootVisualElement;
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

        private void ConfigureFocusSections()
        {
            var root = context.UiDocument.rootVisualElement;
            root.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove);
            root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove);

            createControls.Clear();
            AddCreateControl(context.LobbyNameField);
            AddCreateControl(context.StageSceneField);
            AddCreateControl(context.CreateButton);
            AddCreateControl(context.RefreshButton);
            AddCreateControl(context.LeaveButton);
            lastCreateControl = context.StageSceneField;

            searchControls.Clear();
            AddSearchControl(context.LobbyIdField);
            AddSearchControl(context.JoinByIdButton);
            AddSearchControl(context.LobbyNameSearchField);
            AddSearchControl(context.SearchByNameButton);

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
            var root = context.UiDocument.rootVisualElement;
            var focused = root.focusController?.focusedElement as VisualElement;
            if (focused == null)
                return;

            var searchControl = FindContaining(focused, searchControls);
            var createControl = FindContaining(focused, createControls);
            var lobbyRow = FindContaining(focused, lobbyRows);
            var isInsideLobbyList = context.LobbyListView != null &&
                                    (focused == context.LobbyListView || context.LobbyListView.Contains(focused));

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
                target = context.LobbyListView;

            if (!IsFocusable(target))
                return false;

            SetActiveFocusSection(FocusSection.LobbyList);
            target.Focus();
            ScrollToLobbyRow(target);
            return true;
        }

        private void ScrollToLobbyRow(VisualElement target)
        {
            if (context.LobbyListView == null ||
                target == null ||
                target == context.LobbyListView ||
                !context.LobbyListView.contentContainer.Contains(target))
                return;

            context.LobbyListView.ScrollTo(target);
        }

        private void SetActiveFocusSection(FocusSection section)
        {
            activeFocusSection = section;
            var createIsActive = section == FocusSection.Create;
            var searchIsActive = section == FocusSection.Search;
            context.CreateSectionHost?.EnableInClassList("lobby-focus-group--active", createIsActive);
            context.SearchSectionHost?.EnableInClassList("lobby-focus-group--active", searchIsActive);
            context.ListSectionHost?.EnableInClassList("lobby-focus-group--active", section == FocusSection.LobbyList);
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

        public void RestoreFocusAfterListRebuild()
        {
            if (context.UiDocument == null)
            {
                return;
            }

            var root = context.UiDocument.rootVisualElement;
            if (root == null || root.panel == null)
            {
                return;
            }

            root.schedule.Execute(() =>
            {
                // The document can be detached while a lobby exit replaces scenes.
                if (root.panel == null)
                {
                    return;
                }

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
    }
}



