using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerCompassHud : MonoBehaviour
    {
        private const string PanelResourcePath = "UI/PlayerNameOverlay/PlayerNameOverlay Panel Settings";
        private const float TapeWidth = 620f;
        private const float PixelsPerDegree = 4f;
        private const int StepDegrees = 15;
        private const int MarkerCount = 13;

        private readonly List<Marker> markers = new(MarkerCount);
        private UIDocument document;
        private VisualElement tape;
        private Label headingLabel;
        private Camera targetCamera;
        private static PlayerCompassHud instance;

        private sealed class Marker
        {
            public VisualElement Root;
            public VisualElement Tick;
            public Label Label;
        }

        internal static void Show()
        {
            if (instance != null) return;
            var host = new GameObject("[Player Compass HUD]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<PlayerCompassHud>();
        }

        internal static void Hide()
        {
            if (instance == null) return;
            Destroy(instance.gameObject);
            instance = null;
        }

        private void Awake()
        {
            instance = this;
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>(PanelResourcePath);
            document.sortingOrder = short.MaxValue - 1;
            if (document.panelSettings == null)
            {
                Debug.LogError("Compass HUD could not load its PanelSettings.", this);
                enabled = false;
                return;
            }

            BuildUi();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void BuildUi()
        {
            var documentRoot = document.rootVisualElement;
            documentRoot.pickingMode = PickingMode.Ignore;

            var compass = new VisualElement { name = "player-compass", pickingMode = PickingMode.Ignore };
            compass.style.position = Position.Absolute;
            compass.style.top = 18f;
            compass.style.left = new Length(50f, LengthUnit.Percent);
            compass.style.marginLeft = -TapeWidth * 0.5f;
            compass.style.width = TapeWidth;
            compass.style.height = 82f;
            compass.style.overflow = Overflow.Hidden;
            compass.style.backgroundColor = new Color(0.015f, 0.025f, 0.045f, 0.58f);
            compass.style.borderTopLeftRadius = 8f;
            compass.style.borderTopRightRadius = 8f;
            compass.style.borderBottomLeftRadius = 8f;
            compass.style.borderBottomRightRadius = 8f;
            documentRoot.Add(compass);

            tape = new VisualElement { name = "compass-tape", pickingMode = PickingMode.Ignore };
            tape.style.position = Position.Absolute;
            tape.style.left = 0f;
            tape.style.top = 4f;
            tape.style.width = TapeWidth;
            tape.style.height = 50f;
            compass.Add(tape);

            for (var i = 0; i < MarkerCount; i++)
            {
                var marker = CreateMarker();
                markers.Add(marker);
                tape.Add(marker.Root);
            }

            var pointer = new Label("▼") { pickingMode = PickingMode.Ignore };
            pointer.style.position = Position.Absolute;
            pointer.style.left = TapeWidth * 0.5f - 8f;
            pointer.style.top = 0f;
            pointer.style.width = 16f;
            pointer.style.unityTextAlign = TextAnchor.UpperCenter;
            pointer.style.color = new Color(0.2f, 0.9f, 1f, 1f);
            pointer.style.fontSize = 14f;
            compass.Add(pointer);

            headingLabel = new Label("000°") { pickingMode = PickingMode.Ignore };
            headingLabel.style.position = Position.Absolute;
            headingLabel.style.left = TapeWidth * 0.5f - 30f;
            headingLabel.style.top = 56f;
            headingLabel.style.width = 60f;
            headingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            headingLabel.style.color = Color.white;
            headingLabel.style.fontSize = 14f;
            headingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            compass.Add(headingLabel);
        }

        private static Marker CreateMarker()
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.style.position = Position.Absolute;
            root.style.top = 9f;
            root.style.width = 56f;
            root.style.height = 40f;

            var tick = new VisualElement { pickingMode = PickingMode.Ignore };
            tick.style.position = Position.Absolute;
            tick.style.left = 27f;
            tick.style.top = 0f;
            tick.style.width = 2f;
            tick.style.height = 7f;
            tick.style.backgroundColor = new Color(0.72f, 0.82f, 0.9f, 0.85f);
            root.Add(tick);

            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.top = 9f;
            label.style.width = 56f;
            label.style.unityTextAlign = TextAnchor.UpperCenter;
            label.style.color = new Color(0.86f, 0.93f, 1f, 0.95f);
            label.style.fontSize = 12f;
            root.Add(label);
            return new Marker { Root = root, Tick = tick, Label = label };
        }

        private void LateUpdate()
        {
            targetCamera = ResolveCamera(targetCamera);
            if (targetCamera == null || tape == null) return;

            var forward = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) return;
            var heading = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
            headingLabel.text = $"{Mathf.RoundToInt(heading):000}°";

            var centerAngle = Mathf.RoundToInt(heading / StepDegrees) * StepDegrees;
            var firstAngle = centerAngle - (MarkerCount / 2) * StepDegrees;
            for (var i = 0; i < markers.Count; i++)
            {
                var unwrappedAngle = firstAngle + i * StepDegrees;
                var normalizedAngle = ((unwrappedAngle % 360) + 360) % 360;
                var delta = Mathf.DeltaAngle(heading, normalizedAngle);
                var marker = markers[i];
                marker.Root.style.left = TapeWidth * 0.5f + delta * PixelsPerDegree - 28f;
                var isCardinal = normalizedAngle % 45 == 0;
                marker.Label.text = isCardinal ? GetDirectionName(normalizedAngle) : normalizedAngle.ToString();
                marker.Label.style.fontSize = isCardinal ? 16f : 10f;
                marker.Label.style.unityFontStyleAndWeight = isCardinal ? FontStyle.Bold : FontStyle.Normal;
                marker.Tick.style.height = isCardinal ? 11f : 6f;
                marker.Tick.style.backgroundColor = normalizedAngle == 0
                    ? new Color(1f, 0.28f, 0.25f, 1f)
                    : new Color(0.72f, 0.82f, 0.9f, 0.85f);
            }
        }

        private static string GetDirectionName(int degrees)
        {
            return degrees switch
            {
                0 => "N",
                45 => "NE",
                90 => "E",
                135 => "SE",
                180 => "S",
                225 => "SW",
                270 => "W",
                315 => "NW",
                _ => degrees.ToString()
            };
        }

        private static Camera ResolveCamera(Camera current)
        {
            if (current != null && current.isActiveAndEnabled) return current;
            if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;
            return FindFirstObjectByType<Camera>();
        }
    }
}
