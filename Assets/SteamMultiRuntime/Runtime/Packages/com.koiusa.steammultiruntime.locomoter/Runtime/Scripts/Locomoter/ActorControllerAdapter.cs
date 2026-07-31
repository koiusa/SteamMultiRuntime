using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Gives consumers one stable IActorController regardless of whether the
    /// underlying state is produced locally, by NPC navigation, or by netcode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorControllerAdapter : MonoBehaviour, IActorController, IActorLadderState, IActorWallRunState
    {
        [SerializeField] private MonoBehaviour stateSource;

        private IActorLocomotionState source;
        private IActorLadderState ladderState;
        private IActorWallRunState wallRunState;

        public bool IsGrounded => source != null && source.IsGrounded;
        public bool IsJumping => source != null && source.IsJumping;
        public bool IsFreefall => source != null && source.IsFreefall;
        public bool IsFallingAfterJump => source != null && source.IsFallingAfterJump;
        public bool IsStrafeMode => source != null && source.IsStrafeMode;
        public Vector3 InheritedGroundVelocity => source != null ? source.InheritedGroundVelocity : Vector3.zero;
        public Vector2 MoveInput => source != null ? source.MoveInput : Vector2.zero;
        public Vector3 MoveDirection => source != null ? source.MoveDirection : Vector3.zero;
        public float HorizontalVelocity => source != null ? source.HorizontalVelocity : 0f;
        public float VerticalVelocity => source != null ? source.VerticalVelocity : 0f;
        public float MaxMoveSpeed => source != null ? source.MaxMoveSpeed : 1f;
        public bool IsOnLadder => ladderState != null && ladderState.IsOnLadder;
        public float LadderSpeed => ladderState != null ? ladderState.LadderSpeed : 0f;
        public bool IsWallRunning => wallRunState != null && wallRunState.IsWallRunning;
        public Vector3 WallNormal => wallRunState != null ? wallRunState.WallNormal : Vector3.zero;

        private void Awake()
        {
            ResolveSource();
        }

        private void OnValidate()
        {
            ResolveSource();
        }

        private void ResolveSource()
        {
            source = stateSource as IActorLocomotionState;
            ladderState = null;
            wallRunState = null;

            if (source == null && stateSource == null)
            {
                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    if (component == null || component == this || !(component is IActorLocomotionState candidate))
                        continue;

                    if (source != null)
                    {
                        Debug.LogError("Multiple state sources found. Assign ActorControllerAdapter.stateSource explicitly.", this);
                        source = null;
                        return;
                    }

                    source = candidate;
                }
            }

            ladderState = source as IActorLadderState;
            wallRunState = source as IActorWallRunState;

            if (source == null && Application.isPlaying)
                Debug.LogError("ActorControllerAdapter could not find an IActorLocomotionState source.", this);
        }
    }
}
