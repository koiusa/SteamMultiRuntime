using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Owns the single physics-loop callback for all active NPCs. Individual NPC
    /// components register by lifecycle and never receive FixedUpdate themselves.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed partial class NpcCrowdSimulation : MonoBehaviour
    {
        private const int GroundOverlapHitsPerNpc = 4;
        private static readonly ProfilerMarker PrepareMarker = new("Physics.NpcCrowd.PrepareProbes");
        private static readonly ProfilerMarker QueryMarker = new("Physics.NpcCrowd.QueryAndSteeringWait");
        private static readonly ProfilerMarker ProbeApplyMarker = new("Physics.NpcCrowd.ApplyProbeResults");
        private static readonly ProfilerMarker PenetrationMarker = new("Physics.NpcCrowd.ResolvePenetration");
        private static readonly ProfilerMarker MovementJobMarker = new("Physics.NpcCrowd.MovementJob");
        private static readonly ProfilerMarker MovementApplyMarker = new("Physics.NpcCrowd.ApplyMovementAndContacts");
        [BurstCompile]
        private struct BuildSpatialGridJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<NpcCrowdAgentData> Agents;
            public NativeParallelMultiHashMap<int, int>.ParallelWriter Grid;

            public void Execute(int index)
            {
                Grid.Add(HashCell(ToCell(Agents[index].Position)), index);
            }
        }

        [BurstCompile]
        private struct SteeringJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<NpcCrowdAgentData> Agents;
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

        [BurstCompile]
        private struct MovementJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<NpcCrowdMovementData> Inputs;
            [WriteOnly] public NativeArray<NpcCrowdMovementResult> Results;
            public float DeltaTime;

            public void Execute(int index)
            {
                var input = Inputs[index];
                var up = math.normalizesafe(input.UpAxis, new float3(0f, 1f, 0f));
                var traversalMode = input.TraversalMode;
                var traversalActive = traversalMode == (int)ActorTraversalState.WallRun
                    || traversalMode == (int)ActorTraversalState.WallSlide
                    || traversalMode == (int)ActorTraversalState.Ladder
                    || traversalMode == (int)ActorTraversalState.WallJump
                    || (traversalMode == (int)ActorTraversalState.WireSwing && input.Grounded == 0);
                if (traversalActive)
                {
                    var traversalVelocity = input.Velocity;
                    var groundedTraversal = traversalMode == (int)ActorTraversalState.Ladder;
                    if (!groundedTraversal)
                        traversalVelocity += up * input.Gravity * DeltaTime;
                    if (input.HasWall != 0)
                    {
                        var wallNormal = math.normalizesafe(input.WallNormal);
                        var inwardSpeed = math.dot(traversalVelocity, wallNormal);
                        if (inwardSpeed < 0f && input.WallDistance <= -inwardSpeed * DeltaTime + 0.03f)
                            traversalVelocity -= wallNormal * inwardSpeed;
                    }

                    if (traversalMode == (int)ActorTraversalState.WireSwing)
                    {
                        var toAnchor = input.WireAnchor - input.Position;
                        var ropeDirection = math.normalizesafe(toAnchor);
                        var tangentInput = input.DesiredPlanarVelocity - ropeDirection * math.dot(input.DesiredPlanarVelocity, ropeDirection);
                        traversalVelocity += tangentInput * DeltaTime;
                    }

                    var traversalPosition = input.Position + input.GroundDisplacement + traversalVelocity * DeltaTime;
                    if (traversalMode == (int)ActorTraversalState.WireSwing && input.WireRopeLength > 0.01f)
                    {
                        var fromAnchor = traversalPosition - input.WireAnchor;
                        var distance = math.length(fromAnchor);
                        if (distance > input.WireRopeLength && distance > 0.0001f)
                        {
                            var radial = fromAnchor / distance;
                            traversalPosition = input.WireAnchor + radial * input.WireRopeLength;
                            var outwardSpeed = math.dot(traversalVelocity, radial);
                            if (outwardSpeed > 0f)
                                traversalVelocity -= radial * outwardSpeed;
                        }
                    }

                    Results[index] = new NpcCrowdMovementResult
                    {
                        Position = traversalPosition,
                        Velocity = traversalVelocity,
                        Grounded = groundedTraversal ? 1 : 0,
                        AirborneFromJump = traversalMode == (int)ActorTraversalState.WallJump ? 1 : input.AirborneFromJump
                    };
                    return;
                }
                var currentPlanar = input.Velocity - up * math.dot(input.Velocity, up);
                var target = input.DesiredPlanarVelocity;
                var targetSpeed = math.length(target);
                if (targetSpeed > input.MoveSpeed && targetSpeed > 0.0001f)
                    target *= input.MoveSpeed / targetSpeed;
                var delta = target - currentPlanar;
                var maxDelta = math.max(0f, input.Acceleration) * DeltaTime;
                var deltaLength = math.length(delta);
                if (deltaLength > maxDelta && deltaLength > 0.0001f)
                    delta *= maxDelta / deltaLength;
                var planar = currentPlanar + delta;

                var grounded = input.Grounded != 0;
                var airborneFromJump = input.AirborneFromJump != 0;
                var vertical = math.dot(input.Velocity, up);
                if (input.JumpRequested != 0 && grounded)
                {
                    currentPlanar += input.GroundVelocity - up * math.dot(input.GroundVelocity, up);
                    planar = currentPlanar;
                    vertical = input.JumpSpeed + math.dot(input.GroundVelocity, up);
                    grounded = false;
                    airborneFromJump = true;
                }
                if (!grounded)
                    vertical += input.Gravity * DeltaTime;

                var velocity = planar + up * vertical;
                if (input.HasWall != 0)
                {
                    var wallNormal = math.normalizesafe(input.WallNormal);
                    var inwardSpeed = math.dot(velocity, wallNormal);
                    if (inwardSpeed < 0f && input.WallDistance <= -inwardSpeed * DeltaTime + 0.03f)
                        velocity -= wallNormal * inwardSpeed;
                }
                var position = input.Position + input.GroundDisplacement + velocity * DeltaTime;
                var height = math.dot(position, up);
                if (input.HasGroundSurface != 0 && grounded)
                {
                    // Follow slopes, steps and moving-floor height while grounded. Without
                    // this snap the planar integration leaves the capsule behind the floor.
                    position += up * (input.GroundCoordinate - height);
                    velocity -= up * math.dot(velocity, up);
                }
                else if (input.HasGroundSurface != 0 && !grounded && vertical <= 0f && height <= input.GroundCoordinate)
                {
                    position += up * (input.GroundCoordinate - height);
                    velocity -= up * vertical;
                    grounded = true;
                    airborneFromJump = false;
                }
                Results[index] = new NpcCrowdMovementResult
                {
                    Position = position,
                    Velocity = velocity,
                    Grounded = grounded ? 1 : 0,
                    AirborneFromJump = airborneFromJump ? 1 : 0
                };
            }
        }

        private const float SpatialCellSize = 2f;
        private static NpcCrowdSimulation instance;
        private readonly List<NpcCrowdAgent> activeNpcs = new(256);
        private readonly HashSet<NpcCrowdAgent> activeNpcSet = new();
        private NativeArray<NpcCrowdAgentData> agents;
        private NativeArray<float3> steeringResults;
        private NativeParallelMultiHashMap<int, int> spatialGrid;
        private NativeArray<CapsulecastCommand> groundCommands;
        private NativeArray<RaycastHit> groundHits;
        private NativeArray<OverlapCapsuleCommand> groundOverlapCommands;
        private NativeArray<ColliderHit> groundOverlapHits;
        private NativeArray<CapsulecastCommand> wallCommands;
        private NativeArray<RaycastHit> wallHits;
        private NativeArray<int> wallOwners;
        private int wallProbePhase;
        private NativeArray<NpcCrowdMovementData> movementInputs;
        private NativeArray<NpcCrowdMovementResult> movementResults;
        private int capacity;
        private float crowdStepAccumulator;
        private int originalPathfindingIterationsPerFrame;
        private int appliedPathfindingIterationsPerFrame;
        private const float CrowdStepInterval = 1f / 30f;
        private const float MaximumCrowdStep = 1f / 15f;

        private void Awake()
        {
            originalPathfindingIterationsPerFrame = NavMesh.pathfindingIterationsPerFrame;
            appliedPathfindingIterationsPerFrame = originalPathfindingIterationsPerFrame;
            PrototypeMotionMover.PhysicsPoseApplied += OnMovingPlatformPhysicsPoseApplied;
        }

        private void OnMovingPlatformPhysicsPoseApplied(PrototypeMotionMover source, float deltaTime)
        {
            for (var i = activeNpcs.Count - 1; i >= 0; i--)
            {
                var npc = activeNpcs[i];
                if (npc != null && npc.isActiveAndEnabled)
                    npc.FollowMovingPlatformPhysicsPose(source, deltaTime);
            }
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            for (var i = activeNpcs.Count - 1; i >= 0; i--)
            {
                var npc = activeNpcs[i];
                if (npc != null && npc.isActiveAndEnabled)
                    npc.TickPresentation(deltaTime);
            }

            // Crowd bodies are kinematic and use their own Burst integration. Keeping
            // that work in FixedUpdate made a slow frame execute the entire crowd two
            // or more times while Unity caught up, causing a feedback loop. Run at a
            // stable 30 Hz and never execute more than one crowd step per render frame.
            crowdStepAccumulator += Mathf.Min(deltaTime, MaximumCrowdStep);
            if (crowdStepAccumulator >= CrowdStepInterval)
            {
                var crowdDeltaTime = Mathf.Min(crowdStepAccumulator, MaximumCrowdStep);
                crowdStepAccumulator = 0f;
                RunCrowdStep(crowdDeltaTime);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        internal static void Register(NpcCrowdAgent npc)
        {
            if (npc == null)
                return;
            EnsureInstance().Add(npc);
        }

        internal static void Unregister(NpcCrowdAgent npc)
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

        private void Add(NpcCrowdAgent npc)
        {
            if (activeNpcSet.Add(npc))
                activeNpcs.Add(npc);
        }

        private void Remove(NpcCrowdAgent npc)
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

        private void RunCrowdStep(float deltaTime)
        {
            RemoveDeadEntries();
            var count = activeNpcs.Count;
            if (count == 0)
                return;

            EnsureCapacity(count);
            wallProbePhase ^= 1;
            var wallProbeCount = 0;
            using (PrepareMarker.Auto())
            for (var i = 0; i < count; i++)
            {
                activeNpcs[i].TickRecovery();
                activeNpcs[i].Prepare(deltaTime);
                agents[i] = activeNpcs[i].CaptureAgentData();
                activeNpcs[i].CreateGroundProbes(out var castCommand, out var overlapCommand);
                groundCommands[i] = castCommand;
                groundOverlapCommands[i] = overlapCommand;
                var probeWalls = activeNpcs[i].ShouldProbeWalls
                    && (activeNpcs[i].ShouldProbeWallsEveryStep || ((i + wallProbePhase) & 1) == 0);
                if (probeWalls)
                {
                    activeNpcs[i].ClearWallProbe();
                    activeNpcs[i].CreateWallProbes(out var wallForward, out var wallLeft, out var wallRight);
                    var wallIndex = wallProbeCount * 3;
                    wallCommands[wallIndex] = wallForward;
                    wallCommands[wallIndex + 1] = wallLeft;
                    wallCommands[wallIndex + 2] = wallRight;
                    wallOwners[wallProbeCount] = i;
                    wallProbeCount++;
                }
            }
            UpdatePathfindingBudget(count);

            using (QueryMarker.Auto())
            {
                spatialGrid.Clear();
                var buildHandle = new BuildSpatialGridJob
                {
                    Agents = agents.GetSubArray(0, count), Grid = spatialGrid.AsParallelWriter()
                }.Schedule(count, 64);
                var steeringHandle = new SteeringJob
                {
                    Agents = agents.GetSubArray(0, count), Grid = spatialGrid,
                    Results = steeringResults.GetSubArray(0, count)
                }.Schedule(count, 64, buildHandle);
                var groundHandle = CapsulecastCommand.ScheduleBatch(
                    groundCommands.GetSubArray(0, count), groundHits.GetSubArray(0, count), 32, 1);
                var overlapHandle = OverlapCapsuleCommand.ScheduleBatch(
                    groundOverlapCommands.GetSubArray(0, count),
                    groundOverlapHits.GetSubArray(0, count * GroundOverlapHitsPerNpc),
                    32, GroundOverlapHitsPerNpc, default);
                var wallHandle = wallProbeCount > 0
                    ? CapsulecastCommand.ScheduleBatch(
                        wallCommands.GetSubArray(0, wallProbeCount * 3),
                        wallHits.GetSubArray(0, wallProbeCount * 3), 32, 1)
                    : default;
                var groundAndSteeringHandle = JobHandle.CombineDependencies(steeringHandle, groundHandle, overlapHandle);
                JobHandle.CombineDependencies(groundAndSteeringHandle, wallHandle).Complete();
            }

            using (ProbeApplyMarker.Auto())
            {
            for (var i = 0; i < count; i++)
            {
                activeNpcs[i].ApplySteering(steeringResults[i]);
                var overlapIndex = i * GroundOverlapHitsPerNpc;
                activeNpcs[i].ApplyGroundProbe(
                    groundHits[i],
                    groundOverlapHits[overlapIndex],
                    groundOverlapHits[overlapIndex + 1],
                    groundOverlapHits[overlapIndex + 2],
                    groundOverlapHits[overlapIndex + 3]);
            }
            for (var probeIndex = 0; probeIndex < wallProbeCount; probeIndex++)
            {
                var wallIndex = probeIndex * 3;
                activeNpcs[wallOwners[probeIndex]].ApplyWallProbes(
                    wallHits[wallIndex], wallHits[wallIndex + 1], wallHits[wallIndex + 2]);
            }
            }
            using (PenetrationMarker.Auto())
            for (var i = 0; i < count; i++)
            {
                // Resolve the exact movement capsule after the predictive wall casts.
                // This also recovers contacts when a fast/thin obstacle started inside
                // the cast volume and therefore produced no sweep hit.
                var overlapIndex = i * GroundOverlapHitsPerNpc;
                activeNpcs[i].ResolveEnvironmentOverlaps(
                    groundOverlapHits[overlapIndex],
                    groundOverlapHits[overlapIndex + 1],
                    groundOverlapHits[overlapIndex + 2],
                    groundOverlapHits[overlapIndex + 3]);
                movementInputs[i] = activeNpcs[i].CaptureMovementData();
            }
            using (MovementJobMarker.Auto())
                new MovementJob
                {
                    Inputs = movementInputs.GetSubArray(0, count),
                    Results = movementResults.GetSubArray(0, count),
                    DeltaTime = deltaTime
                }.Schedule(count, 64).Complete();
            using (MovementApplyMarker.Auto())
            for (var i = 0; i < count; i++)
                activeNpcs[i].ApplyMovement(movementResults[i], deltaTime);
        }

        private void RemoveDeadEntries()
        {
            for (var i = activeNpcs.Count - 1; i >= 0; i--)
            {
                if (activeNpcs[i] != null)
                    continue;
                activeNpcSet.Remove(activeNpcs[i]);
                activeNpcs.RemoveAt(i);
            }
        }

        private void UpdatePathfindingBudget(int npcCount)
        {
            // The budget is per rendered frame. Scaling it with the crowd count keeps
            // path throughput stable when FPS drops; lowering it adaptively was tested
            // but increased pending paths without reducing measured Main Thread time.
            var required = Mathf.Clamp(npcCount * 2, originalPathfindingIterationsPerFrame, 2048);
            if (required == appliedPathfindingIterationsPerFrame)
                return;
            NavMesh.pathfindingIterationsPerFrame = required;
            appliedPathfindingIterationsPerFrame = required;
        }

        private static int3 ToCell(float3 position) => (int3)math.floor(position / SpatialCellSize);
        private static int HashCell(int3 cell) => (int)math.hash(cell);
    }
}
