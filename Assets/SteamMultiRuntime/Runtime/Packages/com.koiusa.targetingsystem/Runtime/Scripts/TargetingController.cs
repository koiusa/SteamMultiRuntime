using System;
using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetingController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour contextSource;
        [SerializeField] private MonoBehaviour candidateSource;
        [SerializeField] private MonoBehaviour[] filters = Array.Empty<MonoBehaviour>();
        [SerializeField] private MonoBehaviour[] scorers = Array.Empty<MonoBehaviour>();

        [Header("Selection")]
        [SerializeField, Min(1)] private int maximumTargets = 8;

        private readonly List<ITargetable> candidates = new();
        private readonly List<ITargetable> multiAcquisitionCandidates = new();
        private readonly List<ITargetable> selectedTargets = new();
        private readonly List<ITargetFilter> resolvedFilters = new();
        private readonly List<ITargetScorer> resolvedScorers = new();
        private readonly HashSet<ITargetableLifetime> lifetimeSubscriptions = new();
        private ITargetingContextSource resolvedContextSource;
        private ITargetCandidateSource resolvedCandidateSource;
        private ITargetable primaryTarget;
        private TargetingMode mode;
        private uint revision;

        public TargetingState State { get; private set; } = TargetingState.Empty;
        public event Action<TargetingStateChange> StateChanged;

        public void Configure(
            MonoBehaviour newContextSource,
            MonoBehaviour newCandidateSource,
            MonoBehaviour[] newFilters,
            MonoBehaviour[] newScorers)
        {
            contextSource = newContextSource;
            candidateSource = newCandidateSource;
            filters = newFilters ?? Array.Empty<MonoBehaviour>();
            scorers = newScorers ?? Array.Empty<MonoBehaviour>();
            ResolveDependencies();
        }

        private void Awake()
        {
            ResolveDependencies();
            Publish(TargetingChangeReason.Command);
        }

        private void OnDisable()
        {
            ClearInternal(TargetingChangeReason.ControllerDisabled);
            ClearLifetimeSubscriptions();
        }

        public TargetingResult Execute(in TargetingCommand command)
        {
            RemoveInvalidSelections();
            var changed = command.Type switch
            {
                TargetingCommandType.EnterSingle => EnterSingle(),
                TargetingCommandType.EnterMulti => EnterMulti(),
                TargetingCommandType.Clear => ClearInternal(TargetingChangeReason.Command),
                TargetingCommandType.SelectNext => SelectRelative(1),
                TargetingCommandType.SelectPrevious => SelectRelative(-1),
                TargetingCommandType.AddBestCandidate => AddBestCandidate(),
                TargetingCommandType.ToggleBestCandidate => ToggleBestCandidate(),
                TargetingCommandType.RemovePrimary => RemovePrimary(),
                TargetingCommandType.SelectAllCandidates => SelectAllCandidates(),
                _ => false,
            };

            if (changed && command.Type != TargetingCommandType.Clear)
            {
                Publish(TargetingChangeReason.Command);
            }

            return new TargetingResult(changed, State);
        }

        public void Clear() => ClearInternal(TargetingChangeReason.Command);

        public void RefreshSelectedTargets()
        {
            if (RemoveInvalidSelections())
            {
                Publish(TargetingChangeReason.TargetInvalidated);
            }
        }

        private bool EnterSingle()
        {
            multiAcquisitionCandidates.Clear();
            if (mode == TargetingMode.Multi && primaryTarget != null)
            {
                selectedTargets.Clear();
                selectedTargets.Add(primaryTarget);
                mode = TargetingMode.Single;
                return true;
            }

            var best = FindBestCandidate(includeSelected: true);
            if (best == null)
            {
                return false;
            }

            selectedTargets.Clear();
            selectedTargets.Add(best);
            primaryTarget = best;
            mode = TargetingMode.Single;
            return true;
        }

        private bool EnterMulti()
        {
            if (mode == TargetingMode.Single && primaryTarget != null)
            {
                CaptureMultiAcquisitionCandidates();
                mode = TargetingMode.Multi;
                return true;
            }

            var best = FindBestCandidate(includeSelected: true);
            if (best == null)
            {
                return false;
            }

            selectedTargets.Clear();
            selectedTargets.Add(best);
            primaryTarget = best;
            CaptureCurrentCandidatesForMulti();
            mode = TargetingMode.Multi;
            return true;
        }

        private bool AddBestCandidate()
        {
            if (mode != TargetingMode.Multi || selectedTargets.Count >= maximumTargets)
            {
                return false;
            }

            var best = FindBestMultiAcquisitionCandidate(includeSelected: false);
            if (best == null)
            {
                return false;
            }

            selectedTargets.Add(best);
            primaryTarget ??= best;
            return true;
        }

        private bool ToggleBestCandidate()
        {
            if (mode != TargetingMode.Multi)
            {
                return false;
            }

            var best = FindBestMultiAcquisitionCandidate(includeSelected: true);
            if (best == null)
            {
                return false;
            }

            if (selectedTargets.Contains(best))
            {
                return RemoveTarget(best);
            }

            if (selectedTargets.Count >= maximumTargets)
            {
                return false;
            }

            selectedTargets.Add(best);
            primaryTarget ??= best;
            return true;
        }

        private bool SelectAllCandidates()
        {
            if (mode != TargetingMode.Multi || multiAcquisitionCandidates.Count == 0)
            {
                return false;
            }

            var changed = false;
            foreach (var target in multiAcquisitionCandidates)
            {
                if (selectedTargets.Count >= maximumTargets)
                {
                    break;
                }

                if (!selectedTargets.Contains(target))
                {
                    selectedTargets.Add(target);
                    changed = true;
                }
            }

            primaryTarget ??= selectedTargets.Count > 0 ? selectedTargets[0] : null;
            return changed;
        }

        private bool SelectRelative(int offset)
        {
            if (mode == TargetingMode.None)
            {
                return false;
            }

            var source = mode == TargetingMode.Multi ? selectedTargets : GetSortedCandidates();
            if (source.Count == 0)
            {
                return false;
            }

            var currentIndex = primaryTarget != null ? source.IndexOf(primaryTarget) : -1;
            var nextIndex = (currentIndex + offset + source.Count) % source.Count;
            var next = source[nextIndex];
            if (next == primaryTarget)
            {
                return false;
            }

            primaryTarget = next;
            if (mode == TargetingMode.Single)
            {
                selectedTargets.Clear();
                selectedTargets.Add(next);
            }
            return true;
        }

        private List<ITargetable> GetSortedCandidates()
        {
            CollectCandidates();
            SortCandidates();
            return candidates;
        }

        private ITargetable FindBestCandidate(bool includeSelected)
        {
            if (!CollectCandidates())
            {
                return null;
            }

            SortCandidates();
            for (var i = 0; i < candidates.Count; i++)
            {
                if (includeSelected || !selectedTargets.Contains(candidates[i]))
                {
                    return candidates[i];
                }
            }
            return null;
        }

        private ITargetable FindBestMultiAcquisitionCandidate(bool includeSelected)
        {
            for (var i = 0; i < multiAcquisitionCandidates.Count; i++)
            {
                var target = multiAcquisitionCandidates[i];
                if (!IsValid(target)) continue;
                if (includeSelected || !selectedTargets.Contains(target)) return target;
            }
            return null;
        }

        private void CaptureMultiAcquisitionCandidates()
        {
            if (CollectCandidates()) SortCandidates();
            CaptureCurrentCandidatesForMulti();
        }

        private void CaptureCurrentCandidatesForMulti()
        {
            multiAcquisitionCandidates.Clear();
            for (var i = 0; i < candidates.Count; i++)
            {
                var target = candidates[i];
                if (IsValid(target) && !multiAcquisitionCandidates.Contains(target))
                    multiAcquisitionCandidates.Add(target);
            }
            if (IsValid(primaryTarget) && !multiAcquisitionCandidates.Contains(primaryTarget))
                multiAcquisitionCandidates.Insert(0, primaryTarget);
        }

        private bool CollectCandidates()
        {
            candidates.Clear();
            if (resolvedContextSource == null || resolvedCandidateSource == null
                || !resolvedContextSource.TryGetContext(out var context))
            {
                return false;
            }

            resolvedCandidateSource.Collect(context, candidates);
            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                var target = candidates[i];
                if (!IsValid(target) || !Accept(target, context))
                {
                    candidates.RemoveAt(i);
                }
            }
            return candidates.Count > 0;
        }

        private bool Accept(ITargetable target, in TargetingContext context)
        {
            for (var i = 0; i < resolvedFilters.Count; i++)
            {
                if (!resolvedFilters[i].Accept(target, context)) return false;
            }
            return true;
        }

        private void SortCandidates()
        {
            if (resolvedContextSource == null || !resolvedContextSource.TryGetContext(out var context)) return;
            candidates.Sort((left, right) => Score(left, context).CompareTo(Score(right, context)));
        }

        private float Score(ITargetable target, in TargetingContext context)
        {
            var score = 0f;
            for (var i = 0; i < resolvedScorers.Count; i++) score += resolvedScorers[i].Score(target, context);
            return score;
        }

        private bool RemovePrimary() => primaryTarget != null && RemoveTarget(primaryTarget);

        private bool RemoveTarget(ITargetable target)
        {
            if (!selectedTargets.Remove(target)) return false;
            primaryTarget = selectedTargets.Count > 0 ? selectedTargets[0] : null;
            if (selectedTargets.Count == 0) mode = TargetingMode.None;
            return true;
        }

        private bool RemoveInvalidSelections()
        {
            var changed = false;
            for (var i = selectedTargets.Count - 1; i >= 0; i--)
            {
                if (!IsValid(selectedTargets[i]))
                {
                    selectedTargets.RemoveAt(i);
                    changed = true;
                }
            }
            if (!IsValid(primaryTarget))
            {
                primaryTarget = selectedTargets.Count > 0 ? selectedTargets[0] : null;
                changed = true;
            }
            if (selectedTargets.Count == 0 && mode != TargetingMode.None)
            {
                mode = TargetingMode.None;
                changed = true;
            }
            return changed;
        }

        private static bool IsValid(ITargetable target) => target != null && target.IsTargetable && target.Root != null;

        private bool ClearInternal(TargetingChangeReason reason)
        {
            if (mode == TargetingMode.None && selectedTargets.Count == 0 && primaryTarget == null) return false;
            selectedTargets.Clear();
            multiAcquisitionCandidates.Clear();
            primaryTarget = null;
            mode = TargetingMode.None;
            Publish(reason);
            return true;
        }

        private void Publish(TargetingChangeReason reason)
        {
            var previous = State;
            RebuildLifetimeSubscriptions();
            revision++;
            State = new TargetingState(mode, primaryTarget, selectedTargets.ToArray(), revision);
            StateChanged?.Invoke(new TargetingStateChange(previous, State, reason));
        }

        private void RebuildLifetimeSubscriptions()
        {
            ClearLifetimeSubscriptions();
            foreach (var target in selectedTargets)
            {
                if (target is not ITargetableLifetime lifetime || !lifetimeSubscriptions.Add(lifetime)) continue;
                lifetime.Invalidated += OnTargetInvalidated;
            }
        }

        private void ClearLifetimeSubscriptions()
        {
            foreach (var lifetime in lifetimeSubscriptions) lifetime.Invalidated -= OnTargetInvalidated;
            lifetimeSubscriptions.Clear();
        }

        private void OnTargetInvalidated()
        {
            if (RemoveInvalidSelections()) Publish(TargetingChangeReason.TargetInvalidated);
        }

        private void ResolveDependencies()
        {
            resolvedContextSource = contextSource as ITargetingContextSource;
            resolvedCandidateSource = candidateSource as ITargetCandidateSource;
            resolvedFilters.Clear();
            resolvedScorers.Clear();
            foreach (var item in filters) if (item is ITargetFilter filter) resolvedFilters.Add(filter);
            foreach (var item in scorers) if (item is ITargetScorer scorer) resolvedScorers.Add(scorer);
        }
    }
}
