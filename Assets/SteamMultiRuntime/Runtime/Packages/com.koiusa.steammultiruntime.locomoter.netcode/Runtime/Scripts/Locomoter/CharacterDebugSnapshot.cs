using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal interface ICharacterDebugSnapshotSource
    {
        bool Matches(Transform root, IActorController controller, Rigidbody body, Animator bodyAnimator,
            Animator faceAnimator, NetworkBehaviour networkBehaviour, int faceLayer);
        void Capture(CharacterDebugSnapshot destination);
    }

    internal sealed class CharacterDebugSnapshot
    {
        public string TargetName = "No target";
        public string CharacterName = "Unknown";
        public string NetworkMode = "Local";
        public bool HasController;
        public bool Grounded;
        public bool Jumping;
        public bool Freefall;
        public bool FallingAfterJump;
        public bool HasLadderState;
        public bool OnLadder;
        public float LadderSpeed;
        public float HorizontalVelocity;
        public float VerticalVelocity;
        public float MaxMoveSpeed;
        public Vector3 InheritedGroundVelocity;
        public bool HasRigidbody;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public readonly AnimatorDebugSnapshot BodyAnimator = new();
        public readonly AnimatorDebugSnapshot FaceAnimator = new();
    }

    internal sealed class AnimatorDebugSnapshot
    {
        public bool IsAvailable;
        public string AnimatorName = "none";
        public string Layer = "none";
        public string State = "none";
        public string Clip = "none";
        public float NormalizedTime;
        public float LayerWeight;
        public readonly List<AnimatorParameterDebugSnapshot> Parameters = new();
    }

    internal readonly struct AnimatorParameterDebugSnapshot
    {
        public readonly string Name;
        public readonly string Value;

        public AnimatorParameterDebugSnapshot(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    internal sealed class CharacterDebugSnapshotSource : ICharacterDebugSnapshotSource
    {
        private readonly Transform root;
        private readonly IActorController controller;
        private readonly Rigidbody body;
        private readonly Animator bodyAnimator;
        private readonly Animator faceAnimator;
        private readonly NetworkBehaviour networkBehaviour;
        private readonly int faceLayer;
        private readonly AnimatorControllerParameter[] bodyParameters;
        private readonly List<AnimatorClipInfo> bodyClips = new();
        private readonly List<AnimatorClipInfo> faceClips = new();

        public CharacterDebugSnapshotSource(Transform root, IActorController controller, Rigidbody body,
            Animator bodyAnimator, Animator faceAnimator, NetworkBehaviour networkBehaviour, int faceLayer)
        {
            this.root = root;
            this.controller = controller;
            this.body = body;
            this.bodyAnimator = bodyAnimator;
            this.faceAnimator = faceAnimator;
            this.networkBehaviour = networkBehaviour;
            this.faceLayer = faceLayer;
            bodyParameters = bodyAnimator != null ? bodyAnimator.parameters : new AnimatorControllerParameter[0];
        }

        public void Capture(CharacterDebugSnapshot destination)
        {
            destination.TargetName = GetTargetName();
            destination.CharacterName = GetCharacterName();
            destination.NetworkMode = GetNetworkMode();
            CaptureController(destination);
            CaptureRigidbody(destination);
            CaptureAnimator(bodyAnimator, 0, bodyParameters, bodyClips, destination.BodyAnimator);
            CaptureAnimator(faceAnimator, faceLayer, null, faceClips, destination.FaceAnimator);
        }

        public bool Matches(Transform candidateRoot, IActorController candidateController, Rigidbody candidateBody,
            Animator candidateBodyAnimator, Animator candidateFaceAnimator, NetworkBehaviour candidateNetworkBehaviour,
            int candidateFaceLayer)
        {
            return root == candidateRoot
                && ReferenceEquals(controller, candidateController)
                && body == candidateBody
                && bodyAnimator == candidateBodyAnimator
                && faceAnimator == candidateFaceAnimator
                && networkBehaviour == candidateNetworkBehaviour
                && faceLayer == candidateFaceLayer;
        }

        private void CaptureController(CharacterDebugSnapshot destination)
        {
            destination.HasController = IsAlive(controller);
            if (!destination.HasController) return;
            destination.Grounded = controller.IsGrounded;
            destination.Jumping = controller.IsJumping;
            destination.Freefall = controller.IsFreefall;
            destination.FallingAfterJump = controller.IsFallingAfterJump;
            destination.HorizontalVelocity = controller.HorizontalVelocity;
            destination.VerticalVelocity = controller.VerticalVelocity;
            destination.MaxMoveSpeed = controller.MaxMoveSpeed;
            destination.InheritedGroundVelocity = controller.InheritedGroundVelocity;
            destination.HasLadderState = controller is IActorLadderState;
            if (controller is IActorLadderState ladder)
            {
                destination.OnLadder = ladder.IsOnLadder;
                destination.LadderSpeed = ladder.LadderSpeed;
            }
        }

        private void CaptureRigidbody(CharacterDebugSnapshot destination)
        {
            destination.HasRigidbody = body != null;
            if (!destination.HasRigidbody) return;
            destination.Position = body.position;
            destination.Velocity = body.linearVelocity;
            destination.AngularVelocity = body.angularVelocity;
        }

        private static void CaptureAnimator(Animator animator, int layer, AnimatorControllerParameter[] parameters,
            List<AnimatorClipInfo> clips, AnimatorDebugSnapshot destination)
        {
            destination.IsAvailable = animator != null && layer >= 0 && layer < animator.layerCount;
            destination.Parameters.Clear();
            if (!destination.IsAvailable) return;
            destination.AnimatorName = animator.name;
            destination.Layer = $"{animator.GetLayerName(layer)} ({layer})";
            var state = animator.GetCurrentAnimatorStateInfo(layer);
            clips.Clear();
            animator.GetCurrentAnimatorClipInfo(layer, clips);
            destination.State = clips.Count > 0 && clips[0].clip != null ? clips[0].clip.name : state.shortNameHash.ToString();
            destination.Clip = clips.Count > 0 && clips[0].clip != null ? clips[0].clip.name : "none";
            destination.NormalizedTime = state.normalizedTime;
            destination.LayerWeight = animator.GetLayerWeight(layer);
            if (parameters == null) return;
            foreach (var parameter in parameters)
                destination.Parameters.Add(new AnimatorParameterDebugSnapshot(parameter.name, GetParameterValue(animator, parameter)));
        }

        private string GetTargetName()
        {
            if (root == null) return "Target missing";
            return networkBehaviour == null ? root.name : $"{root.name} (Owner:{networkBehaviour.OwnerClientId})";
        }

        private string GetCharacterName()
        {
            if (bodyAnimator == null) return "Unknown";
            var modelName = root != null && bodyAnimator.gameObject != root.gameObject
                ? bodyAnimator.gameObject.name
                : bodyAnimator.avatar != null ? bodyAnimator.avatar.name : bodyAnimator.gameObject.name;
            const string cloneSuffix = "(Clone)";
            return modelName.EndsWith(cloneSuffix, System.StringComparison.Ordinal)
                ? modelName.Substring(0, modelName.Length - cloneSuffix.Length).TrimEnd()
                : modelName;
        }

        private static string GetNetworkMode()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening) return "Local";
            if (manager.IsHost) return "Host";
            if (manager.IsServer) return "Server";
            return manager.IsClient ? "Client" : "Unknown";
        }

        private static string GetParameterValue(Animator animator, AnimatorControllerParameter parameter) => parameter.type switch
        {
            AnimatorControllerParameterType.Float => animator.GetFloat(parameter.nameHash).ToString("F3"),
            AnimatorControllerParameterType.Int => animator.GetInteger(parameter.nameHash).ToString(),
            AnimatorControllerParameterType.Bool => animator.GetBool(parameter.nameHash).ToString(),
            _ => "trigger"
        };

        private static bool IsAlive(object value) => value is Object unityObject ? unityObject != null : value != null;
    }
}
