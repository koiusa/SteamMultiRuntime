using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerMoveInputReceiver
    {
        void SetMoveInput(Vector2 moveInput);
        void SetMoveReferenceRotation(Quaternion referenceRotation);
    }
}
