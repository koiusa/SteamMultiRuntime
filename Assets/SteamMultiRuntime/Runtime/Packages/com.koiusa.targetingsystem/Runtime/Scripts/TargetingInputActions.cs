using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    public abstract class TargetingInputActions : ScriptableObject
    {
        public abstract InputAction LookAction { get; }
        public abstract InputAction SoloLockAction { get; }
        public abstract InputAction MultiLockAction { get; }
        public abstract InputAction ClearLockAction { get; }
        public abstract InputAction BulkLockAction { get; }
        public abstract InputAction PreviousTargetAction { get; }
        public abstract InputAction NextTargetAction { get; }
        public abstract InputAction FocusAction { get; }
    }
}
