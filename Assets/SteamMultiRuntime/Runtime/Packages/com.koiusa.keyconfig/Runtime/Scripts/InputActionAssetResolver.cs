using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    [CreateAssetMenu(fileName = "InputActionAssetResolver", menuName = "Koiusa/Keyconfig/Input Action Asset Resolver", order = 101)]
    public class InputActionAssetResolver : ScriptableObject
    {
        [Header("Input Actions")]
        [Tooltip("The InputActionAsset used by key configuration and input guides.")]
        [InspectorName("Input Action Asset")]
        [SerializeField] private InputActionAsset inputActionAsset;

        public InputActionAsset Resolve()
        {
            return inputActionAsset;
        }
    }
}
