namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public partial struct WallRunTraversalSettings
    {
        public float WallRunSpeed;
        public float WallRunAcceleration;
        public float WallRunGravityMultiplier;
        public float WallRunMaxFallSpeed;
        public float WallRunMinInputDot;
        public float WallRunMinAlongWallSpeed;
        public float WallRunMaxUpwardStartSpeed;
        public float WallMaxUpDot;
        public int WallRunStartContactFrames;
        public float WallRunAwayFromWallMinSpeed;
        public float WallRunInputReleaseGraceTime;

        public static WallRunTraversalSettings CreateDefault()
        {
            return new WallRunTraversalSettings
            {
                WallRunSpeed = 7f,
                WallRunAcceleration = 20f,
                WallRunGravityMultiplier = 0.35f,
                WallRunMaxFallSpeed = 2f,
                WallRunMinInputDot = 0.15f,
                WallRunMinAlongWallSpeed = 2f,
                WallRunMaxUpwardStartSpeed = 0f,
                WallMaxUpDot = 0.2f,
                WallRunStartContactFrames = 2,
                WallRunAwayFromWallMinSpeed = 0.15f,
                WallRunInputReleaseGraceTime = 0.2f,
            };
        }

            }
        }
