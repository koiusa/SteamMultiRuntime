using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerCompassHudOwner : MonoBehaviour
    {
        private ILocalPlayerOwnershipNotifier ownership;
        private bool registered;

        private void Awake()
        {
            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is ILocalPlayerOwnershipNotifier source)
                {
                    ownership = source;
                    break;
                }
            }
        }

        private void OnEnable()
        {
            if (ownership != null)
                ownership.OwnershipChanged += OnOwnershipChanged;
            ApplyOwnership();
        }

        private void ApplyOwnership()
        {
            if (ownership != null && ownership.IsOwnershipResolved && ownership.IsLocalOwner)
            {
                Register();
                return;
            }

            Unregister();
        }

        private void Register()
        {
            if (registered) return;
            registered = true;
            PlayerCompassHud.Show();
        }

        private void OnDisable()
        {
            if (ownership != null)
                ownership.OwnershipChanged -= OnOwnershipChanged;
            Unregister();
        }

        private void OnOwnershipChanged()
        {
            ApplyOwnership();
        }

        private void Unregister()
        {
            if (!registered) return;
            registered = false;
            PlayerCompassHud.Hide();
        }
    }
}
