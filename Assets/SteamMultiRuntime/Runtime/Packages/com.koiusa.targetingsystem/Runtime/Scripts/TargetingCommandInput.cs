using System.Collections;
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
        [SerializeField, Min(0f)] private float multiAddInitialDelay = 0.3f;
        [SerializeField, Min(0.05f)] private float multiAddInterval = 0.2f;
        [SerializeField, Range(0f, 1f)] private float directionalSelectionDeadzone = 0.35f;

        private InputActionBinding singleBinding;
        private InputActionBinding multiBinding;
        private InputActionBinding promoteToMultiBinding;
        private InputActionBinding clearBinding;
        private InputActionBinding bulkBinding;
        private InputActionBinding previousBinding;
        private InputActionBinding nextBinding;
        private bool promotedToMultiWhileHeld;
        private Coroutine progressiveMultiAddRoutine;

        public void Configure(TargetingController newController, TargetingInputActions newInputActions)
        {
            controller = newController;
            inputActions = newInputActions;
        }

        private void Awake()
        {
            if (controller == null) controller = GetComponent<TargetingController>();
        }

        private void OnEnable()
        {
            singleBinding = InputActionBinding.Bind(inputActions?.SoloLockAction, OnSingle);
            multiBinding = InputActionBinding.Bind(inputActions?.MultiLockAction, OnMulti);
            promoteToMultiBinding = InputActionBinding.Bind(
                inputActions?.PromoteToMultiAction,
                OnPromoteToMulti,
                OnPromoteToMultiCanceled);
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
            promoteToMultiBinding?.Dispose();
            singleBinding?.Dispose();
            nextBinding = previousBinding = bulkBinding = clearBinding = promoteToMultiBinding = multiBinding = singleBinding = null;
            EndHeldMultiPromotion(restoreSingle: false);
        }

        private void OnSingle(InputAction.CallbackContext context)
        {
            if (controller == null) return;
            var command = ResolveLockButtonCommand(controller.State.Mode);
            if (command == TargetingCommandType.Clear) ClearAllLocks();
            else Execute(command);
        }

        private void OnMulti(InputAction.CallbackContext context)
        {
            if (controller == null) return;
            EndHeldMultiPromotion(restoreSingle: false);
            if (controller.State.Mode == TargetingMode.Multi)
            {
                ClearAllLocks();
                return;
            }

            if (Execute(TargetingCommandType.EnterMulti))
            {
                Execute(TargetingCommandType.SelectAllCandidates);
            }
        }

        private void OnPromoteToMultiCanceled(InputAction.CallbackContext context) =>
            EndHeldMultiPromotion(restoreSingle: true);

        private void OnClear(InputAction.CallbackContext context) => ClearAllLocks();
        private void OnBulk(InputAction.CallbackContext context) => Execute(TargetingCommandType.SelectAllCandidates);
        private void OnPrevious(InputAction.CallbackContext context) => Execute(TargetingCommandType.SelectPrevious);
        private void OnNext(InputAction.CallbackContext context)
        {
            var direction = IsRightStickPress(context)
                ? inputActions?.LookAction?.ReadValue<Vector2>() ?? Vector2.zero
                : Vector2.zero;
            Execute(direction.magnitude >= directionalSelectionDeadzone
                ? new TargetingCommand(TargetingCommandType.SelectNext, direction.normalized)
                : new TargetingCommand(TargetingCommandType.SelectNext));
        }

        private void OnPromoteToMulti(InputAction.CallbackContext context)
        {
            if (controller == null || controller.State.Mode != TargetingMode.Single)
            {
                return;
            }

            if (Execute(TargetingCommandType.EnterMulti))
            {
                BeginHeldMultiPromotion();
            }
        }

        private static TargetingCommandType ResolveLockButtonCommand(TargetingMode mode) =>
            mode == TargetingMode.None ? TargetingCommandType.EnterSingle : TargetingCommandType.Clear;

        private void BeginHeldMultiPromotion()
        {
            EndHeldMultiPromotion(restoreSingle: false);
            promotedToMultiWhileHeld = true;
            progressiveMultiAddRoutine = StartCoroutine(AddTargetsWhilePromoted());
        }

        private void EndHeldMultiPromotion(bool restoreSingle)
        {
            if (!promotedToMultiWhileHeld && progressiveMultiAddRoutine == null) return;

            StopProgressiveMultiAdd();
            var shouldRestoreSingle = restoreSingle
                && promotedToMultiWhileHeld
                && controller != null
                && controller.State.Mode == TargetingMode.Multi;
            promotedToMultiWhileHeld = false;
            if (shouldRestoreSingle) Execute(TargetingCommandType.EnterSingle);
        }

        private void ClearAllLocks()
        {
            EndHeldMultiPromotion(restoreSingle: false);
            Execute(TargetingCommandType.Clear);
        }

        private IEnumerator AddTargetsWhilePromoted()
        {
            var initialDelay = Mathf.Max(0f, multiAddInitialDelay);
            if (initialDelay > 0f) yield return new WaitForSecondsRealtime(initialDelay);

            var interval = new WaitForSecondsRealtime(Mathf.Max(0.05f, multiAddInterval));
            while (promotedToMultiWhileHeld && controller != null && controller.State.Mode == TargetingMode.Multi)
            {
                Execute(TargetingCommandType.AddBestCandidate);
                yield return interval;
            }
            progressiveMultiAddRoutine = null;
        }

        private void StopProgressiveMultiAdd()
        {
            if (progressiveMultiAddRoutine == null) return;
            StopCoroutine(progressiveMultiAddRoutine);
            progressiveMultiAddRoutine = null;
        }

        private bool Execute(TargetingCommandType type)
        {
            return Execute(new TargetingCommand(type));
        }

        private bool Execute(in TargetingCommand command) =>
            controller != null && controller.Execute(command).Changed;

        private static bool IsRightStickPress(InputAction.CallbackContext context) =>
            context.control?.device is Gamepad && context.control.name == "rightStickPress";
    }
}
