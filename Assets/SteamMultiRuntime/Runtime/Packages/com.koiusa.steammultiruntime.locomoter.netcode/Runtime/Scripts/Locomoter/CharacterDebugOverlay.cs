using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class CharacterDebugOverlay : MonoBehaviour
    {
        private const string PreferredFaceLayerName = "face";

        [Header("Display")]
        [SerializeField] private bool showOnStart = false;
        [SerializeField] private bool autoShowInPlayMode = true;
        [SerializeField] private bool onlyLocalOwnedCharacter = true;
        [SerializeField] private bool aggregateFromAllInstances = true;
        [SerializeField] private bool includeNonOwnedCharactersInAggregate = true;
        [SerializeField] private bool showLauncherButtonWhenHidden = true;
        [SerializeField] private float windowWidth = 360f;
        [SerializeField] private float windowHeight = 520f;
        [SerializeField] private Vector2 windowPosition = new Vector2(12f, 12f);
        [SerializeField] private Vector2 launcherButtonSize = new Vector2(140f, 28f);

        [Header("Target")]
        [SerializeField] private Transform targetRoot;
        [SerializeField] private Rigidbody targetRigidbody;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Animator targetFaceAnimator;

        private static readonly List<CharacterDebugOverlay> ActiveInstances = new List<CharacterDebugOverlay>();
        private static int selectedInstanceIndex;

        private IPlayerController playerController;
        private NetworkBehaviour targetNetworkBehaviour;
        private CharacterDebugDisplayScope displayScope;
        private Rect windowRect;
        private Vector2 scrollPosition;
        private bool isVisible;

        private void Awake()
        {
            isVisible = showOnStart || (autoShowInPlayMode && Application.isPlaying);
            windowRect = new Rect(windowPosition.x, windowPosition.y, windowWidth, windowHeight);
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (!ActiveInstances.Contains(this))
            {
                ActiveInstances.Add(this);
            }
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
        }

        private void OnGUI()
        {
            if (!IsDisplayAllowed())
            {
                return;
            }

            if (!isVisible)
            {
                DrawLauncherButtonIfNeeded();
                return;
            }

            if (aggregateFromAllInstances)
            {
                if (!IsPrimaryRenderer())
                {
                    return;
                }

                windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawAggregateWindow, "Character Debug");
                return;
            }

            if (!CanRenderForThisInstance())
            {
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawSingleWindow, "Character Debug");
        }

        public void Toggle()
        {
            isVisible = !isVisible;
        }

        public void Show()
        {
            isVisible = true;
        }

        public void Hide()
        {
            isVisible = false;
        }

        public void ResolveReferences()
        {
            var root = targetRoot != null ? targetRoot : FindPlayerRoot();

            displayScope = GetComponentInParent<CharacterDebugDisplayScope>();
            playerController = root.GetComponent<IPlayerController>();
            targetNetworkBehaviour = root.GetComponent<NetworkBehaviour>();

            if (targetRigidbody == null)
            {
                targetRigidbody = root.GetComponent<Rigidbody>();
            }

            if (targetAnimator == null)
            {
                targetAnimator = GetComponent<Animator>();
            }

            if (targetFaceAnimator == null)
            {
                targetFaceAnimator = FindFaceAnimator(root);
            }
        }

        private Transform FindPlayerRoot()
        {
            var current = transform;
            while (current != null)
            {
                if (current.GetComponent<IPlayerController>() != null)
                {
                    return current;
                }

                current = current.parent;
            }

            current = transform;
            while (current != null)
            {
                if (current.GetComponent<Rigidbody>() != null ||
                    current.GetComponent<NetworkBehaviour>() != null)
                {
                    return current;
                }

                current = current.parent;
            }

            return transform;
        }

        private bool CanRenderForThisInstance()
        {
            if (!onlyLocalOwnedCharacter)
            {
                return true;
            }

            if (targetNetworkBehaviour == null)
            {
                return true;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return true;
            }

            return targetNetworkBehaviour.IsSpawned && targetNetworkBehaviour.IsOwner;
        }

        private bool IsPrimaryRenderer()
        {
            for (var i = 0; i < ActiveInstances.Count; i++)
            {
                var candidate = ActiveInstances[i];
                if (candidate != null && candidate.IsDisplayAllowed())
                {
                    return ReferenceEquals(candidate, this);
                }
            }

            return false;
        }

        private bool IsDisplayAllowed()
        {
            if (displayScope == null)
            {
                displayScope = GetComponentInParent<CharacterDebugDisplayScope>();
            }

            return displayScope == null || displayScope.IsVisible;
        }

        private void DrawAggregateWindow(int windowId)
        {
            var targets = CollectAggregateTargets();
            if (targets.Count == 0)
            {
                GUILayout.Label("Debug target not found.");
                if (GUILayout.Button("Hide Overlay"))
                {
                    Hide();
                }

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
            GUILayout.Label($"{selectedInstanceIndex + 1}/{targets.Count} {selectedTarget.GetTargetDisplayName()}");

            if (GUILayout.Button(">", GUILayout.Width(28f)))
            {
                selectedInstanceIndex = (selectedInstanceIndex + 1) % targets.Count;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh References"))
            {
                selectedTarget.ResolveReferences();
            }

            if (GUILayout.Button("Hide Overlay"))
            {
                Hide();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
            DrawDebugContent(selectedTarget);
            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawSingleWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh References"))
            {
                ResolveReferences();
            }

            if (GUILayout.Button("Hide Overlay"))
            {
                Hide();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
            DrawDebugContent(this);
            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawDebugContent(CharacterDebugOverlay target)
        {
            if (target == null)
            {
                GUILayout.Label("Target: null");
                return;
            }

            target.ResolveReferences();

            target.BeginSection("Target");
            GUILayout.Label($"Name: {target.GetTargetDisplayName()}");
            GUILayout.Label($"NetworkMode: {target.GetNetworkModeDisplayName()}");
            target.EndSection();

            target.BeginSection("Controller State");
            if (target.playerController == null)
            {
                GUILayout.Label("IPlayerController: not found");
            }
            else
            {
                GUILayout.Label($"Grounded: {target.playerController.IsGrounded}");
                GUILayout.Label($"Jumping: {target.playerController.IsJumping}");
                GUILayout.Label($"Freefall: {target.playerController.IsFreefall}");
                GUILayout.Label($"FallingAfterJump: {target.playerController.IsFallingAfterJump}");
                if (target.playerController is IPlayerLadderState ladderState)
                {
                    GUILayout.Label($"OnLadder: {ladderState.IsOnLadder}");
                    GUILayout.Label($"LadderSpeed: {ladderState.LadderSpeed:F3}");
                }
                GUILayout.Label($"HorizontalVelocity: {target.playerController.HorizontalVelocity:F3}");
                GUILayout.Label($"VerticalVelocity: {target.playerController.VerticalVelocity:F3}");
                GUILayout.Label($"MaxMoveSpeed: {target.playerController.MaxMoveSpeed:F3}");
                GUILayout.Label($"InheritedGroundVelocity: {target.playerController.InheritedGroundVelocity}");
            }
            target.EndSection();

            target.BeginSection("Rigidbody");
            if (target.targetRigidbody == null)
            {
                GUILayout.Label("Rigidbody: not found");
            }
            else
            {
                GUILayout.Label($"Position: {target.targetRigidbody.position}");
                GUILayout.Label($"Velocity: {target.targetRigidbody.linearVelocity}");
                GUILayout.Label($"Speed: {target.targetRigidbody.linearVelocity.magnitude:F3}");
                GUILayout.Label($"AngularVelocity: {target.targetRigidbody.angularVelocity}");
            }
            target.EndSection();

            target.DrawAnimatorSection("Body Animator", target.targetAnimator, 0, true);
            target.DrawFaceAnimationSection();
        }

        private void DrawAnimatorSection(string title, Animator animator, int layerIndex, bool includeParameters)
        {
            BeginSection(title);
            if (animator == null)
            {
                GUILayout.Label($"{title}: not found");
                EndSection();
                return;
            }

            if (layerIndex < 0 || layerIndex >= animator.layerCount)
            {
                GUILayout.Label($"Layer {layerIndex}: not found");
                EndSection();
                return;
            }

            DrawAnimatorLayerState(animator, layerIndex);

            if (includeParameters)
            {
                GUILayout.Space(4f);
                GUILayout.Label("Parameters");
                var parameters = animator.parameters;
                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    switch (parameter.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            GUILayout.Label($"{parameter.name}: {animator.GetFloat(parameter.nameHash):F3}");
                            break;
                        case AnimatorControllerParameterType.Int:
                            GUILayout.Label($"{parameter.name}: {animator.GetInteger(parameter.nameHash)}");
                            break;
                        case AnimatorControllerParameterType.Bool:
                            GUILayout.Label($"{parameter.name}: {animator.GetBool(parameter.nameHash)}");
                            break;
                        case AnimatorControllerParameterType.Trigger:
                            GUILayout.Label($"{parameter.name}: trigger");
                            break;
                    }
                }
            }

            EndSection();
        }

        private void DrawFaceAnimationSection()
        {
            BeginSection("Face Animation");

            if (!TryGetFaceAnimatorAndLayer(out var animator, out var layerIndex))
            {
                GUILayout.Label("Face animation layer/animator not found");
                EndSection();
                return;
            }

            GUILayout.Label($"Animator: {animator.name}");
            DrawAnimatorLayerState(animator, layerIndex);
            EndSection();
        }

        private void DrawAnimatorLayerState(Animator animator, int layerIndex)
        {
            var state = animator.GetCurrentAnimatorStateInfo(layerIndex);
            GUILayout.Label($"Layer: {GetAnimatorLayerName(animator, layerIndex)} ({layerIndex})");
            GUILayout.Label($"State: {GetAnimatorStateDisplayName(animator, layerIndex, state)}");
            GUILayout.Label($"NormalizedTime: {state.normalizedTime:F3}");
            GUILayout.Label($"LayerWeight: {animator.GetLayerWeight(layerIndex):F3}");

            var clipInfos = animator.GetCurrentAnimatorClipInfo(layerIndex);
            if (clipInfos != null && clipInfos.Length > 0 && clipInfos[0].clip != null)
            {
                GUILayout.Label($"Clip: {clipInfos[0].clip.name}");
            }
        }

        private bool TryGetFaceAnimatorAndLayer(out Animator animator, out int layerIndex)
        {
            if (targetFaceAnimator != null)
            {
                animator = targetFaceAnimator;
                layerIndex = FindFaceLayerIndex(animator);
                if (layerIndex < 0)
                {
                    layerIndex = 0;
                }
                return true;
            }

            if (targetAnimator != null)
            {
                layerIndex = FindFaceLayerIndex(targetAnimator);
                if (layerIndex >= 0)
                {
                    animator = targetAnimator;
                    return true;
                }
            }

            animator = null;
            layerIndex = -1;
            return false;
        }

        private Animator FindFaceAnimator(Transform root)
        {
            var animators = root.GetComponentsInChildren<Animator>(true);
            Animator fallbackAnimator = null;

            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null || animator == targetAnimator)
                {
                    continue;
                }

                var animatorName = animator.name;
                if (animatorName.IndexOf(PreferredFaceLayerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return animator;
                }

                if (FindFaceLayerIndex(animator) >= 0)
                {
                    fallbackAnimator = animator;
                }
            }

            return fallbackAnimator;
        }

        private int FindFaceLayerIndex(Animator animator)
        {
            if (animator == null)
            {
                return -1;
            }

            for (var i = 0; i < animator.layerCount; i++)
            {
                var layerName = GetAnimatorLayerName(animator, i);
                if (layerName.IndexOf(PreferredFaceLayerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private string GetAnimatorLayerName(Animator animator, int layerIndex)
        {
            return animator != null && layerIndex >= 0 && layerIndex < animator.layerCount
                ? animator.GetLayerName(layerIndex)
                : $"Layer {layerIndex}";
        }

        private string GetAnimatorStateDisplayName(Animator animator, int layerIndex, AnimatorStateInfo state)
        {
            var clipInfos = animator.GetCurrentAnimatorClipInfo(layerIndex);
            if (clipInfos != null && clipInfos.Length > 0 && clipInfos[0].clip != null)
            {
                return clipInfos[0].clip.name;
            }

            return state.shortNameHash.ToString();
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

        private string GetTargetDisplayName()
        {
            var root = targetRoot != null ? targetRoot : transform;
            if (targetNetworkBehaviour == null)
            {
                return root.name;
            }

            return $"{root.name} (Owner:{targetNetworkBehaviour.OwnerClientId})";
        }

        private string GetNetworkModeDisplayName()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return "Offline";
            }

            if (networkManager.IsHost)
            {
                return "Host";
            }

            if (networkManager.IsServer)
            {
                return "Server";
            }

            if (networkManager.IsClient)
            {
                return "Client";
            }

            return "Unknown";
        }

        private List<CharacterDebugOverlay> CollectAggregateTargets()
        {
            var targets = new List<CharacterDebugOverlay>();

            for (var i = 0; i < ActiveInstances.Count; i++)
            {
                var candidate = ActiveInstances[i];
                if (candidate == null)
                {
                    continue;
                }

                if (!candidate.IsDisplayAllowed())
                {
                    continue;
                }

                if (!includeNonOwnedCharactersInAggregate && !candidate.CanRenderForThisInstance())
                {
                    continue;
                }

                targets.Add(candidate);
            }

            return targets;
        }

        private void DrawLauncherButtonIfNeeded()
        {
            if (!showLauncherButtonWhenHidden)
            {
                return;
            }

            if (aggregateFromAllInstances && !IsPrimaryRenderer())
            {
                return;
            }

            var launcherRect = new Rect(windowPosition.x, windowPosition.y, launcherButtonSize.x, launcherButtonSize.y);
            if (GUI.Button(launcherRect, "Show Character Debug"))
            {
                Show();
            }
        }
    }
}
