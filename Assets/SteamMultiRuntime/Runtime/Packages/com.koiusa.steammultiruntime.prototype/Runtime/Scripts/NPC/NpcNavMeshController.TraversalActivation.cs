using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public partial class NpcNavMeshController
    {
        private static readonly HashSet<string> OnDemandWireFeatureNames = new()
        {
            "WireLineVisualFeature",
            "WireTraversalFeature",
            "WireGrappleTargetingFeature",
            "WireAttachAction",
            "WireSwingAction",
            "WireReelAction",
            "WireGroundAction"
        };
        private static readonly HashSet<string> OnDemandWallFeatureNames = new()
        {
            "WallTraversalFeature", "WallRunAction", "WallSlideAction", "WallJumpAction"
        };
        private static readonly HashSet<string> OnDemandLadderFeatureNames = new()
        {
            "LadderTraversalFeature", "LadderClimbAction", "LadderDetachAction"
        };

        private Behaviour[] _onDemandWireFeatures;
        private IWireConnection _onDemandWireConnection;
        private bool _useOnDemandWireActivation;
        private bool _onDemandWireFeaturesActive;
        private Behaviour[] _onDemandWallFeatures;
        private Behaviour[] _onDemandLadderFeatures;
        private bool _onDemandWallFeaturesActive;
        private bool _onDemandLadderFeaturesActive;
        private Behaviour _onDemandTraversalCoordinator;

        private void InitializeOnDemandTraversalFeatures()
        {
            // Conventional authority enables concrete Wire actions only while pseudo-input
            // requests them. Remote clients restore the features before this controller is
            // disabled so replicated presentation can continue independently.
            _useOnDemandWireActivation = !useCrowdSimulation;
            if (!_useOnDemandWireActivation)
                return;

            _onDemandWireConnection = GetComponent<IWireConnection>();
            _onDemandTraversalCoordinator = _traversalCoordinator as Behaviour;
            var behaviours = GetComponents<Behaviour>();
            var features = new List<Behaviour>(OnDemandWireFeatureNames.Count);
            var wallFeatures = new List<Behaviour>(OnDemandWallFeatureNames.Count);
            var ladderFeatures = new List<Behaviour>(OnDemandLadderFeatureNames.Count);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour != null && OnDemandWireFeatureNames.Contains(behaviour.GetType().Name))
                    features.Add(behaviour);
                if (behaviour != null && OnDemandWallFeatureNames.Contains(behaviour.GetType().Name))
                    wallFeatures.Add(behaviour);
                if (behaviour != null && OnDemandLadderFeatureNames.Contains(behaviour.GetType().Name))
                    ladderFeatures.Add(behaviour);
            }
            _onDemandWireFeatures = features.ToArray();
            _onDemandWallFeatures = wallFeatures.ToArray();
            _onDemandLadderFeatures = ladderFeatures.ToArray();
            _onDemandWireFeaturesActive = true;
            _onDemandWallFeaturesActive = true;
            _onDemandLadderFeaturesActive = true;
            SetOnDemandWireFeaturesActive(false);
            SetFeaturesActive(_onDemandWallFeatures, ref _onDemandWallFeaturesActive, false);
            SetFeaturesActive(_onDemandLadderFeatures, ref _onDemandLadderFeaturesActive, false);
            if (_onDemandTraversalCoordinator != null)
                _onDemandTraversalCoordinator.enabled = false;
        }

        private void UpdateOnDemandWireFeatures(bool hasWireIntent)
        {
            if (_useOnDemandWireActivation && hasWireIntent)
                SetOnDemandWireFeaturesActive(true);
        }

        private void SuspendDetachedOnDemandWireFeatures(bool hasWireIntent)
        {
            if (_useOnDemandWireActivation && !hasWireIntent && _onDemandWireFeaturesActive
                && (_onDemandWireConnection == null || !_onDemandWireConnection.IsAttached))
                SetOnDemandWireFeaturesActive(false);
        }

        private void RestoreOnDemandTraversalFeatures()
        {
            if (_useOnDemandWireActivation)
            {
                SetOnDemandWireFeaturesActive(true);
                SetFeaturesActive(_onDemandWallFeatures, ref _onDemandWallFeaturesActive, true);
                SetFeaturesActive(_onDemandLadderFeatures, ref _onDemandLadderFeaturesActive, true);
                if (_onDemandTraversalCoordinator != null)
                    _onDemandTraversalCoordinator.enabled = true;
            }
        }

        private void UpdateOnDemandTraversalActivity(
            bool hasWireIntent,
            bool hasWallIntent,
            bool hasLadderIntent)
        {
            if (!_useOnDemandWireActivation)
                return;

            var state = _traversalCoordinator != null
                ? _traversalCoordinator.CurrentState
                : ActorTraversalState.Grounded;
            var wallActive = hasWallIntent
                || state == ActorTraversalState.WallRun
                || state == ActorTraversalState.WallSlide
                || state == ActorTraversalState.WallJump;
            var ladderActive = hasLadderIntent || state == ActorTraversalState.Ladder;
            var wireActive = hasWireIntent
                || (_onDemandWireConnection != null && _onDemandWireConnection.IsAttached);

            SetFeaturesActive(_onDemandWallFeatures, ref _onDemandWallFeaturesActive, wallActive);
            SetFeaturesActive(_onDemandLadderFeatures, ref _onDemandLadderFeaturesActive, ladderActive);
            if (wireActive)
                SetOnDemandWireFeaturesActive(true);
            if (_onDemandTraversalCoordinator != null)
                _onDemandTraversalCoordinator.enabled = wallActive || ladderActive || wireActive;
        }

        private static void SetFeaturesActive(Behaviour[] features, ref bool current, bool active)
        {
            if (features == null || current == active)
                return;
            current = active;
            for (var i = 0; i < features.Length; i++)
                if (features[i] != null)
                    features[i].enabled = active;
        }

        private void SetOnDemandWireFeaturesActive(bool active)
        {
            if (_onDemandWireFeatures == null || _onDemandWireFeaturesActive == active)
                return;
            _onDemandWireFeaturesActive = active;
            for (var i = 0; i < _onDemandWireFeatures.Length; i++)
            {
                if (_onDemandWireFeatures[i] != null)
                    _onDemandWireFeatures[i].enabled = active;
            }
        }
    }
}
