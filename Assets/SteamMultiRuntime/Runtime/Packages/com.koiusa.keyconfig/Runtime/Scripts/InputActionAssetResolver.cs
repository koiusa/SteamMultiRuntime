using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    [CreateAssetMenu(fileName = "InputActionAssetResolver", menuName = "Koiusa/Keyconfig/Input Action Asset Resolver", order = 101)]
    public class InputActionAssetResolver : ScriptableObject
    {
        [SerializeField] private InputActionAsset inputActionAsset;

        public InputActionAsset Resolve()
        {
            return inputActionAsset;
        }
    }
}
