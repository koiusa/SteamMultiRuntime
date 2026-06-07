using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;

namespace Koiusa.SteamMultiRuntime.UnityChan {

[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class FaceAnimationDriver : MonoBehaviour {
    
    void OnEnable() {
        if (m_hasStarted)
            InitializeForCharacter();
    }

    void Awake() {
        EnsureAnimatorReference();
        RefreshFaceStateNames();
    }

    void Start() {
        InitializeForCharacter();
        m_hasStarted = true;
        m_shouldResetFaceLayerWeight = true;
        m_curFaceLayerWeight = 1f;
    }

    void InitializeForCharacter() {
        EnsureAnimatorReference();
        RefreshFaceStateNames();
        CacheFaceLayerMixer();
    }

    bool CanControlFaceLayer() {
        if (m_animator == null || m_animator.runtimeAnimatorController == null)
            return false;

        var controller = m_animator.runtimeAnimatorController;
        if (m_cachedController != controller) {
            CacheFaceLayerMixer();
        }

        return m_hasCachedFaceLayerMixer && m_cachedFaceLayerMixer.IsValid();
    }

    void CacheFaceLayerMixer() {
        m_hasCachedFaceLayerMixer = false;
        m_cachedFaceLayerMixer = default;
        m_cachedController = m_animator != null ? m_animator.runtimeAnimatorController : null;

        if (m_animator == null || m_cachedController == null)
            return;

        if (!TryGetFaceLayerMixer(m_animator, m_faceLayerIndex, out var layerMixer))
            return;

        m_cachedFaceLayerMixer = layerMixer;
        m_hasCachedFaceLayerMixer = true;
        ApplyFaceLayerMask();
    }

    void SetResolvedAnimator(Animator animator) {
        if (m_animator == animator)
            return;

        m_animator = animator;
        CacheFaceLayerMixer();
    }

    void RefreshFaceStateNames() {
        m_faceStateNames ??= new List<string>();

        if (m_animator == null || m_animator.runtimeAnimatorController == null)
            return;

        var clips = m_animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return;

        m_faceStateNames.Clear();
        var uniqueNames = new HashSet<string>();

        for (int i = 0; i < clips.Length; i++) {
            var clip = clips[i];
            if (clip == null || string.IsNullOrEmpty(clip.name))
                continue;

            if (uniqueNames.Add(clip.name)) {
                m_faceStateNames.Add(clip.name);
            }
        }

        m_faceStateNames.Sort();
    }

    void Update() {
        if (m_needsRecache) {
            m_needsRecache = false;
            CacheFaceLayerMixer();
        }

        if (!CanControlFaceLayer())
            return;

        if (m_shouldResetFaceLayerWeight) {
            m_curFaceLayerWeight = 1;
        } else if (!m_lockFace) {
            m_curFaceLayerWeight = Mathf.Lerp(m_curFaceLayerWeight, 0, m_delayWeight * Time.deltaTime);
        }

        m_animator.SetLayerWeight(m_faceLayerIndex, m_curFaceLayerWeight);
        UpdateReturnToDefaultTimer();
    }

    void ApplyFaceLayerMask() {
        if (m_faceAvatarMask == null || !m_hasCachedFaceLayerMixer || !m_cachedFaceLayerMixer.IsValid())
            return;

        Playable currentMixer = m_cachedFaceLayerMixer;
        if (m_maskAppliedAvatarMask == m_faceAvatarMask
            && m_maskAppliedMixer.IsValid()
            && m_maskAppliedMixer.Equals(currentMixer)) {
            return;
        }

        m_cachedFaceLayerMixer.SetLayerMaskFromAvatarMask((uint)m_faceLayerIndex, m_faceAvatarMask);
        m_maskAppliedMixer = currentMixer;
        m_maskAppliedAvatarMask = m_faceAvatarMask;
    }

    static bool TryGetFaceLayerMixer(Animator animator, int layerIndex, out AnimationLayerMixerPlayable layerMixer) {
        layerMixer = default;
        if (animator == null)
            return false;

        var graph = animator.playableGraph;
        if (!graph.IsValid())
            return false;

        int rootPlayableCount = graph.GetRootPlayableCount();
        for (int i = 0; i < rootPlayableCount; i++) {
            var rootPlayable = graph.GetRootPlayable(i);
            if (TryFindFaceLayerMixer(rootPlayable, layerIndex, out layerMixer))
                return true;
        }

        return false;
    }

    static bool TryFindFaceLayerMixer(Playable playable, int layerIndex, out AnimationLayerMixerPlayable layerMixer) {
        layerMixer = default;

        if (!playable.IsValid())
            return false;

        if (playable.GetPlayableType() == typeof(AnimationLayerMixerPlayable)) {
            var candidate = (AnimationLayerMixerPlayable)playable;
            if (candidate.IsValid() && candidate.GetInputCount() > layerIndex) {
                layerMixer = candidate;
                return true;
            }
        }

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++) {
            if (TryFindFaceLayerMixer(playable.GetInput(i), layerIndex, out layerMixer))
                return true;
        }

        return false;
    }

    void UpdateReturnToDefaultTimer() {
        if (!m_isAutoReturnPending || m_returnToDefaultDelay <= 0f)
            return;

        if (m_lockFace)
            return;

        if (!CanControlFaceLayer())
            return;

        m_autoReturnElapsed += Time.deltaTime;
        if (m_autoReturnElapsed < m_returnToDefaultDelay)
            return;

        m_isAutoReturnPending = false;

        if (!TryGetDefaultFaceStateName(out var defaultStateName))
            return;

        m_shouldResetFaceLayerWeight = true;
        m_animator.Play(defaultStateName, m_faceLayerIndex);
        m_needsRecache = true;
    }

    void PlayFaceState(string stateName) {
        if (!CanControlFaceLayer())
            return;

        if (!TryResolveFaceStateName(stateName, out var resolvedStateName)) {
            m_isAutoReturnPending = false;
            return;
        }

        m_animator.Play(resolvedStateName, m_faceLayerIndex);
        m_needsRecache = true;

        if (m_returnToDefaultDelay <= 0f) {
            m_isAutoReturnPending = false;
            return;
        }

        if (TryGetDefaultFaceStateName(out var defaultStateName) && !IsSameFaceState(resolvedStateName, defaultStateName)) {
            m_isAutoReturnPending = true;
            m_autoReturnElapsed = 0f;
        } else {
            m_isAutoReturnPending = false;
        }
    }

    bool TryGetDefaultFaceStateName(out string stateName) {
        stateName = null;

        if (string.IsNullOrWhiteSpace(m_defaultFaceStateName))
            return false;

        return TryResolveFaceStateName(m_defaultFaceStateName, out stateName);
    }

    static string NormalizeFaceStateKey(string stateName) {
        if (string.IsNullOrEmpty(stateName))
            return string.Empty;

        return stateName.Split('@')[0];
    }

    bool HasFaceState(string stateName) {
        if (string.IsNullOrEmpty(stateName) || m_animator == null)
            return false;

        return m_animator.HasState(m_faceLayerIndex, Animator.StringToHash(stateName));
    }

    bool TryResolveFaceStateName(string requestedStateName, out string resolvedStateName) {
        resolvedStateName = null;

        if (string.IsNullOrWhiteSpace(requestedStateName))
            return false;

        var requested = requestedStateName.Trim();
        if (HasFaceState(requested)) {
            resolvedStateName = requested;
            return true;
        }

        var normalizedRequested = NormalizeFaceStateKey(requested);
        if (HasFaceState(normalizedRequested)) {
            resolvedStateName = normalizedRequested;
            return true;
        }

        return false;
    }

    bool IsSameFaceState(string a, string b) {
        if (a == null || b == null)
            return false;

        return NormalizeFaceStateKey(a) == NormalizeFaceStateKey(b);
    }
//----------------------------------------------------------------------------------------------------------------------
    
    //Called by Events set in the AnimationClip asset
    private void OnCallChangeFace(string str) {
        EnsureAnimatorReference();

        str = NormalizeFaceStateKey(str); //the previous state names contain suffix with'@'

        if (TryResolveFaceStateName(str, out var resolvedStateName)) {
            TryOverrideFaceAnimation(resolvedStateName);
        }
    }

    void TryOverrideFaceAnimation(string str) {
        if (m_lockFace)
            return;
        
        m_shouldResetFaceLayerWeight = true;
        PlayFaceState(str);
    }
//----------------------------------------------------------------------------------------------------------------------

    private void OnValidate() {
        m_faceLayerIndex = Mathf.Max(0, m_faceLayerIndex);
        EnsureAnimatorReference();
        RefreshFaceStateNames();
        CacheFaceLayerMixer();
    }
    

//----------------------------------------------------------------------------------------------------------------------
    
    [SerializeField] private Animator m_animator;
    [SerializeField] private AvatarMask m_faceAvatarMask;
    [SerializeField, Min(0)] private int m_faceLayerIndex = 1;
    [SerializeField] private string m_defaultFaceStateName = "default";
    [SerializeField] private bool m_lockFace = false;
    [FormerlySerializedAs("delayWeight")] [SerializeField] private float m_delayWeight;
    [SerializeField, Min(0f)] private float m_returnToDefaultDelay = 2f;
    
    [HideInInspector][SerializeField] private List<string> m_faceStateNames;
    
    //----------------------------------------------------------------------------------------------------------------------

    float m_curFaceLayerWeight = 0;
    bool m_shouldResetFaceLayerWeight = false;
    bool m_hasStarted = false;
    bool m_needsRecache = false;

    bool m_isAutoReturnPending = false;
    float m_autoReturnElapsed = 0f;
    AnimationLayerMixerPlayable m_cachedFaceLayerMixer;
    RuntimeAnimatorController m_cachedController;
    bool m_hasCachedFaceLayerMixer;
    Playable m_maskAppliedMixer;
    AvatarMask m_maskAppliedAvatarMask;

    void EnsureAnimatorReference() {
        if (m_animator == null) {
            SetResolvedAnimator(GetComponent<Animator>());
        }
    }
}

}