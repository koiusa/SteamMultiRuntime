namespace Koiusa.TargetingSystem.Runtime
{
    public enum TargetingCommandType
    {
        EnterSingle,
        EnterMulti,
        Clear,
        SelectNext,
        SelectPrevious,
        AddBestCandidate,
        ToggleBestCandidate,
        RemovePrimary,
        SelectAllCandidates,
    }

    public readonly struct TargetingCommand
    {
        public TargetingCommand(TargetingCommandType type)
        {
            Type = type;
        }

        public TargetingCommandType Type { get; }
    }

    public readonly struct TargetingResult
    {
        public TargetingResult(bool changed, TargetingState state)
        {
            Changed = changed;
            State = state;
        }

        public bool Changed { get; }
        public TargetingState State { get; }
    }
}
