namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Push-based ownership state for consumers that must react to runtime transitions.</summary>
    public interface ILocalPlayerOwnershipNotifier : ILocalPlayerOwnership
    {
        event System.Action OwnershipChanged;
    }
}
