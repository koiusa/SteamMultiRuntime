using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    public static class PlayerPointerAim
    {
        public static Vector3 ResolveDirection(Transform cameraTransform, Transform fallbackTransform, Vector3 aimOrigin)
        {
            var camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (camera == null)
            {
                camera = Camera.main;
            }

            if (camera != null && Mouse.current != null)
            {
                var pointerPosition = Mouse.current.position.ReadValue();
                var pointerRay = camera.ScreenPointToRay(pointerPosition);
                var distantPoint = pointerRay.GetPoint(camera.farClipPlane);
                return (distantPoint - aimOrigin).normalized;
            }

            var reference = cameraTransform != null ? cameraTransform : fallbackTransform;
            return reference != null ? reference.forward : Vector3.forward;
        }
    }
}
