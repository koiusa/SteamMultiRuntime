namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public struct LadderTraversalSettings
    {
        /// <summary>梯子昇降速度（Units/秒）。</summary>
        public float ClimbSpeed;

        /// <summary>梯子昇降加速度。</summary>
        public float ClimbAcceleration;

        /// <summary>梯子の上端に到達したときに与える射出速度。</summary>
        public float ExitTopBoostSpeed;

        public static LadderTraversalSettings CreateDefault()
        {
            return new LadderTraversalSettings
            {
                ClimbSpeed = 4f,
                ClimbAcceleration = 20f,
                ExitTopBoostSpeed = 2f,
            };
        }
    }
}
