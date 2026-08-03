using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public sealed class ActorMovementDebuggerWindow : EditorWindow
    {
        private ActorCompositeMotor selectedActor;
        private IActorMovementDebugTarget target;
        private Vector2 scroll;
        private bool coordinatorOpen = true;
        private bool wallOpen = true;
        private bool ladderOpen = true;
        private bool wireOpen = true;
        private const float LabelWidth = 190f;
        private const float HierarchyIndent = 22f;

        [MenuItem("Tools/SteamMultiRuntime/Diagnostics/Player/Movement Debugger")]
        private static void Open() => GetWindow<ActorMovementDebuggerWindow>("Movement Debugger");

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Selection.selectionChanged += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Selection.selectionChanged -= Repaint;
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            DrawTargetSelector();
            if (target == null || !target.IsValid)
            {
                if (target != null) SetTarget(null);
                EditorGUILayout.HelpBox("ActorCompositeMotor を選択してください。Hierarchy の子を選択して Use Selection を押すこともできます。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4f);
            DrawCompositeMotor();
            DrawBaseMotor();
            DrawCoordinator();
            if (coordinatorOpen)
            {
                DrawWall();
                DrawLadder();
                DrawWire();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetSelector()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var nextActor = (ActorCompositeMotor)EditorGUILayout.ObjectField(selectedActor, typeof(ActorCompositeMotor), true);
                if (nextActor != selectedActor) SetTarget(nextActor);
                if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    SetTarget(FindFromSelection());
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55f)) && selectedActor != null)
                    SetTarget(selectedActor);
                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(40f)) && target != null)
                    EditorGUIUtility.PingObject(target.Context);
            }
        }

        private void DrawCompositeMotor()
        {
            BeginPanel("PLAYER COMPOSITE MOTOR", 0, "●");
            StatusValue("Composite Motor", true, target.IsEnabled);
            Value("Grounded / Traversal", $"{target.IsGrounded} / {target.IsTraversalActive}");
            Value("Jumping / Falling / Freefall", $"{target.IsJumping} / {target.IsFallingAfterJump} / {target.IsFreefall}");
            Value("Horizontal / Vertical Velocity", $"{target.HorizontalVelocity:F3} / {target.VerticalVelocity:F3}");
            Value("Inherited Ground Velocity", target.InheritedGroundVelocity.ToString("F3"));
            var debug = target.Composite;
            Value("Raw Move Input", debug.RawMoveInput.ToString("F3"));
            Value("Move Reference", debug.MoveReferenceRotation.eulerAngles.ToString("F1"));
            Value("External Motion", debug.HasActiveExternalMotion
                ? $"active ({debug.ActiveExternalMotionRemaining:F3} s)"
                : "inactive");
            EndPanel();
        }

        private void DrawBaseMotor()
        {
            BeginPanel("BASE MOTOR", 1, "├");
            var motor = target.BaseMotor;
            StatusValue("ActorMotor", motor != null, motor != null && motor.IsEnabled);
            if (motor != null)
            {
                Value("Grounded / Airborne From Jump", $"{motor.IsGrounded} / {motor.IsAirborneFromJump}");
                Value("Horizontal / Vertical Velocity", $"{motor.HorizontalVelocity:F3} / {motor.VerticalVelocity:F3}");
            }
            EndPanel();
        }

        private void DrawCoordinator()
        {
            if (!BeginFoldoutPanel("TRAVERSAL COORDINATOR", ref coordinatorOpen, 1, "└")) return;
            var traversal = target.Traversal;
            if (!target.HasTraversalCoordinator)
            {
                StatusValue("Coordinator", false, false);
                EndPanel();
                return;
            }
                StatusValue("Coordinator", true, traversal.IsEnabled);
                Value("State", traversal.CurrentState);
                Value("State Elapsed", $"{traversal.StateElapsedTime:F3} s");
                Value("Traversal Active", traversal.IsTraversalActive);
                var debug = target.TraversalDebug;
                var logTransitions = EditorGUILayout.Toggle("Console Log", debug.LogStateTransitions);
                if (logTransitions != debug.LogStateTransitions)
                {
                    Undo.RecordObject(target.Context, "Toggle Traversal State Logging");
                    target.SetStateTransitionLogging(logTransitions);
                    EditorUtility.SetDirty(target.Context);
                }
                Value("Intent", debug.IntentFlags);
                Value("Wall Block Remaining", $"{debug.WallTraversalBlockRemaining:F3} s");
                Value("WallRun Latched Off", debug.WallRunBlockedUntilWallExit);
                Value("Wire Aim", debug.WireAimResult.State);
                Value("Wire Aim Attach Point", debug.WireAimResult.AttachPoint);
                Value("On Ladder", traversal.IsOnLadder);
                Value("Wall Running", traversal.IsWallRunning);
                Value("Wire Attached", traversal.IsWireAttached);
            EndPanel();
        }

        private void DrawWall()
        {
            if (!BeginFoldoutPanel("WALL FEATURE", ref wallOpen, 2, "├")) return;
                var feature = target.Wall;
                StatusValue("Feature", feature != null, feature != null && feature.IsEnabled);
                if (feature != null)
                {
                    var debug = target.WallDebug;
                    Value("Obstacle Contact", debug.HasObstacleContact);
                    Value("Resolved Normal", debug.HasWallNormal ? debug.WallNormal.ToString("F3") : "none");
                }
                DrawAction("Run", target.WallRunEnabled, target.WallRunInstalled);
                DrawAction("Jump", target.WallJumpEnabled, target.WallJumpInstalled);
                DrawAction("Slide", target.WallSlideEnabled, target.WallSlideInstalled);
            EndPanel();
        }

        private void DrawLadder()
        {
            if (!BeginFoldoutPanel("LADDER FEATURE", ref ladderOpen, 2, "├")) return;
                var feature = target.Ladder;
                StatusValue("Feature", feature != null, feature != null && feature.IsEnabled);
                if (feature != null) Value("On Ladder / Speed", $"{feature.IsOnLadder} / {feature.ClimbSpeed:F3}");
                if (feature != null)
                {
                    var debug = target.LadderDebug;
                    Value("Current Ladder", debug.CurrentLadder != null ? debug.CurrentLadder.name : "none");
                    Value("Overlapping Volumes", debug.OverlappingLadderCount);
                    Value("Reattach Block", $"{debug.ReattachBlockRemaining:F3} s");
                    Value("Gravity / Facing", $"{debug.UsesGravity} / {debug.FacingDirection:F3}");
                }
                DrawAction("Climb", target.LadderClimbEnabled, target.LadderClimbInstalled);
                DrawAction("Detach", target.LadderDetachEnabled, target.LadderDetachInstalled);
            EndPanel();
        }

        private void DrawWire()
        {
            if (!BeginFoldoutPanel("WIRE FEATURE", ref wireOpen, 2, "└")) return;
                var wire = target.Wire;
                StatusValue("Feature", wire != null, wire != null && wire.IsEnabled);
                if (wire != null)
                {
                    Value("Attached / Mode", $"{wire.IsAttached} / {wire.ConstraintMode}");
                    Value("Anchor", wire.IsAttached ? wire.AnchorPoint.ToString("F3") : "none");
                    Value("Anchor Object", wire.AnchorTransform != null ? wire.AnchorTransform.name : "static / none");
                    Value("Target Length", $"{wire.RopeLength:F3} m ({wire.MinimumRopeLength:F1}–{wire.MaximumRopeLength:F1})");
                }
                if (wire != null)
                {
                    var debug = target.WireDebug;
                    Value("Actual Length / Stretch", $"{debug.ActualLength:F3} / {debug.RopeStretch:F3} m");
                    Value("Dynamic Anchor", debug.HasDynamicAnchor);
                }
                var traversal = target.Traversal;
                if (traversal != null)
                    Value("Ground Action", $"{traversal.IsWireGroundActionActive} (Strafe {traversal.WireGroundStrafeBlend:F2}, Facing {traversal.WireGroundFacingBlend:F2})");
                DrawAction("Attach", target.WireAttachEnabled, target.WireAttachInstalled);
                DrawAction("Swing", target.WireSwingEnabled, target.WireSwingInstalled);
                DrawAction("Reel", target.WireReelEnabled, target.WireReelInstalled);
                if (target.WireReelInstalled)
                {
                    var reel = target.WireReelDebug;
                    Value("    Reel Input / In", $"{reel.Input:F3} / {reel.IsReelingIn}");
                    Value("    Reel Speed", $"{reel.ReelSpeed:F3} m/s");
                    Value("    Last Apply Before / After", $"{reel.LastLengthBeforeApply:F3} / {reel.LastLengthAfterApply:F3} m");
                }
                DrawAction("Ground", target.WireGroundEnabled, target.WireGroundInstalled);
            EndPanel();
        }

        private static void DrawAction(string name, bool enabled, bool installed) =>
            StatusValue($"  {name} Action", installed, enabled);

        private static void Value(string label, object value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(LabelWidth));
                EditorGUILayout.SelectableLabel(value?.ToString() ?? "null", EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static void StatusValue(string label, bool installed, bool enabled)
        {
            var text = !installed ? "MISSING" : enabled ? "ENABLED" : "DISABLED";
            var color = !installed
                ? new Color(1f, 0.38f, 0.32f)
                : enabled ? new Color(0.35f, 0.78f, 0.42f) : new Color(1f, 0.68f, 0.25f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(LabelWidth));
                var style = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = color } };
                EditorGUILayout.LabelField(text, style);
            }
        }

        private static void BeginPanel(string title, int depth, string connector)
        {
            EditorGUILayout.BeginHorizontal();
            DrawHierarchyPrefix(depth, connector);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            DrawDivider();
        }

        private static bool BeginFoldoutPanel(string title, ref bool open, int depth, string connector)
        {
            EditorGUILayout.BeginHorizontal();
            DrawHierarchyPrefix(depth, connector);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            open = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            if (open) DrawDivider();
            else
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            return open;
        }

        private static void EndPanel()
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3f);
        }

        private static void DrawHierarchyPrefix(int depth, string connector)
        {
            if (depth > 0) GUILayout.Space(depth * HierarchyIndent);
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.55f, 0.72f, 0.9f)
                    : new Color(0.18f, 0.38f, 0.62f) }
            };
            GUILayout.Label(connector, style, GUILayout.Width(18f));
        }

        private static void DrawDivider()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.12f)
                : new Color(0f, 0f, 0f, 0.16f));
            EditorGUILayout.Space(2f);
        }

        private static ActorCompositeMotor FindFromSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null) return null;
            return selected.GetComponentInParent<ActorCompositeMotor>()
                ?? selected.GetComponentInChildren<ActorCompositeMotor>(true);
        }

        private void SetTarget(ActorCompositeMotor actor)
        {
            selectedActor = actor;
            target = actor != null ? new ActorMovementDebugTarget(actor) : null;
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange _)
        {
            // Scene objects are recreated across Play Mode boundaries. Cached interface
            // references do not participate in Unity's overloaded null comparison.
            SetTarget(null);
        }
    }
}
