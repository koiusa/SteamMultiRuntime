using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class LobbyViewContext
    {
        public UIDocument UiDocument;
        public Label ConnectionLabel;
        public Label CurrentLobbyLabel;
        public Label InfoLabel;
        public Label MemberConnectionStrengthLabel;
        public ScrollView OnlineMemberListView;
        public TextField LobbyNameField;
        public DropdownField StageSceneField;
        public TextField LobbyIdField;
        public TextField LobbyNameSearchField;
        public ScrollView LobbyListView;
        public Button CreateButton;
        public Button JoinByIdButton;
        public Button SearchByNameButton;
        public Button RefreshButton;
        public Button LeaveButton;
        public VisualElement CreateSectionHost;
        public VisualElement SearchSectionHost;
        public VisualElement ListSectionHost;
    }
}
