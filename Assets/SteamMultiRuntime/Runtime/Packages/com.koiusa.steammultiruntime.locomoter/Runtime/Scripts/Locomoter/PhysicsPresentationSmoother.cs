using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class PhysicsPresentationSmoother : MonoBehaviour
    {
        private const string PresentationRootName = "Presentation";

        private Rigidbody targetRigidbody;
        private Transform presentationRoot;
        private Vector3 previousPosition;
        private Vector3 currentPosition;
        private Quaternion previousRotation;
        private Quaternion currentRotation;
        private bool hasPhysicsSample;
        private bool useExplicitSampleTiming;
        private float currentSampleTime;
        private float sampleInterval;

        public void Initialize(Rigidbody body)
        {
            targetRigidbody = body;
            EnsurePresentationRoot();
        }

        public void CapturePhysicsPose()
        {
            CapturePhysicsPoseInternal(false, Time.fixedDeltaTime);
        }

        public void CapturePhysicsPose(float explicitSampleInterval)
        {
            CapturePhysicsPoseInternal(true, explicitSampleInterval);
        }

        private void CapturePhysicsPoseInternal(bool explicitTiming, float interval)
        {
            if (targetRigidbody == null)
            {
                return;
            }

            useExplicitSampleTiming = explicitTiming;
            sampleInterval = Mathf.Max(interval, 0.0001f);
            if (explicitTiming)
                currentSampleTime = Time.time;

            if (!hasPhysicsSample)
            {
                previousPosition = targetRigidbody.position;
                previousRotation = targetRigidbody.rotation;
                currentPosition = previousPosition;
                currentRotation = previousRotation;
                hasPhysicsSample = true;
                return;
            }

            previousPosition = currentPosition;
            previousRotation = currentRotation;
            currentPosition = targetRigidbody.position;
            currentRotation = targetRigidbody.rotation;
        }

        private void Update()
        {
            TickPresentation();
        }

        public void TickPresentation()
        {
            if (!hasPhysicsSample || presentationRoot == null)
            {
                return;
            }

            var interval = useExplicitSampleTiming ? sampleInterval : Time.fixedDeltaTime;
            if (interval <= 0f)
                return;
            var sampleTime = useExplicitSampleTiming ? currentSampleTime : Time.fixedTime;
            var alpha = Mathf.Clamp01((Time.time - sampleTime) / interval);
            presentationRoot.SetPositionAndRotation(
                Vector3.Lerp(previousPosition, currentPosition, alpha),
                Quaternion.Slerp(previousRotation, currentRotation, alpha));
        }

        private void EnsurePresentationRoot()
        {
            presentationRoot = transform.Find(PresentationRootName);
            if (presentationRoot != null)
            {
                return;
            }

            var presentationObject = new GameObject(PresentationRootName);
            presentationRoot = presentationObject.transform;
            presentationRoot.SetParent(transform, false);

            // Existing children are presentation objects (camera marker and UI).
            // Physics/network components remain on this root object.
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != presentationRoot)
                {
                    child.SetParent(presentationRoot, true);
                }
            }
        }
    }
}
