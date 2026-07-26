using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Exposes a local NPC's locomotion state to systems that consume the
    /// common player-controller contract. Network NPCs use
    /// ServerDrivenPlayerController directly and must not add this adapter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NpcNavMeshController))]
    public sealed class NpcPlayerControllerAdapter : MonoBehaviour, IPlayerController
    {
        private NpcNavMeshController controller;

        public bool IsGrounded => controller != null && controller.IsGrounded;
        public bool IsJumping => controller != null && controller.IsJumping;
        public bool IsFreefall => controller != null && controller.IsFreefall;
        public bool IsFallingAfterJump => controller != null && controller.IsFallingAfterJump;
        public bool IsStrafeMode => controller != null && controller.IsStrafeMode;
        public Vector3 InheritedGroundVelocity => controller != null ? controller.InheritedGroundVelocity : Vector3.zero;
        public Vector2 MoveInput => controller != null ? controller.MoveInput : Vector2.zero;
        public Vector3 MoveDirection => controller != null ? controller.MoveDirection : Vector3.zero;
        public float HorizontalVelocity => controller != null ? controller.HorizontalVelocity : 0f;
        public float VerticalVelocity => controller != null ? controller.VerticalVelocity : 0f;
        public float MaxMoveSpeed => controller != null ? controller.MaxMoveSpeed : 1f;

        private void Awake()
        {
            controller = GetComponent<NpcNavMeshController>();
        }
    }
}
