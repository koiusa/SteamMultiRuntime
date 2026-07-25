namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public partial struct WallSlideTraversalSettings
    {
        public float WallMaxUpDot;
        public float WallSlideGravityMultiplier;
        public float WallSlideMaxFallSpeed;
        public float WallSlideMinDownSpeed;
        public int WallSlideStartContactFrames;
        public float WallSlideExitMoveOppositeNormalDot;
        public float WallSlideAwayFromWallMinSpeed;

        public static WallSlideTraversalSettings CreateDefault()
        {
            return new WallSlideTraversalSettings
            {
                WallMaxUpDot = 0.2f,
                WallSlideGravityMultiplier = 0.5f,
                WallSlideMaxFallSpeed = 3f,
                WallSlideMinDownSpeed = 1.5f,
                WallSlideStartContactFrames = 2,
                WallSlideExitMoveOppositeNormalDot = 0.3f,
                WallSlideAwayFromWallMinSpeed = 0.15f,
            };
        }

    }
}
