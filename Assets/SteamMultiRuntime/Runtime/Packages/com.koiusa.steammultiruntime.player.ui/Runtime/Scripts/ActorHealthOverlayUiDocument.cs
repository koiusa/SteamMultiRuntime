using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class ActorHealthOverlayUiDocument : MonoBehaviour
    {
        private UIDocument uiDocument;
        private VisualElement healthBar;
        private VisualElement healthBarFill;
        private Label healthValueLabel;
        private ActorHealthFeature healthFeature;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            uiDocument = GetComponent<UIDocument>();
            BindVisualTreeAndRefresh();
            uiDocument.rootVisualElement.schedule.Execute(BindVisualTreeAndRefresh);
            healthFeature = GetComponentInParent<ActorHealthFeature>();
            if (healthFeature != null) healthFeature.HealthChanged += OnHealthChanged;
            RefreshHealth();
        }

        private void OnDisable()
        {
            if (healthFeature != null) healthFeature.HealthChanged -= OnHealthChanged;
            healthFeature = null;
            healthBar = null;
            healthBarFill = null;
            healthValueLabel = null;
        }

        private void BindVisualTreeAndRefresh()
        {
            var root = uiDocument?.rootVisualElement;
            healthBar = root?.Q<VisualElement>("health-bar");
            healthBarFill = root?.Q<VisualElement>("health-bar-fill");
            healthValueLabel = root?.Q<Label>("health-value-label");
            RefreshHealth();
        }

        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            RefreshHealth(currentHealth, maxHealth);
        }

        private void RefreshHealth()
        {
            RefreshHealth(
                healthFeature != null ? healthFeature.CurrentHealth : 0f,
                healthFeature != null ? healthFeature.MaxHealth : 0f);
        }

        private void RefreshHealth(float currentHealth, float maxHealth)
        {
            var available = healthFeature != null && maxHealth > 0f;
            if (healthBar != null) healthBar.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            if (healthValueLabel != null) healthValueLabel.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            if (!available || healthBarFill == null) return;

            var normalized = Mathf.Clamp01(currentHealth / maxHealth);
            ApplyFillAmount(healthBarFill, normalized);
            healthBarFill.style.backgroundColor = Color.Lerp(
                new Color(0.95f, 0.16f, 0.12f), new Color(0.24f, 0.87f, 0.42f), normalized);
            if (healthValueLabel != null)
                healthValueLabel.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }

        internal static void ApplyFillAmount(VisualElement fill, float normalized)
        {
            if (fill == null) return;
            normalized = Mathf.Clamp01(normalized);
            fill.style.width = Length.Percent(100f);
            fill.style.transformOrigin = new TransformOrigin(Length.Percent(0f), Length.Percent(50f), 0f);
            fill.style.scale = new Scale(new Vector3(normalized, 1f, 1f));
        }
    }
}
