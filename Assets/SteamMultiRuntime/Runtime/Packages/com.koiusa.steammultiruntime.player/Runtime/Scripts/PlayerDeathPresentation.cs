using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerRespawnFeature))]
    public sealed class PlayerDeathPresentation : MonoBehaviour
    {
        private const string DeathVfxPath = "VFX/Skills/SkillHealBurst";
        private const string DissolveShaderPath = "Shaders/CharacterDeathDissolve";
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private bool playDeathEffect = true;
        [SerializeField, Min(0.1f)] private float dissolveDuration = 1f;
        [SerializeField, Min(0.1f)] private float effectLifetime = 1.6f;
        [SerializeField] private Color dissolveEdgeColor = new Color(0.25f, 0.8f, 1f, 1f);
        [SerializeField] private Vector3 effectLocalPosition = new Vector3(0f, 0.8f, 0f);
        [SerializeField] private Vector3 effectLocalScale = new Vector3(1.35f, 1.8f, 1.35f);

        private readonly List<DissolveRendererState> rendererStates = new();
        private readonly List<IPlayerRespawnPresentationNotifier> respawnNotifiers = new();
        private PlayerRespawnFeature lifeState;
        private Coroutine dissolveRoutine;
        private GameObject activeDeathEffect;

        private sealed class DissolveRendererState
        {
            public Renderer Renderer;
            public Material[] OriginalMaterials;
            public Material[] DissolveMaterials;
        }

        private void Awake() => lifeState = GetComponent<PlayerRespawnFeature>();

        private void OnEnable()
        {
            if (lifeState == null) lifeState = GetComponent<PlayerRespawnFeature>();
            if (lifeState != null) lifeState.LifeStateChanged += OnLifeStateChanged;
            FindRespawnNotifiers();
            if (lifeState != null && lifeState.IsDead) BeginDissolve();
        }

        private void OnDisable()
        {
            if (lifeState != null) lifeState.LifeStateChanged -= OnLifeStateChanged;
            for (var i = 0; i < respawnNotifiers.Count; i++)
                respawnNotifiers[i].RespawnPresentationReady -= OnRespawnPresentationReady;
            respawnNotifiers.Clear();
            RestoreOriginalMaterials();
        }

        private void OnLifeStateChanged(bool isDead)
        {
            if (isDead) BeginDissolve();
            else if (respawnNotifiers.Count == 0) RestoreOriginalMaterials();
        }

        private void BeginDissolve()
        {
            RestoreOriginalMaterials();
            var shader = Resources.Load<Shader>(DissolveShaderPath);
            if (shader == null)
            {
                Debug.LogWarning("Character death dissolve shader is missing.", this);
                return;
            }

            CaptureAndReplaceMaterials(shader);
            dissolveRoutine = StartCoroutine(AnimateDissolve());
        }

        private void CaptureAndReplaceMaterials(Shader shader)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var candidate = renderers[i];
                if (candidate == null || !candidate.enabled
                    || candidate.GetComponentInParent<VisualEffect>() != null
                    || candidate.GetComponentInParent<PlayerSkillEffectVisual>() != null
                    || candidate.GetComponentInParent<GuardShieldVisual>() != null)
                    continue;

                var originals = candidate.sharedMaterials;
                var replacements = new Material[originals.Length];
                for (var materialIndex = 0; materialIndex < originals.Length; materialIndex++)
                    replacements[materialIndex] = CreateDissolveMaterial(shader, originals[materialIndex]);

                rendererStates.Add(new DissolveRendererState
                {
                    Renderer = candidate,
                    OriginalMaterials = originals,
                    DissolveMaterials = replacements
                });
                candidate.sharedMaterials = replacements;
            }
        }

        private Material CreateDissolveMaterial(Shader shader, Material source)
        {
            var material = new Material(shader) { name = $"{source?.name ?? "Character"} (Death Dissolve)" };
            var textureProperty = source != null && source.HasProperty(BaseMapId)
                ? BaseMapId
                : Shader.PropertyToID("_MainTex");
            if (source != null && source.HasProperty(textureProperty))
            {
                material.SetTexture(BaseMapId, source.GetTexture(textureProperty));
                material.SetTextureScale(BaseMapId, source.GetTextureScale(textureProperty));
                material.SetTextureOffset(BaseMapId, source.GetTextureOffset(textureProperty));
            }

            var colorProperty = source != null && source.HasProperty(BaseColorId)
                ? BaseColorId
                : Shader.PropertyToID("_Color");
            if (source != null && source.HasProperty(colorProperty))
                material.SetColor(BaseColorId, source.GetColor(colorProperty));
            material.SetColor("_EdgeColor", dissolveEdgeColor);
            material.SetFloat(DissolveAmountId, 0f);
            return material;
        }

        private IEnumerator AnimateDissolve()
        {
            var elapsed = 0f;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                SetDissolveAmount(Mathf.Clamp01(elapsed / dissolveDuration));
                yield return null;
            }
            SetDissolveAmount(1f);
            dissolveRoutine = null;
            if (playDeathEffect) PlayDeathEffect();
        }

        private void SetDissolveAmount(float amount)
        {
            for (var stateIndex = 0; stateIndex < rendererStates.Count; stateIndex++)
            {
                var materials = rendererStates[stateIndex].DissolveMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    if (materials[materialIndex] != null) materials[materialIndex].SetFloat(DissolveAmountId, amount);
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (dissolveRoutine != null) StopCoroutine(dissolveRoutine);
            dissolveRoutine = null;
            if (activeDeathEffect != null) Destroy(activeDeathEffect);
            activeDeathEffect = null;
            for (var stateIndex = 0; stateIndex < rendererStates.Count; stateIndex++)
            {
                var state = rendererStates[stateIndex];
                if (state.Renderer != null) state.Renderer.sharedMaterials = state.OriginalMaterials;
                for (var materialIndex = 0; materialIndex < state.DissolveMaterials.Length; materialIndex++)
                    if (state.DissolveMaterials[materialIndex] != null) Destroy(state.DissolveMaterials[materialIndex]);
            }
            rendererStates.Clear();
        }

        private void FindRespawnNotifiers()
        {
            respawnNotifiers.Clear();
            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is not IPlayerRespawnPresentationNotifier notifier) continue;
                respawnNotifiers.Add(notifier);
                notifier.RespawnPresentationReady += OnRespawnPresentationReady;
            }
        }

        private void OnRespawnPresentationReady(Vector3 position, Quaternion rotation)
        {
            RestoreOriginalMaterials();
        }

        private void PlayDeathEffect()
        {
            var asset = Resources.Load<VisualEffectAsset>(DeathVfxPath);
            if (asset == null) return;
            if (activeDeathEffect != null) Destroy(activeDeathEffect);
            var effectObject = new GameObject("DeathDissolveVFX", typeof(VisualEffect));
            activeDeathEffect = effectObject;
            effectObject.transform.SetParent(FindPresentationRoot(), false);
            effectObject.transform.localPosition = effectLocalPosition;
            effectObject.transform.localScale = effectLocalScale;
            var effect = effectObject.GetComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            effect.Play();
            Destroy(effectObject, effectLifetime);
        }

        private Transform FindPresentationRoot()
        {
            for (var i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).name == "Presentation") return transform.GetChild(i);
            return transform;
        }
    }
}
