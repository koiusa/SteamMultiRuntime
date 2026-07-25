using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    [CreateAssetMenu(fileName = "InputActionAssetResolver", menuName = "Koiusa/Keyconfig/Input Action Asset Resolver", order = 101)]
    public class InputActionAssetResolver : ScriptableObject
    {
        [Header("Resolution Sources")]
        [Tooltip("Optional. When assigned, this asset takes priority over Asset Source Action.")]
        [InspectorName("Direct Input Action Asset (Optional)")]
        [SerializeField] private InputActionAsset inputActionAsset;

        [Tooltip("Fallback source. The InputActionAsset containing this action is resolved automatically.")]
        [InspectorName("Asset Source Action (Fallback)")]
        [SerializeField] private InputActionReference inputActionReference;

        public InputActionAsset Resolve()
        {
            return inputActionAsset != null
                ? inputActionAsset
                : inputActionReference?.action?.actionMap?.asset;
        }
    }
}
