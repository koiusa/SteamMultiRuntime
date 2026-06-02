using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public readonly struct PlayerInputState
    {
        public PlayerInputState(Vector2 move, bool jumpPressed)
        {
            Move = move;
            JumpPressed = jumpPressed;
        }

        public Vector2 Move { get; }
        public bool JumpPressed { get; }

        public static PlayerInputState Empty => new PlayerInputState(Vector2.zero, false);
    }
}
