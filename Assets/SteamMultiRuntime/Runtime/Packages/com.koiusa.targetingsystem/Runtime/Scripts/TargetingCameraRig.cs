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

        public CameraMode CurrentMode { get; private set; } = CameraMode.NoLock;

        private Coroutine switchRoutine;

        private void Awake()
        {
            if (multiLockVCam != null)
            {
                targetGroupBinder = multiLockVCam.GetComponent<ILockOnTargetBinder>();
            }
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
