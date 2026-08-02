using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IGroundMotionPresentationUpdater
    {
        void TickGroundMotionPresentation();
    }

    /// <summary>
    /// Applies moving-floor presentation before actor presentation in one Update.
    /// Physics ownership remains in FixedUpdate; this scheduler only orders visuals.
    /// </summary>
    public sealed class GroundMotionPresentationScheduler : MonoBehaviour
    {
        private static GroundMotionPresentationScheduler instance;
        private readonly List<IGroundMotionPresentationUpdater> sources = new(8);
        private readonly List<PhysicsPresentationSmoother> actors = new(256);

        public static void RegisterSource(IGroundMotionPresentationUpdater source)
        {
            if (source == null)
                return;
            var scheduler = EnsureInstance();
            if (!scheduler.sources.Contains(source))
                scheduler.sources.Add(source);
        }

        public static void UnregisterSource(IGroundMotionPresentationUpdater source)
        {
            if (instance != null && source != null)
                instance.sources.Remove(source);
        }

        internal static void RegisterActor(PhysicsPresentationSmoother actor)
        {
            if (actor == null)
                return;
            var scheduler = EnsureInstance();
            if (!scheduler.actors.Contains(actor))
                scheduler.actors.Add(actor);
        }

        internal static void UnregisterActor(PhysicsPresentationSmoother actor)
        {
            if (instance != null && actor != null)
                instance.actors.Remove(actor);
        }

        private static GroundMotionPresentationScheduler EnsureInstance()
        {
            if (instance != null)
                return instance;
            var host = new GameObject("GroundMotionPresentationScheduler");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            instance = host.AddComponent<GroundMotionPresentationScheduler>();
            return instance;
        }

        private void Update()
        {
            for (var i = sources.Count - 1; i >= 0; i--)
            {
                var source = sources[i];
                if (source == null)
                {
                    sources.RemoveAt(i);
                    continue;
                }
                source.TickGroundMotionPresentation();
            }

            for (var i = actors.Count - 1; i >= 0; i--)
            {
                var actor = actors[i];
                if (actor == null)
                {
                    actors.RemoveAt(i);
                    continue;
                }
                if (actor.isActiveAndEnabled)
                    actor.TickPresentation();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (instance != null)
                Destroy(instance.gameObject);
            instance = null;
        }

        private void OnDestroy()
        {
            sources.Clear();
            actors.Clear();
            if (instance == this)
                instance = null;
        }
    }
}
