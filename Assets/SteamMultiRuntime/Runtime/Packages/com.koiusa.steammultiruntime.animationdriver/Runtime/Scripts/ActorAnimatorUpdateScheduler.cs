using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    internal sealed class ActorAnimatorUpdateScheduler : MonoBehaviour
    {
        private static ActorAnimatorUpdateScheduler instance;
        private readonly List<ActorAnimatorStateDriver> drivers = new(256);
        private readonly Dictionary<ActorAnimatorStateDriver, float> nextUpdates = new(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        internal static void Register(ActorAnimatorStateDriver driver)
        {
            if (driver == null)
                return;
            var scheduler = EnsureInstance();
            if (scheduler.nextUpdates.ContainsKey(driver))
                return;
            scheduler.drivers.Add(driver);
            var phase = (driver.GetInstanceID() & 0x7fffffff) % 997 / 997f;
            scheduler.nextUpdates.Add(driver, Time.unscaledTime + driver.MidAnimationUpdateInterval * phase);
        }

        internal static void Unregister(ActorAnimatorStateDriver driver)
        {
            if (instance == null || !instance.nextUpdates.Remove(driver))
                return;
            instance.drivers.Remove(driver);
        }

        private static ActorAnimatorUpdateScheduler EnsureInstance()
        {
            if (instance != null)
                return instance;
            var host = new GameObject("ActorAnimatorUpdateScheduler");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ActorAnimatorUpdateScheduler>();
            return instance;
        }

        private void Update()
        {
            var camera = Camera.main;
            var hasCamera = camera != null;
            var cameraPosition = hasCamera ? camera.transform.position : Vector3.zero;
            var now = Time.unscaledTime;

            for (var i = drivers.Count - 1; i >= 0; i--)
            {
                var driver = drivers[i];
                if (driver == null)
                {
                    drivers.RemoveAt(i);
                    continue;
                }
                if (!driver.gameObject.activeInHierarchy)
                    continue;

                // Distance alone is insufficient for a crowd: hundreds of actors may be
                // close to the camera while outside its frustum. The former per-driver
                // implementation skipped those actors through Renderer.isVisible. Keep
                // that behavior so the scheduler does not re-enable their Animator graph.
                if (!driver.IsPresentationVisible)
                {
                    driver.SetScheduledAnimatorActive(false);
                    continue;
                }

                var distanceSqr = hasCamera
                    ? (driver.transform.position - cameraPosition).sqrMagnitude
                    : float.PositiveInfinity;
                var nearDistance = driver.NearAnimationDistance;
                var midDistance = driver.MidAnimationDistance;
                if (!hasCamera)
                {
                    driver.SetScheduledAnimatorActive(false);
                    continue;
                }

                if (distanceSqr > midDistance * midDistance)
                {
                    var farInterval = driver.FarAnimationUpdateInterval;
                    if (now < nextUpdates[driver])
                    {
                        driver.SetScheduledAnimatorActive(false);
                        continue;
                    }
                    nextUpdates[driver] = now + farInterval;
                    driver.TickFarScheduled(farInterval);
                    continue;
                }

                driver.SetScheduledAnimatorActive(true);
                var interval = distanceSqr <= nearDistance * nearDistance
                    ? driver.NearAnimationUpdateInterval
                    : driver.MidAnimationUpdateInterval;
                if (now < nextUpdates[driver])
                    continue;
                nextUpdates[driver] = now + interval;
                driver.TickScheduled(interval);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
