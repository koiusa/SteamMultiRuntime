using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class KeyConfigUiDocument : MonoBehaviour
    {
        private const string DefaultLayoutResourcePath = "UI/KeyConfig/KeyConfig";
        private const string DefaultStyleSheetResourcePath = "UI/KeyConfig/KeyConfig";

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActionAsset;
        [SerializeField] private string userId = "LocalUser";
        [SerializeField] private string bindingGroup = string.Empty;

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private StyleSheet styleSheet;

        private UIDocument uiDocument;
        private KeyConfigView view;
        private InputBindingService bindingService;
        private InputRebindController rebindController;
        private List<InputBindingService.BindingEntry> currentEntries = new List<InputBindingService.BindingEntry>();
        private bool usingBindingGroupFallback;

        public string BindingGroup => bindingGroup;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();

            if (layoutAsset == null)
            {
                layoutAsset = Resources.Load<VisualTreeAsset>(DefaultLayoutResourcePath);
            }

            if (styleSheet == null)
            {
                styleSheet = Resources.Load<StyleSheet>(DefaultStyleSheetResourcePath);
            }

            view = new KeyConfigView(uiDocument, layoutAsset, styleSheet);

            if (inputActionAsset != null)
            {
                bindingService = new InputBindingService(inputActionAsset);
                rebindController = new InputRebindController(bindingService);
                rebindController.RebindStarted += OnRebindStarted;
                rebindController.RebindCompleted += OnRebindCompleted;
                rebindController.RebindCanceled += OnRebindCanceled;
                rebindController.RebindFailed += OnRebindFailed;
            }
        }

        private void OnEnable()
        {
            view.Build();
            view.BindActions(OnLoadClicked, OnSaveClicked, OnResetAllClicked, OnCloseClicked, OnBindingGroupChangedFromUi);

            if (bindingService == null)
            {
                view.SetInteractive(false);
                view.SetStatus("InputActionAsset が未設定です。");
                view.SetBindingGroupChoices(null, bindingGroup);
                view.RenderBindingEntries(currentEntries, null, null);
                return;
            }

            view.SetBindingGroupChoices(bindingService.GetBindingGroups(), bindingGroup);

            _ = bindingService.TryLoadOverrides(userId);
            RebuildBindingList();
            view.SetStatus(BuildReadyStatus());
        }

        private void OnDisable()
        {
            view.UnbindActions();
            rebindController?.CancelRebind();
        }

        public void SetBindingGroup(string group)
        {
            bindingGroup = group?.Trim() ?? string.Empty;

            if (bindingService == null || !isActiveAndEnabled)
            {
                return;
            }

            view.SetBindingGroupChoices(bindingService.GetBindingGroups(), bindingGroup);
            RebuildBindingList();
            view.SetStatus(BuildBindingGroupChangedStatus());
        }

        public void ClearBindingGroupFilter()
        {
            SetBindingGroup(string.Empty);
        }

        private void OnBindingGroupChangedFromUi(string group)
        {
            SetBindingGroup(group);
        }

        private void OnDestroy()
        {
            if (rebindController != null)
            {
                rebindController.RebindStarted -= OnRebindStarted;
                rebindController.RebindCompleted -= OnRebindCompleted;
                rebindController.RebindCanceled -= OnRebindCanceled;
                rebindController.RebindFailed -= OnRebindFailed;
                rebindController.Dispose();
                rebindController = null;
            }
        }

        private void RebuildBindingList()
        {
            if (bindingService == null)
            {
                currentEntries.Clear();
                usingBindingGroupFallback = false;
                view.RenderBindingEntries(currentEntries, null, null);
                return;
            }

            currentEntries = bindingService.GetBindingEntries(bindingGroup);
            usingBindingGroupFallback = false;

            if (currentEntries.Count == 0 && !string.IsNullOrWhiteSpace(bindingGroup))
            {
                currentEntries = bindingService.GetBindingEntries();
                usingBindingGroupFallback = currentEntries.Count > 0;
            }

            view.RenderBindingEntries(currentEntries, OnRebindRequested, OnResetRequested);
        }

        private void OnLoadClicked()
        {
            if (bindingService == null)
            {
                return;
            }

            var loaded = bindingService.TryLoadOverrides(userId);
            RebuildBindingList();
            view.SetStatus(loaded ? BuildLoadedStatus() : "保存済み設定がありません。\n");
        }

        private void OnSaveClicked()
        {
            if (bindingService == null)
            {
                return;
            }

            bindingService.SaveOverrides(userId);
            view.SetStatus("設定を保存しました。");
        }

        private void OnResetAllClicked()
        {
            if (bindingService == null)
            {
                return;
            }

            bindingService.ResetAllOverrides(userId);
            RebuildBindingList();
            view.SetStatus("すべてのキー設定をリセットしました。");
        }

        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }

        private void OnRebindRequested(int index)
        {
            if (rebindController == null)
            {
                return;
            }

            if (index < 0 || index >= currentEntries.Count)
            {
                return;
            }

            var entry = currentEntries[index];
            if (entry.IsComposite)
            {
                return;
            }

            var effectiveBindingGroup = usingBindingGroupFallback ? null : bindingGroup;
            var started = rebindController.StartRebind(entry.ActionId, entry.BindingIndex, effectiveBindingGroup);
            if (!started)
            {
                view.SetStatus("リバインドを開始できませんでした。");
            }
        }

        private void OnResetRequested(int index)
        {
            if (bindingService == null)
            {
                return;
            }

            if (index < 0 || index >= currentEntries.Count)
            {
                return;
            }

            var entry = currentEntries[index];
            if (!bindingService.TryFindAction(entry.ActionId, out var action))
            {
                view.SetStatus("Action の取得に失敗しました。");
                return;
            }

            bindingService.ResetBinding(action, entry.BindingIndex);
            RebuildBindingList();
            view.SetStatus("バインドをリセットしました。");
        }

        private void OnRebindStarted()
        {
            view.SetInteractive(false);
            view.SetStatus("新しいキーを入力してください（Escでキャンセル）");
        }

        private void OnRebindCompleted(string displayName)
        {
            RebuildBindingList();
            view.SetInteractive(true);
            view.SetStatus(BuildRebindCompletedStatus(displayName));
        }

        private void OnRebindCanceled()
        {
            RebuildBindingList();
            view.SetInteractive(true);
            view.SetStatus(BuildRebindCanceledStatus());
        }

        private void OnRebindFailed(string message)
        {
            RebuildBindingList();
            view.SetInteractive(true);
            view.SetStatus(string.IsNullOrWhiteSpace(message) ? BuildRebindFailedStatus() : message);
        }

        private string BuildReadyStatus()
        {
            return usingBindingGroupFallback
                ? $"Ready（'{bindingGroup}' に一致するバインドがないため全表示中）"
                : "Ready";
        }

        private string BuildLoadedStatus()
        {
            return usingBindingGroupFallback
                ? $"設定を読み込みました。('{bindingGroup}' に一致するバインドがないため全表示中)"
                : "設定を読み込みました。";
        }

        private string BuildRebindCompletedStatus(string displayName)
        {
            return usingBindingGroupFallback
                ? $"変更しました: {displayName}（全表示中）"
                : $"変更しました: {displayName}";
        }

        private string BuildRebindCanceledStatus()
        {
            return usingBindingGroupFallback
                ? "リバインドをキャンセルしました。（全表示中）"
                : "リバインドをキャンセルしました。";
        }

        private string BuildRebindFailedStatus()
        {
            return usingBindingGroupFallback
                ? "リバインドに失敗しました。（全表示中）"
                : "リバインドに失敗しました。";
        }

        private string BuildBindingGroupChangedStatus()
        {
            if (string.IsNullOrWhiteSpace(bindingGroup))
            {
                return "BindingGroup: すべて";
            }

            return usingBindingGroupFallback
                ? $"BindingGroup: {bindingGroup}（一致なしのため全表示）"
                : $"BindingGroup: {bindingGroup}";
        }
    }
}
