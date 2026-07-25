using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Koiusa.SteamMultiRuntime
{
    [CreateAssetMenu(fileName = "PlayerInputActionsProfile", menuName = "SteamMultiRuntime/Input/Player Input Actions Profile")]
    public sealed class PlayerInputActionsProfile : ScriptableObject
    {
        [FormerlySerializedAs("moveAction")]
        [SerializeField] private InputActionReference assetSource;

        // Kept only as a migration fallback for assets created before input.core.
        [HideInInspector, SerializeField] private InputActionReference jumpAction;
        [HideInInspector, SerializeField] private InputActionReference strafeToggleAction;
        [HideInInspector, SerializeField] private InputActionReference grappleAction;
        [HideInInspector, SerializeField] private InputActionReference reelAction;

        public InputAction MoveInputAction => Resolve("Player/Move");
        public InputAction JumpInputAction => jumpAction?.action ?? Resolve("Player/Jump");
        public InputAction StrafeToggleInputAction => strafeToggleAction?.action ?? Resolve("Player/StrafeToggle");
        public InputAction GrappleInputAction => grappleAction?.action ?? Resolve("Player/Grapple");
        public InputAction ReelInputAction => reelAction?.action ?? Resolve("Player/Reel");

        private InputAction Resolve(string path) => InputActionResolver.Resolve(assetSource, path);
    }
}
