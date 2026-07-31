using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CreateAssetMenu(
        fileName = "ActorPresentationSettings",
        menuName = "Steam Multi Runtime/Player/Actor Presentation Settings")]
    public sealed class ActorPresentationSettings : ScriptableObject
    {
        public const float DefaultOverheadHealthVisibleDuration = 3f;
        public const float DefaultDissolveDuration = 1f;
        public const float DefaultDeathEffectLifetime = 1.6f;
        public static Color DefaultDissolveEdgeColor => new(0.25f, 0.8f, 1f, 1f);
        public static Vector3 DefaultDeathEffectLocalPosition => new(0f, 0.8f, 0f);
        public static Vector3 DefaultDeathEffectLocalScale => new(1.35f, 1.8f, 1.35f);

        [Header("Health Bar")]
        [SerializeField] private bool damageOnlyOverhead = true;
        [SerializeField, Min(0.1f)] private float overheadHealthVisibleDuration = DefaultOverheadHealthVisibleDuration;

        [Header("Name Plate")]
        [SerializeField] private bool hideNameWhenDead = true;

        [Header("Death")]
        [SerializeField] private bool playDeathEffect = true;
        [SerializeField, Min(0.1f)] private float dissolveDuration = DefaultDissolveDuration;
        [SerializeField, Min(0.1f)] private float deathEffectLifetime = DefaultDeathEffectLifetime;
        [SerializeField] private Color dissolveEdgeColor = DefaultDissolveEdgeColor;
        [SerializeField] private Vector3 deathEffectLocalPosition = DefaultDeathEffectLocalPosition;
        [SerializeField] private Vector3 deathEffectLocalScale = DefaultDeathEffectLocalScale;

        public bool DamageOnlyOverhead => damageOnlyOverhead;
        public float OverheadHealthVisibleDuration => overheadHealthVisibleDuration;
        public bool HideNameWhenDead => hideNameWhenDead;
        public bool PlayDeathEffect => playDeathEffect;
        public float DissolveDuration => dissolveDuration;
        public float DeathEffectLifetime => deathEffectLifetime;
        public Color DissolveEdgeColor => dissolveEdgeColor;
        public Vector3 DeathEffectLocalPosition => deathEffectLocalPosition;
        public Vector3 DeathEffectLocalScale => deathEffectLocalScale;

        private void OnValidate()
        {
            overheadHealthVisibleDuration = Mathf.Max(0.1f, overheadHealthVisibleDuration);
            dissolveDuration = Mathf.Max(0.1f, dissolveDuration);
            deathEffectLifetime = Mathf.Max(0.1f, deathEffectLifetime);
        }
    }
}
