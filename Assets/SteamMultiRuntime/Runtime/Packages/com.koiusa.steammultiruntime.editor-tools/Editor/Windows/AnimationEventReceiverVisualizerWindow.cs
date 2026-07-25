using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    /// <summary>
    /// AnimationEvent がどの GameObject / コンポーネント のメソッドを呼ぶのかを可視化するツール。
    /// </summary>
    public sealed class AnimationEventReceiverVisualizerWindow : EditorWindow
    {
        private GameObject targetGameObject;
        private Vector2 scrollPosition = Vector2.zero;
        private List<ReceiverInfo> receivers = new List<ReceiverInfo>();

        [MenuItem("Tools/SteamMultiRuntime/Read Only/Animation Events/Receiver Visualizer")]
        private static void Open()
        {
            var window = GetWindow<AnimationEventReceiverVisualizerWindow>("Animation Event Receivers");
            window.minSize = new Vector2(700f, 400f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Animation Event Receiver Visualizer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Shows which component methods will be called when AnimationEvents are triggered.\n" +
                "AnimationEvents search for methods by name on all attached components at runtime.",
                MessageType.Info);

            EditorGUILayout.Space();
            targetGameObject = EditorGUILayout.ObjectField("Target GameObject", targetGameObject, typeof(GameObject), allowSceneObjects: true) as GameObject;

            if (targetGameObject == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with an Animator to analyze.", MessageType.Warning);
                return;
            }

            var animator = targetGameObject.GetComponent<Animator>();
            if (animator == null)
            {
                EditorGUILayout.HelpBox("GameObject does not have an Animator component.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Analyze Animation Events", GUILayout.Height(40f)))
            {
                Analyze(animator);
            }

            if (receivers.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Found Receivers: {receivers.Count}", EditorStyles.boldLabel);

                using (var scroll = new GUILayout.ScrollViewScope(scrollPosition))
                {
                    scrollPosition = scroll.scrollPosition;

                    for (var i = 0; i < receivers.Count; i++)
                    {
                        DrawReceiverInfo(receivers[i], i);
                    }
                }
            }
        }

        private void DrawReceiverInfo(ReceiverInfo info, int index)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var headerLabel = info.IsAvailable ? "✓ AVAILABLE" : "✗ NOT FOUND";
                var headerColor = info.IsAvailable ? Color.green : Color.red;

                EditorGUILayout.LabelField($"[{index}] {info.MethodName}", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ColorField(headerColor, GUILayout.Width(100f));
                        EditorGUILayout.LabelField(headerLabel);
                    }

                    if (info.IsAvailable)
                    {
                        EditorGUILayout.TextField("Component", info.ComponentTypeName);
                        EditorGUILayout.TextField("Method Signature", info.MethodSignature);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            $"No component on '{targetGameObject.name}' has a public method named '{info.MethodName}'.",
                            MessageType.Warning);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Used in Clips:", EditorStyles.boldLabel);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        for (var i = 0; i < info.ClipsUsingThisMethod.Count; i++)
                        {
                            EditorGUILayout.TextField($"[{i}]", info.ClipsUsingThisMethod[i]);
                        }
                    }
                }
            }
        }

        private void Analyze(Animator animator)
        {
            receivers.Clear();

            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Error", "Cannot analyze runtime animator controller.", "OK");
                return;
            }

            var methodToClips = new Dictionary<string, List<string>>();

            // 全 AnimatorController の AnimationClip を走査
            var layers = controller.layers;
            for (var l = 0; l < layers.Length; l++)
            {
                var stateMachine = layers[l].stateMachine;
                CollectAnimationEventsFromStateMachine(stateMachine, methodToClips);
            }

            // 各メソッドに対して受信コンポーネントを確認
            foreach (var kvp in methodToClips)
            {
                var methodName = kvp.Key;
                var clips = kvp.Value;

                var isAvailable = HasMethod(targetGameObject, methodName, out var componentTypeName, out var methodSignature);

                receivers.Add(new ReceiverInfo
                {
                    MethodName = methodName,
                    ComponentTypeName = componentTypeName ?? "N/A",
                    MethodSignature = methodSignature ?? "N/A",
                    IsAvailable = isAvailable,
                    ClipsUsingThisMethod = clips
                });
            }
        }

        private void CollectAnimationEventsFromStateMachine(
            UnityEditor.Animations.AnimatorStateMachine stateMachine,
            Dictionary<string, List<string>> methodToClips)
        {
            if (stateMachine == null)
            {
                return;
            }

            var states = stateMachine.states;
            for (var i = 0; i < states.Length; i++)
            {
                var motion = states[i].state.motion;
                if (motion is AnimationClip clip)
                {
                    CollectEventsFromClip(clip, methodToClips);
                }
                else if (motion is UnityEditor.Animations.BlendTree blendTree)
                {
                    CollectEventsFromBlendTree(blendTree, methodToClips);
                }
            }

            var subStateMachines = stateMachine.stateMachines;
            for (var i = 0; i < subStateMachines.Length; i++)
            {
                CollectAnimationEventsFromStateMachine(subStateMachines[i].stateMachine, methodToClips);
            }
        }

        private void CollectEventsFromBlendTree(UnityEditor.Animations.BlendTree blendTree, Dictionary<string, List<string>> methodToClips)
        {
            if (blendTree == null)
            {
                return;
            }

            for (var i = 0; i < blendTree.children.Length; i++)
            {
                var motion = blendTree.children[i].motion;
                if (motion is AnimationClip clip)
                {
                    CollectEventsFromClip(clip, methodToClips);
                }
                else if (motion is UnityEditor.Animations.BlendTree childBlendTree)
                {
                    CollectEventsFromBlendTree(childBlendTree, methodToClips);
                }
            }
        }

        private void CollectEventsFromClip(AnimationClip clip, Dictionary<string, List<string>> methodToClips)
        {
            var events = AnimationUtility.GetAnimationEvents(clip);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (!methodToClips.ContainsKey(evt.functionName))
                {
                    methodToClips[evt.functionName] = new List<string>();
                }

                if (!methodToClips[evt.functionName].Contains(clip.name))
                {
                    methodToClips[evt.functionName].Add(clip.name);
                }
            }
        }

        private bool HasMethod(GameObject gameObject, string methodName, out string componentTypeName, out string methodSignature)
        {
            componentTypeName = null;
            methodSignature = null;

            var components = gameObject.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                var componentType = component.GetType();
                var method = componentType.GetMethod(
                    methodName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    componentTypeName = componentType.Name;
                    methodSignature = $"{method.ReturnType.Name} {method.Name}(...)";
                    return true;
                }
            }

            return false;
        }

        private class ReceiverInfo
        {
            public string MethodName;
            public string ComponentTypeName;
            public string MethodSignature;
            public bool IsAvailable;
            public List<string> ClipsUsingThisMethod = new List<string>();
        }
    }
}
