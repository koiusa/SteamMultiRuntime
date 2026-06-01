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

        /// <summary>意図的離脱後の再捕捉抑制秒数（横入力・下降離脱用）。</summary>
        public float DirectionalDetachReattachDelay;

        /// <summary>ジャンプ離脱後の再捕捉抑制秒数。</summary>
        public float JumpDetachReattachDelay;

        public static LadderTraversalSettings CreateDefault()
        {
            return new LadderTraversalSettings
            {
                ClimbSpeed = 4f,
                ClimbAcceleration = 20f,
                ExitTopBoostSpeed = 2f,
                DirectionalDetachReattachDelay = 0.15f,
                JumpDetachReattachDelay = 0.12f,
            };
        }
    }
}
