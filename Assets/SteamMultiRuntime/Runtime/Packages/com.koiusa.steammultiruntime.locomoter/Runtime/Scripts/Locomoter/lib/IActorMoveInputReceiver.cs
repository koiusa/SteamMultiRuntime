using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IActorMoveInputReceiver
    {
        void SetMoveInput(Vector2 moveInput);
        void SetMoveReferenceRotation(Quaternion referenceRotation);
    }
}
