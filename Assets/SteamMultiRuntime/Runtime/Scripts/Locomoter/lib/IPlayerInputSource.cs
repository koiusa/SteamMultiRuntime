namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerInputSource
    {
        void Enable();
        void Disable();
        PlayerInputState ReadState();
    }
}
