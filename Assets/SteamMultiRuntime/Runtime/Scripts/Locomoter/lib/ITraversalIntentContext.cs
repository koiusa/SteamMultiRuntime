namespace Koiusa.SteamMultiRuntime
{
    [System.Flags]
    public enum TraversalIntentFlags
    {
        None = 0,
        JumpRequested = 1 << 0,
        WantsLadderDetachByLateral = 1 << 1,
        WantsLadderDetachByDescendOnGround = 1 << 2,
        WantsLadderIdleOnGround = 1 << 3,
    }

    public interface ITraversalIntentContext
    {
        TraversalIntentFlags CurrentIntentFlags { get; }
        bool HasIntent(TraversalIntentFlags flag);
    }
}
