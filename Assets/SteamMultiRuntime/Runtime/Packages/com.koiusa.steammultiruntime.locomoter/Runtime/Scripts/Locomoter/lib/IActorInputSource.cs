namespace Koiusa.SteamMultiRuntime
{
    public interface IActorInputSource
    {
        void Enable();
        void Disable();
        ActorInputState ReadState();
    }
}
