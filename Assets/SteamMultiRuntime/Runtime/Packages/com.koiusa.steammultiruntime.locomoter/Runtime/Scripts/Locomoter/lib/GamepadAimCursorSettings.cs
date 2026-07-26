using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public enum GamepadAimCursorMode
    {
        Relative = 0,
        Absolute = 1,
    }

    [Serializable]
    public sealed class GamepadAimCursorSettings
    {
        [SerializeField] private GamepadAimCursorMode mode = GamepadAimCursorMode.Relative;
        [SerializeField, Min(0.05f)] private float radiusInScreenHeights = 0.3f;
        [SerializeField, Min(0.1f)] private float speedInScreenHeightsPerSecond = 2.5f;
        [SerializeField, Range(1f, 4f)] private float responseExponent = 2f;
        [SerializeField] private bool rememberLastRelativePosition = true;
        [SerializeField] private bool syncSystemPointerPosition = true;

        public GamepadAimCursorMode Mode => mode;
        public float RadiusInScreenHeights => Mathf.Max(0.05f, radiusInScreenHeights);
        public float SpeedInScreenHeightsPerSecond => Mathf.Max(0.1f, speedInScreenHeightsPerSecond);
        public float ResponseExponent => Mathf.Clamp(responseExponent, 1f, 4f);
        public bool RememberLastRelativePosition => rememberLastRelativePosition;
        public bool SyncSystemPointerPosition => syncSystemPointerPosition;
    }
}
