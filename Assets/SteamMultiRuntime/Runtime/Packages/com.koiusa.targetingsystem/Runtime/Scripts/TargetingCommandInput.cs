using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetingCommandInput : MonoBehaviour
    {
        [SerializeField] private TargetingController controller;
        [SerializeField] private TargetingInputActions inputActions;

        private InputActionBinding singleBinding;
        private InputActionBinding multiBinding;
        private InputActionBinding clearBinding;
        private InputActionBinding bulkBinding;
        private InputActionBinding previousBinding;
        private InputActionBinding nextBinding;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<TargetingController>();
        }

        private void OnEnable()
        {
            singleBinding = InputActionBinding.Bind(inputActions?.SoloLockAction, OnSingle);
            multiBinding = InputActionBinding.Bind(inputActions?.MultiLockAction, OnMulti);
            clearBinding = InputActionBinding.Bind(inputActions?.ClearLockAction, OnClear);
            bulkBinding = InputActionBinding.Bind(inputActions?.BulkLockAction, OnBulk);
            previousBinding = InputActionBinding.Bind(inputActions?.PreviousTargetAction, OnPrevious);
            nextBinding = InputActionBinding.Bind(inputActions?.NextTargetAction, OnNext);
        }

        private void OnDisable()
        {
            nextBinding?.Dispose();
            previousBinding?.Dispose();
            bulkBinding?.Dispose();
            clearBinding?.Dispose();
            multiBinding?.Dispose();
            singleBinding?.Dispose();
            nextBinding = previousBinding = bulkBinding = clearBinding = multiBinding = singleBinding = null;
        }

        private void OnSingle(InputAction.CallbackContext context)
        {
            if (controller == null) return;
            var command = controller.State.Mode == TargetingMode.Single
                ? TargetingCommandType.Clear
                : TargetingCommandType.EnterSingle;
            Execute(command);
        }

        private void OnMulti(InputAction.CallbackContext context)
        {
            if (controller == null) return;
            var command = controller.State.Mode == TargetingMode.Multi
                ? TargetingCommandType.Clear
                : TargetingCommandType.EnterMulti;
            Execute(command);
        }

        private void OnClear(InputAction.CallbackContext context) => Execute(TargetingCommandType.Clear);
        private void OnBulk(InputAction.CallbackContext context) => Execute(TargetingCommandType.SelectAllCandidates);
        private void OnPrevious(InputAction.CallbackContext context) => Execute(TargetingCommandType.SelectPrevious);
        private void OnNext(InputAction.CallbackContext context) => Execute(TargetingCommandType.SelectNext);

        private void Execute(TargetingCommandType type)
        {
            if (controller != null) controller.Execute(new TargetingCommand(type));
        }
    }
}
