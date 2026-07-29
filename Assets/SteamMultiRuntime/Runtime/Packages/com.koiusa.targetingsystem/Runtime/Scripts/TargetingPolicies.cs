using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    public readonly struct TargetingContext
    {
        public TargetingContext(Transform owner, Vector3 origin, Vector3 forward, Camera viewCamera)
        {
            Owner = owner;
            Origin = origin;
            Forward = forward;
            ViewCamera = viewCamera;
        }

        public Transform Owner { get; }
        public Vector3 Origin { get; }
        public Vector3 Forward { get; }
        public Camera ViewCamera { get; }
    }

    public interface ITargetingContextSource
    {
        bool TryGetContext(out TargetingContext context);
    }

    public interface ITargetCandidateSource
    {
        void Collect(in TargetingContext context, List<ITargetable> results);
    }

    public interface ITargetFilter
    {
        bool Accept(ITargetable target, in TargetingContext context);
    }

    public interface ITargetScorer
    {
        float Score(ITargetable target, in TargetingContext context);
    }

    public interface ITargetableLifetime
    {
        event System.Action Invalidated;
    }
}
