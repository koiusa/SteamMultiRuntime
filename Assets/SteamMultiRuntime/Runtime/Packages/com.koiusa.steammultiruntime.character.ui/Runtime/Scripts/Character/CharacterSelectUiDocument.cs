using System;
using Koiusa.Input;
using TNRD;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.Character.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class CharacterSelectUiDocument : MonoBehaviour
    {
        private const string DefaultLayoutResourcePath = "UI/CharacterSelect/CharacterSelect";
        private const string DefaultStyleSheetResourcePath = "UI/CharacterSelect/CharacterSelect";

        [Header("References")]
        [SerializeField] private SerializableInterface<IRuntimeUserProfileModelSource> userProfile;
        [SerializeField] private InputActionsConfig inputActionsConfig;

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private StyleSheet styleSheet;

        private UIDocument uiDocument;
        private CharacterSelectView view;
        private VisualElement registeredRoot;
        private int pendingIndex = -1;
        private UiNavigationInputSession inputSession;
        private Action closeRequested;

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

            pendingIndex = UserProfile != null ? UserProfile.SelectedModelIndex : 0;
            view.SetSelectedIndex(pendingIndex, ids != null ? ids.modelIds : null);
            view.FocusSelectedCharacter();
            BindUiInput();
        }

        private void OnDisable()
        {
            UnbindUiInput();
            registeredRoot = null;
            view?.UnbindActions();
            view?.Dispose();
        }

        public void ConfigureInputActions(InputActionsConfig config)
        {
            inputActionsConfig = config;
        }

        public void ConfigureClose(Action close) => closeRequested = close;

        public void FocusInitial() => view?.FocusSelectedCharacter();

        private void BindUiInput()
        {
            UnbindUiInput();
            inputSession = new UiNavigationInputSession(
                inputActionsConfig,
                view.MoveSelection,
                OnConfirmClicked,
                Close,
                registeredRoot);
        }

        private void UnbindUiInput()
        {
            inputSession?.Dispose();
            inputSession = null;
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

        private void Close()
        {
            if (closeRequested != null) closeRequested.Invoke();
            else gameObject.SetActive(false);
        }
    }
}
