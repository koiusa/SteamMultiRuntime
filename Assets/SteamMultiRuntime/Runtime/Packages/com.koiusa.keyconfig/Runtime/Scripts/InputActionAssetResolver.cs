using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    [CreateAssetMenu(fileName = "InputActionAssetResolver", menuName = "Koiusa/Keyconfig/Input Action Asset Resolver", order = 101)]
    public class InputActionAssetResolver : ScriptableObject
    {
        [SerializeField] private InputActionAsset inputActionAsset;
        [Tooltip("Optional fallback. The owning InputActionAsset is resolved from this action.")]
        [SerializeField] private InputActionReference inputActionReference;

        public InputActionAsset Resolve()
        {
            return inputActionAsset != null
                ? inputActionAsset
                : inputActionReference?.action?.actionMap?.asset;
        }
    }
}
