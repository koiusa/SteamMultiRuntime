using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Koiusa.Keyconfig.Runtime
{
    /// <summary>Suppresses logical aliases emitted by the state event that completes a composite part.</summary>
    internal sealed class RebindAliasSuppression : IDisposable
    {
        private const float ActuationThreshold = 0.15f;

        private readonly HashSet<string> suppressedPaths = new(StringComparer.OrdinalIgnoreCase);

        internal void BeginPartObservation()
        {
            InputSystem.onEvent -= OnInputEvent;
            InputSystem.onEvent += OnInputEvent;
        }

        internal void ApplyExclusions(InputActionRebindingExtensions.RebindingOperation operation)
        {
            if (operation == null) return;
            foreach (var path in suppressedPaths) operation.WithControlsExcluding(path);
        }

        internal void EndPartObservation() => StopObservation();

        internal void ResetSession()
        {
            StopObservation();
            suppressedPaths.Clear();
        }

        private void RecordChangedControl(InputControl control)
        {
            if (control != null && !string.IsNullOrWhiteSpace(control.path))
                suppressedPaths.Add(control.path);
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

            foreach (var control in eventPtr.EnumerateChangedControls(device, ActuationThreshold))
                RecordChangedControl(control);
        }

        private void StopObservation()
        {
            InputSystem.onEvent -= OnInputEvent;
        }

        public void Dispose() => ResetSession();
    }
}
