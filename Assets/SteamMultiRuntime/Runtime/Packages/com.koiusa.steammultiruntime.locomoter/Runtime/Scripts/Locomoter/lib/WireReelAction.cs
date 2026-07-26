using UnityEngine;
namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(WireConnection)), DisallowMultipleComponent]
    public sealed class WireReelAction : MonoBehaviour, IWireReelAction
    {
        [SerializeField, Min(0f)] private float reelSpeed = 12f;
        [SerializeField, Min(0f)] private float stepDistance = 1.5f;
        private IWireConnection connection; private float input;
        public bool IsEnabled => isActiveAndEnabled;
        private void Awake() => connection = GetComponent<IWireConnection>();
        private void OnValidate() { reelSpeed = Mathf.Max(0f, reelSpeed); stepDistance = Mathf.Max(0f, stepDistance); }
        public void SetInput(float value) => input = Mathf.Clamp(value, -1f, 1f);
        public void ReelStep() { if (connection != null && connection.IsAttached) connection.SetRopeLength(connection.RopeLength - stepDistance); }
        private void FixedUpdate() { if (connection != null && connection.IsAttached) connection.SetRopeLength(connection.RopeLength - input * reelSpeed * Time.fixedDeltaTime); }
    }
}
