using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetMarkerRandomMover : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float destinationInterval = 2.5f;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.8f;
        [SerializeField, Min(0f)] private float maximumSpeed = 4f;
        [SerializeField] private Vector3 movementRange = new Vector3(3f, 1.5f, 3f);

        private Vector3 origin;
        private Vector3 destination;
        private Vector3 velocity;
        private float nextDestinationTime;

        private void OnEnable()
        {
            origin = transform.position;
            PickDestination();
        }

        private void Update()
        {
            if (Time.time >= nextDestinationTime)
            {
                PickDestination();
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                destination,
                ref velocity,
                smoothTime,
                maximumSpeed,
                Time.deltaTime);
        }

        private void PickDestination()
        {
            destination = origin + new Vector3(
                Random.Range(-movementRange.x, movementRange.x),
                Random.Range(-movementRange.y, movementRange.y),
                Random.Range(-movementRange.z, movementRange.z));
            nextDestinationTime = Time.time + destinationInterval;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.72f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(Application.isPlaying ? origin : transform.position, movementRange * 2f);
        }
    }
}
