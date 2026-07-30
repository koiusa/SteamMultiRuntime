using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// The single Input System adapter for player gameplay. Controllers consume
    /// ActorInputState and traversal features never depend on InputAction.
    /// </summary>
    public sealed class PlayerGameplayInputReader : IActorInputSource
    {
        private readonly InputAction moveAction;
        private readonly InputAction jumpAction;
        private readonly InputAction strafeAction;
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
        private InputActionLease strafeLease;
        private InputActionLease grappleLease;
        private InputActionLease reelLease;
        private InputActionLease aimCursorDeltaLease;
        private InputActionLease aimCursorPositionLease;
        private InputActionLease aimCursorMoveLease;
        private InputActionLease grappleFireLease;
        private Vector2 aimCursorPosition;
        private bool hasAimCursor;
        private bool wasGrappleHeld;
        private float lastPointerMoveTime = float.NegativeInfinity;
        private Vector2 lastGamepadAimCursorPosition;
        private bool hasLastGamepadAimCursorPosition;

        public bool IsAimCursorRecentlyMoved => Time.unscaledTime - lastPointerMoveTime <= 1f;

        public PlayerGameplayInputReader(InputActionsConfig profile, GamepadAimCursorSettings gamepadAimCursorSettings = null)
        {
            if (profile == null)
            {
                throw new System.ArgumentNullException(nameof(profile));
            }

            moveAction = profile.FindAction("Player/Move");
            jumpAction = profile.FindAction("Player/Jump");
            strafeAction = profile.FindAction("Player/Strafe");
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

            if (strafeAction != null)
            {
                strafeAction.performed += OnStrafeChanged;
                strafeAction.canceled += OnStrafeChanged;
                strafeLease = InputActionLease.Acquire(strafeAction);
                isStrafeMode = strafeAction.IsPressed();
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

            if (strafeAction != null)
            {
                strafeAction.performed -= OnStrafeChanged;
                strafeAction.canceled -= OnStrafeChanged;
                strafeLease?.Dispose();
                strafeLease = null;
                isStrafeMode = false;
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

        public ActorInputState ReadState()
        {
            var grappleHeld = grappleAction != null && grappleAction.IsPressed();
            var isGamepadAim = grappleHeld && grappleAction?.activeControl?.device is Gamepad;
            if (isGamepadAim && !wasGrappleHeld)
            {
                if (gamepadAimCursorSettings.Mode == GamepadAimCursorMode.Relative
                    && gamepadAimCursorSettings.RememberLastRelativePosition
                    && hasLastGamepadAimCursorPosition)
                {
                    aimCursorPosition = lastGamepadAimCursorPosition;
                }
                else
                {
                    CenterAimCursor();
                }
            }

            UpdateAimCursor(isGamepadAim);
            if (isGamepadAim)
            {
                lastGamepadAimCursorPosition = aimCursorPosition;
                hasLastGamepadAimCursorPosition = true;
                SyncSystemPointerPosition();
            }
            if (HasGamepadActivity())
            {
                lastPointerMoveTime = float.NegativeInfinity;
            }
            wasGrappleHeld = grappleHeld;
            var move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            var jumpPressed = jumpToken > 0;
            jumpToken = 0;
            var grappleFirePressed = grappleHeld && grappleFireAction != null && grappleFireAction.WasPressedThisFrame();
            var reelInput = reelAction != null ? reelAction.ReadValue<float>() : 0f;
            return new ActorInputState(move, jumpPressed, grappleHeld, reelInput, isStrafeMode, grappleFirePressed);
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
                var pointerPosition = aimCursorPositionAction.ReadValue<Vector2>();
                if ((pointerPosition - aimCursorPosition).sqrMagnitude > 0.01f)
                {
                    lastPointerMoveTime = Time.unscaledTime;
                }

                aimCursorPosition = pointerPosition;
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

        private bool HasGamepadActivity()
        {
            return IsActiveGamepadControl(moveAction)
                || IsActiveGamepadControl(jumpAction)
                || IsActiveGamepadControl(strafeAction)
                || IsActiveGamepadControl(grappleAction)
                || IsActiveGamepadControl(grappleFireAction)
                || IsActiveGamepadControl(reelAction)
                || IsActiveGamepadControl(aimCursorMoveAction);
        }

        private void SyncSystemPointerPosition()
        {
            if (!gamepadAimCursorSettings.SyncSystemPointerPosition || Mouse.current == null)
            {
                return;
            }

            if ((Mouse.current.position.ReadValue() - aimCursorPosition).sqrMagnitude > 0.25f)
            {
                Mouse.current.WarpCursorPosition(aimCursorPosition);
            }
        }

        private static bool IsActiveGamepadControl(InputAction action)
        {
            var control = action?.activeControl;
            return control?.device is Gamepad && control.IsActuated();
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            jumpToken++;
        }

        private void OnStrafeChanged(InputAction.CallbackContext context) =>
            isStrafeMode = context.ReadValueAsButton();
    }
}
