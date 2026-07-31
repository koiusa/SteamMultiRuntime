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

        private void OnHealthChanged(float currentHealth, float maxHealth) => RefreshHealth();

        private void RefreshHealth()
        {
            var available = healthFeature != null && healthFeature.MaxHealth > 0f;
            if (healthBar != null) healthBar.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            if (healthValueLabel != null) healthValueLabel.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            if (!available || healthBarFill == null) return;

            var normalized = Mathf.Clamp01(healthFeature.CurrentHealth / healthFeature.MaxHealth);
            healthBarFill.style.width = Length.Percent(normalized * 100f);
            healthBarFill.style.backgroundColor = Color.Lerp(
                new Color(0.95f, 0.16f, 0.12f), new Color(0.24f, 0.87f, 0.42f), normalized);
            if (healthValueLabel != null)
                healthValueLabel.text = $"{Mathf.CeilToInt(healthFeature.CurrentHealth)} / {Mathf.CeilToInt(healthFeature.MaxHealth)}";
        }
    }
}
