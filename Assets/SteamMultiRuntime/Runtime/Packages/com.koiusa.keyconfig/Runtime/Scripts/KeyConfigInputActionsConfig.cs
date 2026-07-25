using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    [CreateAssetMenu(fileName = "KeyConfigInputActionsConfig", menuName = "Koiusa/Keyconfig/Input Actions Config", order = 101)]
    public class KeyConfigInputActionsConfig : ScriptableObject
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
