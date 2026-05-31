using System;
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// Switches cameras based on lock-on state (NoLock, SoloLock, MultiLock).
    /// Uses a CinemachineMixingCamera as the parent and controls child weights.
    /// Call SetMode() from input to switch cameras.
    /// </summary>
    [DisallowMultipleComponent]
    public class TargetingCameraRig : MonoBehaviour
    {
        public enum CameraMode
        {
            NoLock,
            SoloLock,
            MultiLock,
        }

        /// <summary>Fired whenever the active camera mode changes.</summary>
        public event Action<CameraMode> OnModeChanged;

        [Header("Mixing Camera")]
        [SerializeField] private CinemachineMixingCamera mixingCamera;

        [Header("VCams")]
        [SerializeField] private CinemachineCamera noLockVCam;
        [SerializeField] private CinemachineCamera soloLockVCam;
        [SerializeField] private CinemachineCamera multiLockVCam;

        [Header("Smoothing")]
        [SerializeField, Min(0f)] private float switchSmoothTime = 0.25f;

        private ILockOnTargetBinder targetGroupBinder;
        private ITargetBinder soloLockBinder;
        private ITransitionGuard soloLockGuard;
        private ITransitionGuard multiLockGuard;
        private CinemachineBrain cinemachineBrain;

        public CameraMode CurrentMode { get; private set; } = CameraMode.NoLock;

        private Coroutine switchRoutine;

        private void Awake()
        {
            if (multiLockVCam != null)
            {
                targetGroupBinder = multiLockVCam.GetComponent<ILockOnTargetBinder>();
                multiLockGuard = multiLockVCam.GetComponent<ITransitionGuard>();
            }

            if (soloLockVCam != null)
            {
                soloLockGuard = soloLockVCam.GetComponent<ITransitionGuard>();
                soloLockBinder = soloLockVCam.GetComponent<ITargetBinder>();
            }

            if (Camera.main != null)
            {
                cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
            }
        }

        /// <summary>
        /// 指定したカメラモードへ遷移可能かどうかを返す。
        /// 対応するBinder に ITransitionGuard が存在しない場合は常に遷移可能とみなす。
        /// </summary>
        public bool CanTransitionTo(CameraMode mode)
        {
            return mode switch
            {
                CameraMode.SoloLock => soloLockGuard == null || soloLockGuard.CanTransition(),
                CameraMode.MultiLock => multiLockGuard == null || multiLockGuard.CanTransition(),
                _ => true,
            };
        }

        private void OnEnable()
        {
            ApplyMode(CurrentMode, instant: true);

            if (targetGroupBinder != null)
            {
                targetGroupBinder.AllLockedTargetsCleared += OnAllLockedTargetsCleared;
            }
        }

        private void OnDisable()
        {
            if (targetGroupBinder != null)
            {
                targetGroupBinder.AllLockedTargetsCleared -= OnAllLockedTargetsCleared;
            }

            if (switchRoutine != null)
            {
                StopCoroutine(switchRoutine);
                switchRoutine = null;
            }

            SetWeight(noLockVCam, 0f);
            SetWeight(soloLockVCam, 0f);
            SetWeight(multiLockVCam, 0f);
        }

        public void SetMode(CameraMode mode)
        {
            if (CurrentMode == mode)
            {
                return;
            }

            CurrentMode = mode;

            if (mode == CameraMode.MultiLock && targetGroupBinder != null)
            {
                // SoloLock の現在ターゲットを先に保持（ClearLookAt でリセットされる前に）
                var soloTarget = soloLockBinder?.CurrentTarget;

                // ロック済みが0件の場合、SoloLock の現在ターゲットを引き継ぐか最近傍を自動ロック
                if (targetGroupBinder.LockedTargets.Count == 0)
                {
                    if (soloTarget != null)
                    {
                        targetGroupBinder.LockTarget(soloTarget);
                    }
                    else
                    {
                        targetGroupBinder.LockClosestVisibleTarget();
                    }
                }
                // SoloLock VCam はブレンド中もウェイトが残るためここではクリアしない
                // ブレンド完了後にウェイト0になってから自然に無効化される
            }
            else if (mode == CameraMode.SoloLock)
            {
                // MultiLock → SoloLock: ブレンド前に TargetGroup を初期化
                targetGroupBinder?.ClearLookAt();
            }
            else if (mode == CameraMode.NoLock)
            {
                // * → NoLock: ブレンド前に両 Binder を初期化
                soloLockBinder?.ClearLookAt();
                targetGroupBinder?.ClearLookAt();
            }

            if (mode == CameraMode.SoloLock)
            {
                soloLockBinder?.SelectNext();
            }

            ApplyMode(mode);
            OnModeChanged?.Invoke(mode);
        }

        public void SetNoLock()
        {
            SetMode(CameraMode.NoLock);
        }

        private void OnAllLockedTargetsCleared()
        {
            SetMode(CameraMode.NoLock);
        }

        private void ApplyMode(CameraMode mode, bool instant = false)
        {
            if (switchRoutine != null)
            {
                StopCoroutine(switchRoutine);
                switchRoutine = null;
            }

            // NoLock に戻る際、noLockVCam を現在のカメラ位置・向きにスナップしてからブレンド開始（暴れ防止）
            if (mode == CameraMode.NoLock && !instant && noLockVCam != null && cinemachineBrain != null)
            {
                var outputCam = cinemachineBrain.OutputCamera;
                if (outputCam != null)
                {
                    noLockVCam.ForceCameraPosition(outputCam.transform.position, outputCam.transform.rotation);
                }
            }

            if (instant || switchSmoothTime <= 0f)
            {
                SetWeight(noLockVCam, mode == CameraMode.NoLock ? 1f : 0f);
                SetWeight(soloLockVCam, mode == CameraMode.SoloLock ? 1f : 0f);
                SetWeight(multiLockVCam, mode == CameraMode.MultiLock ? 1f : 0f);
                return;
            }

            switchRoutine = StartCoroutine(BlendWeights(mode, switchSmoothTime));
        }

        private IEnumerator BlendWeights(CameraMode mode, float duration)
        {
            var startNoLock = GetWeight(noLockVCam);
            var startSoloLock = GetWeight(soloLockVCam);
            var startMultiLock = GetWeight(multiLockVCam);

            var targetNoLock = mode == CameraMode.NoLock ? 1f : 0f;
            var targetSoloLock = mode == CameraMode.SoloLock ? 1f : 0f;
            var targetMultiLock = mode == CameraMode.MultiLock ? 1f : 0f;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetWeight(noLockVCam, Mathf.Lerp(startNoLock, targetNoLock, t));
                SetWeight(soloLockVCam, Mathf.Lerp(startSoloLock, targetSoloLock, t));
                SetWeight(multiLockVCam, Mathf.Lerp(startMultiLock, targetMultiLock, t));
                yield return null;
            }

            SetWeight(noLockVCam, targetNoLock);
            SetWeight(soloLockVCam, targetSoloLock);
            SetWeight(multiLockVCam, targetMultiLock);
            switchRoutine = null;
        }

        private float GetWeight(CinemachineCamera vCam)
        {
            if (mixingCamera != null && vCam != null)
            {
                return mixingCamera.GetWeight(vCam);
            }

            return 0f;
        }

        private void SetWeight(CinemachineCamera vCam, float weight)
        {
            if (mixingCamera != null && vCam != null)
            {
                mixingCamera.SetWeight(vCam, weight);
            }
        }
    }
}
