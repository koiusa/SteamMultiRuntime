using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class KeyConfigUiDocument : MonoBehaviour
    {
        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private StyleSheet styleSheet;
        
        [Header("Input")]
        [SerializeField] private KeyConfigInputActionsConfig inputActionsConfig;
        [SerializeField] private string userId = "LocalUser";
        [SerializeField] private string bindingGroup = string.Empty;

        [Header("Resolvers")]
        [SerializeField] private InputBindingIconResolver iconResolver;

        private UIDocument uiDocument;
        private KeyConfigView view;
        private InputBindingService bindingService;
        private InputRebindController rebindController;
        private List<InputBindingService.BindingEntry> currentEntries = new List<InputBindingService.BindingEntry>();
        private bool usingBindingGroupFallback;
        private InputAction previousSectionAction;
        private InputAction nextSectionAction;
        private bool enabledPreviousSectionAction;
        private bool enabledNextSectionAction;

        public string BindingGroup => bindingGroup;
        public event Action Closed;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();

            view = new KeyConfigView(uiDocument, layoutAsset, styleSheet);
            view.SetIconResolver(iconResolver);

            var resolvedInputActionAsset = ResolveInputActionAsset();
            if (resolvedInputActionAsset != null)
            {
                bindingService = new InputBindingService(
                    resolvedInputActionAsset,
                    nonRebindableActionMaps: inputActionsConfig.NonRebindableActionMaps);
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
            BindSectionNavigation();

            if (bindingService == null)
            {
                view.SetInteractive(false);
                view.SetLocalizedStatus("keyconfig.config_missing");
                view.SetBindingGroupChoices(null, bindingGroup);
                view.RenderBindingEntries(currentEntries, null, null);
                return;
            }

            view.SetBindingGroupChoices(bindingService.GetBindingGroups(), bindingGroup);

            _ = bindingService.TryLoadOverrides(userId);
            RebuildBindingList();
            ApplyReadyStatus();
            view.FocusDefault();
        }

        private void OnDisable()
        {
            view.UnbindActions();
            view.Dispose();
            rebindController?.CancelRebind();
            UnbindSectionNavigation();
        }

        private void Update()
        {
            if (bindingService != null && !rebindController.IsRebinding)
            {
                view.UpdateInputStates(bindingService.InputActionAsset);
            }
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
            ApplyBindingGroupChangedStatus();
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
            if (loaded) ApplyLoadedStatus();
            else view.SetLocalizedStatus("keyconfig.no_saved_settings");
        }

        private void OnSaveClicked()
        {
            if (bindingService == null)
            {
                return;
            }

            bindingService.SaveOverrides(userId);
            view.SetLocalizedStatus("keyconfig.saved");
        }

        private void OnResetAllClicked()
        {
            if (bindingService == null)
            {
                return;
            }

            bindingService.ResetAllOverrides(userId);
            RebuildBindingList();
            view.SetLocalizedStatus("keyconfig.reset_all_done");
        }

        private void OnCloseClicked()
        {
            Closed?.Invoke();
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
            if (entry.IsComposite || !entry.IsRebindable)
            {
                return;
            }

            var effectiveBindingGroup = usingBindingGroupFallback ? null : bindingGroup;
            var started = rebindController.StartRebind(entry.ActionId, entry.BindingIndex, effectiveBindingGroup);
            if (!started)
            {
                view.SetLocalizedStatus("keyconfig.rebind_start_failed");
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
                view.SetLocalizedStatus("keyconfig.action_missing");
                return;
            }

            if (!bindingService.IsActionRebindable(action))
            {
                return;
            }

            bindingService.ResetBinding(action, entry.BindingIndex);
            RebuildBindingList();
            view.SetLocalizedStatus("keyconfig.binding_reset");
        }

        private void OnRebindStarted()
        {
            view.SetInteractive(false);
            view.SetLocalizedStatus("keyconfig.enter_new_key");
        }

        private void OnRebindCompleted(string displayName)
        {
            RebuildBindingList();
            view.SetInteractive(true);
            view.SetLocalizedStatus(usingBindingGroupFallback ? "keyconfig.changed_fallback" : "keyconfig.changed", displayName);
            view.FocusDefault();
        }

        private void OnRebindCanceled()
        {
            RebuildBindingList();
            view.SetInteractive(true);
            view.SetLocalizedStatus(usingBindingGroupFallback ? "keyconfig.rebind_canceled_fallback" : "keyconfig.rebind_canceled");
            view.FocusDefault();
        }

        private void OnRebindFailed(string message)
        {
            RebuildBindingList();
            view.SetInteractive(true);
            if (string.IsNullOrWhiteSpace(message))
                view.SetLocalizedStatus(usingBindingGroupFallback ? "keyconfig.rebind_failed_fallback" : "keyconfig.rebind_failed");
            else
                view.SetStatus(message);
            view.FocusDefault();
        }

        private InputActionAsset ResolveInputActionAsset()
        {
            return inputActionsConfig != null ? inputActionsConfig.Resolve() : null;
        }

        private void BindSectionNavigation()
        {
            UnbindSectionNavigation();
            var asset = bindingService?.InputActionAsset;
            if (asset == null || inputActionsConfig == null)
            {
                return;
            }

            previousSectionAction = asset.FindAction(inputActionsConfig.PreviousSectionActionPath);
            nextSectionAction = asset.FindAction(inputActionsConfig.NextSectionActionPath);

            if (previousSectionAction != null)
            {
                previousSectionAction.performed += OnPreviousSectionPerformed;
                enabledPreviousSectionAction = !previousSectionAction.enabled;
                if (enabledPreviousSectionAction) previousSectionAction.Enable();
            }

            if (nextSectionAction != null)
            {
                nextSectionAction.performed += OnNextSectionPerformed;
                enabledNextSectionAction = !nextSectionAction.enabled;
                if (enabledNextSectionAction) nextSectionAction.Enable();
            }
        }

        private void UnbindSectionNavigation()
        {
            if (previousSectionAction != null)
            {
                previousSectionAction.performed -= OnPreviousSectionPerformed;
                if (enabledPreviousSectionAction) previousSectionAction.Disable();
            }

            if (nextSectionAction != null)
            {
                nextSectionAction.performed -= OnNextSectionPerformed;
                if (enabledNextSectionAction) nextSectionAction.Disable();
            }

            previousSectionAction = null;
            nextSectionAction = null;
            enabledPreviousSectionAction = false;
            enabledNextSectionAction = false;
        }

        private void OnPreviousSectionPerformed(InputAction.CallbackContext context)
        {
            if (rebindController == null || !rebindController.IsRebinding) view.SelectAdjacentMap(-1);
        }

        private void OnNextSectionPerformed(InputAction.CallbackContext context)
        {
            if (rebindController == null || !rebindController.IsRebinding) view.SelectAdjacentMap(1);
        }

        private void ApplyReadyStatus()
        {
            if (usingBindingGroupFallback) view.SetLocalizedStatus("keyconfig.ready_fallback", bindingGroup);
            else view.SetLocalizedStatus("common.ready");
        }

        private void ApplyLoadedStatus()
        {
            if (usingBindingGroupFallback) view.SetLocalizedStatus("keyconfig.loaded_fallback", bindingGroup);
            else view.SetLocalizedStatus("keyconfig.loaded");
        }

        private void ApplyBindingGroupChangedStatus()
        {
            if (string.IsNullOrWhiteSpace(bindingGroup))
            {
                view.SetLocalizedStatus("keyconfig.group_all");
                return;
            }

            view.SetLocalizedStatus(usingBindingGroupFallback ? "keyconfig.group_fallback" : "keyconfig.group", bindingGroup);
        }
    }
}
