using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerTraversalCoordinator
    {
        bool IsTraversalActive { get; }
        void ResetState();
        void ApplyTraversal(Vector3 moveDirection, bool jumpRequested, bool isGrounded);
    }
}
