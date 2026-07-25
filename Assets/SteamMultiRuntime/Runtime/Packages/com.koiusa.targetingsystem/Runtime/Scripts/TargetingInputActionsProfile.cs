using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [CreateAssetMenu(fileName = "TargetingInputActionsProfile", menuName = "Koiusa/Targeting/Input Actions Profile")]
    public sealed class TargetingInputActionsProfile : ScriptableObject
    {
        [Header("Package Sample Only")]
        [TextArea, SerializeField] private string purpose =
            "TargetingSystemパッケージ単体サンプル専用。本番のSteamMultiRuntime入力設定には使用しません。";

        public string Purpose => purpose;

        public InputAction LookAction => Resolve("Player/Look");
        public InputAction SoloLockAction => Resolve("Player/SingleLockOn");
        public InputAction MultiLockAction => Resolve("Player/MultiLockOn");
        public InputAction ClearLockAction => Resolve("Player/ClearLockOn");
        public InputAction BulkLockAction => Resolve("Player/BulkLockOn");
        public InputAction PreviousTargetAction => Resolve("Player/Previous");
        public InputAction NextTargetAction => Resolve("Player/Next");
        public InputAction FocusAction => Resolve("Player/Focus");

        [Tooltip("TargetingSystem sample's complete InputActionAsset. Configure its actions and bindings in that asset.")]
        [SerializeField] private InputActionAsset inputActionAsset;

        private InputAction Resolve(string path) => inputActionAsset?.FindAction(path, throwIfNotFound: false);
    }
}
