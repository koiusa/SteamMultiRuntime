using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerTraversalCoordinator
    {
        bool IsTraversalActive { get; }
        void ResetState();
        void ApplyTraversal(Vector3 moveDirection, Vector2 moveInput, bool jumpRequested, bool isGrounded);
    }
}
