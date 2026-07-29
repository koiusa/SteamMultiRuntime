using System;
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
        public const float DefaultThreshold = 0.5f;
        public const float DefaultRepeatDelay = 0.4f;
        public const float DefaultRepeatInterval = 0.1f;

        private readonly Action<UiNavigationDirection> move;
        private readonly float threshold;
        private readonly float repeatDelay;
        private readonly float repeatInterval;
        private readonly Func<UiNavigationDirection, bool> blockNavigationEvent;
        private InputAction navigateAction;
        private InputActionLease navigateLease;
        private InputActionBinding submitBinding;
        private InputActionBinding cancelBinding;
        private VisualElement blockedEventRoot;
        private UiNavigationDirection heldDirection;
        private float nextRepeatTime;

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
            navigateLease = InputActionLease.Acquire(this.navigateAction);
            InputSystem.onAfterUpdate += OnAfterInputUpdate;
            submitBinding = submit == null
                ? null
                : InputActionBinding.Bind(submitAction, _ => submit.Invoke());
            cancelBinding = cancel == null
                ? null
                : InputActionBinding.Bind(cancelAction, _ => cancel.Invoke());
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

        private void OnAfterInputUpdate()
        {
            if (navigateAction == null || move == null)
            {
                return;
            }

            var direction = ResolveDirection(navigateAction.ReadValue<Vector2>(), threshold);
            if (direction == UiNavigationDirection.None)
            {
                heldDirection = UiNavigationDirection.None;
                return;
            }

            if (direction != heldDirection)
            {
                heldDirection = direction;
                nextRepeatTime = Time.unscaledTime + repeatDelay;
                move(direction);
                return;
            }

            if (Time.unscaledTime >= nextRepeatTime)
            {
                nextRepeatTime = Time.unscaledTime + repeatInterval;
                move(direction);
            }
        }

        public void Dispose()
        {
            InputSystem.onAfterUpdate -= OnAfterInputUpdate;
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
            navigateAction = null;
            submitBinding?.Dispose();
            submitBinding = null;
            cancelBinding?.Dispose();
            cancelBinding = null;
            heldDirection = UiNavigationDirection.None;
        }

        private void OnNavigationMoveEvent(NavigationMoveEvent evt)
        {
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
            ConsumeEvent(evt);
        }

        private void OnNavigationCancelEvent(NavigationCancelEvent evt)
        {
            ConsumeEvent(evt);
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
