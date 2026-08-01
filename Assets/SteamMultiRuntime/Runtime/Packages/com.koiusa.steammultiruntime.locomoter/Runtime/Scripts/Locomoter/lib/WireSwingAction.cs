using UnityEngine;
namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(WireTraversalFeature)), DisallowMultipleComponent]
    public sealed class WireSwingAction : MonoBehaviour, IWireSwingAction
    {
        [SerializeField, Min(0f)] private float swingAcceleration = 16f;
        [SerializeField, Min(0f)] private float maximumInputSwingSpeed = 8f;
        private Rigidbody body; private IWireGroundAction groundAction; private IWireConnection connection; private Vector3 moveDirection;
        public bool IsEnabled => isActiveAndEnabled;
        private void Awake() { body = GetComponent<Rigidbody>(); groundAction = GetComponent<IWireGroundAction>(); connection = GetComponent<IWireConnection>(); }
        private void OnValidate() { swingAcceleration = Mathf.Max(0f, swingAcceleration); maximumInputSwingSpeed = Mathf.Max(0f, maximumInputSwingSpeed); }
        public void SetMoveDirection(Vector3 value) => moveDirection = Vector3.ClampMagnitude(value, 1f);
        internal void TickAttachedFixed()
        {
            if (connection == null || !connection.IsAttached || body == null || body.isKinematic || swingAcceleration <= 0f || maximumInputSwingSpeed <= 0f || (groundAction != null && groundAction.BlocksSwing)) return;
            var ropeDirection = (connection.AnchorPoint - body.worldCenterOfMass).normalized;
            var tangent = Vector3.ProjectOnPlane(moveDirection, ropeDirection);
            if (tangent.sqrMagnitude <= 0.0001f) return;
            var speed = Vector3.ProjectOnPlane(body.linearVelocity, ropeDirection).magnitude;
            var targetSpeed = maximumInputSwingSpeed * Mathf.Clamp01(tangent.magnitude);
            var remaining = Mathf.Clamp01((targetSpeed - speed) / targetSpeed);
            if (remaining > 0f) body.AddForce(tangent.normalized * (swingAcceleration * remaining * remaining), ForceMode.Acceleration);
        }
    }
}
