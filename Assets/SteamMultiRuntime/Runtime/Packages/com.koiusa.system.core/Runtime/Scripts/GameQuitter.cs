using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Common.System
{
    public sealed class GameQuitter : MonoBehaviour
    {
        [SerializeField] private InputActionReference gameQuitAction;

        public static event Action QuitRequested;

        private void OnEnable()
        {
            if (gameQuitAction == null || gameQuitAction.action == null)
            {
                Debug.LogWarning("GameQuit Action is not assigned.", this);
                return;
            }

            gameQuitAction.action.Enable();
        }

        private void OnDisable()
        {
            gameQuitAction?.action?.Disable();
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
            return gameQuitAction != null
                && gameQuitAction.action != null
                && gameQuitAction.action.WasPressedThisFrame();
        }
    }
}