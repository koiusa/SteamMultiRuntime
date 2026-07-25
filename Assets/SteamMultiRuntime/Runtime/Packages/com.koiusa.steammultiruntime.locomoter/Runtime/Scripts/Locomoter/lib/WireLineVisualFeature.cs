using UnityEngine;
using UnityEngine.Rendering;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Owns the wire origin and LineRenderer presentation.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class WireLineVisualFeature : MonoBehaviour, IWireLineVisualFeature
    {
        private static Material sharedBuiltInMaterial;
        private static Material sharedUniversalMaterial;
        private static Material sharedHighDefinitionMaterial;
        private static Material sharedCustomPipelineMaterial;

        [SerializeField] private Transform wireOrigin;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Material wireMaterial;
        [SerializeField, Min(0f)] private float wireWidth = 0.035f;
        [SerializeField] private Color wireColor = Color.white;

        private MaterialPropertyBlock propertyBlock;
        private Rigidbody ownerBody;
        private bool initialized;

        public LineRenderer Renderer => lineRenderer;
        public bool IsEnabled => isActiveAndEnabled;

        private void Awake()
        {
            Initialize();
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnValidate()
        {
            wireWidth = Mathf.Max(0f, wireWidth);
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            ownerBody = GetComponent<Rigidbody>();
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            Configure();
            SetVisible(false);
            initialized = true;
        }

        public void SetVisible(bool visible)
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = visible && IsEnabled;
            }
        }

        public void UpdateEndpoints(Vector3 anchorPoint)
        {
            if (lineRenderer == null || !lineRenderer.enabled)
            {
                return;
            }

            lineRenderer.SetPosition(0, wireOrigin != null ? wireOrigin.position : ownerBody.worldCenterOfMass);
            lineRenderer.SetPosition(1, anchorPoint);
        }

        private void Configure()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = wireWidth;
            lineRenderer.endWidth = wireWidth;
            lineRenderer.startColor = wireColor;
            lineRenderer.endColor = wireColor;
            lineRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", wireColor);
            propertyBlock.SetColor("_UnlitColor", wireColor);
            propertyBlock.SetColor("_Color", wireColor);
            lineRenderer.SetPropertyBlock(propertyBlock);

            var material = wireMaterial != null ? wireMaterial : GetSharedPipelineMaterial();
            if (material != null)
            {
                lineRenderer.sharedMaterial = material;
            }
            else
            {
                Debug.LogWarning("No compatible wire shader was found. Assign Wire Material explicitly.", lineRenderer);
            }
        }

        private static Material GetSharedPipelineMaterial()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                return sharedBuiltInMaterial != null ? sharedBuiltInMaterial : sharedBuiltInMaterial = CreateSharedMaterial("Particles/Standard Unlit", "Sprites/Default");
            }

            var name = pipeline.GetType().FullName ?? string.Empty;
            if (name.Contains("UniversalRenderPipeline"))
            {
                return sharedUniversalMaterial != null ? sharedUniversalMaterial : sharedUniversalMaterial = CreateSharedMaterial("Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Unlit");
            }

            if (name.Contains("HDRenderPipeline"))
            {
                return sharedHighDefinitionMaterial != null ? sharedHighDefinitionMaterial : sharedHighDefinitionMaterial = CreateSharedMaterial("HDRP/Unlit", "HDRenderPipeline/Unlit");
            }

            return sharedCustomPipelineMaterial != null ? sharedCustomPipelineMaterial : sharedCustomPipelineMaterial = CreateSharedMaterial("Universal Render Pipeline/Particles/Unlit", "HDRP/Unlit", "Particles/Standard Unlit", "Sprites/Default");
        }

        private static Material CreateSharedMaterial(params string[] shaderNames)
        {
            for (var i = 0; i < shaderNames.Length; i++)
            {
                var shader = Shader.Find(shaderNames[i]);
                if (shader != null && shader.isSupported)
                {
                    return new Material(shader) { name = $"Wire ({shaderNames[i]})", hideFlags = HideFlags.HideAndDontSave };
                }
            }

            return null;
        }
    }
}
