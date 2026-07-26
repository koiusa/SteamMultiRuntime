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
        private readonly InputAction aimCursorMoveAction;
        private readonly InputAction grappleFireAction;
        private readonly GamepadAimCursorSettings gamepadAimCursorSettings;

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
        private InputActionLease aimCursorMoveLease;
        private InputActionLease grappleFireLease;
        private Vector2 aimCursorPosition;
        private bool hasAimCursor;
        private bool wasGrappleHeld;

        public PlayerGameplayInputReader(InputActionsConfig profile, GamepadAimCursorSettings gamepadAimCursorSettings = null)
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
            aimCursorMoveAction = profile.FindAction("Player/AimCursorMove");
            grappleFireAction = profile.FindAction("Player/GrappleFire");
            this.gamepadAimCursorSettings = gamepadAimCursorSettings ?? new GamepadAimCursorSettings();
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
            aimCursorMoveLease = InputActionLease.Acquire(aimCursorMoveAction);
            grappleFireLease = InputActionLease.Acquire(grappleFireAction);
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
            aimCursorMoveLease?.Dispose();
            grappleFireLease?.Dispose();
            moveLease = null;
            grappleLease = null;
            reelLease = null;
            aimCursorDeltaLease = null;
            aimCursorPositionLease = null;
            aimCursorMoveLease = null;
            grappleFireLease = null;
            hasAimCursor = false;
            wasGrappleHeld = false;
            jumpToken = 0;
            isStrafeMode = false;
        }

        public PlayerInputState ReadState()
        {
            var grappleHeld = grappleAction != null && grappleAction.IsPressed();
            var isGamepadAim = grappleHeld && grappleAction?.activeControl?.device is Gamepad;
            if (isGamepadAim && !wasGrappleHeld)
            {
                CenterAimCursor();
            }

            UpdateAimCursor(isGamepadAim);
            wasGrappleHeld = grappleHeld;
            var move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            var jumpPressed = jumpToken > 0;
            jumpToken = 0;
            var grappleFirePressed = grappleHeld && grappleFireAction != null && grappleFireAction.WasPressedThisFrame();
            var reelInput = reelAction != null ? reelAction.ReadValue<float>() : 0f;
            return new PlayerInputState(move, jumpPressed, grappleHeld, reelInput, isStrafeMode, grappleFirePressed);
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
            CenterAimCursor();
            hasAimCursor = aimCursorDeltaAction != null || aimCursorPositionAction != null || aimCursorMoveAction != null;
        }

        private void CenterAimCursor()
        {
            aimCursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private void UpdateAimCursor(bool isGamepadAim)
        {
            if (!hasAimCursor)
            {
                return;
            }

            const float edgePadding = 12f;
            if (!isGamepadAim && aimCursorPositionAction?.activeControl != null)
            {
                aimCursorPosition = aimCursorPositionAction.ReadValue<Vector2>();
            }

            var delta = aimCursorDeltaAction != null
                ? aimCursorDeltaAction.ReadValue<Vector2>()
                : Vector2.zero;
            aimCursorPosition += delta;
            if (isGamepadAim && aimCursorMoveAction != null)
            {
                var stick = ApplyAimResponseCurve(aimCursorMoveAction.ReadValue<Vector2>());
                var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var radius = Screen.height * gamepadAimCursorSettings.RadiusInScreenHeights;
                if (gamepadAimCursorSettings.Mode == GamepadAimCursorMode.Absolute)
                {
                    aimCursorPosition = center + Vector2.ClampMagnitude(stick, 1f) * radius;
                }
                else
                {
                    var speed = Screen.height * gamepadAimCursorSettings.SpeedInScreenHeightsPerSecond;
                    aimCursorPosition += stick * (speed * Time.unscaledDeltaTime);
                    aimCursorPosition = center + Vector2.ClampMagnitude(aimCursorPosition - center, radius);
                }
            }
            aimCursorPosition.x = Mathf.Clamp(aimCursorPosition.x, edgePadding, Mathf.Max(edgePadding, Screen.width - edgePadding));
            aimCursorPosition.y = Mathf.Clamp(aimCursorPosition.y, edgePadding, Mathf.Max(edgePadding, Screen.height - edgePadding));
        }

        private Vector2 ApplyAimResponseCurve(Vector2 stick)
        {
            var magnitude = Mathf.Clamp01(stick.magnitude);
            if (magnitude <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            var curvedMagnitude = Mathf.Pow(magnitude, gamepadAimCursorSettings.ResponseExponent);
            return stick / magnitude * curvedMagnitude;
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
