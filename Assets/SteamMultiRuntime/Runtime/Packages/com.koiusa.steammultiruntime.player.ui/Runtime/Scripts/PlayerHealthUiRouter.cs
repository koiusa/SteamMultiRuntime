using System.Collections;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealthUiRouter : MonoBehaviour
    {
        [SerializeField] private GameObject overheadHealthUi;
        [SerializeField] private GameObject localHealthHud;
        [SerializeField] private bool damageOnlyOverhead;
        [SerializeField, Min(0.1f)] private float npcVisibleDuration = 3f;

        private ILocalPlayerOwnershipNotifier ownership;
        private PlayerHealthFeature health;
        private Coroutine npcHideRoutine;
        private float previousHealth;

        private void Awake()
        {
            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
                if (components[i] is ILocalPlayerOwnershipNotifier source) { ownership = source; break; }
            health = GetComponent<PlayerHealthFeature>();
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
            if (npcHideRoutine != null) StopCoroutine(npcHideRoutine);
            npcHideRoutine = null;
        }

        private void Apply()
        {
            var isLocalOwner = ownership != null && ownership.IsOwnershipResolved && ownership.IsLocalOwner;
            if (overheadHealthUi != null)
                overheadHealthUi.SetActive(!damageOnlyOverhead && ownership != null && !isLocalOwner);
            if (localHealthHud != null) localHealthHud.SetActive(isLocalOwner);
        }

        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            var tookDamage = currentHealth < previousHealth;
            previousHealth = currentHealth;
            if (!damageOnlyOverhead || !tookDamage || overheadHealthUi == null) return;

            overheadHealthUi.SetActive(true);
            if (npcHideRoutine != null) StopCoroutine(npcHideRoutine);
            npcHideRoutine = StartCoroutine(HideNpcHealthAfterDelay());
        }

        private IEnumerator HideNpcHealthAfterDelay()
        {
            yield return new WaitForSeconds(npcVisibleDuration);
            if (overheadHealthUi != null) overheadHealthUi.SetActive(false);
            npcHideRoutine = null;
        }
    }
}
