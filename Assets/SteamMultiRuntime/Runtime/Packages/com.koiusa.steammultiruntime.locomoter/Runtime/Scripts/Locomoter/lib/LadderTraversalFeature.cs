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
        private ILadderClimbAction climbAction;

        private float ladderEnteredTime;
        private bool hasResolvedGroundEntry;
        private bool enteredLadderFromGround;
        private bool groundEntryHadMoveInput;
        private bool releasedGroundEntryInput;

        private Vector3 facingDirection;
        private bool hasFacing;

        public bool IsEnabled => isActiveAndEnabled;
        public bool IsOnLadder => isActiveAndEnabled && currentLadder != null;
        public float ClimbSpeed => IsOnLadder && rb != null
            ? Vector3.Dot(rb.linearVelocity, currentLadder.UpDirection)
            : 0f;
        public float WallTraversalBlockDuration => settings.WallTraversalBlockDuration > 0f
            ? settings.WallTraversalBlockDuration
            : LadderTraversalSettings.CreateDefault().WallTraversalBlockDuration;

        internal LadderTraversalDebugSnapshot GetDebugSnapshot() => new LadderTraversalDebugSnapshot(
            currentLadder,
            activeLadders.Count,
            Mathf.Max(0f, reattachBlockedUntilTime - Time.time),
            rb != null && rb.useGravity,
            hasFacing ? facingDirection : Vector3.zero);

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            traversalIntentContext = GetComponent<ITraversalIntentContext>();
            climbAction = GetComponent<ILadderClimbAction>();

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
            if (rb != null) rb.useGravity = true;
            reattachBlockedUntilTime = 0f;
            hasResolvedGroundEntry = false;
            enteredLadderFromGround = false;
            groundEntryHadMoveInput = false;
            releasedGroundEntryInput = false;
            hasFacing = false;
        }

        public void DetachFromLadder(float reattachDelaySeconds)
        {
            currentLadder = null;
            activeLadders.Clear();
            if (rb != null) rb.useGravity = true;
            reattachBlockedUntilTime = Time.time + Mathf.Max(0f, reattachDelaySeconds);
            hasResolvedGroundEntry = false;
            enteredLadderFromGround = false;
            groundEntryHadMoveInput = false;
            releasedGroundEntryInput = false;
            hasFacing = false;
        }

        public void NotifyEnterLadder(LadderVolume ladder)
        {
            if (Time.time < reattachBlockedUntilTime)
            {
                return;
            }

            if (currentLadder != ladder)
            {
                hasResolvedGroundEntry = false;
                enteredLadderFromGround = false;
                groundEntryHadMoveInput = false;
                releasedGroundEntryInput = false;
            }

            activeLadders.Add(ladder);
            currentLadder = ladder;
            ladderEnteredTime = Time.time;
            UpdateFacingDirection(ladder);
        }

        public void NotifyExitLadder(LadderVolume ladder)
        {
            activeLadders.Remove(ladder);

            if (activeLadders.Count == 0)
            {
                currentLadder = null;
                if (rb != null) rb.useGravity = true;

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
                hasResolvedGroundEntry = false;
                enteredLadderFromGround = false;
                groundEntryHadMoveInput = false;
                releasedGroundEntryInput = false;
                UpdateFacingDirection(currentLadder);
            }
        }

        internal bool TryApplyLadderMovement(Vector3 velocity, float climbInput, Vector3 upAxis, out Vector3 nextVelocity)
        {
            return TryApplyLadderMovement(velocity, climbInput, 0f, upAxis, out nextVelocity);
        }

        private bool TryApplyLadderMovement(Vector3 velocity, float climbInput, float lateralInput, Vector3 upAxis, out Vector3 nextVelocity)
        {
            nextVelocity = velocity;

            if (currentLadder == null)
            {
                rb.useGravity = true;
                return false;
            }

            rb.useGravity = false;

            var ladderUp = currentLadder.UpDirection;
            var ladderRight = currentLadder.RightDirection.normalized;

            // 生の前後入力（moveInput.y）を梯子の上方向に直接マッピングする
            var targetClimbVelocity = ladderUp * (climbInput * settings.ClimbSpeed);

            var defaults = LadderTraversalSettings.CreateDefault();
            var lateralSpeed = settings.LateralMoveSpeed > 0f ? settings.LateralMoveSpeed : defaults.LateralMoveSpeed;
            var lateralAcceleration = settings.LateralMoveAcceleration > 0f ? settings.LateralMoveAcceleration : defaults.LateralMoveAcceleration;
            var currentLateralVelocity = Vector3.Project(velocity, ladderRight);
            var targetLateralVelocity = ladderRight * (lateralInput * lateralSpeed);
            var nextLateralVelocity = Vector3.MoveTowards(currentLateralVelocity, targetLateralVelocity, lateralAcceleration * Time.fixedDeltaTime);

            // 梯子面に対する前後速度はゼロに収束させ、面に吸い付かせる。
            var normalVelocity = velocity - Vector3.Project(velocity, ladderUp) - currentLateralVelocity;
            var nextNormalVelocity = Vector3.MoveTowards(normalVelocity, Vector3.zero, settings.ClimbAcceleration * Time.fixedDeltaTime);

            // 上下速度を加速させる
            var currentClimbVelocity = Vector3.Project(velocity, ladderUp);
            var nextClimbVelocity = Vector3.MoveTowards(currentClimbVelocity, targetClimbVelocity, settings.ClimbAcceleration * Time.fixedDeltaTime);

            nextVelocity = nextNormalVelocity + nextLateralVelocity + nextClimbVelocity;

            // 梯子昇降中は常に梯子の方を向く
            ApplyFacingRotation(upAxis);
            return true;
        }

        internal bool TryHandleTraversal(Vector3 velocity, Vector2 moveInput, Quaternion moveReferenceRotation, bool jumpRequested, bool isGrounded, Vector3 upAxis, out Vector3 nextVelocity, out bool detachedByJump)
        {
            nextVelocity = velocity;
            detachedByJump = false;

            if (!IsOnLadder)
            {
                return false;
            }

            var directionalDetachDelay = Mathf.Max(0f, settings.DirectionalDetachReattachDelay);
            var jumpDetachDelay = Mathf.Max(0f, settings.JumpDetachReattachDelay);

            var wantsJumpDetach = traversalIntentContext != null
                ? traversalIntentContext.HasIntent(TraversalIntentFlags.JumpRequested)
                : jumpRequested;

            var climbInput = ResolveClimbInput(moveInput, moveReferenceRotation, out var detachInput);
            var defaults = LadderTraversalSettings.CreateDefault();
            var groundEnterGrace = settings.GroundEnterDetachGraceTime > 0f
                ? settings.GroundEnterDetachGraceTime
                : defaults.GroundEnterDetachGraceTime;
            var lateralDetachThreshold = settings.LateralDetachInputThreshold > 0f
                ? settings.LateralDetachInputThreshold
                : defaults.LateralDetachInputThreshold;
            if (!hasResolvedGroundEntry)
            {
                enteredLadderFromGround = isGrounded;
                hasResolvedGroundEntry = true;
            }

            var isJustEnteredFromGround = enteredLadderFromGround
                && (Time.time - ladderEnteredTime) <= groundEnterGrace;

            if (enteredLadderFromGround && !releasedGroundEntryInput)
            {
                if (moveInput.sqrMagnitude > 0.0001f)
                {
                    groundEntryHadMoveInput = true;
                }
                else if (groundEntryHadMoveInput)
                {
                    releasedGroundEntryInput = true;
                }
            }

            var restrictLateralUntilInputRelease = enteredLadderFromGround && !releasedGroundEntryInput;

            var lateralMode = currentLadder.LateralMovementMode;
            // 地上から幅広梯子へ入った際は、接近時の入力を一度離すまで横移動へ引き継がない。
            var hasLateralInput = !restrictLateralUntilInputRelease && Mathf.Abs(detachInput) > lateralDetachThreshold;
            var wantsLateralDetach = lateralMode == LadderLateralMovementMode.Detach && hasLateralInput;
            var lateralMoveInput = lateralMode == LadderLateralMovementMode.MoveWithinBounds && !restrictLateralUntilInputRelease
                ? detachInput
                : 0f;
            var edgePadding = settings.LateralEdgePadding > 0f
                ? settings.LateralEdgePadding
                : defaults.LateralEdgePadding;
            var wantsEdgeDetach = lateralMode == LadderLateralMovementMode.MoveWithinBounds
                && hasLateralInput
                && currentLadder.IsAtLateralEdge(rb.position, lateralMoveInput, edgePadding);
            var wantsGroundDescendDetach = isGrounded && climbInput < -0.01f;
            var wantsGroundIdleDetach = isGrounded
                && moveInput.sqrMagnitude <= 0.0001f
                && !isJustEnteredFromGround;

            if (wantsJumpDetach)
            {
                DetachFromLadder(jumpDetachDelay);
                detachedByJump = true;
                return true;
            }

            if (wantsLateralDetach || wantsEdgeDetach)
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

            if (climbAction != null && climbAction.IsEnabled
                && Mathf.Abs(lateralMoveInput) <= 0.0001f
                && climbAction.TryApplyMovement(velocity, climbInput, upAxis, out var ladderVelocity))
            {
                nextVelocity = ladderVelocity;
                return true;
            }

            // Wide ladders also need lateral input, which remains an internal overload
            // while the public climb contract stays independent of LadderVolume details.
            if (TryApplyLadderMovement(velocity, climbInput, lateralMoveInput, upAxis, out var lateralLadderVelocity))
            {
                nextVelocity = lateralLadderVelocity;
                return true;
            }

            return false;
        }

        private float ResolveClimbInput(Vector2 moveInput, Quaternion moveReferenceRotation, out float detachInput)
        {
            var screenRight = moveReferenceRotation * Vector3.right;
            var ladderRight = currentLadder != null ? currentLadder.RightDirection.normalized : Vector3.right;

            // Ladderモード中はカメラ角度や入力の離し直しに関係なく、上下入力を昇降専用にする。
            var lateralSign = Vector3.Dot(ladderRight, screenRight);
            lateralSign = Mathf.Abs(lateralSign) > 0.05f ? Mathf.Sign(lateralSign) : 1f;
            detachInput = moveInput.x * lateralSign;
            return moveInput.y;
        }

        private void UpdateFacingDirection(LadderVolume ladder)
        {
            hasFacing = false;

            if (ladder == null || rb == null)
            {
                return;
            }

            var up = GetUpAxis();

            // 梯子面の法線を主基準にして常に面へ垂直に正対させる。
            // 侵入位置（梯子コライダーのどこで触れたか）のブレに影響されず、
            // 同じ梯子なら常に同じ向きで張り付くようにする。
            var normal = Vector3.ProjectOnPlane(ladder.PlaneNormal, up);
            if (normal.sqrMagnitude > 0.0001f)
            {
                normal.Normalize();

                // プレイヤーが梯子のどちら側にいるかで符号を決め、梯子面へ向かう向きを採用する。
                var toLadder = Vector3.ProjectOnPlane(ladder.transform.position - rb.position, up);
                float sign;
                if (toLadder.sqrMagnitude > 0.0001f)
                {
                    sign = Vector3.Dot(normal, toLadder) >= 0f ? 1f : -1f;
                }
                else
                {
                    // 梯子軸の真上／真下にいる場合は現在の前方に近い符号を維持する。
                    var forward = rb.rotation * Vector3.forward;
                    sign = Vector3.Dot(normal, forward) >= 0f ? 1f : -1f;
                }

                facingDirection = normal * sign;
                hasFacing = true;
                return;
            }

            // 梯子面の法線が水平面上で取得できない異常時のみ、梯子中心へ向かう方向へフォールバックする。
            var fallback = Vector3.ProjectOnPlane(ladder.transform.position - rb.position, up);
            if (fallback.sqrMagnitude > 0.0001f)
            {
                facingDirection = fallback.normalized;
                hasFacing = true;
            }
        }

        private void ApplyFacingRotation(Vector3 upAxis)
        {
            if (!hasFacing || rb == null)
            {
                return;
            }

            var up = upAxis.sqrMagnitude > 0.0001f ? upAxis.normalized : GetUpAxis();
            var facing = Vector3.ProjectOnPlane(facingDirection, up);
            if (facing.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(facing.normalized, up);
            var rotationSpeed = settings.FacingRotationSpeed > 0f
                ? settings.FacingRotationSpeed
                : LadderTraversalSettings.CreateDefault().FacingRotationSpeed;
            var nextRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(nextRotation);
        }

        private static Vector3 GetUpAxis()
        {
            return Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        }

        private static bool IsSettingsEmpty(LadderTraversalSettings s)
        {
            return s.ClimbSpeed == 0f && s.ClimbAcceleration == 0f;
        }
    }
}
