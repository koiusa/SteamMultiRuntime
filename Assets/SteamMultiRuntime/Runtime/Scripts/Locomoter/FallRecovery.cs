using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class FallRecovery : MonoBehaviour
    {
        [Header("Recovery")]
        [SerializeField] private float recoverBelowY = -30f;
        [SerializeField] private Transform recoveryPoint;
        [SerializeField] private Vector3 recoveryOffset = new Vector3(0f, 1f, 0f);

        [Header("Events")]
        [SerializeField] private UnityEvent onRecovered;

        private Rigidbody rb;
        private NetworkObject networkObject;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        public event Action Recovered;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            networkObject = GetComponent<NetworkObject>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        private void OnEnable()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (!ShouldProcess())
            {
                return;
            }

            if (transform.position.y >= recoverBelowY)
            {
                return;
            }

            Recover();
        }

        private bool ShouldProcess()
        {
            if (networkObject == null)
            {
                return true;
            }

            if (!networkObject.IsSpawned)
            {
                return false;
            }

            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }

        public void Recover()
        {
            var targetPosition = recoveryPoint != null ? recoveryPoint.position + recoveryOffset : initialPosition + recoveryOffset;
            var targetRotation = recoveryPoint != null ? recoveryPoint.rotation : initialRotation;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = targetPosition;
            rb.rotation = targetRotation;
            rb.Sleep();

            onRecovered?.Invoke();
            Recovered?.Invoke();
        }
    }
}
