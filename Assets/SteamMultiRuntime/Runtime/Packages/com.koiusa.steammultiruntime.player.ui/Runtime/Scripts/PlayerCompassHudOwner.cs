using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerCompassHudOwner : MonoBehaviour
    {
        private ILocalPlayerOwnership ownership;
        private bool registered;

        private void Awake()
        {
            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is ILocalPlayerOwnership source)
                {
                    ownership = source;
                    break;
                }
            }
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Update()
        {
            if (!registered) TryRegister();
        }

        private void TryRegister()
        {
            if (ownership != null && ownership.IsOwnershipResolved && ownership.IsLocalOwner)
                Register();
        }

        private void Register()
        {
            if (registered) return;
            registered = true;
            PlayerCompassHud.Show();
        }

        private void OnDisable()
        {
            if (!registered) return;
            registered = false;
            PlayerCompassHud.Hide();
        }
    }
}
