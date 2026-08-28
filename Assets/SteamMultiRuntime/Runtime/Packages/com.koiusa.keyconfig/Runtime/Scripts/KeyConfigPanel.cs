using System;
using System.Collections.Generic;
using Koiusa.Input;
using Koiusa.Input.Icons;
using Koiusa.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class KeyConfigPanel : MonoBehaviour, IUiMenu
    {
        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private StyleSheet styleSheet;
        
        [Header("Input")]
        [SerializeField] private KeyConfigSettings inputActionsConfig;
        [SerializeField] private string bindingGroup = string.Empty;

        [Header("Resolvers")]
        [SerializeField] private KeyConfigIconSet iconResolver;

        private UIDocument uiDocument;
        private KeyConfigView view;
        private KeyConfigController controller;
        private List<KeyConfigBinding> currentEntries = new List<KeyConfigBinding>();
        private Func<string> loadOverrides;
        private Action<string> saveOverrides;
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
        public bool IsVisible => gameObject.activeSelf;
        public event Action Closed;

        public void FocusInitial() => view?.FocusDefault();
        public void Open() => UiMenuNavigator.OpenRoot(this);
        public void Close() => UiMenuNavigator.Close(this);
        public void Toggle() => UiMenuNavigator.ToggleRoot(this);
        public void Show() => Open();
        public void Hide() => Close();
        public void Activate() => gameObject.SetActive(true);

        public void SetPersistence(Func<string> load, Action<string> save)
        {
            loadOverrides = load;
            saveOverrides = save;
        }

        public void Deactivate()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();

            view = new KeyConfigView(uiDocument, layoutAsset, styleSheet);
            view.SetIconResolver(iconResolver);

            var resolvedInputActionAsset = ResolveInputActionAsset();
            if (resolvedInputActionAsset != null)
            {
                controller = new KeyConfigController(resolvedInputActionAsset, inputActionsConfig.NonRebindableActionMaps);
                controller.ConflictDetected += OnRebindConflict;
                controller.RebindFinished += OnRebindFinished;
            }
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            InputSystem.onDeviceChange += OnInputDeviceChange;
            InputSystem.onEvent += OnInputEvent;
            view.Build();
            view.SetNavigationSubmitBlocked(false);
            sectionNavigationBlocked = false;
            view.BindActions(OnLoadClicked, OnSaveClicked, OnResetAllClicked, OnCloseClicked, OnBindingGroupChangedFromUi);
            var asset = controller?.Actions;
            navigationSession = new UiNavigationInputSession(
                asset?.FindAction("UI/Navigate"),
                new UiNavigationInputHandlers(view.HandleNavigationMove),
                new UiNavigationInputOptions { EventRoot = uiDocument.rootVisualElement });
            BindSectionNavigation();
            SuspendNonUiActions();

            if (controller == null)
            {
                view.SetInteractive(false, allowCloseWhenDisabled: true);
                view.SetLocalizedStatus("keyconfig.config_missing");
                view.SetBindingGroupChoices(null, bindingGroup);
                view.RenderBindingEntries(currentEntries, null, null, null, null);
                return;
            }

            view.SetBindingGroupChoices(controller.GetBindingGroups(), bindingGroup);

            var savedOverrides = loadOverrides?.Invoke();
            if (!string.IsNullOrWhiteSpace(savedOverrides)) controller.ImportOverrides(savedOverrides);
            BeginEditSession();
            RebuildBindingList();
            ApplyReadyStatus();
            view.FocusDefault();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
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
            controller?.CancelRebind();
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
            if (inputStateUpdate != null || controller == null || controller.IsRebinding) return;
            var root = uiDocument?.rootVisualElement;
            if (root == null) return;

            inputStateUpdate = root.schedule.Execute(() =>
            {
                inputStateUpdate = null;
                if (isActiveAndEnabled && !controller.IsRebinding)
                    view.UpdateInputStates(controller.Actions);
            });
        }

        public void SetBindingGroup(string group)
        {
            bindingGroup = group?.Trim() ?? string.Empty;

            if (controller == null || !isActiveAndEnabled)
            {
                return;
            }

            view.SetBindingGroupChoices(controller.GetBindingGroups(), bindingGroup);
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
            if (controller != null)
            {
                controller.ConflictDetected -= OnRebindConflict;
                controller.RebindFinished -= OnRebindFinished;
                controller.Dispose();
                controller = null;
            }

        }

        private void RebuildBindingList()
        {
            if (controller == null)
            {
                currentEntries.Clear();
                usingBindingGroupFallback = false;
                view.RenderBindingEntries(currentEntries, null, null, null, null);
                return;
            }

            currentEntries = new List<KeyConfigBinding>(controller.GetBindings(bindingGroup));
            usingBindingGroupFallback = false;

            if (currentEntries.Count == 0 && !string.IsNullOrWhiteSpace(bindingGroup))
            {
                currentEntries = new List<KeyConfigBinding>(controller.GetBindings());
                usingBindingGroupFallback = currentEntries.Count > 0;
            }

            view.RenderBindingEntries(currentEntries, OnRebindRequested, OnAddModifierRequested, OnRemoveModifierRequested, OnResetRequested);
            if (!controller.IsRebinding) view.UpdateInputStates(controller.Actions);
        }

        private void OnLoadClicked()
        {
            if (controller == null)
            {
                return;
            }

            var json = loadOverrides?.Invoke();
            var loaded = !string.IsNullOrWhiteSpace(json);
            if (loaded) controller.ImportOverrides(json);
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
            if (controller == null)
            {
                return;
            }

            if (saveOverrides == null)
            {
                view.SetLocalizedStatus("keyconfig.persistence_missing");
                return;
            }
            saveOverrides(controller.ExportOverrides());
            BeginEditSession();
            view.SetLocalizedStatus("keyconfig.saved");
        }

        private void OnResetAllClicked()
        {
            if (controller == null)
            {
                return;
            }

            controller.ResetAll();
            RebuildBindingList();
            view.SetLocalizedStatus("keyconfig.reset_all_done");
        }

        private void OnCloseClicked()
        {
            UiMenuNavigator.Back(this);
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene) => UiMenuNavigator.CloseAll();

        private void BeginEditSession()
        {
            sessionOverridesJson = controller?.ExportOverrides();
        }

        private void RestoreUnsavedChanges()
        {
            if (sessionOverridesJson == null || controller == null)
            {
                return;
            }

            controller.ImportOverrides(sessionOverridesJson);
            sessionOverridesJson = null;
        }

        private void OnRebindRequested(int index)
        {
            if (controller == null || pendingRebindStart.HasValue)
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
            submitAction ??= controller.Actions.FindAction(inputActionsConfig.SubmitActionPath);
            if (submitAction != null && submitAction.IsPressed())
            {
                pendingRebindStart = new PendingRebindRequest(entry.Id, effectiveBindingGroup);
                return;
            }

            StartRebind(entry.Id, effectiveBindingGroup);
        }

        private void CancelPendingRebindStart()
        {
            pendingRebindStart = null;
        }

        private void StartRebind(KeyConfigBindingId bindingId, string effectiveBindingGroup)
        {
            OnRebindStarted();
            var started = controller.StartRebind(bindingId, effectiveBindingGroup);
            if (!started)
            {
                activeRebindEntryIndex = -1;
                view.SetInteractive(true);
                view.SetLocalizedStatus("keyconfig.rebind_start_failed");
            }
        }

        private void OnResetRequested(int index)
        {
            if (controller == null)
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

            controller.Reset(entry.Id);
            RebuildBindingList();
            view.SetLocalizedStatus("keyconfig.binding_reset");
            view.FocusBindingEntry(index);
        }

        private void OnInputDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (controller == null || controller.IsRebinding) return;
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
            if (controller == null || index < 0 || index >= currentEntries.Count) return;
            var entry = currentEntries[index];
            if (!entry.IsRebindable) return;
            var changed = add
                ? controller.AddModifier(entry.Id)
                : controller.RemoveModifier(entry.Id);
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

        private void OnRebindConflict(KeyConfigConflict conflict)
        {
            ScheduleReleasedInputBlockRefresh();
            view.ShowConflict(
                conflict.Target?.ActionName ?? string.Empty,
                conflict.Existing?.ActionName ?? string.Empty,
                () => controller.ResolveConflict(KeyConfigConflictResolution.ReplaceExisting),
                () => controller.ResolveConflict(KeyConfigConflictResolution.KeepBoth),
                () => controller.ResolveConflict(KeyConfigConflictResolution.Cancel));
        }

        private void OnRebindFinished(KeyConfigRebindResult result)
        {
            switch (result.Status)
            {
                case KeyConfigRebindStatus.Completed:
                    FinishRebindUi(usingBindingGroupFallback ? "keyconfig.changed_fallback" : "keyconfig.changed", result.DisplayName);
                    break;
                case KeyConfigRebindStatus.Canceled:
                case KeyConfigRebindStatus.TimedOut:
                    FinishRebindUi(usingBindingGroupFallback ? "keyconfig.rebind_canceled_fallback" : "keyconfig.rebind_canceled");
                    break;
                default:
                    FinishRebindUi(string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? usingBindingGroupFallback ? "keyconfig.rebind_failed_fallback" : "keyconfig.rebind_failed"
                        : null, result.ErrorMessage);
                    break;
            }
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
            var asset = controller?.Actions;
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
            if (!sectionNavigationBlocked && !pendingRebindStart.HasValue && (controller == null || !controller.IsRebinding))
                view.SelectAdjacentSection(-1);
        }

        private void OnSubmitCanceled(InputAction.CallbackContext context)
        {
            if (submitAction?.IsPressed() == true) return;
            view.SetNavigationSubmitBlocked(false);
            if (!pendingRebindStart.HasValue) return;

            var request = pendingRebindStart.Value;
            CancelPendingRebindStart();
            if (isActiveAndEnabled) StartRebind(request.BindingId, request.BindingGroup);
        }

        private void OnSectionNavigationCanceled(InputAction.CallbackContext context)
        {
            if (controller?.IsRebinding == true) return;
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
            var asset = controller?.Actions;
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
            if (!sectionNavigationBlocked && !pendingRebindStart.HasValue && (controller == null || !controller.IsRebinding))
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
            public PendingRebindRequest(KeyConfigBindingId bindingId, string bindingGroup)
            {
                BindingId = bindingId;
                BindingGroup = bindingGroup;
            }

            public KeyConfigBindingId BindingId { get; }
            public string BindingGroup { get; }
        }
    }
}
