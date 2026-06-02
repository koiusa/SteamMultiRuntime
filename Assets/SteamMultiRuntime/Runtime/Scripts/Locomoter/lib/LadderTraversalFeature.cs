using System.Collections.Generic;
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
        private readonly HashSet<LadderVolume> activeLadders = new HashSet<LadderVolume>();
        private float reattachBlockedUntilTime;
        private ITraversalIntentContext traversalIntentContext;

        public bool IsEnabled => isActiveAndEnabled;
        public bool IsOnLadder => isActiveAndEnabled && currentLadder != null;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            traversalIntentContext = GetComponent<ITraversalIntentContext>();

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
            activeLadders.Clear();
            rb.useGravity = true;
            reattachBlockedUntilTime = 0f;
        }

        public void DetachFromLadder(float reattachDelaySeconds)
        {
            currentLadder = null;
            activeLadders.Clear();
            rb.useGravity = true;
            reattachBlockedUntilTime = Time.time + Mathf.Max(0f, reattachDelaySeconds);
        }

        public void NotifyEnterLadder(LadderVolume ladder)
        {
            if (Time.time < reattachBlockedUntilTime)
            {
                return;
            }

            activeLadders.Add(ladder);
            currentLadder = ladder;
        }

        public void NotifyExitLadder(LadderVolume ladder)
        {
            activeLadders.Remove(ladder);

            if (activeLadders.Count == 0)
            {
                currentLadder = null;
                rb.useGravity = true;

                // 本当に梯子コライダー外へ出た時だけ再捕捉ブロックを解除する
                // （ジャンプ離脱直後の即再捕捉は維持）
                reattachBlockedUntilTime = 0f;
            }
            else
            {
                // 別の梯子がまだ重なっている場合は最後に追加されたものを使う
                foreach (var remaining in activeLadders)
                {
                    currentLadder = remaining;
                }
            }
        }

        public bool TryApplyLadderMovement(Vector3 velocity, float climbInput, Vector3 upAxis, out Vector3 nextVelocity)
        {
            nextVelocity = velocity;

            if (currentLadder == null)
            {
                rb.useGravity = true;
                return false;
            }

            rb.useGravity = false;

            var ladderUp = currentLadder.UpDirection;

            // 生の前後入力（moveInput.y）を梯子の上方向に直接マッピングする
            var targetClimbVelocity = ladderUp * (climbInput * settings.ClimbSpeed);

            // 水平方向の速度はゼロに収束させる（梯子に吸い付く）
            var horizontalVelocity = Vector3.ProjectOnPlane(velocity, ladderUp);
            var nextHorizontal = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, settings.ClimbAcceleration * Time.fixedDeltaTime);

            // 上下速度を加速させる
            var currentClimbVelocity = Vector3.Project(velocity, ladderUp);
            var nextClimbVelocity = Vector3.MoveTowards(currentClimbVelocity, targetClimbVelocity, settings.ClimbAcceleration * Time.fixedDeltaTime);

            nextVelocity = nextHorizontal + nextClimbVelocity;
            return true;
        }

        public bool TryHandleTraversal(Vector3 velocity, Vector2 moveInput, bool jumpRequested, bool isGrounded, Vector3 upAxis, out Vector3 nextVelocity, out bool detachedByJump)
        {
            nextVelocity = velocity;
            detachedByJump = false;

            if (!IsOnLadder)
            {
                return false;
            }

            var directionalDetachDelay = Mathf.Max(0f, settings.DirectionalDetachReattachDelay);
            var jumpDetachDelay = Mathf.Max(0f, settings.JumpDetachReattachDelay);

            var hasIntentContext = traversalIntentContext != null;
            var wantsJumpDetach = hasIntentContext
                ? traversalIntentContext.HasIntent(TraversalIntentFlags.JumpRequested)
                : jumpRequested;
            var wantsLateralDetach = hasIntentContext
                ? traversalIntentContext.HasIntent(TraversalIntentFlags.WantsLadderDetachByLateral)
                : Mathf.Abs(moveInput.x) > 0.2f;
            var wantsGroundDescendDetach = hasIntentContext
                ? traversalIntentContext.HasIntent(TraversalIntentFlags.WantsLadderDetachByDescendOnGround)
                : (isGrounded && moveInput.y < -0.01f);
            var wantsGroundIdleDetach = hasIntentContext
                ? traversalIntentContext.HasIntent(TraversalIntentFlags.WantsLadderIdleOnGround)
                : (isGrounded && Mathf.Abs(moveInput.y) <= 0.01f);

            if (wantsJumpDetach)
            {
                DetachFromLadder(jumpDetachDelay);
                detachedByJump = true;
                return true;
            }

            if (wantsLateralDetach)
            {
                DetachFromLadder(directionalDetachDelay);
                return true;
            }

            if (wantsGroundDescendDetach)
            {
                DetachFromLadder(directionalDetachDelay);
                return true;
            }

            if (wantsGroundIdleDetach)
            {
                ResetState();
                return true;
            }

            if (TryApplyLadderMovement(velocity, moveInput.y, upAxis, out var ladderVelocity))
            {
                nextVelocity = ladderVelocity;
                return true;
            }

            return false;
        }

        private static bool IsSettingsEmpty(LadderTraversalSettings s)
        {
            return s.ClimbSpeed == 0f && s.ClimbAcceleration == 0f;
        }
    }
}
