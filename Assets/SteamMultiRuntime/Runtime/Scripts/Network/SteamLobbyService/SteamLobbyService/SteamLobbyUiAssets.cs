using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [CreateAssetMenu(menuName = "SteamMultiRuntime/Steam Lobby UI Assets", fileName = "SteamLobbyUiAssets")]
    public sealed class SteamLobbyUiAssets : ScriptableObject
    {
        private const string DefaultLayoutResourcePath = "UI/SteamLobby/SteamLobbyView";
        private const string DefaultThemeStyleSheetResourcePath = "UI/SteamLobby/Themes/SteamLobbyUnityTheme";
        private const string DefaultStatusPanelResourcePath = "UI/SteamLobby/Panels/SteamLobbyStatusPanel";
        private const string DefaultOnlineMembersPanelResourcePath = "UI/SteamLobby/Panels/SteamLobbyOnlineMembersPanel";
        private const string DefaultCreatePanelResourcePath = "UI/SteamLobby/Panels/SteamLobbyCreatePanel";
        private const string DefaultJoinPanelResourcePath = "UI/SteamLobby/Panels/SteamLobbyJoinPanel";
        private const string DefaultSearchPanelResourcePath = "UI/SteamLobby/Panels/SteamLobbySearchPanel";
        private const string DefaultActionsPanelResourcePath = "UI/SteamLobby/Panels/SteamLobbyActionsPanel";
        private const string DefaultListPanelResourcePath = "UI/SteamLobby/Panels/SteamLobbyListPanel";

        [Header("Layout")]
        [SerializeField] private VisualTreeAsset layoutAsset;

        [Header("Theme")]
        [SerializeField] private StyleSheet themeStyleSheet;

        [Header("Panels")]
        [SerializeField] private VisualTreeAsset statusPanelAsset;
        [SerializeField] private VisualTreeAsset onlineMembersPanelAsset;
        [SerializeField] private VisualTreeAsset createPanelAsset;
        [SerializeField] private VisualTreeAsset joinPanelAsset;
        [SerializeField] private VisualTreeAsset searchPanelAsset;
        [SerializeField] private VisualTreeAsset actionsPanelAsset;
        [SerializeField] private VisualTreeAsset listPanelAsset;

        private SteamLobbyUiDocument uiDocument;
        private bool isSteamEventSubscribed;

        public VisualTreeAsset LayoutAsset => layoutAsset;
        public StyleSheet ThemeStyleSheet => themeStyleSheet;
        public VisualTreeAsset StatusPanelAsset => statusPanelAsset;
        public VisualTreeAsset OnlineMembersPanelAsset => onlineMembersPanelAsset;
        public VisualTreeAsset CreatePanelAsset => createPanelAsset;
        public VisualTreeAsset JoinPanelAsset => joinPanelAsset;
        public VisualTreeAsset SearchPanelAsset => searchPanelAsset;
        public VisualTreeAsset ActionsPanelAsset => actionsPanelAsset;
        public VisualTreeAsset ListPanelAsset => listPanelAsset;

        public void SetOwner(SteamLobbyUiDocument owner)
        {
            uiDocument = owner;
        }

        public void SubscribeSteamMatchmakingEvents()
        {
            if (isSteamEventSubscribed)
            {
                return;
            }

            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
            SteamMatchmaking.OnLobbyMemberDataChanged += OnLobbyMemberDataChanged;
            isSteamEventSubscribed = true;
        }

        public void UnsubscribeSteamMatchmakingEvents()
        {
            if (!isSteamEventSubscribed)
            {
                return;
            }

            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataChanged;
            SteamMatchmaking.OnLobbyMemberDataChanged -= OnLobbyMemberDataChanged;
            isSteamEventSubscribed = false;
        }

        public void EnsureDefaultsLoaded()
        {
            if (layoutAsset == null)
            {
                layoutAsset = Resources.Load<VisualTreeAsset>(DefaultLayoutResourcePath);
            }

            if (themeStyleSheet == null)
            {
                themeStyleSheet = Resources.Load<StyleSheet>(DefaultThemeStyleSheetResourcePath);
            }

            if (statusPanelAsset == null)
            {
                statusPanelAsset = Resources.Load<VisualTreeAsset>(DefaultStatusPanelResourcePath);
            }

            if (onlineMembersPanelAsset == null)
            {
                onlineMembersPanelAsset = Resources.Load<VisualTreeAsset>(DefaultOnlineMembersPanelResourcePath);
            }

            if (createPanelAsset == null)
            {
                createPanelAsset = Resources.Load<VisualTreeAsset>(DefaultCreatePanelResourcePath);
            }

            if (joinPanelAsset == null)
            {
                joinPanelAsset = Resources.Load<VisualTreeAsset>(DefaultJoinPanelResourcePath);
            }

            if (searchPanelAsset == null)
            {
                searchPanelAsset = Resources.Load<VisualTreeAsset>(DefaultSearchPanelResourcePath);
            }

            if (actionsPanelAsset == null)
            {
                actionsPanelAsset = Resources.Load<VisualTreeAsset>(DefaultActionsPanelResourcePath);
            }

            if (listPanelAsset == null)
            {
                listPanelAsset = Resources.Load<VisualTreeAsset>(DefaultListPanelResourcePath);
            }
        }

        internal bool TryBuild(LobbyView lobbyView, Object context)
        {
            if (lobbyView == null)
            {
                Debug.LogError("LobbyView is null.", context);
                return false;
            }

            EnsureDefaultsLoaded();

            if (layoutAsset == null)
            {
                Debug.LogError("SteamLobbyUiAssets layoutAsset is not assigned.", context);
                return false;
            }

            lobbyView.Build(
                layoutAsset,
                themeStyleSheet,
                statusPanelAsset,
                onlineMembersPanelAsset,
                createPanelAsset,
                joinPanelAsset,
                searchPanelAsset,
                actionsPanelAsset,
                listPanelAsset);
            return true;
        }

        private void OnLobbyCreated(Result result, Lobby lobby)
        {
            uiDocument?.RequestRefresh();
        }

        private void OnLobbyEntered(Lobby lobby)
        {
            uiDocument?.RequestRefresh();
        }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            uiDocument?.RequestRefresh();
        }

        private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            uiDocument?.RequestRefresh();
        }

        private void OnLobbyDataChanged(Lobby lobby)
        {
            uiDocument?.RequestRefresh();
        }

        private void OnLobbyMemberDataChanged(Lobby lobby, Friend friend)
        {
            uiDocument?.RequestRefresh();
        }
    }
}
