namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerLadderState
    {
        bool IsOnLadder { get; }
        float LadderSpeed { get; }
    }
}
