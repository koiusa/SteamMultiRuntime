using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

namespace Koiusa.SteamMultiRuntime
{
    public class FootstepColliderSpawner : MonoBehaviour, IFootstepReceiver
    {
        [Tooltip("Optional Animator to use. If null, Animator will be auto-detected from loaded VRM or current hierarchy.")]
        public Animator TargetAnimator;

        [Tooltip("Optional reference to a component (e.g. ThirdPersonController) to copy GroundLayers from.")]
        public Component ControllerComponent;

        [Tooltip("Radius of spawned sphere trigger collider (m)")]
        public float ColliderRadius = 0.08f;

        [Tooltip("Local offset applied to the collider relative to the foot bone")]
        public Vector3 LocalOffset = Vector3.zero;

        [Tooltip("Minimum interval between footstep sounds from this collider (s)")]
        public float MinInterval = 0.2f;

        [Tooltip("Layer mask to treat as ground for footstep detection")]
        public LayerMask GroundLayers = ~0;

        [Tooltip("If true, uses HumanBodyBones.LeftFoot/RightFoot. If false, creates one collider at animator root.")]
        public bool UseHumanBones = true;

        private int _animIDSpeed;
        private int _animIDLocomotionMode;
        private int _animIDFallSpeed;

        [Header("Animator Conditions")]
        [Tooltip("Animator float parameter name used to check movement speed.")]
        public string SpeedParamName = "Speed";

        [Tooltip("Minimum speed required to play footstep.")]
        public float MinFootstepSpeed = 0.1f;

        [FormerlySerializedAs("GroundedParamName")]
        [Tooltip("Animator int parameter used to check locomotion mode. Grounded must be 0.")]
        public string LocomotionModeParamName = "LocomotionMode";

        [Tooltip("If true, footsteps are played only when LocomotionMode is Grounded (0).")]
        public bool RequireGroundedForFootstep = true;

        [Tooltip("If true, landing is played only when LocomotionMode is Grounded (0).")]
        public bool RequireGroundedForLand = true;

        [Tooltip("If true, landing is played only when grounded transitions from false to true.")]
        public bool RequireGroundedTransitionForLand = true;

        [Tooltip("Minimum interval between landing sounds (s).")]
        public float MinLandInterval = 0.2f;

        [Tooltip("Animator float parameter name used to check vertical/fall speed.")]
        public string FallSpeedParamName = "VerticalVelocity";

        [Tooltip("If true, landing requires fall speed condition.")]
        public bool RequireFallSpeedForLand = true;

        [Tooltip("Landing requires fall speed <= this value (usually negative).")]
        public float FallSpeedForLandThreshold = -0.5f;

        [Tooltip("Maximum distance at which footstep sounds can be heard.")]
        public float MaxDistance = 500f;

        [Header("3D Audio")]
        [Tooltip("Force spatial (3D) settings to prevent hearing footsteps from too far away.")]
        public bool Force3DAudio = true;

        [Tooltip("Minimum distance for 3D attenuation.")]
        public float MinDistance = 1f;

        [Tooltip("Rolloff mode for footstep/landing audio sources.")]
        public AudioRolloffMode RolloffMode = AudioRolloffMode.Logarithmic;

        [Tooltip("Output AudioMixerGroup for character audio.")]
        public AudioMixerGroup CharacterAudioMixer;

        [Tooltip("AudioSource used for footstep sounds. If null, a new one will be created.")]
        public AudioSource FootstepAudioSource;

        [Tooltip("AudioSource used for landing sounds. If null, a new one will be created.")]
        public AudioSource LandingAudioSource;

        [Range(0f, 1f)]
        public float FootstepAudioVolume = 1f;

        private bool _hasSpeedParam;
        private bool _hasLocomotionModeParam;
        private bool _hasFallSpeedParam;
        private float _lastFootstepTime = -10f;
        private float _lastLandTime = -10f;
        private bool _hasGroundedState;
        private bool _prevGrounded;
        private bool _pendingLandFromTransition;

        private void OnEnable()
        {
            TrySetupFromCurrentHierarchy();
        }

        private void Update()
        {
            UpdateGroundedTransitionState();
        }

        private void TrySetupFromCurrentHierarchy()
        {
            var animator = TargetAnimator != null ? TargetAnimator : GetComponentInChildren<Animator>(true);
            SetupForAnimator(animator, gameObject.name);
        }

        // Called when a character model is loaded by the system
        public void SetCharacterRoot(GameObject characterRoot)
        {
            if (characterRoot == null) return;

            var animator = TargetAnimator != null ? TargetAnimator : characterRoot.GetComponentInChildren<Animator>(true);
            SetupForAnimator(animator, characterRoot.name);
        }

        private void SetupForAnimator(Animator animator, string sourceName)
        {
            if (animator == null)
            {
                Debug.LogWarning($"FootstepColliderSpawner: Animator not found on '{sourceName}'");
                return;
            }

            TargetAnimator = animator;

            _animIDSpeed = Animator.StringToHash(SpeedParamName);
            _animIDLocomotionMode = Animator.StringToHash(LocomotionModeParamName);
            _animIDFallSpeed = Animator.StringToHash(FallSpeedParamName);
            _hasSpeedParam = HasAnimatorParam(TargetAnimator, _animIDSpeed, AnimatorControllerParameterType.Float);
            _hasLocomotionModeParam = HasAnimatorParam(TargetAnimator, _animIDLocomotionMode, AnimatorControllerParameterType.Int);
            _hasFallSpeedParam = HasAnimatorParam(TargetAnimator, _animIDFallSpeed, AnimatorControllerParameterType.Float);

            if (_hasLocomotionModeParam)
            {
                _prevGrounded = IsAnimatorGrounded();
                _hasGroundedState = true;
                _pendingLandFromTransition = false;
            }
            else
            {
                _hasGroundedState = false;
                _pendingLandFromTransition = false;
            }

            if (GroundLayers == 0 && ControllerComponent is IGroundLayerProvider groundLayerProvider)
            {
                GroundLayers = groundLayerProvider.GroundLayers;
            }

            MinInterval = Mathf.Max(MinInterval, 0.01f);
            MinLandInterval = Mathf.Max(MinLandInterval, 0.01f);
            MaxDistance = Mathf.Max(MaxDistance, 0.1f);
            MinDistance = Mathf.Clamp(MinDistance, 0f, MaxDistance - 0.01f);

            ConfigureAudioSource(FootstepAudioSource);
            ConfigureAudioSource(LandingAudioSource);

            if (UseHumanBones)
            {
                var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);

                if (left != null)
                    CreateFootCollider(left, "FootCollider_Left");

                if (right != null)
                    CreateFootCollider(right, "FootCollider_Right");
            }
            else
            {
                CreateFootCollider(TargetAnimator.transform, "FootCollider_Auto");
            }
        }

        private bool HasAnimatorParam(Animator animator, int paramHash, AnimatorControllerParameterType type)
        {
            if (animator == null) return false;
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.nameHash == paramHash && p.type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void CreateFootCollider(Transform footBone, string name)
        {
            if (footBone == null) return;

            var existing = footBone.Find(name);
            if (existing != null)
            {
                var existingCollider = existing.GetComponent<FootstepCollider>();
                if (existingCollider != null)
                {
                    existingCollider.GroundLayers = GroundLayers;
                    existingCollider.MinInterval = MinInterval;
                    existingCollider.ColliderRadius = ColliderRadius;
                    existingCollider.PlayReceiver = this;
                    existingCollider.EnsureDetectionOnlyCollider();
                    return;
                }
            }

            var go = new GameObject(name);
            go.transform.SetParent(footBone, false);
            go.transform.localPosition = LocalOffset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var fc = go.AddComponent<FootstepCollider>();
            fc.GroundLayers = GroundLayers;
            fc.MinInterval = MinInterval;
            fc.ColliderRadius = ColliderRadius;
            fc.PlayReceiver = this;
            fc.EnsureDetectionOnlyCollider();

            go.hideFlags = HideFlags.None;
        }

        public void PlayFootstep(Vector3 worldPosition)
        {
            if (TargetAnimator == null) return;
            if (_hasSpeedParam && TargetAnimator.GetFloat(_animIDSpeed) < MinFootstepSpeed) return;
            if (RequireGroundedForFootstep && _hasLocomotionModeParam && !IsAnimatorGrounded()) return;
            if (Time.time - _lastFootstepTime < MinInterval) return;

            var source = FootstepAudioSource;
            if (source == null) return;

            _lastFootstepTime = Time.time;
            ConfigureAudioSource(source);
            source.volume = FootstepAudioVolume;
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, MaxDistance);
            source.transform.position = worldPosition;
            source.Play();
        }

        private void UpdateGroundedTransitionState()
        {
            if (TargetAnimator == null || !_hasLocomotionModeParam) return;

            var grounded = IsAnimatorGrounded();

            if (_hasGroundedState)
            {
                if (!_prevGrounded && grounded)
                {
                    _pendingLandFromTransition = true;
                }
            }
            else
            {
                _hasGroundedState = true;
            }

            _prevGrounded = grounded;
        }

        public void PlayLand(Vector3 worldPosition)
        {
            if (TargetAnimator == null) return;

            UpdateGroundedTransitionState();

            if (RequireGroundedForLand && _hasLocomotionModeParam && !IsAnimatorGrounded()) return;
            if (RequireGroundedTransitionForLand && _hasLocomotionModeParam && !_pendingLandFromTransition) return;
            if (RequireFallSpeedForLand && _hasFallSpeedParam && TargetAnimator.GetFloat(_animIDFallSpeed) < FallSpeedForLandThreshold) return;
            if (Time.time - _lastLandTime < MinLandInterval) return;

            _lastLandTime = Time.time;
            _pendingLandFromTransition = false;

            var source = LandingAudioSource;
            if (source == null) return;

            ConfigureAudioSource(source);
            source.volume = FootstepAudioVolume;
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, MaxDistance);
            source.transform.position = worldPosition;
            source.Play();
        }

        private bool IsAnimatorGrounded()
        {
            return TargetAnimator != null && TargetAnimator.GetInteger(_animIDLocomotionMode) == 0;
        }

        private void ConfigureAudioSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            if (CharacterAudioMixer != null)
            {
                source.outputAudioMixerGroup = CharacterAudioMixer;
            }

            if (!Force3DAudio)
            {
                return;
            }

            source.spatialBlend = 1f;
            source.rolloffMode = RolloffMode;
            source.minDistance = MinDistance;
            source.maxDistance = Mathf.Max(MinDistance + 0.01f, MaxDistance);
        }
    }
}
