using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class WireAimCursorOverlay : MonoBehaviour, IScreenAimCursor
    {
        private const float LineThickness = 2f;
        private const float CrossArmLength = 9f;
        private const float ClosedCrossGap = 3f;
        private const float OpenCrossGap = 16f;
        private const float ClosedHalfSize = 7f;
        private const float OpenHalfSize = 18f;
        private const float CloseDelay = 0.08f;
        private const float CloseDuration = 0.22f;

        private Vector2 screenPosition;
        private bool isVisible;
        private bool isAiming;
        private float aimStartedAt;
        private ScreenAimTargetState targetState = ScreenAimTargetState.Invalid;

        public void SetPosition(Vector2 position)
        {
            screenPosition = position;
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
        }

        public void SetAiming(bool aiming)
        {
            if (aiming && !isAiming)
            {
                aimStartedAt = Time.unscaledTime;
            }

            isAiming = aiming;
        }

        public void SetTargetState(ScreenAimTargetState state)
        {
            targetState = state;
        }

        private void OnGUI()
        {
            // The gameplay cursor must not participate in IMGUI event processing while
            // a screen-space menu owns the visible system pointer.
            if (!isVisible || Cursor.visible)
            {
                return;
            }

            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var closeProgress = isAiming
                ? Mathf.Clamp01((Time.unscaledTime - aimStartedAt - CloseDelay) / CloseDuration)
                : 0f;
            closeProgress = closeProgress * closeProgress * (3f - 2f * closeProgress);
            var halfSize = Mathf.Lerp(OpenHalfSize, ClosedHalfSize, closeProgress);
            var crossGap = Mathf.Lerp(OpenCrossGap, ClosedCrossGap, closeProgress);
            var previousColor = GUI.color;
            GUI.color = targetState switch
            {
                ScreenAimTargetState.Valid => new Color(0.2f, 0.9f, 1f, 0.95f),
                ScreenAimTargetState.Obstructed => new Color(1f, 0.58f, 0.1f, 0.95f),
                _ => new Color(1f, 0.2f, 0.15f, 0.95f),
            };

            if (targetState != ScreenAimTargetState.Invalid)
            {
                if (targetState == ScreenAimTargetState.Valid)
                {
                    DrawCross(guiPosition, crossGap);
                }
                else
                {
                    DrawDiamond(guiPosition, halfSize);
                    DrawRotatedLine(guiPosition, halfSize * 2.4f, -45f);
                }
            }
            else
            {
                DrawDiamond(guiPosition, OpenHalfSize);
            }

            GUI.color = previousColor;
        }

        private static void DrawRect(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        }

        private static void DrawRotatedLine(Vector2 center, float length, float angle)
        {
            var previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);
            DrawRect(center.x - length * 0.5f, center.y - LineThickness * 0.5f, length, LineThickness);
            GUI.matrix = previousMatrix;
        }

        private static void DrawCross(Vector2 center, float gap)
        {
            DrawRect(center.x - CrossArmLength - gap, center.y - LineThickness * 0.5f, CrossArmLength, LineThickness);
            DrawRect(center.x + gap, center.y - LineThickness * 0.5f, CrossArmLength, LineThickness);
            DrawRect(center.x - LineThickness * 0.5f, center.y - CrossArmLength - gap, LineThickness, CrossArmLength);
            DrawRect(center.x - LineThickness * 0.5f, center.y + gap, LineThickness, CrossArmLength);
        }

        private static void DrawDiamond(Vector2 center, float halfSize)
        {
            var sideLength = halfSize * 1.41421356f;
            DrawRotatedLine(center + new Vector2(halfSize * 0.5f, -halfSize * 0.5f), sideLength, 45f);
            DrawRotatedLine(center + new Vector2(halfSize * 0.5f, halfSize * 0.5f), sideLength, -45f);
            DrawRotatedLine(center + new Vector2(-halfSize * 0.5f, halfSize * 0.5f), sideLength, 45f);
            DrawRotatedLine(center + new Vector2(-halfSize * 0.5f, -halfSize * 0.5f), sideLength, -45f);
        }
    }
}
