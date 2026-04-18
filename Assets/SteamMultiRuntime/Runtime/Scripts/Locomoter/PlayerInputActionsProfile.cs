using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    [CreateAssetMenu(fileName = "PlayerInputActionsProfile", menuName = "SteamMultiRuntime/Input/Player Input Actions Profile")]
    public sealed class PlayerInputActionsProfile : ScriptableObject
    {
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference strafeToggleAction;

        public InputActionReference MoveAction => moveAction;
        public InputActionReference JumpAction => jumpAction;
        public InputActionReference StrafeToggleAction => strafeToggleAction;
    }
}
