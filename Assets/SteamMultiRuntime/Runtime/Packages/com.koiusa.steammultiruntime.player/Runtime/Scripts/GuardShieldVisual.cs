using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class GuardShieldVisual : MonoBehaviour, IGuardImpactPresenter
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int ImpactPositionId = Shader.PropertyToID("_ImpactPosition");
        private static readonly int ImpactRadiusId = Shader.PropertyToID("_ImpactRadius");
        private static readonly int ImpactStrengthId = Shader.PropertyToID("_ImpactStrength");
        private static Mesh sharedIcosphere;

        [SerializeField] private Vector3 fallbackLocalCenter = new Vector3(0f, 1f, 0f);
        [SerializeField, Min(0.1f)] private float radius = 1.15f;
        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.08f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.16f;
        [SerializeField, Range(0f, 1f)] private float visibleOpacity = 0.65f;
        [SerializeField, Min(0.01f)] private float attackImpactDuration = 0.35f;

        private Renderer shieldRenderer;
        private Transform shieldTransform;
        private MaterialPropertyBlock propertyBlock;
        private float opacity;
        private float expansion;
        private bool isGuarding;
        private float attackImpactEndsAt;
        private Vector3 attackImpactPosition;

        public bool IsGuarding => isGuarding;

        internal bool OwnsRenderer(Renderer candidate) => candidate == shieldRenderer;

        private void Awake() => EnsureShield();

        private void Update()
        {
            var duration = isGuarding ? fadeInDuration : fadeOutDuration;
            var target = isGuarding ? visibleOpacity : 0f;
            opacity = Mathf.MoveTowards(opacity, target, Time.deltaTime / duration);
            expansion = Mathf.MoveTowards(expansion, isGuarding ? 1f : 0f, Time.deltaTime / duration);
            var easedExpansion = expansion * expansion * (3f - 2f * expansion);
            if (shieldTransform != null)
                shieldTransform.localScale = Vector3.one * (radius * easedExpansion);
            UpdateAttackImpact();
            ApplyOpacity();

            if (shieldRenderer != null)
                shieldRenderer.enabled = opacity > 0.001f;
        }

        public void SetGuarding(bool value)
        {
            EnsureShield();
            isGuarding = value;
            if (value && shieldRenderer != null)
            {
                RefreshShieldCenter();
                shieldRenderer.enabled = true;
            }
        }

        private void EnsureShield()
        {
            if (shieldRenderer != null) return;

            var shader = Shader.Find("Koiusa/Effects/GuardShield");
            if (shader == null)
            {
                Debug.LogError("Guard shield shader was not found.", this);
                enabled = false;
                return;
            }

            var characterCenter = ResolveCharacterCenterWorld();
            var presentationRoot = transform.Find("Presentation");
            var visualParent = presentationRoot != null ? presentationRoot : transform;
            var shield = new GameObject("GuardShieldVisual", typeof(MeshFilter), typeof(MeshRenderer));
            shield.name = "GuardShieldVisual";
            shield.transform.SetParent(visualParent, false);
            shield.transform.localPosition = visualParent.InverseTransformPoint(characterCenter);
            shield.transform.localRotation = Quaternion.identity;
            shield.transform.localScale = Vector3.zero;
            shield.layer = gameObject.layer;
            shieldTransform = shield.transform;

            shield.GetComponent<MeshFilter>().sharedMesh = GetIcosphere();

            shieldRenderer = shield.GetComponent<Renderer>();
            shieldRenderer.sharedMaterial = new Material(shader)
            {
                name = "GuardShield (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            shieldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shieldRenderer.receiveShadows = false;
            shieldRenderer.lightProbeUsage = LightProbeUsage.Off;
            shieldRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shieldRenderer.enabled = false;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void RefreshShieldCenter()
        {
            if (shieldTransform == null) return;

            var visualParent = shieldTransform.parent;
            var characterCenter = ResolveCharacterCenterWorld();
            shieldTransform.localPosition = visualParent != null
                ? visualParent.InverseTransformPoint(characterCenter)
                : characterCenter;
        }

        public void PlayAttackImpact(Vector3 worldPosition)
        {
            if (!isGuarding) return;
            attackImpactPosition = worldPosition;
            attackImpactEndsAt = Time.time + attackImpactDuration;
        }

        private Vector3 ResolveCharacterCenterWorld()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var bounds = new Bounds();
            for (var i = 0; i < renderers.Length; i++)
            {
                var candidate = renderers[i];
                if (candidate == null || candidate == shieldRenderer ||
                    candidate is ParticleSystemRenderer || candidate is TrailRenderer || candidate is LineRenderer ||
                    candidate.GetComponentInParent<ActorSkillEffectVisual>() != null)
                    continue;

                if (!hasBounds)
                {
                    bounds = candidate.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(candidate.bounds);
                }
            }

            return hasBounds ? bounds.center : transform.TransformPoint(fallbackLocalCenter);
        }

        private void ApplyOpacity()
        {
            if (shieldRenderer == null) return;
            shieldRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(OpacityId, opacity);
            var impactProgress = attackImpactDuration > 0f
                ? Mathf.Clamp01(1f - (attackImpactEndsAt - Time.time) / attackImpactDuration)
                : 1f;
            var impactStrength = Time.time < attackImpactEndsAt ? 1f - impactProgress : 0f;
            propertyBlock.SetVector(ImpactPositionId, attackImpactPosition);
            propertyBlock.SetFloat(ImpactRadiusId, Mathf.Lerp(0.05f, radius * 0.9f, impactProgress));
            propertyBlock.SetFloat(ImpactStrengthId, impactStrength * 4f);
            shieldRenderer.SetPropertyBlock(propertyBlock);
        }

        private void UpdateAttackImpact()
        {
            if (!isGuarding) attackImpactEndsAt = 0f;
        }

        private void OnDisable()
        {
            isGuarding = false;
            opacity = 0f;
            expansion = 0f;
            attackImpactEndsAt = 0f;
            if (shieldTransform != null) shieldTransform.localScale = Vector3.zero;
            ApplyOpacity();
            if (shieldRenderer != null) shieldRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (shieldRenderer != null && shieldRenderer.sharedMaterial != null)
                Destroy(shieldRenderer.sharedMaterial);
        }

        private static Mesh GetIcosphere()
        {
            if (sharedIcosphere != null) return sharedIcosphere;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var initialVertices = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (var i = 0; i < initialVertices.Length; i++) vertices.Add(initialVertices[i].normalized);

            triangles.AddRange(new[]
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            });

            for (var subdivision = 0; subdivision < 3; subdivision++)
            {
                var midpointCache = new Dictionary<long, int>();
                var refined = new List<int>(triangles.Count * 4);
                for (var i = 0; i < triangles.Count; i += 3)
                {
                    var a = triangles[i];
                    var b = triangles[i + 1];
                    var c = triangles[i + 2];
                    var ab = GetMidpoint(a, b, vertices, midpointCache);
                    var bc = GetMidpoint(b, c, vertices, midpointCache);
                    var ca = GetMidpoint(c, a, vertices, midpointCache);
                    refined.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = refined;
            }

            sharedIcosphere = new Mesh { name = "Guard Shield Icosphere", hideFlags = HideFlags.HideAndDontSave };
            sharedIcosphere.SetVertices(vertices);
            sharedIcosphere.SetTriangles(triangles, 0);
            sharedIcosphere.SetNormals(vertices);
            sharedIcosphere.RecalculateBounds();
            sharedIcosphere.UploadMeshData(true);
            return sharedIcosphere;
        }

        private static int GetMidpoint(
            int first,
            int second,
            List<Vector3> vertices,
            Dictionary<long, int> cache)
        {
            var smaller = Mathf.Min(first, second);
            var greater = Mathf.Max(first, second);
            var key = ((long)smaller << 32) + greater;
            if (cache.TryGetValue(key, out var index)) return index;

            index = vertices.Count;
            vertices.Add(((vertices[first] + vertices[second]) * 0.5f).normalized);
            cache.Add(key, index);
            return index;
        }
    }
}
