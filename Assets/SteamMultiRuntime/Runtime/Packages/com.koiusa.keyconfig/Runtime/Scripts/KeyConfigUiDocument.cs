using System;
using System.Collections.Generic;
using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        private UiNavigationInputSession navigationSession;
        private InputAction submitAction;
        private InputAction previousSectionAction;
        private InputAction nextSectionAction;
        private InputActionBinding previousSectionBinding;
        private InputActionBinding nextSectionBinding;
        private PendingRebindRequest? pendingRebindStart;
        private int activeRebindEntryIndex = -1;
        private bool sectionNavigationBlocked;
        private readonly List<InputAction> suspendedActions = new List<InputAction>();
        private string sessionOverridesJson;
        private IVisualElementScheduledItem inputStateUpdate;
        private IVisualElementScheduledItem releaseBlockRefresh;

        public string BindingGroup => bindingGroup;
        public event Action Closed;

        public void FocusInitial() => view?.FocusDefault();

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
                rebindController.RebindConflict += OnRebindConflict;
                rebindController.RebindCanceled += OnRebindCanceled;
                rebindController.RebindFailed += OnRebindFailed;
            }
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnInputDeviceChange;
            InputSystem.onEvent += OnInputEvent;
            view.Build();
            view.SetNavigationSubmitBlocked(false);
            sectionNavigationBlocked = false;
            view.BindActions(OnLoadClicked, OnSaveClicked, OnResetAllClicked, OnCloseClicked, OnBindingGroupChangedFromUi);
            var asset = bindingService?.InputActionAsset;
            navigationSession = new UiNavigationInputSession(
                asset?.FindAction("UI/Navigate"),
                null,
                null,
                view.HandleNavigationMove,
                null,
                null,
                uiDocument.rootVisualElement);
            BindSectionNavigation();
            SuspendNonUiActions();

            if (bindingService == null)
            {
                view.SetInteractive(false, allowCloseWhenDisabled: true);
                view.SetLocalizedStatus("keyconfig.config_missing");
                view.SetBindingGroupChoices(null, bindingGroup);
                view.RenderBindingEntries(currentEntries, null, null, null, null);
                return;
            }

            view.SetBindingGroupChoices(bindingService.GetBindingGroups(), bindingGroup);

            _ = bindingService.TryLoadOverrides(userId);
            BeginEditSession();
            RebuildBindingList();
            ApplyReadyStatus();
            view.FocusDefault();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnInputDeviceChange;
            InputSystem.onEvent -= OnInputEvent;
            inputStateUpdate?.Pause();
            inputStateUpdate = null;
            releaseBlockRefresh?.Pause();
            releaseBlockRefresh = null;
            view.SetNavigationSubmitBlocked(false);
            sectionNavigationBlocked = false;
            CancelPendingRebindStart();
            activeRebindEntryIndex = -1;
            rebindController?.CancelRebind();
            navigationSession?.Dispose();
            navigationSession = null;
            RestoreUnsavedChanges();
            view.HideConflict();
            view.UnbindActions();
            view.Dispose();
            UnbindSectionNavigation();
            RestoreSuspendedActions();
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            ScheduleInputStateUpdate();
        }

        private void ScheduleInputStateUpdate()
        {
            if (inputStateUpdate != null || bindingService == null || rebindController?.IsBusy == true) return;
            var root = uiDocument?.rootVisualElement;
            if (root == null) return;

            inputStateUpdate = root.schedule.Execute(() =>
            {
                inputStateUpdate = null;
                if (isActiveAndEnabled && rebindController?.IsBusy != true)
                    view.UpdateInputStates(bindingService.InputActionAsset);
            });
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
                rebindController.RebindConflict -= OnRebindConflict;
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
                view.RenderBindingEntries(currentEntries, null, null, null, null);
                return;
            }

            currentEntries = bindingService.GetBindingEntries(bindingGroup);
            usingBindingGroupFallback = false;

            if (currentEntries.Count == 0 && !string.IsNullOrWhiteSpace(bindingGroup))
            {
                currentEntries = bindingService.GetBindingEntries();
                usingBindingGroupFallback = currentEntries.Count > 0;
            }

            view.RenderBindingEntries(currentEntries, OnRebindRequested, OnAddModifierRequested, OnRemoveModifierRequested, OnResetRequested);
            if (rebindController?.IsBusy != true) view.UpdateInputStates(bindingService.InputActionAsset);
        }

        private void OnLoadClicked()
        {
            if (bindingService == null)
            {
                return;
            }

            var loaded = bindingService.TryLoadOverrides(userId);
            if (loaded)
            {
                BeginEditSession();
            }
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
            BeginEditSession();
            view.SetLocalizedStatus("keyconfig.saved");
        }

        private void OnResetAllClicked()
        {
            if (bindingService == null)
            {
                return;
            }

            bindingService.ResetAllOverrides();
            RebuildBindingList();
            view.SetLocalizedStatus("keyconfig.reset_all_done");
        }

        private void OnCloseClicked()
        {
            Closed?.Invoke();
            gameObject.SetActive(false);
        }

        private void BeginEditSession()
        {
            sessionOverridesJson = bindingService?.CaptureOverrides();
        }

        private void RestoreUnsavedChanges()
        {
            if (sessionOverridesJson == null || bindingService == null)
            {
                return;
            }

            bindingService.RestoreOverrides(sessionOverridesJson);
            sessionOverridesJson = null;
        }

        private void OnRebindRequested(int index)
        {
            if (rebindController == null || pendingRebindStart.HasValue)
            {
                return;
            }

            if (index < 0 || index >= currentEntries.Count)
            {
                return;
            }

            var entry = currentEntries[index];
            if (!entry.IsRebindable)
            {
                return;
            }

            var effectiveBindingGroup = usingBindingGroupFallback ? null : bindingGroup;
            activeRebindEntryIndex = index;
            view.SetInteractive(false);
            submitAction ??= bindingService.InputActionAsset.FindAction(inputActionsConfig.SubmitActionPath);
            if (submitAction != null && submitAction.IsPressed())
            {
                pendingRebindStart = new PendingRebindRequest(entry.ActionId, entry.BindingIndex, effectiveBindingGroup);
                return;
            }

            StartRebind(entry.ActionId, entry.BindingIndex, effectiveBindingGroup);
        }

        private void CancelPendingRebindStart()
        {
            pendingRebindStart = null;
        }

        private void StartRebind(Guid actionId, int bindingIndex, string effectiveBindingGroup)
        {
            var started = rebindController.StartRebind(actionId, bindingIndex, effectiveBindingGroup);
            if (!started)
            {
                activeRebindEntryIndex = -1;
                view.SetInteractive(true);
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
            view.FocusBindingEntry(index);
        }

        private void OnInputDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (bindingService == null || rebindController.IsBusy) return;
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Removed:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Enabled:
                case InputDeviceChange.Disabled:
                    RebuildBindingList();
                    break;
            }
        }

        private void OnAddModifierRequested(int index) => ChangeModifierCount(index, true);

        private void OnRemoveModifierRequested(int index) => ChangeModifierCount(index, false);

        private void ChangeModifierCount(int index, bool add)
        {
            if (bindingService == null || index < 0 || index >= currentEntries.Count) return;
            var entry = currentEntries[index];
            if (!bindingService.TryFindAction(entry.ActionId, out var action) || !entry.IsRebindable) return;
            var changed = add
                ? bindingService.AddModifier(action, entry.BindingIndex)
                : bindingService.RemoveModifier(action, entry.BindingIndex);
            if (!changed) return;
            RebuildBindingList();
            view.SetLocalizedStatus(add ? "keyconfig.modifier_added" : "keyconfig.modifier_removed");
            view.FocusBindingEntry(
                Mathf.Min(index, currentEntries.Count - 1),
                add ? KeyConfigBindingNavigation.AddModifierColumn : KeyConfigBindingNavigation.RemoveModifierColumn);
        }

        private void OnRebindStarted()
        {
            sectionNavigationBlocked = true;
            view.SetNavigationSubmitBlocked(submitAction?.IsPressed() == true);
            view.SetInteractive(false);
            view.SetLocalizedStatus("keyconfig.enter_new_key");
        }

        private void OnRebindCompleted(string displayName)
        {
            FinishRebindUi(usingBindingGroupFallback ? "keyconfig.changed_fallback" : "keyconfig.changed", displayName);
        }

        private void OnRebindConflict(string targetAction, string existingAction)
        {
            ScheduleReleasedInputBlockRefresh();
            view.ShowConflict(
                targetAction,
                existingAction,
                () => rebindController.ResolveConflict(RebindConflictResolution.ReplaceExisting),
                () => rebindController.ResolveConflict(RebindConflictResolution.KeepBoth),
                () => rebindController.ResolveConflict(RebindConflictResolution.Cancel));
        }

        private void OnRebindCanceled()
        {
            FinishRebindUi(usingBindingGroupFallback ? "keyconfig.rebind_canceled_fallback" : "keyconfig.rebind_canceled");
        }

        private void OnRebindFailed(string message)
        {
            FinishRebindUi(
                string.IsNullOrWhiteSpace(message)
                    ? usingBindingGroupFallback ? "keyconfig.rebind_failed_fallback" : "keyconfig.rebind_failed"
                    : null,
                message);
        }

        private void FinishRebindUi(string statusKey, string statusArgument = null)
        {
            ScheduleReleasedInputBlockRefresh();
            RebuildBindingList();
            view.SetInteractive(true);
            if (statusKey == null) view.SetStatus(statusArgument);
            else if (statusArgument == null) view.SetLocalizedStatus(statusKey);
            else view.SetLocalizedStatus(statusKey, statusArgument);
            view.FocusBindingEntry(activeRebindEntryIndex);
            activeRebindEntryIndex = -1;
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

            previousSectionBinding = InputActionBinding.Bind(
                previousSectionAction = asset.FindAction(inputActionsConfig.PreviousSectionActionPath),
                OnPreviousSectionPerformed,
                OnSectionNavigationCanceled);
            nextSectionBinding = InputActionBinding.Bind(
                nextSectionAction = asset.FindAction(inputActionsConfig.NextSectionActionPath),
                OnNextSectionPerformed,
                OnSectionNavigationCanceled);
            submitAction = asset.FindAction(inputActionsConfig.SubmitActionPath);
            if (submitAction != null) submitAction.canceled += OnSubmitCanceled;
        }

        private void UnbindSectionNavigation()
        {
            previousSectionBinding?.Dispose();
            previousSectionBinding = null;
            nextSectionBinding?.Dispose();
            nextSectionBinding = null;
            if (submitAction != null) submitAction.canceled -= OnSubmitCanceled;
            submitAction = null;
            previousSectionAction = null;
            nextSectionAction = null;
        }

        private void OnPreviousSectionPerformed(InputAction.CallbackContext context)
        {
            if (!sectionNavigationBlocked && !pendingRebindStart.HasValue && (rebindController == null || !rebindController.IsBusy))
                view.SelectAdjacentSection(-1);
        }

        private void OnSubmitCanceled(InputAction.CallbackContext context)
        {
            if (submitAction?.IsPressed() == true) return;
            view.SetNavigationSubmitBlocked(false);
            if (!pendingRebindStart.HasValue) return;

            var request = pendingRebindStart.Value;
            CancelPendingRebindStart();
            if (isActiveAndEnabled) StartRebind(request.ActionId, request.BindingIndex, request.BindingGroup);
        }

        private void OnSectionNavigationCanceled(InputAction.CallbackContext context)
        {
            if (rebindController?.IsBusy == true) return;
            if (previousSectionAction?.IsPressed() == true || nextSectionAction?.IsPressed() == true) return;
            sectionNavigationBlocked = false;
        }

        private void ScheduleReleasedInputBlockRefresh()
        {
            if (releaseBlockRefresh != null) return;
            var root = uiDocument?.rootVisualElement;
            if (root == null) return;

            releaseBlockRefresh = root.schedule.Execute(() =>
            {
                releaseBlockRefresh = null;
                if (isActiveAndEnabled) RefreshReleasedInputBlocks();
            });
        }

        private void RefreshReleasedInputBlocks()
        {
            view.SetNavigationSubmitBlocked(submitAction?.IsPressed() == true);
            sectionNavigationBlocked = previousSectionAction?.IsPressed() == true || nextSectionAction?.IsPressed() == true;
        }

        private void SuspendNonUiActions()
        {
            RestoreSuspendedActions();
            var asset = bindingService?.InputActionAsset;
            var uiMap = asset?.FindAction(inputActionsConfig?.SubmitActionPath)?.actionMap;
            if (asset == null)
            {
                return;
            }

            foreach (var action in asset)
            {
                if (action.enabled && action.actionMap != uiMap)
                {
                    suspendedActions.Add(action);
                    action.Disable();
                }
            }
        }

        private void RestoreSuspendedActions()
        {
            for (var i = 0; i < suspendedActions.Count; i++)
            {
                suspendedActions[i]?.Enable();
            }
            suspendedActions.Clear();
        }

        private void OnNextSectionPerformed(InputAction.CallbackContext context)
        {
            if (!sectionNavigationBlocked && !pendingRebindStart.HasValue && (rebindController == null || !rebindController.IsBusy))
                view.SelectAdjacentSection(1);
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

        private readonly struct PendingRebindRequest
        {
            public PendingRebindRequest(Guid actionId, int bindingIndex, string bindingGroup)
            {
                ActionId = actionId;
                BindingIndex = bindingIndex;
                BindingGroup = bindingGroup;
            }

            public Guid ActionId { get; }
            public int BindingIndex { get; }
            public string BindingGroup { get; }
        }
    }
}
