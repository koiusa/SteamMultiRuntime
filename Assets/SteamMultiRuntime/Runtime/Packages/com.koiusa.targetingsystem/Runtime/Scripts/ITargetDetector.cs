using System;
using System.Collections.Generic;

namespace Koiusa.TargetingSystem.Runtime
{
    public interface ITargetDetector
    {
        IReadOnlyCollection<ITargetable> Candidates { get; }
        event Action<ITargetable> TargetEntered;
        event Action<ITargetable> TargetExited;

        void Refresh();
    }
}
