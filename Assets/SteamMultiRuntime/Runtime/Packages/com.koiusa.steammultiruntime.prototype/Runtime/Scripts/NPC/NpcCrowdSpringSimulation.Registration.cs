using UnityEngine;
using LegacySpringBone = global::UnityChan.SpringBone;
using LegacySpringCollider = global::UnityChan.SpringCollider;
using LegacySpringManager = global::UnityChan.SpringManager;
using UtjSpringBone = UTJ.SpringBone;
using UtjCapsuleCollider = UTJ.SpringCapsuleCollider;
using UtjSpringManager = UTJ.SpringManager;
using UtjPanelCollider = UTJ.SpringPanelCollider;
using UtjSphereCollider = UTJ.SpringSphereCollider;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed partial class NpcCrowdSpringSimulation
    {
        private readonly System.Collections.Generic.HashSet<int> observedNpcRoots = new();

        internal static void ObserveNpc(GameObject npcRoot)
        {
            if (npcRoot == null)
                return;
            var simulation = EnsureInstance();
            simulation.observedNpcRoots.Add(npcRoot.GetInstanceID());
            var loader = npcRoot.GetComponent<CharacterPrefabLoader>();
            if (loader != null && loader.LastInstantiatedObject != null)
                NpcCrowdModelPresentation.Configure(loader.LastInstantiatedObject);
        }

        internal static void UnobserveNpc(GameObject npcRoot)
        {
            if (instance != null && npcRoot != null)
                instance.observedNpcRoots.Remove(npcRoot.GetInstanceID());
        }

        internal static void RegisterModel(GameObject root)
        {
            if (root != null)
                EnsureInstance().Register(root);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static NpcCrowdSpringSimulation EnsureInstance()
        {
            if (instance != null)
                return instance;
            var host = new GameObject("NpcCrowdSpringSimulation") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            instance = host.AddComponent<NpcCrowdSpringSimulation>();
            return instance;
        }

        private void OnEnable() => CharacterPrefabLoader.AnyPrefabInstantiated += OnAnyPrefabInstantiated;

        private void OnDisable() => CharacterPrefabLoader.AnyPrefabInstantiated -= OnAnyPrefabInstantiated;

        private void OnAnyPrefabInstantiated(CharacterPrefabLoader loader, GameObject model)
        {
            if (loader == null || model == null || !observedNpcRoots.Contains(loader.gameObject.GetInstanceID()))
                return;
            NpcCrowdModelPresentation.Configure(model);
        }

        private void Register(GameObject root)
        {
            if (!registeredRoots.Add(root.GetInstanceID()))
                return;
            var rig = new Rig
            {
                Root = root,
                AnimatorDriver = root.GetComponentInParent<ActorAnimatorStateDriver>()
                    ?? root.GetComponentInChildren<ActorAnimatorStateDriver>(true),
                LastTick = Time.time
            };
            var utjManagers = root.GetComponentsInChildren<UtjSpringManager>(true);
            for (var i = 0; i < utjManagers.Length; i++)
            {
                var manager = utjManagers[i];
                var bones = manager.springBones;
                if (bones == null || bones.Length == 0)
                    bones = manager.GetComponentsInChildren<UtjSpringBone>(true);
                var previousBoneCount = rig.Bones.Count;
                for (var j = 0; j < bones.Length; j++)
                    AddUtjBone(rig, manager, bones[j]);
                if (rig.Bones.Count == previousBoneCount)
                    continue;
                manager.automaticUpdates = false;
                manager.enabled = false;
            }
            var legacyManagers = root.GetComponentsInChildren<LegacySpringManager>(true);
            for (var i = 0; i < legacyManagers.Length; i++)
            {
                var manager = legacyManagers[i];
                var bones = manager.GetSpringBones();
                if (bones == null || bones.Length == 0)
                    bones = manager.GetComponentsInChildren<LegacySpringBone>(true);
                var previousBoneCount = rig.Bones.Count;
                for (var j = 0; bones != null && j < bones.Length; j++)
                    AddLegacyBone(rig, manager, bones[j]);
                if (rig.Bones.Count == previousBoneCount)
                    continue;
                manager.enabled = false;
            }
            if (rig.Bones.Count == 0)
            {
                registeredRoots.Remove(root.GetInstanceID());
                return;
            }
            rigs.Add(rig);
            layoutDirty = true;
        }

        private static void AddUtjBone(Rig rig, UtjSpringManager manager, UtjSpringBone source)
        {
            if (source == null || source.transform.parent == null)
                return;
            var tip = source.ComputeChildPosition();
            var direction = (tip - source.transform.position).normalized;
            AddBone(rig, source.transform, source.transform.InverseTransformDirection(direction), tip,
                source.stiffnessForce, source.dragForce, source.springForce + manager.gravity,
                manager.dynamicRatio, false, source.radius, source.sphereColliders,
                source.capsuleColliders, source.panelColliders, null);
            source.enabled = false;
        }

        private static void AddLegacyBone(Rig rig, LegacySpringManager manager, LegacySpringBone source)
        {
            if (source == null || source.child == null || source.transform.parent == null)
                return;
            AddBone(rig, source.transform, source.boneAxis.normalized, source.child.position,
                source.stiffnessForce, source.dragForce, source.springForce,
                manager.GetDynamicRatio(), true, source.radius, null, null, null, source.colliders);
            source.enabled = false;
        }

        private static void AddBone(Rig rig, Transform transform, Vector3 axis, Vector3 tip,
            float stiffness, float drag, Vector3 force, float ratio, bool legacy, float radius,
            UtjSphereCollider[] spheres, UtjCapsuleCollider[] capsules, UtjPanelCollider[] panels,
            LegacySpringCollider[] legacySpheres)
        {
            var bone = new Bone
            {
                Transform = transform,
                LocalAxis = axis,
                InitialLocalRotation = transform.localRotation,
                RestLength = Mathf.Max(0.0001f, Vector3.Distance(transform.position, tip)),
                Stiffness = Mathf.Max(0f, stiffness),
                Drag = Mathf.Clamp01(drag),
                Force = Vector3.ClampMagnitude(force, 100f),
                DynamicRatio = Mathf.Clamp01(ratio),
                CurrentTip = tip,
                PreviousTip = tip,
                Legacy = legacy,
                Radius = Mathf.Max(0f, radius)
            };
            for (var i = 0; spheres != null && i < spheres.Length; i++)
                if (spheres[i] != null && (spheres[i].linkedRenderer == null || spheres[i].linkedRenderer.enabled))
                    bone.Colliders.Add(CreateCollider(spheres[i], 0, spheres[i].radius, 0f, 0f));
            for (var i = 0; capsules != null && i < capsules.Length; i++)
                if (capsules[i] != null && (capsules[i].linkedRenderer == null || capsules[i].linkedRenderer.enabled))
                    bone.Colliders.Add(CreateCollider(capsules[i], 1, capsules[i].radius, capsules[i].height, 0f));
            for (var i = 0; panels != null && i < panels.Length; i++)
                if (panels[i] != null && (panels[i].linkedRenderer == null || panels[i].linkedRenderer.enabled))
                    bone.Colliders.Add(CreateCollider(panels[i], 2, 0f, panels[i].height, panels[i].width));
            for (var i = 0; legacySpheres != null && i < legacySpheres.Length; i++)
                if (legacySpheres[i] != null)
                    bone.Colliders.Add(CreateCollider(legacySpheres[i], 0, legacySpheres[i].radius, 0f, 0f));
            rig.Bones.Add(bone);
        }

        private static ColliderRef CreateCollider(Component component, int type, float radius, float height, float width) => new()
        {
            Component = component,
            Transform = component.transform,
            Type = type,
            Radius = Mathf.Max(0f, radius),
            Height = Mathf.Max(0f, height),
            Width = Mathf.Max(0f, width)
        };
    }
}
