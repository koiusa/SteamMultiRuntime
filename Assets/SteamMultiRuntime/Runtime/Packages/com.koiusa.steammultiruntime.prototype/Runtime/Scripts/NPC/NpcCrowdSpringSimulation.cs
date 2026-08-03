using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Centralized Burst spring solver used only by NPC presentation models.</summary>
    internal sealed partial class NpcCrowdSpringSimulation : MonoBehaviour
    {
        private struct SpringData
        {
            public float3 LocalAxis;
            public quaternion InitialLocalRotation;
            public float RestLength;
            public float Stiffness;
            public float Drag;
            public float3 Force;
            public float DynamicRatio;
            public float3 CurrentTip;
            public float3 PreviousTip;
            public float DeltaTime;
            public int Legacy;
            public int Active;
            public float Radius;
            public int ColliderStart;
            public int ColliderCount;
        }

        private struct ColliderStatic
        {
            public int Type;
            public float Radius;
            public float Height;
            public float Width;
        }

        private struct ColliderWorld
        {
            public float3 Center;
            public float3 End;
            public float3 Normal;
            public float Radius;
            public float HalfWidth;
            public float HalfHeight;
        }

        private struct AnimatedPose
        {
            public float3 Position;
            public quaternion WorldRotation;
            public quaternion LocalRotation;
        }

        private struct SolvedPose
        {
            public float3 Position;
            public quaternion WorldRotation;
            public quaternion LocalRotation;
        }

        private struct RigRange
        {
            public int Start;
            public int Count;
        }

        [BurstCompile]
        private struct ColliderSnapshotJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<ColliderStatic> Statics;
            [WriteOnly] public NativeArray<ColliderWorld> Worlds;

            public void Execute(int index, TransformAccess transform)
            {
                var item = Statics[index];
                var matrix = transform.localToWorldMatrix;
                var axisX = new float3(matrix.m00, matrix.m10, matrix.m20);
                var axisY = new float3(matrix.m01, matrix.m11, matrix.m21);
                var axisZ = new float3(matrix.m02, matrix.m12, matrix.m22);
                var scaleX = math.length(axisX);
                var scaleY = math.length(axisY);
                var scaleZ = math.length(axisZ);
                var center = new float3(matrix.m03, matrix.m13, matrix.m23);
                Worlds[index] = new ColliderWorld
                {
                    Center = center,
                    End = center + math.normalizesafe(axisY, new float3(0f, 1f, 0f)) * item.Height * scaleY,
                    Normal = math.normalizesafe(axisZ, new float3(0f, 0f, 1f)),
                    Radius = item.Radius * math.max(scaleX, math.max(scaleY, scaleZ)),
                    HalfWidth = item.Width * 0.5f * scaleX,
                    HalfHeight = item.Height * 0.5f * scaleY
                };
            }
        }

        [BurstCompile]
        private struct PoseSnapshotJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<SpringData> Data;
            [WriteOnly] public NativeArray<AnimatedPose> Poses;

            public void Execute(int index, TransformAccess transform)
            {
                if (Data[index].Active == 0)
                    return;
                Poses[index] = new AnimatedPose
                {
                    Position = ToFloat3(transform.position),
                    WorldRotation = ToMath(transform.rotation),
                    LocalRotation = ToMath(transform.localRotation)
                };
            }

            private static float3 ToFloat3(Vector3 value) => new(value.x, value.y, value.z);
            private static quaternion ToMath(Quaternion value) => new(value.x, value.y, value.z, value.w);
        }

        [BurstCompile]
        private struct SpringRigJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<SpringData> Data;
            [ReadOnly] public NativeArray<AnimatedPose> AnimatedPoses;
            [NativeDisableParallelForRestriction] public NativeArray<SolvedPose> SolvedPoses;
            [ReadOnly] public NativeArray<int> ParentSpringIndices;
            [ReadOnly] public NativeArray<RigRange> RigRanges;
            [ReadOnly] public NativeArray<int> ActiveRigIndices;
            [ReadOnly] public NativeArray<int> ColliderIndices;
            [ReadOnly] public NativeArray<ColliderStatic> ColliderStatics;
            [ReadOnly] public NativeArray<ColliderWorld> ColliderWorlds;
            public int DisableColliders;

            public void Execute(int rigIndex)
            {
                var range = RigRanges[ActiveRigIndices[rigIndex]];
                for (var offset = 0; offset < range.Count; offset++)
                    Solve(range.Start + offset);
            }

            private void Solve(int index)
            {
                var data = Data[index];
                var animated = AnimatedPoses[index];
                var head = animated.Position;
                var worldAnimated = animated.WorldRotation;
                var parentSpringIndex = ParentSpringIndices[index];
                if (parentSpringIndex >= 0)
                {
                    var animatedParent = AnimatedPoses[parentSpringIndex];
                    var solvedParent = SolvedPoses[parentSpringIndex];
                    var parentDelta = math.mul(solvedParent.WorldRotation, math.inverse(animatedParent.WorldRotation));
                    head = solvedParent.Position + math.mul(parentDelta, animated.Position - animatedParent.Position);
                    worldAnimated = math.mul(parentDelta, animated.WorldRotation);
                }
                if (data.Active == 0)
                {
                    SolvedPoses[index] = new SolvedPose
                    {
                        Position = head,
                        WorldRotation = worldAnimated,
                        LocalRotation = animated.LocalRotation
                    };
                    return;
                }
                data.Active = 0;

                var parentRotation = math.mul(worldAnimated, math.inverse(animated.LocalRotation));
                var baseWorldRotation = math.mul(parentRotation, data.InitialLocalRotation);
                var restDirection = math.normalizesafe(
                    math.mul(baseWorldRotation, data.LocalAxis), new float3(0f, -1f, 0f));
                var restTip = head + restDirection * data.RestLength;
                var inertia = (data.CurrentTip - data.PreviousTip) * math.saturate(1f - data.Drag);
                float3 nextTip;
                if (data.Legacy != 0)
                {
                    nextTip = data.CurrentTip + inertia
                        + restDirection * data.Stiffness + data.Force;
                }
                else
                {
                    var dt2 = data.DeltaTime * data.DeltaTime;
                    var acceleration = (restTip - data.CurrentTip) * data.Stiffness + data.Force;
                    nextTip = data.CurrentTip + inertia + acceleration * (0.5f * dt2);
                }

                var direction = math.normalizesafe(nextTip - head, restDirection);
                nextTip = head + direction * data.RestLength;
                for (var colliderOffset = 0; DisableColliders == 0 && colliderOffset < data.ColliderCount; colliderOffset++)
                {
                    var colliderIndex = ColliderIndices[data.ColliderStart + colliderOffset];
                    var colliderStatic = ColliderStatics[colliderIndex];
                    var collider = ColliderWorlds[colliderIndex];
                    switch (colliderStatic.Type)
                    {
                        case 0:
                            ResolveSphere(ref nextTip, collider.Center, collider.Radius + data.Radius);
                            break;
                        case 1:
                            ResolveCapsule(ref nextTip, collider.Center, collider.End, collider.Radius + data.Radius);
                            break;
                        case 2:
                            ResolvePanel(ref nextTip, collider, data.Radius);
                            break;
                    }
                    direction = math.normalizesafe(nextTip - head, restDirection);
                    nextTip = head + direction * data.RestLength;
                }
                quaternion springLocal;
                if (data.Legacy != 0)
                {
                    var delta = FromTo(restDirection, direction);
                    var springWorld = math.mul(delta, baseWorldRotation);
                    springLocal = math.mul(math.inverse(parentRotation), springWorld);
                }
                else
                {
                    var localDirection = math.normalizesafe(
                        math.mul(math.inverse(baseWorldRotation), direction), data.LocalAxis);
                    var aimRotation = FromTo(data.LocalAxis, localDirection);
                    springLocal = math.mul(data.InitialLocalRotation, aimRotation);
                }
                var animationLocalRotation = data.Legacy != 0
                    ? data.InitialLocalRotation
                    : animated.LocalRotation;
                var localRotation = math.slerp(
                    animationLocalRotation, springLocal, math.saturate(data.DynamicRatio));
                SolvedPoses[index] = new SolvedPose
                {
                    Position = head,
                    WorldRotation = math.mul(parentRotation, localRotation),
                    LocalRotation = localRotation
                };
                data.PreviousTip = data.CurrentTip;
                data.CurrentTip = nextTip;
                Data[index] = data;
            }

            private static void ResolveSphere(ref float3 point, float3 center, float radius)
            {
                var offset = point - center;
                var distanceSq = math.lengthsq(offset);
                if (distanceSq >= radius * radius)
                    return;
                var normal = math.normalizesafe(offset, new float3(0f, 1f, 0f));
                point = center + normal * radius;
            }

            private static void ResolveCapsule(ref float3 point, float3 start, float3 end, float radius)
            {
                var segment = end - start;
                var segmentLengthSq = math.lengthsq(segment);
                var t = segmentLengthSq > 0.000001f
                    ? math.saturate(math.dot(point - start, segment) / segmentLengthSq)
                    : 0f;
                ResolveSphere(ref point, start + segment * t, radius);
            }

            private static void ResolvePanel(ref float3 point, ColliderWorld panel, float radius)
            {
                var relative = point - panel.Center;
                var signedDistance = math.dot(relative, panel.Normal);
                if (signedDistance >= radius)
                    return;
                var up = math.normalizesafe(panel.End - panel.Center, new float3(0f, 1f, 0f));
                var right = math.normalizesafe(math.cross(up, panel.Normal), new float3(1f, 0f, 0f));
                if (math.abs(math.dot(relative, right)) > panel.HalfWidth + radius
                    || math.abs(math.dot(relative, up)) > panel.HalfHeight + radius)
                    return;
                point += panel.Normal * (radius - signedDistance);
            }

            private static quaternion FromTo(float3 from, float3 to)
            {
                var dot = math.clamp(math.dot(from, to), -1f, 1f);
                if (dot > 0.99999f)
                    return quaternion.identity;
                if (dot < -0.99999f)
                {
                    var axis = math.normalizesafe(math.cross(from, new float3(1f, 0f, 0f)));
                    if (math.lengthsq(axis) < 0.0001f)
                        axis = math.normalizesafe(math.cross(from, new float3(0f, 1f, 0f)));
                    return quaternion.AxisAngle(axis, math.PI);
                }
                var cross = math.cross(from, to);
                return math.normalize(new quaternion(cross.x, cross.y, cross.z, 1f + dot));
            }

        }

        private sealed class Bone
        {
            public Transform Transform;
            public Vector3 LocalAxis;
            public Quaternion InitialLocalRotation;
            public float RestLength;
            public float Stiffness;
            public float Drag;
            public Vector3 Force;
            public float DynamicRatio;
            public Vector3 CurrentTip;
            public Vector3 PreviousTip;
            public bool Legacy;
            public int Index;
            public float Radius;
            public readonly List<ColliderRef> Colliders = new();
        }

        private sealed class ColliderRef
        {
            public Component Component;
            public Transform Transform;
            public int Type;
            public float Radius;
            public float Height;
            public float Width;
        }

        private sealed class Rig
        {
            public GameObject Root;
            public ActorAnimatorStateDriver AnimatorDriver;
            public readonly List<Bone> Bones = new();
            public float NextTick;
            public float LastTick;
        }

        private static NpcCrowdSpringSimulation instance;
        private static readonly ProfilerMarker JobMarker = new("Animation.NpcCrowdSpring.TransformJob");
        private static readonly ProfilerMarker SnapshotMarker = new("Animation.NpcCrowdSpring.Snapshot");
        private static readonly ProfilerMarker ColliderMarker = new("Animation.NpcCrowdSpring.ColliderSnapshot");
        private static readonly ProfilerMarker SolveMarker = new("Animation.NpcCrowdSpring.Solve");
        private static readonly ProfilerMarker ApplyMarker = new("Animation.NpcCrowdSpring.Apply");
        internal static bool DiagnosticCompleteStages { get; set; }
        internal static bool DiagnosticDisableColliders { get; set; }
        private readonly List<Rig> rigs = new();
        private readonly HashSet<int> registeredRoots = new();
        private NativeArray<SpringData> data;
        private TransformAccessArray boneTransforms;
        private NativeArray<AnimatedPose> animatedPoses;
        private NativeArray<SolvedPose> solvedPoses;
        private NativeArray<int> parentSpringIndices;
        private NativeArray<RigRange> rigRanges;
        private NativeArray<int> activeRigIndices;
        private NativeArray<ColliderStatic> colliderStatics;
        private NativeArray<ColliderWorld> colliderWorlds;
        private NativeArray<int> colliderIndices;
        private TransformAccessArray colliderTransforms;
        private bool layoutDirty;
        private JobHandle pendingSpringHandle;
        private bool springPending;
        private readonly List<int> pendingBoneIndices = new();
        private readonly List<Transform> indexedBoneTransforms = new();

        private void LateUpdate()
        {
            RemoveDestroyedRigs();
            if (springPending)
            {
                if (!pendingSpringHandle.IsCompleted && !layoutDirty && !DiagnosticCompleteStages)
                    return;
                CompleteAndApplyPending();
            }
            if (layoutDirty)
                RebuildLayout();
            if (!data.IsCreated || !boneTransforms.isCreated || boneTransforms.length == 0)
                return;

            var now = Time.time;
            var camera = Camera.main;
            var activeRigCount = 0;
            pendingBoneIndices.Clear();
            for (var i = 0; i < rigs.Count; i++)
            {
                var rig = rigs[i];
                if (rig.AnimatorDriver != null
                    && rig.AnimatorDriver.LastScheduledFrame != Time.frameCount)
                    continue;
                if (now < rig.NextTick)
                    continue;
                var distance = camera != null
                    ? Vector3.Distance(camera.transform.position, rig.Root.transform.position)
                    : 0f;
                var frequency = distance <= 15f ? 30f : distance <= 40f ? 15f : 5f;
                var deltaTime = Mathf.Clamp(now - rig.LastTick, 1f / 120f, 0.2f);
                rig.LastTick = now;
                rig.NextTick = now + 1f / frequency;
                activeRigIndices[activeRigCount++] = i;
                for (var boneIndex = 0; boneIndex < rig.Bones.Count; boneIndex++)
                {
                    var index = rig.Bones[boneIndex].Index;
                    var item = data[index];
                    item.DeltaTime = deltaTime;
                    item.Active = 1;
                    data[index] = item;
                    pendingBoneIndices.Add(index);
                }
            }
            if (activeRigCount == 0)
                return;
            using (JobMarker.Auto())
            {
                var colliderHandle = !DiagnosticDisableColliders
                    && colliderTransforms.isCreated && colliderTransforms.length > 0
                    ? new ColliderSnapshotJob
                    {
                        Statics = colliderStatics,
                        Worlds = colliderWorlds
                    }.Schedule(colliderTransforms)
                    : default;
                var snapshotHandle = new PoseSnapshotJob
                {
                    Data = data,
                    Poses = animatedPoses
                }.Schedule(boneTransforms);
                if (DiagnosticCompleteStages)
                {
                    using (SnapshotMarker.Auto()) snapshotHandle.Complete();
                    using (ColliderMarker.Auto()) colliderHandle.Complete();
                }
                var inputHandle = JobHandle.CombineDependencies(snapshotHandle, colliderHandle);
                var springHandle = new SpringRigJob
                {
                    Data = data,
                    AnimatedPoses = animatedPoses,
                    SolvedPoses = solvedPoses,
                    ParentSpringIndices = parentSpringIndices,
                    RigRanges = rigRanges,
                    ActiveRigIndices = activeRigIndices,
                    ColliderIndices = colliderIndices,
                    ColliderStatics = colliderStatics,
                    ColliderWorlds = colliderWorlds,
                    DisableColliders = DiagnosticDisableColliders ? 1 : 0
                }.Schedule(activeRigCount, 1, inputHandle);
                pendingSpringHandle = springHandle;
                springPending = true;
                if (DiagnosticCompleteStages)
                    using (SolveMarker.Auto()) CompleteAndApplyPending();
            }
        }

        private void CompleteAndApplyPending()
        {
            if (!springPending)
                return;
            pendingSpringHandle.Complete();
            using (ApplyMarker.Auto())
                for (var i = 0; i < pendingBoneIndices.Count; i++)
                {
                    var index = pendingBoneIndices[i];
                    if ((uint)index >= solvedPoses.Length)
                        continue;
                    var transform = (uint)index < indexedBoneTransforms.Count
                        ? indexedBoneTransforms[index]
                        : null;
                    if (transform == null)
                        continue;
                    var value = solvedPoses[index].LocalRotation.value;
                    transform.localRotation = new Quaternion(value.x, value.y, value.z, value.w);
                }
            springPending = false;
        }

        private void RemoveDestroyedRigs()
        {
            for (var i = rigs.Count - 1; i >= 0; i--)
            {
                if (rigs[i].Root != null)
                    continue;
                rigs.RemoveAt(i);
                layoutDirty = true;
            }
        }

        private void RebuildLayout()
        {
            if (data.IsCreated)
            {
                for (var i = 0; i < rigs.Count; i++)
                    for (var j = 0; j < rigs[i].Bones.Count; j++)
                    {
                        var bone = rigs[i].Bones[j];
                        if ((uint)bone.Index >= data.Length)
                            continue;
                        bone.CurrentTip = data[bone.Index].CurrentTip;
                        bone.PreviousTip = data[bone.Index].PreviousTip;
                    }
                data.Dispose();
            }
            DisposeBoneLayout();
            if (colliderStatics.IsCreated) colliderStatics.Dispose();
            if (colliderWorlds.IsCreated) colliderWorlds.Dispose();
            if (colliderIndices.IsCreated) colliderIndices.Dispose();
            if (colliderTransforms.isCreated) colliderTransforms.Dispose();

            var count = 0;
            for (var i = rigs.Count - 1; i >= 0; i--)
            {
                for (var j = rigs[i].Bones.Count - 1; j >= 0; j--)
                {
                    if (rigs[i].Bones[j].Transform == null)
                        rigs[i].Bones.RemoveAt(j);
                }
                if (rigs[i].Bones.Count == 0)
                {
                    rigs.RemoveAt(i);
                    continue;
                }
                rigs[i].Bones.Sort((left, right) =>
                    GetHierarchyDepth(left.Transform).CompareTo(GetHierarchyDepth(right.Transform)));
                count += rigs[i].Bones.Count;
            }
            if (count == 0)
            {
                layoutDirty = false;
                return;
            }
            var uniqueColliders = new List<ColliderRef>();
            var colliderMap = new Dictionary<int, int>();
            var colliderReferenceCount = 0;
            for (var i = 0; i < rigs.Count; i++)
                for (var j = 0; j < rigs[i].Bones.Count; j++)
                    for (var k = 0; k < rigs[i].Bones[j].Colliders.Count; k++)
                    {
                        var collider = rigs[i].Bones[j].Colliders[k];
                        if (collider.Component == null || collider.Transform == null)
                            continue;
                        colliderReferenceCount++;
                        var id = collider.Component.GetInstanceID();
                        if (colliderMap.ContainsKey(id))
                            continue;
                        colliderMap.Add(id, uniqueColliders.Count);
                        uniqueColliders.Add(collider);
                    }
            data = new NativeArray<SpringData>(count, Allocator.Persistent);
            colliderStatics = new NativeArray<ColliderStatic>(uniqueColliders.Count, Allocator.Persistent);
            colliderWorlds = new NativeArray<ColliderWorld>(uniqueColliders.Count, Allocator.Persistent);
            colliderIndices = new NativeArray<int>(colliderReferenceCount, Allocator.Persistent);
            colliderTransforms = new TransformAccessArray(uniqueColliders.Count);
            for (var i = 0; i < uniqueColliders.Count; i++)
            {
                var collider = uniqueColliders[i];
                colliderTransforms.Add(collider.Transform);
                colliderStatics[i] = new ColliderStatic
                {
                    Type = collider.Type,
                    Radius = collider.Radius,
                    Height = collider.Height,
                    Width = collider.Width
                };
            }
            var index = 0;
            var colliderReferenceIndex = 0;
            indexedBoneTransforms.Clear();
            for (var i = 0; i < rigs.Count; i++)
                for (var j = 0; j < rigs[i].Bones.Count; j++)
                {
                    var bone = rigs[i].Bones[j];
                    bone.Index = index;
                    indexedBoneTransforms.Add(bone.Transform);
                    var colliderStart = colliderReferenceIndex;
                    for (var k = 0; k < bone.Colliders.Count; k++)
                    {
                        var collider = bone.Colliders[k];
                        if (collider.Component == null || !colliderMap.TryGetValue(collider.Component.GetInstanceID(), out var colliderIndex))
                            continue;
                        colliderIndices[colliderReferenceIndex++] = colliderIndex;
                    }
                    data[index++] = new SpringData
                    {
                        LocalAxis = bone.LocalAxis,
                        InitialLocalRotation = bone.InitialLocalRotation,
                        RestLength = bone.RestLength,
                        Stiffness = bone.Stiffness,
                        Drag = bone.Drag,
                        Force = bone.Force,
                        DynamicRatio = bone.DynamicRatio,
                        CurrentTip = bone.CurrentTip,
                        PreviousTip = bone.PreviousTip,
                        Legacy = bone.Legacy ? 1 : 0,
                        Radius = bone.Radius,
                        ColliderStart = colliderStart,
                        ColliderCount = colliderReferenceIndex - colliderStart
                    };
                }
            BuildBoneLayout(count);
            layoutDirty = false;
        }

        private static int GetHierarchyDepth(Transform transform)
        {
            var depth = 0;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                depth++;
            return depth;
        }

        private void BuildBoneLayout(int count)
        {
            boneTransforms = new TransformAccessArray(count);
            animatedPoses = new NativeArray<AnimatedPose>(count, Allocator.Persistent);
            solvedPoses = new NativeArray<SolvedPose>(count, Allocator.Persistent);
            parentSpringIndices = new NativeArray<int>(count, Allocator.Persistent);
            rigRanges = new NativeArray<RigRange>(rigs.Count, Allocator.Persistent);
            activeRigIndices = new NativeArray<int>(rigs.Count, Allocator.Persistent);
            var transformToIndex = new Dictionary<Transform, int>(count);
            for (var i = 0; i < rigs.Count; i++)
                for (var j = 0; j < rigs[i].Bones.Count; j++)
                {
                    var bone = rigs[i].Bones[j];
                    boneTransforms.Add(bone.Transform);
                    transformToIndex.Add(bone.Transform, bone.Index);
                }
            for (var i = 0; i < rigs.Count; i++)
            {
                var rig = rigs[i];
                rigRanges[i] = new RigRange { Start = rig.Bones[0].Index, Count = rig.Bones.Count };
                for (var j = 0; j < rigs[i].Bones.Count; j++)
                {
                    var bone = rigs[i].Bones[j];
                    parentSpringIndices[bone.Index] = -1;
                    for (var parent = bone.Transform.parent; parent != null; parent = parent.parent)
                        if (transformToIndex.TryGetValue(parent, out var parentIndex))
                        {
                            parentSpringIndices[bone.Index] = parentIndex;
                            break;
                        }
                }
            }
        }

        private void DisposeBoneLayout()
        {
            if (boneTransforms.isCreated) boneTransforms.Dispose();
            if (animatedPoses.IsCreated) animatedPoses.Dispose();
            if (solvedPoses.IsCreated) solvedPoses.Dispose();
            if (parentSpringIndices.IsCreated) parentSpringIndices.Dispose();
            if (rigRanges.IsCreated) rigRanges.Dispose();
            if (activeRigIndices.IsCreated) activeRigIndices.Dispose();
            indexedBoneTransforms.Clear();
        }

        private void OnDestroy()
        {
            if (springPending)
                pendingSpringHandle.Complete();
            if (data.IsCreated) data.Dispose();
            DisposeBoneLayout();
            if (colliderStatics.IsCreated) colliderStatics.Dispose();
            if (colliderWorlds.IsCreated) colliderWorlds.Dispose();
            if (colliderIndices.IsCreated) colliderIndices.Dispose();
            if (colliderTransforms.isCreated) colliderTransforms.Dispose();
            if (instance == this) instance = null;
        }
    }
}
