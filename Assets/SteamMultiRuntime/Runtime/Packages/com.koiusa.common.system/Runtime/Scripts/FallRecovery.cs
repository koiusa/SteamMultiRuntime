using System;
using UnityEngine;
using UnityEngine.Events;

namespace Koiusa.Common.System
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class FallRecovery : MonoBehaviour
    {
        [Header("Recovery")]
        [SerializeField] private float recoverBelowY = -30f;
        [SerializeField] private Transform recoveryPoint;
        [SerializeField] private Vector3 recoveryOffset = new Vector3(0f, 1f, 0f);

        [Header("Process")]
        [SerializeField] private MonoBehaviour processGate;

        [Header("Events")]
        [SerializeField] private UnityEvent onRecovered;

        private Rigidbody rb;
        private IFallRecoveryProcessGate gate;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        public event Action Recovered;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ResolveProcessGate();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        protected virtual void OnEnable()
        {
            ResolveProcessGate();
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

        protected virtual bool ShouldProcess()
        {
            return gate == null || gate.ShouldProcess();
        }

        public virtual void Recover()
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

        private void ResolveProcessGate()
        {
            gate = processGate as IFallRecoveryProcessGate;
        }
    }
}
