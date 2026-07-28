using Koiusa.Input;
using Koiusa.TargetingSystem.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime.TargetingSystem
{
    [CreateAssetMenu(
        fileName = "SteamMultiRuntimeTargetingInputActions",
        menuName = "Koiusa/Steam Multi Runtime/Targeting/Input Actions")]
    public sealed class SteamMultiRuntimeTargetingInputActions : TargetingInputActions
    {
        [SerializeField] private InputActionsConfig inputActionsConfig;

        [Header("Action Paths")]
        [SerializeField] private string lookActionPath = "Player/Look";
        [SerializeField] private string soloLockActionPath = "Player/LockOn";
        [SerializeField] private string multiLockActionPath = "";
        [SerializeField] private string clearLockActionPath = "";
        [SerializeField] private string bulkLockActionPath = "";
        [SerializeField] private string previousTargetActionPath = "Player/Previous";
        [SerializeField] private string nextTargetActionPath = "Player/Next";
        [SerializeField] private string focusActionPath = "";

        public override InputAction LookAction => Resolve(lookActionPath);
        public override InputAction SoloLockAction => Resolve(soloLockActionPath);
        public override InputAction MultiLockAction => Resolve(multiLockActionPath);
        public override InputAction ClearLockAction => Resolve(clearLockActionPath);
        public override InputAction BulkLockAction => Resolve(bulkLockActionPath);
        public override InputAction PreviousTargetAction => Resolve(previousTargetActionPath);
        public override InputAction NextTargetAction => Resolve(nextTargetActionPath);
        public override InputAction FocusAction => Resolve(focusActionPath);

        private InputAction Resolve(string actionPath) =>
            string.IsNullOrWhiteSpace(actionPath) ? null : inputActionsConfig?.FindAction(actionPath);
    }
}
