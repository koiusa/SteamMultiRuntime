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
        [SerializeField] private InputActionReference grappleAction;
        [SerializeField] private InputActionReference reelAction;

        public InputActionReference MoveAction => moveAction;
        public InputActionReference JumpAction => jumpAction;
        public InputActionReference StrafeToggleAction => strafeToggleAction;
        public InputActionReference GrappleAction => grappleAction;
        public InputActionReference ReelAction => reelAction;
        public InputAction GrappleInputAction => ResolveAction(grappleAction, "Player/Grapple");
        public InputAction ReelInputAction => ResolveAction(reelAction, "Player/Reel");

        private InputAction ResolveAction(InputActionReference explicitReference, string actionPath)
        {
            if (explicitReference != null && explicitReference.action != null)
            {
                return explicitReference.action;
            }

            var asset = moveAction != null && moveAction.action != null
                ? moveAction.action.actionMap?.asset
                : null;
            return asset?.FindAction(actionPath, throwIfNotFound: false);
        }
    }
}
