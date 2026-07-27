using Koiusa.SteamMultiRuntime.Network;
using TNRD;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class CharacterSelectUiDocument : MonoBehaviour
    {
        private const string DefaultLayoutResourcePath = "UI/CharacterSelect/CharacterSelect";
        private const string DefaultStyleSheetResourcePath = "UI/CharacterSelect/CharacterSelect";

        [Header("References")]
        [SerializeField] private SerializableInterface<IRuntimeUserProfileModelSource> userProfile;

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private StyleSheet styleSheet;

        private UIDocument uiDocument;
        private CharacterSelectView view;
        private VisualElement registeredRoot;
        private int pendingIndex = -1;

        private IRuntimeUserProfileModelSource UserProfile => userProfile != null ? userProfile.Value : null;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (uiDocument != null && view != null)
            {
                return;
            }

            uiDocument = GetComponent<UIDocument>();

            if (layoutAsset == null)
                layoutAsset = Resources.Load<VisualTreeAsset>(DefaultLayoutResourcePath);
            if (styleSheet == null)
                styleSheet = Resources.Load<StyleSheet>(DefaultStyleSheetResourcePath);

            view = new CharacterSelectView(uiDocument, layoutAsset, styleSheet);

            if (UserProfile == null)
            {
                var profile = FindFirstObjectByType<PlayerModelProfileBase>();
                if (profile != null)
                {
                    userProfile = new SerializableInterface<IRuntimeUserProfileModelSource>(profile);
                }
            }
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (uiDocument == null || view == null)
            {
                return;
            }

            var ids = ResolveModelIdList();
            view.Build(ids);
            view.BindActions(OnCharacterSelected, OnConfirmClicked);
            registeredRoot = uiDocument.rootVisualElement;
            registeredRoot?.RegisterCallback<NavigationCancelEvent>(OnCancelNavigation);

            pendingIndex = UserProfile != null ? UserProfile.SelectedModelIndex : 0;
            view.SetSelectedIndex(pendingIndex, ids != null ? ids.modelIds : null);
            view.FocusSelectedCharacter();
        }

        private void OnDisable()
        {
            registeredRoot?.UnregisterCallback<NavigationCancelEvent>(OnCancelNavigation);
            registeredRoot = null;
            view?.UnbindActions();
        }

        private CharacterModelIdList ResolveModelIdList()
        {
            if (UserProfile == null)
            {
                var profile = FindFirstObjectByType<PlayerModelProfileBase>();
                if (profile != null)
                {
                    userProfile = new SerializableInterface<IRuntimeUserProfileModelSource>(profile);
                }
            }

            return UserProfile != null ? UserProfile.ModelIdList : null;
        }

        private void OnCharacterSelected(int index)
        {
            pendingIndex = index;
            var ids = ResolveModelIdList();
            view.SetSelectedIndex(index, ids != null ? ids.modelIds : null);
        }

        private void OnConfirmClicked()
        {
            if (pendingIndex < 0 || UserProfile == null)
                return;

            UserProfile.SetSelectedModel(pendingIndex);
            UserProfile.ApplySelectedModel();
            Close();
        }

        private void OnCancelNavigation(NavigationCancelEvent evt)
        {
            evt.PreventDefault();
            evt.StopPropagation();
            Close();
        }

        private void Close()
        {
            gameObject.SetActive(false);
            UnityEngine.Cursor.visible = false;
        }
    }
}
