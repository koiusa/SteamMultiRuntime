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

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 spawnPosition;
        [SerializeField] private Quaternion spawnRotation = Quaternion.identity;
        [Tooltip("アクティブシーンが変わった時にPlayerを再スポーンする")]
        [SerializeField] private bool respawnOnActiveSceneChanged = true;

        /// <summary>
        /// インスタンス化済みのローカルプレイヤーオブジェクト。
        /// NetworkManager.LocalClient.PlayerObject に相当する。
        /// </summary>
        public GameObject LocalPlayerObject { get; private set; }

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

        private void Start()
        {
            SpawnLocalPlayer();
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
            if (!respawnOnActiveSceneChanged)
            {
                return;
            }

            if (!newScene.IsValid())
            {
                return;
            }

            SpawnLocalPlayer();
        }

        /// <summary>
        /// playerPrefab をインスタンス化してローカルプレイヤーとして登録する。
        /// </summary>
        public void SpawnLocalPlayer()
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning($"[{nameof(LocalManager)}] playerPrefab is not set.");
                return;
            }

            if (LocalPlayerObject != null)
            {
                Destroy(LocalPlayerObject);
                LocalPlayerObject = null;
            }

            LocalPlayerObject = Instantiate(playerPrefab, spawnPosition, spawnRotation);
        }
    }
}
