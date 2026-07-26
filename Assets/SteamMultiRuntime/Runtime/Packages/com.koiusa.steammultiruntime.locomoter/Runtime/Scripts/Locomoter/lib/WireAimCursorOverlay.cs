using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class WireAimCursorOverlay : MonoBehaviour, IScreenAimCursor
    {
        private const float ArmLength = 9f;
        private const float ArmThickness = 2f;
        private const float ClosedCenterGap = 3f;
        private const float OpenCenterGap = 16f;
        private const float CloseDelay = 0.08f;
        private const float CloseDuration = 0.22f;

        private Vector2 screenPosition;
        private bool isVisible;
        private float shownAt;

        public void SetPosition(Vector2 position)
        {
            screenPosition = position;
        }

        public void SetVisible(bool visible)
        {
            if (visible && !isVisible)
            {
                shownAt = Time.unscaledTime;
            }

            isVisible = visible;
        }

        private void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var closeProgress = Mathf.Clamp01((Time.unscaledTime - shownAt - CloseDelay) / CloseDuration);
            closeProgress = closeProgress * closeProgress * (3f - 2f * closeProgress);
            var centerGap = Mathf.Lerp(OpenCenterGap, ClosedCenterGap, closeProgress);
            var previousColor = GUI.color;
            GUI.color = new Color(0.2f, 0.9f, 1f, 0.95f);

            DrawRect(guiPosition.x - ArmLength - centerGap, guiPosition.y - ArmThickness * 0.5f, ArmLength, ArmThickness);
            DrawRect(guiPosition.x + centerGap, guiPosition.y - ArmThickness * 0.5f, ArmLength, ArmThickness);
            DrawRect(guiPosition.x - ArmThickness * 0.5f, guiPosition.y - ArmLength - centerGap, ArmThickness, ArmLength);
            DrawRect(guiPosition.x - ArmThickness * 0.5f, guiPosition.y + centerGap, ArmThickness, ArmLength);

            GUI.color = previousColor;
        }

        private static void DrawRect(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        }
    }
}
