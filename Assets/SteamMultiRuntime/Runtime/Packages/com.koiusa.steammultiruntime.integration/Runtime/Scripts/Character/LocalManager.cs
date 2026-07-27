using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// ローカルプレイヤー用のマネージャー。
    /// NetworkManager と同様に PlayerPrefab を保持・インスタンス化し、
    /// LocalPlayerObject として他のコンポーネントから参照できるようにする。
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalManager : MonoBehaviour
    {
        public static LocalManager Singleton { get; private set; }

        [Header("Player Prefab")]
        [Tooltip("ローカルプレイヤーとしてインスタンス化するPrefab")]
        [SerializeField] private GameObject playerPrefab;

        /// <summary>
        /// インスタンス化済みのローカルプレイヤーオブジェクト。
        /// NetworkManager.LocalClient.PlayerObject に相当する。
        /// </summary>
        public GameObject LocalPlayerObject { get; private set; }

        /// <summary>
        /// Playerがスポーンされた時に発火する。引数はスポーンされたPlayerObject。
        /// </summary>
        public event Action<GameObject> PlayerSpawned;

        private void Awake()
        {
            if (Singleton != null && Singleton != this)
            {
                Destroy(gameObject);
                return;
            }

            Singleton = this;
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            if (Singleton == this)
            {
                Singleton = null;
            }
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            if (!newScene.IsValid())
            {
                return;
            }

            SpawnLocalPlayer(newScene);
        }

        /// <summary>
        /// playerPrefab をインスタンス化してローカルプレイヤーとして登録する。
        /// </summary>
        public void SpawnLocalPlayer()
        {
            SpawnLocalPlayer(SceneManager.GetActiveScene());
        }

        private void SpawnLocalPlayer(Scene scene)
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning($"[{nameof(LocalManager)}] playerPrefab is not set.");
                return;
            }

            if (!PlayerSpawnService.TryResolvePose(scene, 0, out var spawnPosition, out var spawnRotation))
            {
                return;
            }

            if (LocalPlayerObject != null)
            {
                // Destroy は実フレーム末まで遅延するため、旧プレイヤーが新プレイヤーと
                // 物理的に干渉しないよう先に無効化してから破棄する。
                LocalPlayerObject.SetActive(false);
                Destroy(LocalPlayerObject);
                LocalPlayerObject = null;
            }

            LocalPlayerObject = Instantiate(
                playerPrefab,
                spawnPosition,
                spawnRotation);
            PlayerSpawnService.Place(LocalPlayerObject, spawnPosition, spawnRotation);
            PlayerSpawned?.Invoke(LocalPlayerObject);
        }
    }
}
