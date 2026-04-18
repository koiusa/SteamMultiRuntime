using System.Reflection;
using UnityEngine;

public class FootstepCollider : MonoBehaviour
{
    [Tooltip("Layer mask for ground detection (set to the same as your character's GroundLayers)")]
    public LayerMask GroundLayers = ~0;

    [Tooltip("Minimum interval between footstep sounds from this collider (s)")]
    public float MinInterval = 0.1f;

    [Tooltip("Sphere trigger radius (m)")]
    public float ColliderRadius = 0.08f;

    [Tooltip("Optional direct receiver (set by spawner). If set, PlayFootstep will be invoked directly on this component.")]
    public MonoBehaviour PlayReceiver;

    [Tooltip("If true, also triggers while staying in contact. Usually keep false to avoid over-triggering.")]
    public bool UseTriggerStay = false;

    private float _lastPlayTime = -1f;
    private Component _playReceiverReflection;
    private MethodInfo _playMethod;
    private MethodInfo _playReceiverMethod;
    private MethodInfo _landMethod;
    private MethodInfo _playReceiverLandMethod;

    private void Awake()
    {
        EnsureDetectionOnlyCollider();
        CacheFallbackReceiver();
        CacheDirectReceiverMethod();
    }

    private void OnValidate()
    {
        EnsureDetectionOnlyCollider();
    }

    public void EnsureDetectionOnlyCollider()
    {
        var sphere = GetComponent<SphereCollider>();
        if (sphere == null)
        {
            sphere = gameObject.AddComponent<SphereCollider>();
        }

        sphere.isTrigger = true;
        sphere.radius = Mathf.Max(0.001f, ColliderRadius);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTrigger(other, true);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!UseTriggerStay) return;
        HandleTrigger(other, false);
    }

    private void HandleTrigger(Collider other, bool isEnter)
    {
        if (!IsValidGround(other)) return;

        var pos = transform.position;

        if (isEnter)
        {
            TryInvokeLand(pos);
        }

        if (Time.time - _lastPlayTime < MinInterval) return;

        _lastPlayTime = Time.time;

        if (TryInvokeDirectReceiver(pos)) return;

        if (_playReceiverReflection != null && _playMethod != null)
        {
            try
            {
                _playMethod.Invoke(_playReceiverReflection, new object[] { pos });
                return;
            }
            catch
            {
            }
        }

        gameObject.SendMessageUpwards("PlayFootstep", pos, SendMessageOptions.DontRequireReceiver);
    }

    private bool IsValidGround(Collider other)
    {
        if (other == null || other.isTrigger) return false;
        if (other.transform.root == transform.root) return false;
        return (GroundLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void CacheFallbackReceiver()
    {
        var comps = GetComponentsInParent<MonoBehaviour>(true);
        foreach (var c in comps)
        {
            if (c == null) continue;
            var footstepMi = c.GetType().GetMethod("PlayFootstep", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null);
            var landMi = c.GetType().GetMethod("PlayLand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null);
            if (footstepMi != null || landMi != null)
            {
                _playReceiverReflection = c;
                _playMethod = footstepMi;
                _landMethod = landMi;
                break;
            }
        }
    }

    private void CacheDirectReceiverMethod()
    {
        _playReceiverMethod = null;
        _playReceiverLandMethod = null;
        if (PlayReceiver == null) return;

        _playReceiverMethod = PlayReceiver.GetType().GetMethod("PlayFootstep", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null);
        _playReceiverLandMethod = PlayReceiver.GetType().GetMethod("PlayLand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null);
    }

    private bool TryInvokeLand(Vector3 pos)
    {
        if (PlayReceiver != null)
        {
            if (_playReceiverLandMethod == null)
            {
                CacheDirectReceiverMethod();
            }

            if (_playReceiverLandMethod != null)
            {
                try
                {
                    _playReceiverLandMethod.Invoke(PlayReceiver, new object[] { pos });
                    return true;
                }
                catch
                {
                }
            }
        }

        if (_playReceiverReflection != null && _landMethod != null)
        {
            try
            {
                _landMethod.Invoke(_playReceiverReflection, new object[] { pos });
                return true;
            }
            catch
            {
            }
        }

        gameObject.SendMessageUpwards("PlayLand", pos, SendMessageOptions.DontRequireReceiver);
        return false;
    }

    private bool TryInvokeDirectReceiver(Vector3 pos)
    {
        if (PlayReceiver == null) return false;

        if (_playReceiverMethod == null)
        {
            CacheDirectReceiverMethod();
        }

        if (_playReceiverMethod == null) return false;

        try
        {
            _playReceiverMethod.Invoke(PlayReceiver, new object[] { pos });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
