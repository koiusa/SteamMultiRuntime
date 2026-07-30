using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public readonly struct ActorInputState
    {
        public ActorInputState(Vector2 move, bool jumpPressed, bool grappleHeld = false, float reelInput = 0f, bool isStrafeMode = false, bool grappleFirePressed = false)
        {
            Move = move;
            JumpPressed = jumpPressed;
            GrappleHeld = grappleHeld;
            ReelInput = reelInput;
            IsStrafeMode = isStrafeMode;
            GrappleFirePressed = grappleFirePressed;
        }

        public Vector2 Move { get; }
        public bool JumpPressed { get; }
        public bool GrappleHeld { get; }
        public float ReelInput { get; }
        public bool IsStrafeMode { get; }
        public bool GrappleFirePressed { get; }

        public static ActorInputState Empty => new ActorInputState(Vector2.zero, false);
    }
}
