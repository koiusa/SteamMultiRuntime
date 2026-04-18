using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    public sealed class AnimationEventFinderWindow : EditorWindow
    {
        private string searchEventName = "OnCallChangeFace";
        private Vector2 scrollPosition = Vector2.zero;
        private List<AnimationEventResult> results = new List<AnimationEventResult>();

        [MenuItem("Tools/SteamMultiRuntime/Animation Events/Event Finder")]
        private static void Open()
        {
            var window = GetWindow<AnimationEventFinderWindow>("Animation Event Finder");
            window.minSize = new Vector2(600f, 300f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Search AnimationClip Events", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                searchEventName = EditorGUILayout.TextField("Event Name", searchEventName);
                if (GUILayout.Button("Search", GUILayout.Width(100f)))
                {
                    Search();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Results: {results.Count}", EditorStyles.boldLabel);

            using (var scroll = new GUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scroll.scrollPosition;
                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    DrawResult(result, i);
                }

                if (results.Count == 0)
                {
                    EditorGUILayout.HelpBox("No results found. Click 'Search' to find AnimationClips.", MessageType.Info);
                }
            }
        }

        private void DrawResult(AnimationEventResult result, int index)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"[{index}] {result.ClipName}", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.TextField("Path", result.AssetPath);
                    EditorGUILayout.LabelField($"Event Time: {result.Time:F3}s");
                    EditorGUILayout.LabelField($"Function Name: {result.FunctionName}");
                    if (!string.IsNullOrWhiteSpace(result.StringParameter))
                    {
                        EditorGUILayout.TextField("Parameter", result.StringParameter);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select Clip", GUILayout.Width(120f)))
                        {
                            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(result.AssetPath);
                            EditorGUIUtility.PingObject(clip);
                            Selection.activeObject = clip;
                        }

                        if (GUILayout.Button("Find Animator Controllers", GUILayout.Width(200f)))
                        {
                            FindAnimatorControllersWithClip(result.ClipName);
                        }
                    }
                }
            }
        }

        private void Search()
        {
            results.Clear();

            if (string.IsNullOrWhiteSpace(searchEventName))
            {
                EditorUtility.DisplayDialog("Error", "Event name is empty.", "OK");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:AnimationClip");
            EditorUtility.DisplayProgressBar("Searching", "Scanning AnimationClips...", 0f);

            for (var i = 0; i < guids.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Searching", $"Progress {i + 1}/{guids.Length}", (i + 1f) / guids.Length);

                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

                if (clip == null)
                {
                    continue;
                }

                var events = AnimationUtility.GetAnimationEvents(clip);
                for (var j = 0; j < events.Length; j++)
                {
                    var evt = events[j];
                    if (evt.functionName == searchEventName)
                    {
                        results.Add(new AnimationEventResult
                        {
                            AssetPath = assetPath,
                            ClipName = clip.name,
                            Time = evt.time,
                            FunctionName = evt.functionName,
                            StringParameter = evt.stringParameter
                        });
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }

        private void FindAnimatorControllersWithClip(string clipName)
        {
            var guids = AssetDatabase.FindAssets("t:AnimatorController");
            var controllerPaths = new List<string>();

            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);

                if (controller == null)
                {
                    continue;
                }

                var layers = controller.layers;
                for (var l = 0; l < layers.Length; l++)
                {
                    var stateMachine = layers[l].stateMachine;
                    if (StateMachineContainsClip(stateMachine, clipName))
                    {
                        controllerPaths.Add(assetPath);
                        break;
                    }
                }
            }

            if (controllerPaths.Count > 0)
            {
                var message = $"Found {controllerPaths.Count} AnimatorController(s) using '{clipName}':\n\n";
                for (var i = 0; i < controllerPaths.Count; i++)
                {
                    message += $"{i + 1}. {controllerPaths[i]}\n";
                }

                EditorUtility.DisplayDialog("Results", message, "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Results", $"No AnimatorController found using '{clipName}'.", "OK");
            }
        }

        private static bool StateMachineContainsClip(AnimatorStateMachine stateMachine, string clipName)
        {
            if (stateMachine == null)
            {
                return false;
            }

            var states = stateMachine.states;
            for (var i = 0; i < states.Length; i++)
            {
                var motion = states[i].state.motion;
                if (motion is AnimationClip clip && clip.name == clipName)
                {
                    return true;
                }

                if (motion is BlendTree blendTree && BlendTreeContainsClip(blendTree, clipName))
                {
                    return true;
                }
            }

            var subStateMachines = stateMachine.stateMachines;
            for (var i = 0; i < subStateMachines.Length; i++)
            {
                if (StateMachineContainsClip(subStateMachines[i].stateMachine, clipName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BlendTreeContainsClip(BlendTree blendTree, string clipName)
        {
            for (var i = 0; i < blendTree.children.Length; i++)
            {
                var motion = blendTree.children[i].motion;
                if (motion is AnimationClip clip && clip.name == clipName)
                {
                    return true;
                }

                if (motion is BlendTree childBlendTree && BlendTreeContainsClip(childBlendTree, clipName))
                {
                    return true;
                }
            }

            return false;
        }

        private class AnimationEventResult
        {
            public string AssetPath;
            public string ClipName;
            public float Time;
            public string FunctionName;
            public string StringParameter;
        }
    }
}
