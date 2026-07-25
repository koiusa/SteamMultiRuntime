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

        private int jumpToken;
        private bool isStrafeMode;
        private bool isEnabled;

        public PlayerGameplayInputReader(PlayerInputActionsProfile profile)
        {
            if (profile == null)
            {
                throw new System.ArgumentNullException(nameof(profile));
            }

            moveAction = profile.MoveInputAction;
            jumpAction = profile.JumpInputAction;
            strafeToggleAction = profile.StrafeToggleInputAction;
            grappleAction = profile.GrappleInputAction;
            reelAction = profile.ReelInputAction;
        }

        public void Enable()
        {
            if (isEnabled)
            {
                return;
            }

            isEnabled = true;
            moveAction?.Enable();
            grappleAction?.Enable();
            reelAction?.Enable();

            if (jumpAction != null)
            {
                jumpAction.performed += OnJumpPerformed;
                jumpAction.Enable();
            }

            if (strafeToggleAction != null)
            {
                strafeToggleAction.performed += OnStrafeTogglePerformed;
                strafeToggleAction.Enable();
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
                jumpAction.Disable();
            }

            if (strafeToggleAction != null)
            {
                strafeToggleAction.performed -= OnStrafeTogglePerformed;
                strafeToggleAction.Disable();
            }

            moveAction?.Disable();
            grappleAction?.Disable();
            reelAction?.Disable();
            jumpToken = 0;
            isStrafeMode = false;
        }

        public PlayerInputState ReadState()
        {
            var move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            var jumpPressed = jumpToken > 0;
            jumpToken = 0;
            var grappleHeld = grappleAction != null && grappleAction.IsPressed();
            var reelInput = reelAction != null ? reelAction.ReadValue<float>() : 0f;
            return new PlayerInputState(move, jumpPressed, grappleHeld, reelInput, isStrafeMode);
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
