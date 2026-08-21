using Koiusa.Input;
using Koiusa.System.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.System.Input
{
    public sealed class GameQuitInputTrigger : MonoBehaviour
    {
        private const string DefaultQuitActionPath = "System/GameQuit";

        [SerializeField] private GameQuitter gameQuitter;
        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private string quitActionPath = DefaultQuitActionPath;

        private InputAction quitAction;
        private InputActionLease quitLease;

        private void OnEnable()
        {
            if (gameQuitter == null)
            {
                Debug.LogWarning("GameQuitter is not assigned.", this);
                return;
            }

            quitAction = inputActionsConfig?.FindAction(quitActionPath);
            if (quitAction == null)
            {
                Debug.LogWarning($"Quit Action '{quitActionPath}' was not found.", this);
                return;
            }

            quitLease = InputActionLease.Acquire(quitAction);
            quitAction.performed += OnQuitPerformed;
        }

        private void OnDisable()
        {
            if (quitAction != null)
            {
                quitAction.performed -= OnQuitPerformed;
            }

            quitLease?.Dispose();
            quitLease = null;
            quitAction = null;
        }

        private void OnQuitPerformed(InputAction.CallbackContext context)
        {
            gameQuitter.RequestQuit();
        }
    }
}
