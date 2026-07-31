using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class PlayerNameOverlayUiDocument : MonoBehaviour
    {
        [SerializeField] private ActorPresentationSettings presentationSettings;

        private UIDocument uiDocument;
        private Label playerNameLabel;
        private IPlayerIdentitySource identitySource;
        private IPlayerDisplayNameNotifier displayNameNotifier;
        private ActorHealthFeature healthFeature;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            uiDocument = GetComponent<UIDocument>();
            BindVisualTreeAndRefresh();
            uiDocument.rootVisualElement.schedule.Execute(BindVisualTreeAndRefresh);
            identitySource = FindIdentitySource();
            displayNameNotifier = identitySource as IPlayerDisplayNameNotifier;
            if (displayNameNotifier != null) displayNameNotifier.DisplayNameChanged += RefreshDisplayName;
            healthFeature = GetComponentInParent<ActorHealthFeature>();
            if (healthFeature != null) healthFeature.HealthChanged += OnHealthChanged;
            RefreshDisplayName();
        }

        private void OnDisable()
        {
            if (displayNameNotifier != null) displayNameNotifier.DisplayNameChanged -= RefreshDisplayName;
            if (healthFeature != null) healthFeature.HealthChanged -= OnHealthChanged;
            displayNameNotifier = null;
            healthFeature = null;
            identitySource = null;
            playerNameLabel = null;
        }

        private void BindVisualTreeAndRefresh()
        {
            playerNameLabel = uiDocument?.rootVisualElement?.Q<Label>("player-name-label");
            RefreshDisplayName();
        }

        private void RefreshDisplayName()
        {
            if (playerNameLabel == null) return;
            if (!ShouldDisplayName(
                    identitySource != null && identitySource.IsAvailable,
                    !HideNameWhenDead || healthFeature == null || healthFeature.IsAlive))
            {
                playerNameLabel.style.display = DisplayStyle.None;
                return;
            }

            var displayName = identitySource.DisplayName;
            playerNameLabel.text = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : identitySource.PlayerId is { } playerId ? $"Player{playerId}" : "Player";
            playerNameLabel.style.display = DisplayStyle.Flex;
        }

        private void OnHealthChanged(float currentHealth, float maxHealth) => RefreshDisplayName();

        internal static bool ShouldDisplayName(bool identityAvailable, bool isAlive)
        {
            return identityAvailable && isAlive;
        }

        private bool HideNameWhenDead => presentationSettings == null || presentationSettings.HideNameWhenDead;

        private IPlayerIdentitySource FindIdentitySource()
        {
            var parents = GetComponentsInParent<MonoBehaviour>(true);
            for (var i = 0; i < parents.Length; i++)
                if (parents[i] != null && parents[i] != this && parents[i] is IPlayerIdentitySource source) return source;
            return null;
        }
    }
}
