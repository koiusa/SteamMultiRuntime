using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public readonly struct PlayerInputState
    {
        public PlayerInputState(Vector2 move, bool jumpPressed, bool grappleHeld = false, float reelInput = 0f)
        {
            Move = move;
            JumpPressed = jumpPressed;
            GrappleHeld = grappleHeld;
            ReelInput = reelInput;
        }

        public Vector2 Move { get; }
        public bool JumpPressed { get; }
        public bool GrappleHeld { get; }
        public float ReelInput { get; }

        public static PlayerInputState Empty => new PlayerInputState(Vector2.zero, false);
    }
}
