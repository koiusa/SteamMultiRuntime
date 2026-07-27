using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class NpcDestinationDebugMarkerVisual : MonoBehaviour
    {
        private Mesh markerMesh;
        private Material markerMaterial;

        private void Awake()
        {
            markerMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var shader = Resources.Load<Shader>("Shaders/NpcDestinationDebugMarker");
            if (shader != null)
            {
                markerMaterial = new Material(shader)
                {
                    color = Color.red
                };
            }
        }

        private void Update()
        {
            if (markerMesh != null && markerMaterial != null)
            {
                Graphics.DrawMesh(markerMesh, transform.localToWorldMatrix, markerMaterial, gameObject.layer);
            }
        }

        private void OnDestroy()
        {
            if (markerMaterial != null)
            {
                Destroy(markerMaterial);
            }
        }
    }
}
