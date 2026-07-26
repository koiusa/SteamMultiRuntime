using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// The single Input System adapter for player gameplay. Controllers consume
    /// PlayerInputState and traversal features never depend on InputAction.
    /// </summary>
    public sealed class PlayerGameplayInputReader : IPlayerInputSource
    {
        private readonly InputAction moveAction;
        private readonly InputAction jumpAction;
        private readonly InputAction strafeToggleAction;
        private readonly InputAction grappleAction;
        private readonly InputAction reelAction;
        private readonly InputAction aimCursorDeltaAction;
        private readonly InputAction aimCursorPositionAction;

        private int jumpToken;
        private bool isStrafeMode;
        private bool isEnabled;
        private InputActionLease moveLease;
        private InputActionLease jumpLease;
        private InputActionLease strafeToggleLease;
        private InputActionLease grappleLease;
        private InputActionLease reelLease;
        private InputActionLease aimCursorDeltaLease;
        private InputActionLease aimCursorPositionLease;
        private Vector2 aimCursorPosition;
        private bool hasAimCursor;

        public PlayerGameplayInputReader(InputActionsConfig profile)
        {
            if (profile == null)
            {
                throw new System.ArgumentNullException(nameof(profile));
            }

            moveAction = profile.FindAction("Player/Move");
            jumpAction = profile.FindAction("Player/Jump");
            strafeToggleAction = profile.FindAction("Player/StrafeToggle");
            grappleAction = profile.FindAction("Player/Grapple");
            reelAction = profile.FindAction("Player/Reel");
            aimCursorDeltaAction = profile.FindAction("Player/AimCursorDelta");
            aimCursorPositionAction = profile.FindAction("Player/AimCursorPosition");
        }

        public void Enable()
        {
            if (isEnabled)
            {
                return;
            }

            isEnabled = true;
            moveLease = InputActionLease.Acquire(moveAction);
            grappleLease = InputActionLease.Acquire(grappleAction);
            reelLease = InputActionLease.Acquire(reelAction);
            aimCursorDeltaLease = InputActionLease.Acquire(aimCursorDeltaAction);
            aimCursorPositionLease = InputActionLease.Acquire(aimCursorPositionAction);
            ResetAimCursor();

            if (jumpAction != null)
            {
                jumpAction.performed += OnJumpPerformed;
                jumpLease = InputActionLease.Acquire(jumpAction);
            }

            if (strafeToggleAction != null)
            {
                strafeToggleAction.performed += OnStrafeTogglePerformed;
                strafeToggleLease = InputActionLease.Acquire(strafeToggleAction);
            }
        }

        public void Disable()
        {
            if (!isEnabled)
            {
                return;
            }

            isEnabled = false;
            if (jumpAction != null)
            {
                jumpAction.performed -= OnJumpPerformed;
                jumpLease?.Dispose();
                jumpLease = null;
            }

            if (strafeToggleAction != null)
            {
                strafeToggleAction.performed -= OnStrafeTogglePerformed;
                strafeToggleLease?.Dispose();
                strafeToggleLease = null;
            }

            moveLease?.Dispose();
            grappleLease?.Dispose();
            reelLease?.Dispose();
            aimCursorDeltaLease?.Dispose();
            aimCursorPositionLease?.Dispose();
            moveLease = null;
            grappleLease = null;
            reelLease = null;
            aimCursorDeltaLease = null;
            aimCursorPositionLease = null;
            hasAimCursor = false;
            jumpToken = 0;
            isStrafeMode = false;
        }

        public PlayerInputState ReadState()
        {
            UpdateAimCursor();
            var move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            var jumpPressed = jumpToken > 0;
            jumpToken = 0;
            var grappleHeld = grappleAction != null && grappleAction.IsPressed();
            var reelInput = reelAction != null ? reelAction.ReadValue<float>() : 0f;
            return new PlayerInputState(move, jumpPressed, grappleHeld, reelInput, isStrafeMode);
        }

        public bool TryReadAimPoint(out Vector2 screenPosition)
        {
            if (!hasAimCursor)
            {
                screenPosition = default;
                return false;
            }

            screenPosition = aimCursorPosition;
            return true;
        }

        private void ResetAimCursor()
        {
            aimCursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            hasAimCursor = aimCursorDeltaAction != null || aimCursorPositionAction != null;
        }

        private void UpdateAimCursor()
        {
            if (!hasAimCursor)
            {
                return;
            }

            const float edgePadding = 12f;
            if (aimCursorPositionAction?.activeControl != null)
            {
                aimCursorPosition = aimCursorPositionAction.ReadValue<Vector2>();
            }

            var delta = aimCursorDeltaAction != null
                ? aimCursorDeltaAction.ReadValue<Vector2>()
                : Vector2.zero;
            aimCursorPosition += delta;
            aimCursorPosition.x = Mathf.Clamp(aimCursorPosition.x, edgePadding, Mathf.Max(edgePadding, Screen.width - edgePadding));
            aimCursorPosition.y = Mathf.Clamp(aimCursorPosition.y, edgePadding, Mathf.Max(edgePadding, Screen.height - edgePadding));
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            jumpToken++;
        }

        private void OnStrafeTogglePerformed(InputAction.CallbackContext context)
        {
            isStrafeMode = !isStrafeMode;
        }
    }
}
