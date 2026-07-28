using System.Reflection;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerCompassHudOwner : MonoBehaviour
    {
        private Component networkObject;
        private PropertyInfo isSpawnedProperty;
        private PropertyInfo isOwnerProperty;
        private bool registered;

        private void Awake()
        {
            var components = GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var candidate = components[i];
                if (candidate == null || candidate.GetType().FullName != "Unity.Netcode.NetworkObject") continue;
                networkObject = candidate;
                isSpawnedProperty = candidate.GetType().GetProperty("IsSpawned");
                isOwnerProperty = candidate.GetType().GetProperty("IsOwner");
                break;
            }
        }

        private void OnEnable()
        {
            if (networkObject == null) Register();
        }

        private void Update()
        {
            if (registered || networkObject == null) return;
            if (ReadBoolean(isSpawnedProperty) && ReadBoolean(isOwnerProperty)) Register();
        }

        private bool ReadBoolean(PropertyInfo property)
        {
            return property != null && property.GetValue(networkObject) is true;
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
