using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// 梯子昇降を処理する TraversalFeature。
    /// LadderVolume が Trigger 通知を行い、このコンポーネントが速度制御を担う。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class LadderTraversalFeature : MonoBehaviour, ILadderTraversalFeature
    {
        [SerializeField] private LadderTraversalSettings settings;

        private Rigidbody rb;
        private LadderVolume currentLadder;
        private int ladderOverlapCount;

        public bool IsEnabled => isActiveAndEnabled;
        public bool IsOnLadder => isActiveAndEnabled && currentLadder != null;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            if (rb == null)
            {
                Debug.LogError("LadderTraversalFeature requires a Rigidbody component.", this);
                enabled = false;
                return;
            }

            if (IsSettingsEmpty(settings))
            {
                settings = LadderTraversalSettings.CreateDefault();
            }
        }

        private void OnValidate()
        {
            if (IsSettingsEmpty(settings))
            {
                settings = LadderTraversalSettings.CreateDefault();
            }
        }

        // ── ILadderTraversalFeature ────────────────────────────────────────

        public void ResetState()
        {
            currentLadder = null;
            ladderOverlapCount = 0;
            rb.useGravity = true;
        }

        public void NotifyEnterLadder(LadderVolume ladder)
        {
            ladderOverlapCount++;
            currentLadder = ladder;
        }

        public void NotifyExitLadder(LadderVolume ladder)
        {
            ladderOverlapCount = Mathf.Max(0, ladderOverlapCount - 1);

            if (ladderOverlapCount == 0)
            {
                currentLadder = null;
                rb.useGravity = true;
            }
        }

        public bool TryApplyLadderMovement(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, out Vector3 nextVelocity)
        {
            nextVelocity = velocity;

            if (currentLadder == null)
            {
                rb.useGravity = true;
                return false;
            }

            rb.useGravity = false;

            var ladderUp = currentLadder.UpDirection;

            // 入力の上下成分をそのまま梯子の上方向に変換
            var verticalInput = Vector3.Dot(moveDirection, upAxis);
            var targetClimbVelocity = ladderUp * (verticalInput * settings.ClimbSpeed);

            // 水平方向の速度はゼロに収束させる（梯子に吸い付く）
            var horizontalVelocity = Vector3.ProjectOnPlane(velocity, ladderUp);
            var nextHorizontal = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, settings.ClimbAcceleration * Time.fixedDeltaTime);

            // 上下速度を加速させる
            var currentClimbVelocity = Vector3.Project(velocity, ladderUp);
            var nextClimbVelocity = Vector3.MoveTowards(currentClimbVelocity, targetClimbVelocity, settings.ClimbAcceleration * Time.fixedDeltaTime);

            nextVelocity = nextHorizontal + nextClimbVelocity;
            return true;
        }

        private static bool IsSettingsEmpty(LadderTraversalSettings s)
        {
            return s.ClimbSpeed == 0f && s.ClimbAcceleration == 0f;
        }
    }
}
