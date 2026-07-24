using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class PhysicsPresentationSmoother : MonoBehaviour
    {
        private const string PresentationRootName = "Presentation";

        private Rigidbody targetRigidbody;
        private ServerDrivenPlayerController controller;
        private Transform presentationRoot;
        private Vector3 previousPosition;
        private Vector3 currentPosition;
        private Quaternion previousRotation;
        private Quaternion currentRotation;
        private bool hasPhysicsSample;

        public void Initialize(Rigidbody body, ServerDrivenPlayerController playerController)
        {
            targetRigidbody = body;
            controller = playerController;
            EnsurePresentationRoot();
        }

        public void CapturePhysicsPose()
        {
            if (targetRigidbody == null || controller == null || !controller.IsSpawned || !controller.IsServer)
            {
                return;
            }

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
            if (!hasPhysicsSample || presentationRoot == null || Time.fixedDeltaTime <= 0f)
            {
                return;
            }

            var alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
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
