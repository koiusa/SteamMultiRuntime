using System.Collections;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [DisallowMultipleComponent]
    public sealed class ActorHealthUiRouter : MonoBehaviour
    {
        [SerializeField] private GameObject overheadHealthUi;
        [SerializeField] private GameObject localHealthHud;
        [SerializeField] private bool damageOnlyOverhead;
        [SerializeField, Min(0.1f)] private float npcVisibleDuration = 3f;

        private ILocalPlayerOwnershipNotifier ownership;
        private ActorHealthFeature health;
        private Coroutine npcHideRoutine;
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
            if (npcHideRoutine != null) StopCoroutine(npcHideRoutine);
            npcHideRoutine = null;
        }

        private void Apply()
        {
            var isAlive = health == null || health.IsAlive;
            var isLocalOwner = ownership != null && ownership.IsOwnershipResolved && ownership.IsLocalOwner;
            if (overheadHealthUi != null)
                overheadHealthUi.SetActive(isAlive && !damageOnlyOverhead && ownership != null && !isLocalOwner);
            if (localHealthHud != null) localHealthHud.SetActive(isAlive && isLocalOwner);
        }

        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            var previous = previousHealth;
            var tookDamage = currentHealth < previousHealth;
            previousHealth = currentHealth;
            if (currentHealth <= 0f)
            {
                if (npcHideRoutine != null) StopCoroutine(npcHideRoutine);
                npcHideRoutine = null;
                if (overheadHealthUi != null) overheadHealthUi.SetActive(false);
                if (localHealthHud != null) localHealthHud.SetActive(false);
                return;
            }

            if (previous <= 0f)
            {
                Apply();
                return;
            }

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
