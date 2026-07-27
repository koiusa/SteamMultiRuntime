using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// 生成したGameObjectを所有し、生成方式に応じた方法でまとめて破棄する。
    /// </summary>
    public sealed class SpawnedObjectCollection
    {
        private readonly List<GameObject> instances = new();

        public int Count => instances.Count;

        public void Add(GameObject instance)
        {
            if (instance != null && !instances.Contains(instance))
            {
                instances.Add(instance);
            }
        }

        public void DestroyAll()
        {
            for (var i = instances.Count - 1; i >= 0; i--)
            {
                DestroyInstance(instances[i]);
            }

            instances.Clear();
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                // NetworkObjectの破棄はサーバーを唯一の起点にする。
                if (networkObject.NetworkManager != null && networkObject.NetworkManager.IsServer)
                {
                    networkObject.Despawn(true);
                }

                return;
            }

            Object.Destroy(instance);
        }
    }
}
