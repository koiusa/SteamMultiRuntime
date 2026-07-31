using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Owns the single physics-loop callback for all active NPCs. Individual NPC
    /// components register by lifecycle and never receive FixedUpdate themselves.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class NpcCrowdSimulation : MonoBehaviour
    {
        internal struct AgentData
        {
            public float3 Position;
            public float3 Velocity;
            public float3 GoalVelocity;
            public float3 UpAxis;
            public float Radius;
            public float TimeHorizon;
            public float GoalWeight;
            public float AvoidanceWeight;
            public float SeparationExponent;
            public float MinApproachSpeed;
            public float ForwardDotMin;
            public int MaxNeighbors;
            public int Mode;
            public int UseForwardFilter;
        }

        [BurstCompile]
        private struct BuildSpatialGridJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<AgentData> Agents;
            public NativeParallelMultiHashMap<int, int>.ParallelWriter Grid;

            public void Execute(int index)
            {
                Grid.Add(HashCell(ToCell(Agents[index].Position)), index);
            }
        }

        [BurstCompile]
        private struct SteeringJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<AgentData> Agents;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;
            [WriteOnly] public NativeArray<float3> Results;

            public void Execute(int index)
            {
                var self = Agents[index];
                var goalSpeed = math.length(self.GoalVelocity);
                if (goalSpeed <= 0.0001f)
                {
                    Results[index] = self.GoalVelocity;
                    return;
                }

                var radius = math.max(0.1f, self.Radius);
                var radiusSqr = radius * radius;
                var center = ToCell(self.Position);
                var cellRange = (int)math.ceil(radius / SpatialCellSize);
                var correction = float3.zero;
                var accepted = 0;
                var goalDirection = self.GoalVelocity / goalSpeed;

                for (var y = -cellRange; y <= cellRange && accepted < self.MaxNeighbors; y++)
                for (var z = -cellRange; z <= cellRange && accepted < self.MaxNeighbors; z++)
                for (var x = -cellRange; x <= cellRange && accepted < self.MaxNeighbors; x++)
                {
                    var iterator = default(NativeParallelMultiHashMapIterator<int>);
                    var key = HashCell(center + new int3(x, y, z));
                    if (!Grid.TryGetFirstValue(key, out var otherIndex, out iterator))
                        continue;

                    do
                    {
                        if (otherIndex == index)
                            continue;
                        var other = Agents[otherIndex];
                            var delta = other.Position - self.Position;
                            delta -= self.UpAxis * math.dot(delta, self.UpAxis);
                        var sqrDistance = math.lengthsq(delta);
                        if (sqrDistance <= 0.0001f || sqrDistance > radiusSqr)
                            continue;

                        var distance = math.sqrt(sqrDistance);
                        var directionToOther = delta / distance;
                        if (self.Mode == 0)
                        {
                            if (self.UseForwardFilter != 0 && math.dot(goalDirection, directionToOther) < self.ForwardDotMin)
                                continue;
                            var strength = math.pow(1f - math.saturate(distance / radius), math.max(1f, self.SeparationExponent));
                            correction -= directionToOther * strength;
                        }
                        else
                        {
                            var relativeVelocity = self.Velocity - other.Velocity;
                            var approachSpeed = math.dot(relativeVelocity, directionToOther);
                            if (approachSpeed <= self.MinApproachSpeed)
                                continue;
                            var timeToCollision = distance / math.max(approachSpeed, 0.001f);
                            if (timeToCollision < 0f || timeToCollision > self.TimeHorizon)
                                continue;
                            var side = math.cross(self.UpAxis, directionToOther);
                            var sideLength = math.length(side);
                            if (sideLength <= 0.0001f)
                                continue;
                            side /= sideLength;
                            var sign = math.dot(side, goalDirection) >= 0f ? 1f : -1f;
                            var urgency = 1f - math.saturate(timeToCollision / math.max(0.1f, self.TimeHorizon));
                            correction += side * sign * urgency * urgency;
                        }
                        accepted++;
                    }
                    while (accepted < self.MaxNeighbors && Grid.TryGetNextValue(out otherIndex, ref iterator));
                }

                if (accepted > 0)
                    correction /= accepted;
                var goal = self.GoalVelocity * self.GoalWeight;
                var avoidance = correction * self.AvoidanceWeight;
                var maxAvoidance = math.length(goal) * 0.75f;
                var avoidanceLength = math.length(avoidance);
                if (avoidanceLength > maxAvoidance && avoidanceLength > 0.0001f)
                    avoidance *= maxAvoidance / avoidanceLength;
                Results[index] = goal + avoidance;
            }
        }

        private const float SpatialCellSize = 2f;
        private static NpcCrowdSimulation instance;
        private readonly List<NpcNavMeshController> activeNpcs = new(256);
        private readonly HashSet<NpcNavMeshController> activeNpcSet = new();
        private NativeArray<AgentData> agents;
        private NativeArray<float3> steeringResults;
        private NativeParallelMultiHashMap<int, int> spatialGrid;
        private int capacity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        internal static void Register(NpcNavMeshController npc)
        {
            if (npc == null)
                return;
            EnsureInstance().Add(npc);
        }

        internal static void Unregister(NpcNavMeshController npc)
        {
            if (instance != null)
                instance.Remove(npc);
        }

        private static NpcCrowdSimulation EnsureInstance()
        {
            if (instance != null)
                return instance;

            var host = new GameObject("NpcCrowdSimulation");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            instance = host.AddComponent<NpcCrowdSimulation>();
            return instance;
        }

        private void Add(NpcNavMeshController npc)
        {
            if (activeNpcSet.Add(npc))
                activeNpcs.Add(npc);
        }

        private void Remove(NpcNavMeshController npc)
        {
            if (!activeNpcSet.Remove(npc))
                return;
            var index = activeNpcs.IndexOf(npc);
            if (index < 0)
                return;
            var last = activeNpcs.Count - 1;
            activeNpcs[index] = activeNpcs[last];
            activeNpcs.RemoveAt(last);
        }

        private void FixedUpdate()
        {
            RemoveDeadEntries();
            var count = activeNpcs.Count;
            if (count == 0)
                return;

            EnsureCapacity(count);
            for (var i = 0; i < count; i++)
                agents[i] = activeNpcs[i].CaptureCrowdAgentData();

            spatialGrid.Clear();
            var buildHandle = new BuildSpatialGridJob
            {
                Agents = agents.GetSubArray(0, count),
                Grid = spatialGrid.AsParallelWriter()
            }.Schedule(count, 64);
            var steeringHandle = new SteeringJob
            {
                Agents = agents.GetSubArray(0, count),
                Grid = spatialGrid,
                Results = steeringResults.GetSubArray(0, count)
            }.Schedule(count, 64, buildHandle);
            steeringHandle.Complete();

            for (var i = 0; i < count; i++)
                activeNpcs[i].ApplyCrowdSteering(steeringResults[i]);

            for (var i = activeNpcs.Count - 1; i >= 0; i--)
            {
                var npc = activeNpcs[i];
                if (npc == null)
                {
                    activeNpcSet.Remove(npc);
                    activeNpcs.RemoveAt(i);
                    continue;
                }
                if (npc.isActiveAndEnabled)
                    npc.TickCrowdPhysics();
            }
        }

        private void RemoveDeadEntries()
        {
            for (var i = activeNpcs.Count - 1; i >= 0; i--)
            {
                if (activeNpcs[i] != null)
                    continue;
                activeNpcs.RemoveAt(i);
            }
        }

        private void EnsureCapacity(int required)
        {
            if (capacity >= required)
                return;
            DisposeNativeCollections();
            capacity = math.ceilpow2(math.max(64, required));
            agents = new NativeArray<AgentData>(capacity, Allocator.Persistent);
            steeringResults = new NativeArray<float3>(capacity, Allocator.Persistent);
            spatialGrid = new NativeParallelMultiHashMap<int, int>(capacity * 2, Allocator.Persistent);
        }

        private void OnDestroy()
        {
            DisposeNativeCollections();
            if (instance == this)
                instance = null;
        }

        private void DisposeNativeCollections()
        {
            if (agents.IsCreated) agents.Dispose();
            if (steeringResults.IsCreated) steeringResults.Dispose();
            if (spatialGrid.IsCreated) spatialGrid.Dispose();
        }

        private static int3 ToCell(float3 position) => (int3)math.floor(position / SpatialCellSize);
        private static int HashCell(int3 cell) => (int)math.hash(cell);
    }
}
