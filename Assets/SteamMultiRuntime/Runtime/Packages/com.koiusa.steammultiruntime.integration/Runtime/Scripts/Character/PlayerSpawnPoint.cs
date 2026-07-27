using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Defines an exact player-root pose in this scene.
    /// Multiple points are assigned in scene-hierarchy order.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        private const float GizmoRadius = 0.35f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, GizmoRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}
