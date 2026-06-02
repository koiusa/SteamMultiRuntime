namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public partial struct TraversalFeatureSettings
    {
        public WallRunTraversalSettings WallRun;
        public WallJumpTraversalSettings WallJump;
        public WallSlideTraversalSettings WallSlide;
        public LadderTraversalSettings Ladder;

        public static TraversalFeatureSettings CreateDefault()
        {
            return new TraversalFeatureSettings
            {
                WallRun = WallRunTraversalSettings.CreateDefault(),
                WallJump = WallJumpTraversalSettings.CreateDefault(),
                WallSlide = WallSlideTraversalSettings.CreateDefault(),
                Ladder = LadderTraversalSettings.CreateDefault(),
            };
        }

            }
        }
