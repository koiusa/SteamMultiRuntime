using System;
using System.Collections.Generic;

namespace Koiusa.TargetingSystem.Runtime
{
    public enum TargetingMode
    {
        None,
        Single,
        Multi,
    }

    public enum TargetingChangeReason
    {
        Command,
        TargetInvalidated,
        ControllerDisabled,
    }

    public readonly struct TargetingState
    {
        private static readonly ITargetable[] EmptyTargets = Array.Empty<ITargetable>();

        public TargetingState(
            TargetingMode mode,
            ITargetable primaryTarget,
            IReadOnlyList<ITargetable> selectedTargets,
            uint revision)
        {
            Mode = mode;
            PrimaryTarget = primaryTarget;
            SelectedTargets = selectedTargets ?? EmptyTargets;
            Revision = revision;
        }

        public TargetingMode Mode { get; }
        public ITargetable PrimaryTarget { get; }
        public IReadOnlyList<ITargetable> SelectedTargets { get; }
        public uint Revision { get; }

        public static TargetingState Empty => new(TargetingMode.None, null, EmptyTargets, 0);
    }

    public readonly struct TargetingStateChange
    {
        public TargetingStateChange(TargetingState previous, TargetingState current, TargetingChangeReason reason)
        {
            Previous = previous;
            Current = current;
            Reason = reason;
        }

        public TargetingState Previous { get; }
        public TargetingState Current { get; }
        public TargetingChangeReason Reason { get; }
    }
}
