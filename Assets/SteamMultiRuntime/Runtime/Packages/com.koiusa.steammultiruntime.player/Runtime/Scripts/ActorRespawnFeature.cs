using System;
using System.Collections;
using Koiusa.SteamMultiRuntime.Core;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealthFeature))]
    public sealed class ActorRespawnFeature : MonoBehaviour, IActorLifeState,
        IActorRespawnPresentationNotifier, ISpawnPoseAppliedReceiver
    {
        [SerializeField, Min(0f)] private float respawnDelay = 3f;

        private ActorHealthFeature health;
        private ActorCharacterCoordinator coordinator;
        private ActorCompositeMotor motor;
        private IActorCombatProcessGate processGate;
        private Rigidbody body;
        private Coroutine respawnRoutine;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private bool initialPoseCaptured;
        private bool bodyWasKinematic;
        private bool motorWasEnabled;
        private bool wasDead;

        public bool IsDead => health != null && !health.IsAlive;
        public event Action<bool> LifeStateChanged;
        public event Action<Vector3, Quaternion> RespawnPresentationReady;

        private void Awake()
        {
            health = GetComponent<ActorHealthFeature>();
            coordinator = GetComponent<ActorCharacterCoordinator>();
            motor = GetComponent<ActorCompositeMotor>();
            processGate = GetComponent<IActorCombatProcessGate>();
            body = GetComponent<Rigidbody>();
            wasDead = !health.IsAlive;
            CaptureSpawnPose();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += OnDied;
                health.HealthChanged += OnHealthChanged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnDied;
                health.HealthChanged -= OnHealthChanged;
            }
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        public void CaptureSpawnPose()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            initialPoseCaptured = true;
        }

        public void OnSpawnPoseApplied(Vector3 position, Quaternion rotation)
        {
            spawnPosition = position;
            spawnRotation = rotation;
            initialPoseCaptured = true;
        }

        private void OnDied(ActorDamageRequest request)
        {
            coordinator?.ResetState();
            if (!CanManageRespawn() || respawnRoutine != null) return;

            if (motor != null)
            {
                motorWasEnabled = motor.enabled;
                motor.enabled = false;
            }

            if (!initialPoseCaptured) CaptureSpawnPose();
            if (body != null)
            {
                bodyWasKinematic = body.isKinematic;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            if (respawnDelay > 0f) yield return new WaitForSeconds(respawnDelay);

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (body != null)
            {
                body.position = spawnPosition;
                body.rotation = spawnRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = bodyWasKinematic;
                if (!body.isKinematic) body.WakeUp();
            }

            if (motor != null) motor.enabled = motorWasEnabled;
            coordinator?.ResetState();
            health.RestoreFullHealth();
            Physics.SyncTransforms();
            respawnRoutine = null;
            RespawnPresentationReady?.Invoke(spawnPosition, spawnRotation);
        }

        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            var isDead = currentHealth <= 0f;
            if (isDead == wasDead) return;
            wasDead = isDead;
            LifeStateChanged?.Invoke(isDead);
        }

        private bool CanManageRespawn()
        {
            return processGate == null || processGate.CanProcessCombat;
        }
    }
}
