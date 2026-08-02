using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Resolves typed ground-motion contracts once per contacted Transform and shares
    /// the result between dynamic actors and the Crowd backend.
    /// </summary>
    public static class GroundMotionSourceResolver
    {
        private readonly struct Sources
        {
            internal Sources(IGroundMotionSource motion, IGroundMotionSnapshotSource snapshot)
            {
                Motion = motion;
                Snapshot = snapshot;
            }

            internal IGroundMotionSource Motion { get; }
            internal IGroundMotionSnapshotSource Snapshot { get; }
        }

        private static readonly Dictionary<Transform, Sources> Cache = new();

        public static void Resolve(
            Transform groundTransform,
            out IGroundMotionSource motion,
            out IGroundMotionSnapshotSource snapshot)
        {
            if (groundTransform == null)
            {
                motion = null;
                snapshot = null;
                return;
            }
            if (!Cache.TryGetValue(groundTransform, out var sources))
            {
                snapshot = groundTransform.GetComponentInParent<IGroundMotionSnapshotSource>();
                motion = snapshot as IGroundMotionSource
                    ?? groundTransform.GetComponentInParent<IGroundMotionSource>();
                sources = new Sources(motion, snapshot);
                Cache.Add(groundTransform, sources);
                return;
            }
            motion = sources.Motion;
            snapshot = sources.Snapshot;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => Cache.Clear();
    }
}
