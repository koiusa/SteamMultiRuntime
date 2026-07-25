using System;
using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Common.System
{
    public sealed class GameQuitter : MonoBehaviour
    {
        [SerializeField] private InputActionAssetProfile inputProfile;

        private InputAction quitAction;
        private InputActionLease quitLease;

        public static event Action QuitRequested;

        private void OnEnable()
        {
            quitAction = inputProfile?.FindAction("UI/Cancel");
            if (quitAction == null)
            {
                Debug.LogWarning("GameQuit Action is not assigned.", this);
                return;
            }

            quitLease = InputActionLease.Acquire(quitAction);
        }

        private void OnDisable()
        {
            quitLease?.Dispose();
            quitLease = null;
            quitAction = null;
        }

        private void Update()
        {
            if (!IsGameQuitPressed())
            {
                return;
            }

            QuitRequested?.Invoke();
            Application.Quit();
        }

        private bool IsGameQuitPressed()
        {
            return quitAction != null && quitAction.WasPressedThisFrame();
        }
    }
}
