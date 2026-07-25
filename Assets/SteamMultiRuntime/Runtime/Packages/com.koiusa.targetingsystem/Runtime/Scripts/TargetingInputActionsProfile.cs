using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [CreateAssetMenu(fileName = "TargetingInputActionsProfile", menuName = "Koiusa/Targeting/Input Actions Profile")]
    public sealed class TargetingInputActionsProfile : ScriptableObject
    {
        public InputAction LookAction => Resolve("Player/Look");
        public InputAction SoloLockAction => Resolve("Player/SingleLockOn");
        public InputAction MultiLockAction => Resolve("Player/MultiLockOn");
        public InputAction ClearLockAction => Resolve("Player/ClearLockOn");
        public InputAction BulkLockAction => Resolve("Player/BulkLockOn");
        public InputAction PreviousTargetAction => Resolve("Player/Previous");
        public InputAction NextTargetAction => Resolve("Player/Next");
        public InputAction FocusAction => Resolve("Player/Focus");

        [SerializeField] private InputActionReference assetSource;

        private InputAction Resolve(string path) => InputActionResolver.Resolve(assetSource, path);
    }
}
