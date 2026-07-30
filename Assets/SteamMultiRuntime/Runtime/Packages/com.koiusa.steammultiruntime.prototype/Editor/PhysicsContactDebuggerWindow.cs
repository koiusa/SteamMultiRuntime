using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public sealed class PhysicsContactDebuggerWindow : EditorWindow
    {
        private const int MaxRows = 20;
        private const float LabelWidth = 180f;

        private readonly List<GroundContactDebugDisplay> targets = new List<GroundContactDebugDisplay>();
        private GroundContactDebugDisplay selectedTarget;
        private Vector2 scroll;
        private int selectedIndex = -1;

        [MenuItem("Tools/SteamMultiRuntime/Debug/Physics Contact Debugger")]
        private static void Open() => GetWindow<PhysicsContactDebuggerWindow>("Contact Debugger");

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Selection.selectionChanged += Repaint;
            RefreshTargets();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Selection.selectionChanged -= Repaint;
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode中の物理接触を表示します。", MessageType.Info);
                return;
            }

            if (selectedTarget == null)
            {
                EditorGUILayout.HelpBox("GroundContactDebugDisplay を選択してください。", MessageType.Info);
                return;
            }

            if (!selectedTarget.isActiveAndEnabled)
            {
                EditorGUILayout.HelpBox("対象が無効なため接触を収集していません。コンポーネントを有効にしてください。", MessageType.Warning);
                return;
            }

            var snapshot = selectedTarget.GetDebugSnapshot();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSummary(snapshot);
            DrawBestGround(snapshot);
            DrawContacts(snapshot);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            RemoveDestroyedTargets();
            if (selectedTarget == null)
            {
                selectedTarget = null;
                selectedIndex = -1;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var nextTarget = (GroundContactDebugDisplay)EditorGUILayout.ObjectField(
                    selectedTarget,
                    typeof(GroundContactDebugDisplay),
                    true);
                if (nextTarget != selectedTarget)
                {
                    SetTarget(nextTarget);
                }

                if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    SetTarget(FindFromSelection());
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                {
                    RefreshTargets();
                }

                using (new EditorGUI.DisabledScope(targets.Count < 2))
                {
                    if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                    {
                        SelectRelative(-1);
                    }

                    if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                    {
                        SelectRelative(1);
                    }
                }

                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(40f)) && selectedTarget != null)
                {
                    EditorGUIUtility.PingObject(selectedTarget);
                }
            }

            if (targets.Count > 0)
            {
                EditorGUILayout.LabelField($"Target {selectedIndex + 1}/{targets.Count}", EditorStyles.miniLabel);
            }
        }

        private static void DrawSummary(GroundContactDebugSnapshot snapshot)
        {
            BeginPanel("SUMMARY");
            Value("Target", snapshot.TargetName);
            Value("Grounded", snapshot.IsGrounded);
            Value("Contacts", $"{snapshot.Contacts.Length} (Ground={snapshot.ValidGroundCount}, Other={snapshot.Contacts.Length - snapshot.ValidGroundCount})");
            Value("Up Axis", snapshot.UpAxis.ToString("F3"));
            Value("Min Ground Dot", snapshot.MinGroundNormalDot.ToString("F3"));
            Value("Ground Mask", snapshot.GroundMask);
            Value("Last Capture Frame", snapshot.LastCapturedFrame);
            EndPanel();
        }

        private static void DrawBestGround(GroundContactDebugSnapshot snapshot)
        {
            BeginPanel("BEST GROUND");
            if (!snapshot.BestGround.HasValue)
            {
                EditorGUILayout.LabelField("None");
            }
            else
            {
                DrawSample(snapshot.BestGround.Value);
            }
            EndPanel();
        }

        private static void DrawContacts(GroundContactDebugSnapshot snapshot)
        {
            BeginPanel("RECENT CONTACTS");
            var count = Mathf.Min(snapshot.Contacts.Length, MaxRows);
            for (var i = 0; i < count; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"[{i}] {snapshot.Contacts[i].ObjectName}", EditorStyles.boldLabel);
                    DrawSample(snapshot.Contacts[i]);
                }
            }

            if (count == 0)
            {
                EditorGUILayout.LabelField("(no active contacts)");
            }
            else if (snapshot.Contacts.Length > count)
            {
                EditorGUILayout.LabelField($"... {snapshot.Contacts.Length - count} more contact(s)");
            }
            EndPanel();
        }

        private static void DrawSample(GroundContactDebugSample sample)
        {
            Value("Object", sample.ObjectName);
            Value("Classification", sample.IsGround ? "Ground" : "Other");
            Value("Layer", $"{LayerMask.LayerToName(sample.Layer)} ({sample.Layer}) / InMask: {sample.InGroundLayer}");
            Value("Up Dot", sample.UpDot.ToString("F3"));
            Value("Point", sample.Point.ToString("F3"));
            Value("Normal", sample.Normal.ToString("F3"));
        }

        private static void BeginPanel(string title)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static void EndPanel()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3f);
        }

        private static void Value(string label, object value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(LabelWidth));
                EditorGUILayout.SelectableLabel(
                    value?.ToString() ?? "null",
                    EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static GroundContactDebugDisplay FindFromSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                return null;
            }

            return selected.GetComponentInParent<GroundContactDebugDisplay>()
                ?? selected.GetComponentInChildren<GroundContactDebugDisplay>(true);
        }

        private void RefreshTargets()
        {
            var previousTarget = selectedTarget;
            targets.Clear();
            targets.AddRange(Object.FindObjectsByType<GroundContactDebugDisplay>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None));
            SortTargets();

            if (previousTarget != null && targets.Contains(previousTarget))
            {
                SetTarget(previousTarget);
            }
            else
            {
                SetTarget(targets.Count > 0 ? targets[0] : null);
            }
        }

        private void SelectRelative(int offset)
        {
            RemoveDestroyedTargets();
            if (targets.Count == 0)
            {
                SetTarget(null);
                return;
            }

            var index = selectedIndex >= 0 ? selectedIndex : 0;
            index = (index + offset + targets.Count) % targets.Count;
            SetTarget(targets[index]);
        }

        private void SetTarget(GroundContactDebugDisplay target)
        {
            RemoveDestroyedTargets();
            if (target != null && !targets.Contains(target))
            {
                targets.Add(target);
                SortTargets();
            }

            selectedTarget = target;
            selectedIndex = target != null ? targets.IndexOf(target) : -1;
            scroll = Vector2.zero;
            Repaint();
        }

        private static string GetHierarchyPath(GroundContactDebugDisplay target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var path = target.name;
            var parent = target.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private void SortTargets()
        {
            RemoveDestroyedTargets();
            targets.Sort((left, right) => string.CompareOrdinal(GetHierarchyPath(left), GetHierarchyPath(right)));
        }

        private void RemoveDestroyedTargets()
        {
            targets.RemoveAll(target => target == null);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange _)
        {
            RefreshTargets();
        }
    }
}
