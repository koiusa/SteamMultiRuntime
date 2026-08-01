using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class NpcCrowdTraversalInput : MonoBehaviour
    {
        private Vector2 move;
        private bool overrideNavigation;
        private bool jumpRequested;
        private bool grappleHeld;
        private bool grappleFireRequested;
        private bool wireInputDirty;
        private float reelInput;
        private Vector3 grappleTargetPoint;
        private SlopeContactResolver slopeContacts;
        private ILadderTraversalFeature ladderFeature;
        private bool hasWallContact;
        private bool hasAppliedWallContact;
        private Vector3 wallNormal;
        private int syntheticContactId;
        private bool hasLadderIntent;

        internal bool HasPendingInput => overrideNavigation || jumpRequested
            || grappleHeld || grappleFireRequested || wireInputDirty || Mathf.Abs(reelInput) > 0.0001f
            || hasWallContact || hasAppliedWallContact;
        internal bool HasWallIntent => hasWallContact;
        internal bool HasLadderIntent => hasLadderIntent;

        private void Awake()
        {
            slopeContacts = GetComponent<SlopeContactResolver>();
            ladderFeature = GetComponent<ILadderTraversalFeature>();
            syntheticContactId = GetInstanceID();
        }

        public void SetMove(Vector2 value, bool overrideNavMovement = true)
        {
            move = Vector2.ClampMagnitude(value, 1f);
            overrideNavigation = overrideNavMovement;
        }

        public void QueueJump() => jumpRequested = true;

        public void SetWire(bool held, bool fireRequested, float reel, Vector3 targetPoint)
        {
            grappleHeld = held;
            grappleFireRequested |= fireRequested;
            wireInputDirty = true;
            reelInput = Mathf.Clamp(reel, -1f, 1f);
            grappleTargetPoint = targetPoint;
        }

        public void SetWallContact(Vector3 normal, bool active = true)
        {
            hasWallContact = active && normal.sqrMagnitude > 0.0001f;
            wallNormal = hasWallContact ? normal.normalized : Vector3.zero;
        }

        public void EnterLadder(LadderVolume ladder)
        {
            hasLadderIntent = ladder != null;
            ladderFeature?.NotifyEnterLadder(ladder);
        }

        public void ExitLadder(LadderVolume ladder)
        {
            ladderFeature?.NotifyExitLadder(ladder);
            hasLadderIntent = false;
        }

        public void DetachFromLadder(float reattachDelaySeconds)
        {
            ladderFeature?.DetachFromLadder(reattachDelaySeconds);
            hasLadderIntent = false;
        }

        public void ClearMoveOverride()
        {
            move = Vector2.zero;
            overrideNavigation = false;
        }

        internal void Consume(ref Vector2 navMove, ref bool jump, out bool wireHeld,
            out bool wireFire, out float reel, out Vector3 targetPoint)
        {
            if (hasWallContact)
            {
                slopeContacts?.SetSyntheticObstacleContact(syntheticContactId, wallNormal);
                hasAppliedWallContact = true;
            }
            else
            {
                slopeContacts?.RemoveSyntheticContact(syntheticContactId);
                hasAppliedWallContact = false;
            }
            if (overrideNavigation)
                navMove = move;
            jump |= jumpRequested;
            jumpRequested = false;
            wireHeld = grappleHeld;
            wireFire = grappleFireRequested;
            grappleFireRequested = false;
            wireInputDirty = false;
            reel = reelInput;
            targetPoint = grappleTargetPoint;
        }

        private void OnDisable()
        {
            slopeContacts?.RemoveSyntheticContact(syntheticContactId);
            hasWallContact = false;
            hasAppliedWallContact = false;
            wallNormal = Vector3.zero;
            grappleHeld = false;
            grappleFireRequested = false;
            wireInputDirty = false;
            reelInput = 0f;
            hasLadderIntent = false;
        }
    }
}
