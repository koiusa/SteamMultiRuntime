using Koiusa.SteamMultiRuntime.Network;
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
        [SerializeField] private PlayerModelProfileBase userProfile;

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private StyleSheet styleSheet;

        private UIDocument uiDocument;
        private CharacterSelectView view;
        private int pendingIndex = -1;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();

            if (layoutAsset == null)
                layoutAsset = Resources.Load<VisualTreeAsset>(DefaultLayoutResourcePath);
            if (styleSheet == null)
                styleSheet = Resources.Load<StyleSheet>(DefaultStyleSheetResourcePath);

            view = new CharacterSelectView(uiDocument, layoutAsset, styleSheet);

            if (userProfile == null)
                userProfile = FindFirstObjectByType<PlayerModelProfileBase>();
        }

        private void OnEnable()
        {
            var ids = ResolveModelIdList();
            view.Build(ids);
            view.BindActions(OnCharacterSelected, OnConfirmClicked);

            pendingIndex = userProfile != null ? userProfile.SelectedModelIndex : 0;
            view.SetSelectedIndex(pendingIndex, ids != null ? ids.modelIds : null);
        }

        private void OnDisable()
        {
            view.UnbindActions();
        }

        private CharacterModelIdList ResolveModelIdList()
        {
            if (userProfile == null)
                userProfile = FindFirstObjectByType<PlayerModelProfileBase>();

            return userProfile != null ? userProfile.ModelIdList : null;
        }

        private void OnCharacterSelected(int index)
        {
            pendingIndex = index;
            var ids = ResolveModelIdList();
            view.SetSelectedIndex(index, ids != null ? ids.modelIds : null);
        }

        private void OnConfirmClicked()
        {
            if (pendingIndex < 0 || userProfile == null)
                return;

            userProfile.SetSelectedModel(pendingIndex);
            userProfile.ApplySelectedModel();
            gameObject.SetActive(false);
        }
    }
}
