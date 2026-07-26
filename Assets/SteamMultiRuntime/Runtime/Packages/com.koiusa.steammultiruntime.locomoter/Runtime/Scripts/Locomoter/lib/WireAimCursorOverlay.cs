using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class WireAimCursorOverlay : MonoBehaviour, IScreenAimCursor
    {
        private const float ArmLength = 9f;
        private const float ArmThickness = 2f;
        private const float CenterGap = 3f;

        private Vector2 screenPosition;
        private bool isVisible;

        public void SetPosition(Vector2 position)
        {
            screenPosition = position;
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
        }

        private void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var previousColor = GUI.color;
            GUI.color = new Color(0.2f, 0.9f, 1f, 0.95f);

            DrawRect(guiPosition.x - ArmLength - CenterGap, guiPosition.y - ArmThickness * 0.5f, ArmLength, ArmThickness);
            DrawRect(guiPosition.x + CenterGap, guiPosition.y - ArmThickness * 0.5f, ArmLength, ArmThickness);
            DrawRect(guiPosition.x - ArmThickness * 0.5f, guiPosition.y - ArmLength - CenterGap, ArmThickness, ArmLength);
            DrawRect(guiPosition.x - ArmThickness * 0.5f, guiPosition.y + CenterGap, ArmThickness, ArmLength);

            GUI.color = previousColor;
        }

        private static void DrawRect(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        }
    }
}
