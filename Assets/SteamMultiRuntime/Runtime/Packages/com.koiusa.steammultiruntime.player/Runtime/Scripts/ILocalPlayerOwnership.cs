namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Typed ownership state consumed by local-only presentation systems.</summary>
    public interface ILocalPlayerOwnership
    {
        bool IsOwnershipResolved { get; }
        bool IsLocalOwner { get; }
    }
}
