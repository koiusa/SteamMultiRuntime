namespace Koiusa.SteamMultiRuntime
{
    public interface IActorLadderState
    {
        bool IsOnLadder { get; }
        float LadderSpeed { get; }
    }

    public interface IActorWallRunState
    {
        bool IsWallRunning { get; }
        UnityEngine.Vector3 WallNormal { get; }
    }
}
