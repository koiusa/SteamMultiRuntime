using Koiusa.SteamMultiRuntime;
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
    private IFootstepReceiver _receiver;

    private void Awake()
    {
        EnsureDetectionOnlyCollider();
        CacheReceiver();
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
            ResolveReceiver()?.PlayLand(pos);
        }

        if (Time.time - _lastPlayTime < MinInterval) return;

        _lastPlayTime = Time.time;

        ResolveReceiver()?.PlayFootstep(pos);
    }

    private bool IsValidGround(Collider other)
    {
        if (other == null || other.isTrigger) return false;
        if (other.transform.root == transform.root) return false;
        return (GroundLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void CacheReceiver()
    {
        _receiver = PlayReceiver as IFootstepReceiver;
        if (_receiver != null)
        {
            return;
        }
        foreach (var component in GetComponentsInParent<MonoBehaviour>(true))
        {
            if (component is IFootstepReceiver receiver)
            {
                _receiver = receiver;
                return;
            }
        }
    }

    private IFootstepReceiver ResolveReceiver()
    {
        if (_receiver == null)
        {
            CacheReceiver();
        }

        return _receiver;
    }
}
