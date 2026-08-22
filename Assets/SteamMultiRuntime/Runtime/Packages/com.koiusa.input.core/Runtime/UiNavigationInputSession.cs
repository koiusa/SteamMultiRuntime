using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Input
{
    public enum UiNavigationDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// Owns the shared UI actions and turns a held Vector2 into discrete navigation steps.
    /// Screens only provide move, submit, and cancel callbacks.
    /// </summary>
    public sealed class UiNavigationInputSession : IDisposable
    {
        private static readonly LinkedList<UiNavigationInputSession> ActiveSessions = new();
        private static int cursorVisibilityLeaseCount;
        private static bool cursorVisibilityBeforeFirstLease;
        private static CursorLockMode cursorLockModeBeforeFirstLease;

        public const float DefaultThreshold = 0.5f;
        public const float DefaultRepeatDelay = 0.4f;
        public const float DefaultRepeatInterval = 0.1f;
        public static bool OwnsCursorVisibility => cursorVisibilityLeaseCount > 0;

        private readonly Action<UiNavigationDirection> move;
        private readonly float threshold;
        private readonly float repeatDelay;
        private readonly float repeatInterval;
        private readonly Func<UiNavigationDirection, bool> blockNavigationEvent;
        private InputAction navigateAction;
        private LinkedListNode<UiNavigationInputSession> activeSessionNode;
        private InputActionLease navigateLease;
        private InputActionLease pointLease;
        private InputActionLease clickLease;
        private InputActionBinding submitBinding;
        private InputActionBinding cancelBinding;
        private VisualElement blockedEventRoot;
        private UiNavigationDirection heldDirection;
        private float nextRepeatTime;
        private bool repeatUpdateSubscribed;
        private bool ownsCursorVisibility;

        private bool IsActiveSession => activeSessionNode != null &&
            ReferenceEquals(ActiveSessions.Last, activeSessionNode);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveSessions.Clear();
            cursorVisibilityLeaseCount = 0;
            cursorVisibilityBeforeFirstLease = false;
            cursorLockModeBeforeFirstLease = CursorLockMode.None;
        }

        public UiNavigationInputSession(
            InputActionsConfig config,
            Action<UiNavigationDirection> move,
            Action submit,
            Action cancel,
            VisualElement blockedEventRoot = null,
            Func<UiNavigationDirection, bool> blockNavigationEvent = null,
            float threshold = DefaultThreshold,
            float repeatDelay = DefaultRepeatDelay,
            float repeatInterval = DefaultRepeatInterval)
            : this(
                config?.FindAction("UI/Navigate"),
                submit != null ? config?.FindAction("UI/Submit") : null,
                cancel != null ? config?.FindAction("UI/Cancel") : null,
                move,
                submit,
                cancel,
                blockedEventRoot,
                blockNavigationEvent,
                threshold,
                repeatDelay,
                repeatInterval)
        {
        }

        public UiNavigationInputSession(
            InputAction navigateAction,
            InputAction submitAction,
            InputAction cancelAction,
            Action<UiNavigationDirection> move,
            Action submit,
            Action cancel,
            VisualElement blockedEventRoot = null,
            Func<UiNavigationDirection, bool> blockNavigationEvent = null,
            float threshold = DefaultThreshold,
            float repeatDelay = DefaultRepeatDelay,
            float repeatInterval = DefaultRepeatInterval)
        {
            this.move = move;
            this.threshold = Mathf.Clamp01(threshold);
            this.repeatDelay = Mathf.Max(0f, repeatDelay);
            this.repeatInterval = Mathf.Max(0.01f, repeatInterval);
            this.blockNavigationEvent = blockNavigationEvent;

            if (navigateAction == null)
            {
                return;
            }

            this.navigateAction = navigateAction;
            activeSessionNode = ActiveSessions.AddLast(this);
            AcquireCursorVisibility();
            navigateLease = InputActionLease.Acquire(this.navigateAction);
            var actionAsset = this.navigateAction.actionMap?.asset;
            pointLease = InputActionLease.Acquire(actionAsset?.FindAction("UI/Point", false));
            clickLease = InputActionLease.Acquire(actionAsset?.FindAction("UI/Click", false));
            this.navigateAction.performed += OnNavigatePerformed;
            this.navigateAction.canceled += OnNavigateCanceled;
            submitBinding = submit == null
                ? null
                : InputActionBinding.Bind(submitAction, _ =>
                {
                    if (IsActiveSession)
                    {
                        submit.Invoke();
                    }
                });
            cancelBinding = cancel == null
                ? null
                : InputActionBinding.Bind(cancelAction, _ =>
                {
                    if (IsActiveSession)
                    {
                        cancel.Invoke();
                    }
                });
            this.blockedEventRoot = blockedEventRoot;
            this.blockedEventRoot?.RegisterCallback<NavigationMoveEvent>(
                OnNavigationMoveEvent,
                TrickleDown.TrickleDown);
            if (submitBinding != null)
            {
                this.blockedEventRoot?.RegisterCallback<NavigationSubmitEvent>(
                    OnNavigationSubmitEvent,
                    TrickleDown.TrickleDown);
            }
            if (cancelBinding != null)
            {
                this.blockedEventRoot?.RegisterCallback<NavigationCancelEvent>(
                    OnNavigationCancelEvent,
                    TrickleDown.TrickleDown);
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (navigateAction == null || move == null || !IsActiveSession)
            {
                StopRepeatUpdates();
                heldDirection = UiNavigationDirection.None;
                return;
            }

            var direction = ResolveDirection(context.ReadValue<Vector2>(), threshold);
            if (direction == UiNavigationDirection.None)
            {
                StopRepeatUpdates();
                heldDirection = UiNavigationDirection.None;
                return;
            }

            if (direction == heldDirection)
            {
                return;
            }

            heldDirection = direction;
            nextRepeatTime = Time.unscaledTime + repeatDelay;
            move(direction);
            StartRepeatUpdates();
        }

        private void OnNavigateCanceled(InputAction.CallbackContext _)
        {
            StopRepeatUpdates();
            heldDirection = UiNavigationDirection.None;
        }

        private void OnAfterInputUpdate()
        {
            if (!IsActiveSession || heldDirection == UiNavigationDirection.None)
            {
                StopRepeatUpdates();
                heldDirection = UiNavigationDirection.None;
                return;
            }

            if (Time.unscaledTime >= nextRepeatTime)
            {
                nextRepeatTime = Time.unscaledTime + repeatInterval;
                move(heldDirection);
            }
        }

        private void StartRepeatUpdates()
        {
            if (repeatUpdateSubscribed)
            {
                return;
            }

            InputSystem.onAfterUpdate += OnAfterInputUpdate;
            repeatUpdateSubscribed = true;
        }

        private void StopRepeatUpdates()
        {
            if (!repeatUpdateSubscribed)
            {
                return;
            }

            InputSystem.onAfterUpdate -= OnAfterInputUpdate;
            repeatUpdateSubscribed = false;
        }

        public void Dispose()
        {
            StopRepeatUpdates();
            if (navigateAction != null)
            {
                navigateAction.performed -= OnNavigatePerformed;
                navigateAction.canceled -= OnNavigateCanceled;
            }
            if (activeSessionNode != null)
            {
                ActiveSessions.Remove(activeSessionNode);
                activeSessionNode = null;
            }
            blockedEventRoot?.UnregisterCallback<NavigationMoveEvent>(
                OnNavigationMoveEvent,
                TrickleDown.TrickleDown);
            if (submitBinding != null)
            {
                blockedEventRoot?.UnregisterCallback<NavigationSubmitEvent>(
                    OnNavigationSubmitEvent,
                    TrickleDown.TrickleDown);
            }
            if (cancelBinding != null)
            {
                blockedEventRoot?.UnregisterCallback<NavigationCancelEvent>(
                    OnNavigationCancelEvent,
                    TrickleDown.TrickleDown);
            }
            blockedEventRoot = null;
            navigateLease?.Dispose();
            navigateLease = null;
            pointLease?.Dispose();
            pointLease = null;
            clickLease?.Dispose();
            clickLease = null;
            navigateAction = null;
            submitBinding?.Dispose();
            submitBinding = null;
            cancelBinding?.Dispose();
            cancelBinding = null;
            heldDirection = UiNavigationDirection.None;
            ReleaseCursorVisibility();
        }

        private void AcquireCursorVisibility()
        {
            if (cursorVisibilityLeaseCount == 0)
            {
                cursorVisibilityBeforeFirstLease = UnityEngine.Cursor.visible;
                cursorLockModeBeforeFirstLease = UnityEngine.Cursor.lockState;
            }

            cursorVisibilityLeaseCount++;
            ownsCursorVisibility = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void ReleaseCursorVisibility()
        {
            if (!ownsCursorVisibility)
            {
                return;
            }

            ownsCursorVisibility = false;
            cursorVisibilityLeaseCount = Mathf.Max(0, cursorVisibilityLeaseCount - 1);
            if (cursorVisibilityLeaseCount == 0)
            {
                UnityEngine.Cursor.lockState = cursorLockModeBeforeFirstLease;
                UnityEngine.Cursor.visible = cursorVisibilityBeforeFirstLease;
            }
        }

        private void OnNavigationMoveEvent(NavigationMoveEvent evt)
        {
            if (!IsActiveSession)
            {
                return;
            }

            var direction = evt.direction switch
            {
                NavigationMoveEvent.Direction.Up => UiNavigationDirection.Up,
                NavigationMoveEvent.Direction.Down => UiNavigationDirection.Down,
                NavigationMoveEvent.Direction.Left => UiNavigationDirection.Left,
                NavigationMoveEvent.Direction.Right => UiNavigationDirection.Right,
                _ => UiNavigationDirection.None
            };
            if (blockNavigationEvent != null && !blockNavigationEvent(direction))
            {
                return;
            }

            ConsumeEvent(evt);
        }

        private void OnNavigationSubmitEvent(NavigationSubmitEvent evt)
        {
            if (IsActiveSession)
            {
                ConsumeEvent(evt);
            }
        }

        private void OnNavigationCancelEvent(NavigationCancelEvent evt)
        {
            if (IsActiveSession)
            {
                ConsumeEvent(evt);
            }
        }

        private void ConsumeEvent(EventBase evt)
        {
            blockedEventRoot?.focusController?.IgnoreEvent(evt);
            evt.StopImmediatePropagation();
        }

        private static UiNavigationDirection ResolveDirection(Vector2 value, float threshold)
        {
            if (Mathf.Abs(value.y) >= Mathf.Abs(value.x) && Mathf.Abs(value.y) >= threshold)
            {
                return value.y > 0f ? UiNavigationDirection.Up : UiNavigationDirection.Down;
            }

            if (Mathf.Abs(value.x) >= threshold)
            {
                return value.x > 0f ? UiNavigationDirection.Right : UiNavigationDirection.Left;
            }

            return UiNavigationDirection.None;
        }
    }
}
