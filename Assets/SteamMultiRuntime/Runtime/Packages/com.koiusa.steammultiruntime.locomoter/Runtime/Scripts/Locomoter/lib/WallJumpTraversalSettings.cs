namespace Koiusa.SteamMultiRuntime
{
    public enum WallJumpTrajectoryMode
    {
        Snappy = 0,
        Arc = 1,
    }

    [System.Serializable]
    public partial struct WallJumpTraversalSettings
    {
        public float WallMaxUpDot;
        public float WallJumpUpForce;
        public float WallJumpAwayForce;
        public float TriangleKickForwardForce;
        public WallJumpTrajectoryMode WallJumpTrajectoryMode;
        public float SameWallKickLockDuration;
        public float SameWallNormalDotThreshold;

        public static WallJumpTraversalSettings CreateDefault()
        {
            return new WallJumpTraversalSettings
            {
                WallMaxUpDot = 0.2f,
                WallJumpUpForce = 6.5f,
                WallJumpAwayForce = 5f,
                TriangleKickForwardForce = 3f,
                WallJumpTrajectoryMode = WallJumpTrajectoryMode.Snappy,
                SameWallKickLockDuration = 0.2f,
                SameWallNormalDotThreshold = 0.97f,
            };
        }

    }
}
