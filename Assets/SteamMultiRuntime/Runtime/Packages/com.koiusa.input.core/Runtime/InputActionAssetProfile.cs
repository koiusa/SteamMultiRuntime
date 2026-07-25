using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Input
{
    [CreateAssetMenu(fileName = "InputActionAssetProfile", menuName = "Koiusa/Input/Input Action Asset Profile")]
    public sealed class InputActionAssetProfile : ScriptableObject
    {
        [SerializeField] private InputActionReference assetSource;

        public InputAction FindAction(string actionPath)
        {
            return InputActionResolver.Resolve(assetSource, actionPath);
        }
    }
}
