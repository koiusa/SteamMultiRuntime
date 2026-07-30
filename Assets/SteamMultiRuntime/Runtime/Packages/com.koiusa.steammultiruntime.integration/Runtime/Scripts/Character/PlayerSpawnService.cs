using System.Collections.Generic;
using Koiusa.SteamMultiRuntime.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Shared spawn-point resolution and placement for local and network players.</summary>
    public static class PlayerSpawnService
    {
        private const float FallbackClearance = 0.5f;
        private const float RaycastMargin = 10f;

        public static bool TryPlace(GameObject player, Scene scene, ulong playerIndex = 0)
        {
            if (player == null || !TryResolvePose(scene, playerIndex, out var position, out var rotation))
            {
                return false;
            }

            Place(player, position, rotation);
            return true;
        }

        public static bool HasSpawnPoint(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<PlayerSpawnPoint>(includeInactive: false) != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static void Place(GameObject player, PlayerSpawnPoint spawnPoint)
        {
            Place(player, spawnPoint.transform.position, spawnPoint.transform.rotation);
        }

        public static void Place(GameObject player, Vector3 position, Quaternion rotation)
        {
            player.transform.SetPositionAndRotation(position, rotation);

            var rigidbodies = player.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
                var body = rigidbodies[i];
                if (body.transform == player.transform)
                {
                    body.position = position;
                    body.rotation = rotation;
                }

                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.WakeUp();
                }
            }

            Physics.SyncTransforms();

            var components = player.GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
                if (components[i] is ISpawnPoseAppliedReceiver receiver)
                    receiver.OnSpawnPoseApplied(position, rotation);
        }

        public static bool TryResolvePose(
            Scene scene,
            ulong playerIndex,
            out Vector3 position,
            out Quaternion rotation)
        {
            if (TryResolve(scene, playerIndex, out var spawnPoint))
            {
                position = spawnPoint.transform.position;
                rotation = spawnPoint.transform.rotation;
                return true;
            }

            return TryResolveGroundFallback(scene, out position, out rotation);
        }

        public static bool TryResolve(Scene scene, ulong playerIndex, out PlayerSpawnPoint spawnPoint)
        {
            spawnPoint = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            var points = new List<PlayerSpawnPoint>();
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                points.AddRange(roots[i].GetComponentsInChildren<PlayerSpawnPoint>(includeInactive: false));
            }

            if (points.Count == 0)
            {
                return false;
            }

            spawnPoint = points[(int)(playerIndex % (ulong)points.Count)];
            return true;
        }

        private static bool TryResolveGroundFallback(
            Scene scene,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            Physics.SyncTransforms();
            Collider largestGroundCandidate = null;
            var largestHorizontalArea = 0f;
            var highestPoint = float.MinValue;
            var lowestPoint = float.MaxValue;
            var roots = scene.GetRootGameObjects();

            for (var i = 0; i < roots.Length; i++)
            {
                var colliders = roots[i].GetComponentsInChildren<Collider>(includeInactive: false);
                for (var j = 0; j < colliders.Length; j++)
                {
                    var candidate = colliders[j];
                    if (!candidate.enabled || candidate.isTrigger)
                    {
                        continue;
                    }

                    var bounds = candidate.bounds;
                    highestPoint = Mathf.Max(highestPoint, bounds.max.y);
                    lowestPoint = Mathf.Min(lowestPoint, bounds.min.y);
                    var horizontalArea = bounds.size.x * bounds.size.z;
                    if (horizontalArea > largestHorizontalArea)
                    {
                        largestHorizontalArea = horizontalArea;
                        largestGroundCandidate = candidate;
                    }
                }
            }

            if (largestGroundCandidate == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerSpawnService)}] Scene '{scene.name}' has neither a spawn point nor " +
                    "a usable collider. Player placement was cancelled.");
                return false;
            }

            var candidateBounds = largestGroundCandidate.bounds;
            var preferredOrigin = new Vector3(0f, highestPoint + RaycastMargin, 0f);
            if (!TryRaycastScene(scene, preferredOrigin, lowestPoint, out var hit))
            {
                var fallbackOrigin = new Vector3(
                    candidateBounds.center.x,
                    highestPoint + RaycastMargin,
                    candidateBounds.center.z);
                if (!TryRaycastScene(scene, fallbackOrigin, lowestPoint, out hit))
                {
                    Debug.LogError(
                        $"[{nameof(PlayerSpawnService)}] Could not find a walkable surface in scene " +
                        $"'{scene.name}'. Player placement was cancelled.");
                    return false;
                }
            }

            position = hit.point + Vector3.up * FallbackClearance;
            Debug.LogWarning(
                $"[{nameof(PlayerSpawnService)}] Scene '{scene.name}' has no {nameof(PlayerSpawnPoint)}; " +
                $"using detected ground at {position}.");
            return true;
        }

        private static bool TryRaycastScene(
            Scene scene,
            Vector3 origin,
            float lowestPoint,
            out RaycastHit selectedHit)
        {
            selectedHit = default;
            var distance = Mathf.Max(RaycastMargin * 2f, origin.y - lowestPoint + RaycastMargin);
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                distance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            var found = false;
            var bestY = float.MinValue;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider.gameObject.scene != scene || hit.normal.y <= 0.5f || hit.point.y <= bestY)
                {
                    continue;
                }

                selectedHit = hit;
                bestY = hit.point.y;
                found = true;
            }

            return found;
        }
    }
}
