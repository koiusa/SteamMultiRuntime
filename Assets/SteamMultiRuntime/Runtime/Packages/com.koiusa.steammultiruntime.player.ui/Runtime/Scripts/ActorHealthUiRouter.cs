using System.Collections;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [DisallowMultipleComponent]
    public sealed class ActorHealthUiRouter : MonoBehaviour
    {
        [SerializeField] private GameObject overheadHealthUi;
        [SerializeField] private GameObject localHealthHud;
        [SerializeField] private ActorPresentationSettings presentationSettings;

        private ILocalPlayerOwnershipNotifier ownership;
        private ActorHealthFeature health;
        private Coroutine overheadHideRoutine;
        private float previousHealth;

        private void Awake()
        {
            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
                if (components[i] is ILocalPlayerOwnershipNotifier source) { ownership = source; break; }
            health = GetComponent<ActorHealthFeature>();
            previousHealth = health != null ? health.CurrentHealth : 0f;
        }

        private void OnEnable()
        {
            if (ownership != null) ownership.OwnershipChanged += Apply;
            if (health != null) health.HealthChanged += OnHealthChanged;
            previousHealth = health != null ? health.CurrentHealth : 0f;
            Apply();
        }

        private void OnDisable()
        {
            if (ownership != null) ownership.OwnershipChanged -= Apply;
            if (health != null) health.HealthChanged -= OnHealthChanged;
            StopOverheadHideRoutine();
        }

        private void Apply()
        {
            var isAlive = health == null || health.IsAlive;
            var isLocalOwner = ownership != null && ownership.IsOwnershipResolved && ownership.IsLocalOwner;
            if (overheadHealthUi != null)
                overheadHealthUi.SetActive(isAlive && !DamageOnlyOverhead && CanShowOverhead());
            if (localHealthHud != null) localHealthHud.SetActive(isAlive && isLocalOwner);
        }

        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            var previous = previousHealth;
            var tookDamage = currentHealth < previousHealth;
            previousHealth = currentHealth;
            if (currentHealth <= 0f)
            {
                StopOverheadHideRoutine();
                if (overheadHealthUi != null) overheadHealthUi.SetActive(false);
                if (localHealthHud != null) localHealthHud.SetActive(false);
                return;
            }

            if (previous <= 0f)
            {
                Apply();
                return;
            }

            if (!DamageOnlyOverhead || !tookDamage || overheadHealthUi == null || !CanShowOverhead()) return;

            overheadHealthUi.SetActive(true);
            StopOverheadHideRoutine();
            overheadHideRoutine = StartCoroutine(HideOverheadHealthAfterDelay());
        }

        private bool CanShowOverhead()
        {
            return CanShowOverhead(
                ownership != null,
                ownership != null && ownership.IsOwnershipResolved,
                ownership != null && ownership.IsLocalOwner);
        }

        internal static bool CanShowOverhead(bool hasOwnership, bool isOwnershipResolved, bool isLocalOwner)
        {
            return !hasOwnership || isOwnershipResolved && !isLocalOwner;
        }

        private IEnumerator HideOverheadHealthAfterDelay()
        {
            var duration = presentationSettings != null
                ? presentationSettings.OverheadHealthVisibleDuration
                : ActorPresentationSettings.DefaultOverheadHealthVisibleDuration;
            yield return new WaitForSeconds(duration);
            if (overheadHealthUi != null) overheadHealthUi.SetActive(false);
            overheadHideRoutine = null;
        }

        private bool DamageOnlyOverhead => presentationSettings == null || presentationSettings.DamageOnlyOverhead;

        private void StopOverheadHideRoutine()
        {
            if (overheadHideRoutine != null) StopCoroutine(overheadHideRoutine);
            overheadHideRoutine = null;
        }
    }
}
