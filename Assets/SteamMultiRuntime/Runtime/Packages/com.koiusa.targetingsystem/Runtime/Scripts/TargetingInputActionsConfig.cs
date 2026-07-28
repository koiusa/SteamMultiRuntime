using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [CreateAssetMenu(fileName = "TargetingInputActionsConfig", menuName = "Koiusa/Targeting/Input Actions Config")]
    public sealed class TargetingInputActionsConfig : TargetingInputActions
    {
        [Header("Purpose")]
        [TextArea, SerializeField] private string purpose;

        [Header("Action Paths")]
        [SerializeField] private string lookActionPath = "Player/Look";
        [SerializeField] private string soloLockActionPath = "Player/SingleLockOn";
        [SerializeField] private string multiLockActionPath = "Player/MultiLockOn";
        [SerializeField] private string clearLockActionPath = "Player/ClearLockOn";
        [SerializeField] private string bulkLockActionPath = "Player/BulkLockOn";
        [SerializeField] private string previousTargetActionPath = "Player/Previous";
        [SerializeField] private string nextTargetActionPath = "Player/Next";
        [SerializeField] private string focusActionPath = "Player/Focus";

        public string Purpose => purpose;

        public override InputAction LookAction => Resolve(lookActionPath);
        public override InputAction SoloLockAction => Resolve(soloLockActionPath);
        public override InputAction MultiLockAction => Resolve(multiLockActionPath);
        public override InputAction ClearLockAction => Resolve(clearLockActionPath);
        public override InputAction BulkLockAction => Resolve(bulkLockActionPath);
        public override InputAction PreviousTargetAction => Resolve(previousTargetActionPath);
        public override InputAction NextTargetAction => Resolve(nextTargetActionPath);
        public override InputAction FocusAction => Resolve(focusActionPath);

        [Tooltip("The complete InputActionAsset used by this targeting profile.")]
        [SerializeField] private InputActionAsset inputActionAsset;

        private InputAction Resolve(string path) => string.IsNullOrWhiteSpace(path)
            ? null
            : inputActionAsset?.FindAction(path, throwIfNotFound: false);
    }
}
