namespace Koiusa.SteamMultiRuntime
{
    public enum WallRunVerticalMotionMode
    {
        Arc = 0,
        MaintainHeight = 1,
        Gravity = 2,
    }

    [System.Serializable]
    public partial struct WallRunTraversalSettings
    {
        public float WallRunSpeed;
        public float WallRunAcceleration;
        public float WallRunGravityMultiplier;
        public float WallRunMaxFallSpeed;
        public float WallRunMinInputDot;
        public float WallMaxUpDot;
        public int WallRunStartContactFrames;
        public float WallRunAwayFromWallMinSpeed;
        public float WallRunInputReleaseGraceTime;
        public WallRunVerticalMotionMode VerticalMotionMode;
        public float HeightHoldAcceleration;
        public float ArcInitialUpSpeed;
        public float ArcGravityMultiplier;

        public static WallRunTraversalSettings CreateDefault()
        {
            return new WallRunTraversalSettings
            {
                WallRunSpeed = 7f,
                WallRunAcceleration = 20f,
                WallRunGravityMultiplier = 0.35f,
                WallRunMaxFallSpeed = 2f,
                WallRunMinInputDot = 0.15f,
                WallMaxUpDot = 0.2f,
                WallRunStartContactFrames = 2,
                WallRunAwayFromWallMinSpeed = 0.15f,
                WallRunInputReleaseGraceTime = 0.2f,
                VerticalMotionMode = WallRunVerticalMotionMode.Arc,
                HeightHoldAcceleration = 12f,
                ArcInitialUpSpeed = 1.5f,
                ArcGravityMultiplier = 0.45f,
            };
        }
    }
}
