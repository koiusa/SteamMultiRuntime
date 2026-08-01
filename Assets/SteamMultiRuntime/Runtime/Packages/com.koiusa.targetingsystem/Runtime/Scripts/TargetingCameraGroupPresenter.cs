using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetingCameraGroupPresenter : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera singleCamera;
        [SerializeField] private CinemachineCamera multiCamera;
        [SerializeField] private CinemachineTargetGroup targetGroup;
        [SerializeField] private TargetingCameraFramingMode framingMode = TargetingCameraFramingMode.PrimaryCentered;
        [SerializeField] private MonoBehaviour framingGroupSource;
        [SerializeField] private TargetingCameraRuntimeObjectFactory runtimeObjectFactory;
        [SerializeField, Min(0f)] private float memberWeight = 1f;
        [SerializeField, Min(0f)] private float memberRadius = 0.5f;

        private readonly List<Transform> members = new();
        private Transform playerAnchor;
        private Transform primaryAimPoint;
        private TargetingCameraLookAtAnchor lookAtAnchor;
        private ITargetingCameraFramingGroup framingGroup;
        private CinemachineGroupFraming multiGroupFraming;
        private CinemachineCamera capturedSingleCamera;
        private CinemachineCamera capturedMultiCamera;
        private Transform defaultSingleFollow;
        private Transform defaultSingleLookAt;
        private Transform defaultMultiFollow;
        private Transform defaultMultiLookAt;

        public void Configure(
            CinemachineCamera newSingleCamera,
            CinemachineCamera newMultiCamera,
            CinemachineTargetGroup newTargetGroup,
            TargetingCameraFramingMode newFramingMode,
            float newMemberWeight,
            float newMemberRadius)
        {
            singleCamera = newSingleCamera;
            multiCamera = newMultiCamera;
            CaptureDefaultTargets();
            targetGroup = newTargetGroup;
            framingMode = newFramingMode;
            if (targetGroup != null)
            {
                targetGroup.PositionMode = CinemachineTargetGroup.PositionModes.GroupCenter;
            }
            memberWeight = Mathf.Max(0f, newMemberWeight);
            memberRadius = Mathf.Max(0f, newMemberRadius);
            if (runtimeObjectFactory == null) runtimeObjectFactory = GetComponent<TargetingCameraRuntimeObjectFactory>();
            ResolveFramingGroup();
            SetGroupFramingEnabled(singleCamera, false);
            EnsureGroupFraming(multiCamera);
            multiGroupFraming = multiCamera != null
                ? multiCamera.GetComponent<CinemachineGroupFraming>()
                : null;
        }

        public void SetPlayerAnchor(Transform anchor)
        {
            if (playerAnchor == anchor) return;
            playerAnchor = anchor;
            RecreateLookAtAnchor();
        }

        public void Present(TargetingState state)
        {
            ClearGroup();
            primaryAimPoint = ResolveAimPoint(state.PrimaryTarget);
            if (state.Mode != TargetingMode.None)
            {
                AddMember(playerAnchor);
                foreach (var target in state.SelectedTargets)
                {
                    AddMember(ResolveAimPoint(target));
                }
            }
            framingGroup?.SetMembers(primaryAimPoint, members, memberWeight, memberRadius);

            ApplySingleTargets(state.Mode == TargetingMode.Single, primaryAimPoint);
            ApplyMultiTargets(state.Mode == TargetingMode.Multi, primaryAimPoint);
        }

        private void OnDisable()
        {
            ClearGroup();
            RestoreDefaultTargets();
        }

        private void OnDestroy()
        {
            var anchor = lookAtAnchor;
            lookAtAnchor = null;
            if (runtimeObjectFactory != null && anchor != null)
            {
                runtimeObjectFactory.Release(anchor);
            }
        }

        private void AddMember(Transform member)
        {
            if (member == null || members.Contains(member)) return;
            members.Add(member);
        }

        private void ClearGroup()
        {
            framingGroup?.Clear();
            members.Clear();
            primaryAimPoint = null;
        }

        private static Transform ResolveAimPoint(ITargetable target) =>
            target?.AimPoint != null ? target.AimPoint : target?.Root;

        private void ApplySingleTargets(bool isTargeting, Transform primaryAimPoint)
        {
            if (singleCamera == null) return;

            EnsureLookAtAnchor();
            if (lookAtAnchor != null)
            {
                lookAtAnchor.SetTarget(isTargeting ? primaryAimPoint : null);
            }
            singleCamera.Follow = isTargeting && lookAtAnchor != null ? lookAtAnchor.transform : playerAnchor;
            singleCamera.LookAt = isTargeting && primaryAimPoint != null ? primaryAimPoint : playerAnchor;
        }

        private void ApplyMultiTargets(bool isTargeting, Transform primaryAimPoint)
        {
            if (multiCamera == null) return;

            EnsureLookAtAnchor();
            if (isTargeting && lookAtAnchor != null)
            {
                lookAtAnchor.SetTarget(primaryAimPoint);
            }
            multiCamera.Follow = isTargeting && lookAtAnchor != null ? lookAtAnchor.transform : playerAnchor;
            multiCamera.LookAt = isTargeting && framingGroup?.CameraTarget != null
                ? framingGroup.CameraTarget
                : playerAnchor;
            if (multiGroupFraming != null) multiGroupFraming.CenterOffset = Vector2.zero;
        }

        private void ResolveFramingGroup()
        {
            if (framingMode == TargetingCameraFramingMode.PrimaryCentered)
            {
                framingGroup = GetComponentInChildren<PrimaryCenteredCinemachineTargetGroup>(true);
                if (framingGroup == null)
                {
                    Debug.LogWarning(
                        "Primary Centered framing requires a preconfigured PrimaryCenteredCinemachineTargetGroup.",
                        this);
                    framingGroupSource = null;
                    return;
                }
                framingGroupSource = framingGroup as MonoBehaviour;
                return;
            }

            if (framingMode == TargetingCameraFramingMode.GroupCentered)
            {
                var standardGroup = GetComponentInChildren<StandardCinemachineTargetGroupFraming>(true);
                if (standardGroup == null)
                {
                    Debug.LogWarning(
                        "Group Centered framing requires a preconfigured StandardCinemachineTargetGroupFraming.",
                        this);
                    framingGroupSource = null;
                    framingGroup = null;
                    return;
                }
                standardGroup.Configure(targetGroup);
                framingGroupSource = standardGroup;
                framingGroup = standardGroup;
                return;
            }

            framingGroup = framingGroupSource as ITargetingCameraFramingGroup;
            if (framingGroup != null) return;

            foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is not ITargetingCameraFramingGroup candidate) continue;
                framingGroupSource = component;
                framingGroup = candidate;
                return;
            }

            Debug.LogWarning("Custom camera framing requires a component implementing ITargetingCameraFramingGroup.", this);
        }

        private void EnsureLookAtAnchor()
        {
            if (lookAtAnchor == null && playerAnchor != null) RecreateLookAtAnchor();
        }

        private void RecreateLookAtAnchor()
        {
            var oldAnchor = lookAtAnchor;
            lookAtAnchor = null;
            if (runtimeObjectFactory != null && oldAnchor != null)
            {
                runtimeObjectFactory.Release(oldAnchor);
            }
            if (playerAnchor == null) return;
            if (runtimeObjectFactory == null)
            {
                Debug.LogWarning(
                    "Targeting camera follow target requires a preconfigured TargetingCameraRuntimeObjectFactory.",
                    this);
                return;
            }
            lookAtAnchor = runtimeObjectFactory.CreateFollowTarget(playerAnchor);
        }

        private void CaptureDefaultTargets()
        {
            if (singleCamera != capturedSingleCamera)
            {
                capturedSingleCamera = singleCamera;
                defaultSingleFollow = singleCamera != null ? singleCamera.Follow : null;
                defaultSingleLookAt = singleCamera != null ? singleCamera.LookAt : null;
            }
            if (multiCamera != capturedMultiCamera)
            {
                capturedMultiCamera = multiCamera;
                defaultMultiFollow = multiCamera != null ? multiCamera.Follow : null;
                defaultMultiLookAt = multiCamera != null ? multiCamera.LookAt : null;
            }
        }

        private void RestoreDefaultTargets()
        {
            if (lookAtAnchor != null) lookAtAnchor.SetTarget(null);
            if (singleCamera != null)
            {
                singleCamera.Follow = defaultSingleFollow;
                singleCamera.LookAt = defaultSingleLookAt;
            }
            if (multiCamera != null)
            {
                multiCamera.Follow = defaultMultiFollow;
                multiCamera.LookAt = defaultMultiLookAt;
            }
            if (multiGroupFraming != null) multiGroupFraming.CenterOffset = Vector2.zero;
        }

        private static void EnsureGroupFraming(CinemachineCamera camera)
        {
            if (camera == null) return;
            var framing = camera.GetComponent<CinemachineGroupFraming>();
            if (framing == null)
            {
                Debug.LogWarning($"{camera.name} requires a preconfigured CinemachineGroupFraming.", camera);
                return;
            }
            framing.enabled = true;
        }

        private static void SetGroupFramingEnabled(CinemachineCamera camera, bool enabled)
        {
            if (camera != null && camera.TryGetComponent<CinemachineGroupFraming>(out var framing))
            {
                framing.enabled = enabled;
            }
        }

    }
}
