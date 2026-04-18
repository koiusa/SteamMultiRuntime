using Unity.Cinemachine;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LobbyCameraMixerWeightController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineMixingCamera mixingCamera;
        [SerializeField] private SteamLobbyService lobbyService;

        [Header("Weight Index")]
        [SerializeField, Min(0)] private int defaultCameraIndex = 0;
        [SerializeField, Min(0)] private int followCameraIndex = 1;

        [Header("Transition")]
        [SerializeField, Min(0f)] private float transitionSpeed = 10f;

        private float targetDefaultWeight;
        private float targetFollowWeight;

        private void Awake()
        {
            if (mixingCamera == null)
            {
                mixingCamera = GetComponent<CinemachineMixingCamera>();
            }

            if (lobbyService == null)
            {
                lobbyService = FindFirstObjectByType<SteamLobbyService>();
            }
        }

        private void OnEnable()
        {
            if (lobbyService != null)
            {
                lobbyService.StateChanged += OnLobbyStateChanged;
            }

            RefreshTargetWeight(true);
        }

        private void OnDisable()
        {
            if (lobbyService != null)
            {
                lobbyService.StateChanged -= OnLobbyStateChanged;
            }
        }

        private void Update()
        {
            if (mixingCamera == null)
            {
                return;
            }

            var t = transitionSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

            var nextDefault = Mathf.Lerp(mixingCamera.GetWeight(defaultCameraIndex), targetDefaultWeight, t);
            var nextFollow = Mathf.Lerp(mixingCamera.GetWeight(followCameraIndex), targetFollowWeight, t);

            mixingCamera.SetWeight(defaultCameraIndex, nextDefault);
            mixingCamera.SetWeight(followCameraIndex, nextFollow);
        }

        private void OnLobbyStateChanged()
        {
            RefreshTargetWeight(false);
        }

        private void RefreshTargetWeight(bool immediate)
        {
            var inLobby = lobbyService != null && lobbyService.IsInLobby;

            targetDefaultWeight = inLobby ? 0f : 1f;
            targetFollowWeight = inLobby ? 1f : 0f;

            if (!immediate || mixingCamera == null)
            {
                return;
            }

            mixingCamera.SetWeight(defaultCameraIndex, targetDefaultWeight);
            mixingCamera.SetWeight(followCameraIndex, targetFollowWeight);
        }
    }
}
