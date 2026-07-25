namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerLadderState
    {
        bool IsOnLadder { get; }
        float LadderSpeed { get; }
    }

    public interface IPlayerWallRunState
    {
        bool IsWallRunning { get; }
        UnityEngine.Vector3 WallNormal { get; }
    }
}
