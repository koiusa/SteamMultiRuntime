using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public readonly struct WireAimResult
    {
        public WireAimResult(ScreenAimTargetState state, Vector3 requestedPoint,
            Vector3 attachPoint = default, Transform anchorTransform = null)
        {
            State = state;
            RequestedPoint = requestedPoint;
            AttachPoint = attachPoint;
            AnchorTransform = anchorTransform;
        }

        public ScreenAimTargetState State { get; }
        public Vector3 RequestedPoint { get; }
        public Vector3 AttachPoint { get; }
        public Transform AnchorTransform { get; }
        public bool CanAttach => State == ScreenAimTargetState.Valid && AnchorTransform != null;

        public static WireAimResult Invalid(Vector3 requestedPoint = default) =>
            new WireAimResult(ScreenAimTargetState.Invalid, requestedPoint);
    }
}
