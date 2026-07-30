namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Read-only state produced by NPC navigation and its active motor.
    /// This contract deliberately does not identify the component as the
    /// authoritative player controller on networked NPCs.
    /// </summary>
    public interface INpcLocomotionState : IActorLocomotionState
    {
        bool HasPath { get; }
        bool IsMoving { get; }
    }
}
