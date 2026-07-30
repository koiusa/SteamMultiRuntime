using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public sealed class AiActorInputSource : IActorInputSource
    {
        private Vector2 _move;
        private bool _jumpRequested;
        private bool _isEnabled;

        public void Enable()
        {
            _isEnabled = true;
        }

        public void Disable()
        {
            _isEnabled = false;
            _move = Vector2.zero;
            _jumpRequested = false;
        }

        public void SetMove(Vector2 move)
        {
            _move = _isEnabled ? Vector2.ClampMagnitude(move, 1f) : Vector2.zero;
        }

        public void QueueJump()
        {
            if (_isEnabled)
            {
                _jumpRequested = true;
            }
        }

        public ActorInputState ReadState()
        {
            if (!_isEnabled)
            {
                return ActorInputState.Empty;
            }

            var jump = _jumpRequested;
            _jumpRequested = false;
            return new ActorInputState(_move, jump);
        }
    }
}
