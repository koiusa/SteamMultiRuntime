using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Koiusa.SteamMultiRuntime.UnityChan {

[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class FaceAnimationDriver : MonoBehaviour {
    
    void OnEnable() {
        if (m_hasStarted)
            EnsureAnimatorReference();
    }

    void Awake() {
        EnsureAnimatorReference();
        RefreshFaceStateNames();
    }

    void Start() {
        m_hasStarted = true;
        m_shouldResetFaceLayerWeight = true;
        m_curFaceLayerWeight = 1f;
    }

    bool CanControlFaceLayer() {
        if (m_animator == null || m_animator.runtimeAnimatorController == null)
            return false;

        return m_faceLayerIndex < m_animator.layerCount;
    }

    void SetResolvedAnimator(Animator animator) {
        if (m_animator == animator)
            return;

        m_animator = animator;
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
        if (!Application.isPlaying)
            return;

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
    }

    void PlayFaceState(string stateName) {
        if (!CanControlFaceLayer())
            return;

        if (!TryResolveFaceStateName(stateName, out var resolvedStateName)) {
            m_isAutoReturnPending = false;
            return;
        }

        m_animator.Play(resolvedStateName, m_faceLayerIndex);

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
    }
    

//----------------------------------------------------------------------------------------------------------------------
    
    [SerializeField] private Animator m_animator;
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

    bool m_isAutoReturnPending = false;
    float m_autoReturnElapsed = 0f;

    void EnsureAnimatorReference() {
        if (m_animator == null) {
            SetResolvedAnimator(GetComponent<Animator>());
        }
    }
}

}
