using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class GroundContactDebugDisplay : MonoBehaviour
    {
        private sealed class ContactSample
        {
            public Collider Collider;
            public Transform GroundTransform;
            public Vector3 Point;
            public Vector3 Normal;
            public float UpDot;
            public int Layer;
            public bool InGroundLayer;
            public bool IsGround;
        }

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private Vector3 upAxis = Vector3.up;
        [SerializeField, Range(-1f, 1f)] private float minGroundNormalDot = 0.55f;

        [Header("Debug")]
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private float normalLength = 0.3f;
        [SerializeField] private Color validGroundColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color invalidGroundColor = new Color(1f, 0.3f, 0.2f, 1f);

        [Header("Overlay")]
        [SerializeField] private Vector2 windowPosition = new Vector2(12f, 12f);
        [SerializeField] private Vector2 windowSize = new Vector2(420f, 320f);
        [SerializeField] private int maxRows = 8;
        [SerializeField] private bool placeWindowTopRightOnStart = true;
        [SerializeField] private bool aggregateFromAllInstances = true;

        private static readonly List<GroundContactDebugDisplay> ActiveInstances = new List<GroundContactDebugDisplay>();
        private static int selectedInstanceIndex;

        private readonly List<ContactSample> contacts = new List<ContactSample>();
        private Rect windowRect;
        private Vector2 scrollPosition;
        private int lastCapturedFrame = -1;

        private void Awake()
        {
            if (placeWindowTopRightOnStart)
            {
                var rightX = Mathf.Max(12f, Screen.width - windowSize.x - 12f);
                windowRect = new Rect(rightX, 12f, windowSize.x, windowSize.y);
            }
            else
            {
                windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
            }

            NormalizeUpAxis();
        }

        private void OnEnable()
        {
            if (!ActiveInstances.Contains(this))
            {
                ActiveInstances.Add(this);
            }
        }

        private void OnValidate()
        {
            NormalizeUpAxis();
        }

        private void OnDisable()
        {
            var removedIndex = ActiveInstances.IndexOf(this);
            if (removedIndex >= 0)
            {
                ActiveInstances.RemoveAt(removedIndex);
                if (selectedInstanceIndex >= ActiveInstances.Count)
                {
                    selectedInstanceIndex = ActiveInstances.Count > 0 ? ActiveInstances.Count - 1 : 0;
                }
            }

            contacts.Clear();
            lastCapturedFrame = -1;
        }

        private void OnCollisionEnter(Collision collision)
        {
            CaptureCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            CaptureCollision(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            var collider = collision.collider;
            if (collider == null)
            {
                return;
            }

            for (var i = contacts.Count - 1; i >= 0; i--)
            {
                if (contacts[i].Collider == collider)
                {
                    contacts.RemoveAt(i);
                }
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || !Application.isPlaying)
            {
                return;
            }

            if (aggregateFromAllInstances)
            {
                if (!IsPrimaryRenderer())
                {
                    return;
                }

                windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawAggregateWindow, "Ground Contact Debug");
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawSingleWindow, "Ground Contact Debug");
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos)
            {
                return;
            }

            for (var i = 0; i < contacts.Count; i++)
            {
                var sample = contacts[i];
                if (sample.Collider == null)
                {
                    continue;
                }

                Gizmos.color = sample.IsGround ? validGroundColor : invalidGroundColor;
                Gizmos.DrawSphere(sample.Point, 0.03f);
                Gizmos.DrawLine(sample.Point, sample.Point + sample.Normal * normalLength);
            }
        }

        private void DrawSingleWindow(int windowId)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
            DrawDebugContent(this);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawAggregateWindow(int windowId)
        {
            var targets = CollectAggregateTargets();
            if (targets.Count == 0)
            {
                GUILayout.Label("Debug target not found.");
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
                return;
            }

            if (selectedInstanceIndex >= targets.Count)
            {
                selectedInstanceIndex = 0;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(28f)))
            {
                selectedInstanceIndex = (selectedInstanceIndex - 1 + targets.Count) % targets.Count;
            }

            var selectedTarget = targets[selectedInstanceIndex];
            GUILayout.Label($"{selectedInstanceIndex + 1}/{targets.Count} {selectedTarget.GetDisplayName()}");

            if (GUILayout.Button(">", GUILayout.Width(28f)))
            {
                selectedInstanceIndex = (selectedInstanceIndex + 1) % targets.Count;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
            DrawDebugContent(selectedTarget);
            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawDebugContent(GroundContactDebugDisplay target)
        {
            if (target == null)
            {
                GUILayout.Label("Target: null");
                return;
            }

            var bestGround = target.FindBestGround();
            var validGroundCount = 0;
            var invalidContactCount = 0;
            for (var i = 0; i < target.contacts.Count; i++)
            {
                if (target.contacts[i].IsGround)
                {
                    validGroundCount++;
                }
                else
                {
                    invalidContactCount++;
                }
            }

            BeginSection("Summary");
            GUILayout.Label($"Target: {target.GetDisplayName()}");
            GUILayout.Label($"Grounded: {bestGround != null}");
            GUILayout.Label($"Contacts: {target.contacts.Count} (Ground={validGroundCount}, Other={invalidContactCount})");
            GUILayout.Label($"Up Axis: {target.upAxis.normalized}");
            GUILayout.Label($"Min Ground Dot: {target.minGroundNormalDot:F3}");
            GUILayout.Label($"Ground Mask: {target.groundLayer.value}");
            GUILayout.Label($"Last Capture Frame: {target.lastCapturedFrame}");
            EndSection();

            BeginSection("Best Ground");
            if (bestGround == null)
            {
                GUILayout.Label("None");
            }
            else
            {
                GUILayout.Label($"Object: {target.GetGroundName(bestGround)}");
                GUILayout.Label($"Layer: {LayerMask.LayerToName(bestGround.Layer)} ({bestGround.Layer})");
                GUILayout.Label($"Up Dot: {bestGround.UpDot:F3}");
                GUILayout.Label($"Point: {bestGround.Point}");
                GUILayout.Label($"Normal: {bestGround.Normal}");
            }
            EndSection();

            BeginSection("Recent Contacts");
            var displayed = 0;
            for (var i = 0; i < target.contacts.Count && displayed < target.maxRows; i++)
            {
                var sample = target.contacts[i];
                if (sample.Collider == null)
                {
                    continue;
                }

                GUILayout.BeginVertical("box");
                GUILayout.Label($"[{displayed}] {target.GetGroundName(sample)}");
                GUILayout.Label($"Classification: {(sample.IsGround ? "Ground" : "Other")}");
                GUILayout.Label($"Layer: {LayerMask.LayerToName(sample.Layer)} ({sample.Layer}) / InMask: {sample.InGroundLayer}");
                GUILayout.Label($"Up Dot: {sample.UpDot:F3}");
                GUILayout.Label($"Point: {sample.Point}");
                GUILayout.Label($"Normal: {sample.Normal}");
                GUILayout.EndVertical();
                displayed++;
            }

            if (displayed == 0)
            {
                GUILayout.Label("(no active contacts)");
            }
            else if (target.contacts.Count > displayed)
            {
                GUILayout.Label($"... {target.contacts.Count - displayed} more contact(s)");
            }
            EndSection();
        }

        private void CaptureCollision(Collision collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            if (lastCapturedFrame != Time.frameCount)
            {
                contacts.Clear();
                lastCapturedFrame = Time.frameCount;
            }

            var layer = collision.gameObject.layer;
            var inGroundLayer = IsInGroundLayer(layer, groundLayer);
            var groundRigidbody = collision.rigidbody != null ? collision.rigidbody : collision.collider.attachedRigidbody;
            var groundTransform = groundRigidbody != null ? groundRigidbody.transform : collision.collider.transform;

            for (var i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                var upDot = Vector3.Dot(contact.normal, upAxis);

                contacts.Add(new ContactSample
                {
                    Collider = collision.collider,
                    GroundTransform = groundTransform,
                    Point = contact.point,
                    Normal = contact.normal,
                    UpDot = upDot,
                    Layer = layer,
                    InGroundLayer = inGroundLayer,
                    IsGround = inGroundLayer && upDot >= minGroundNormalDot
                });
            }
        }

        private ContactSample FindBestGround()
        {
            ContactSample bestGround = null;
            for (var i = 0; i < contacts.Count; i++)
            {
                var sample = contacts[i];
                if (!sample.IsGround)
                {
                    continue;
                }

                if (bestGround == null || sample.UpDot > bestGround.UpDot)
                {
                    bestGround = sample;
                }
            }

            return bestGround;
        }

        private string GetGroundName(ContactSample sample)
        {
            if (sample == null)
            {
                return "null";
            }

            if (sample.GroundTransform != null)
            {
                return sample.GroundTransform.name;
            }

            return sample.Collider != null ? sample.Collider.name : "null";
        }

        private void NormalizeUpAxis()
        {
            if (upAxis.sqrMagnitude <= Mathf.Epsilon)
            {
                upAxis = Vector3.up;
                return;
            }

            upAxis = upAxis.normalized;
        }

        private bool IsPrimaryRenderer()
        {
            return ActiveInstances.Count > 0 && ReferenceEquals(ActiveInstances[0], this);
        }

        private List<GroundContactDebugDisplay> CollectAggregateTargets()
        {
            var targets = new List<GroundContactDebugDisplay>();
            for (var i = 0; i < ActiveInstances.Count; i++)
            {
                var candidate = ActiveInstances[i];
                if (candidate == null || !candidate.showOverlay)
                {
                    continue;
                }

                targets.Add(candidate);
            }

            return targets;
        }

        private string GetDisplayName()
        {
            return transform.name;
        }

        private void BeginSection(string title)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label(title);
        }

        private void EndSection()
        {
            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private static bool IsInGroundLayer(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }
    }
}
