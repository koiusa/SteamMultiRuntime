using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed partial class NpcCrowdSimulation
    {
        private void EnsureCapacity(int required)
        {
            if (capacity >= required)
                return;

            DisposeNativeCollections();
            capacity = math.ceilpow2(math.max(64, required));
            agents = new NativeArray<NpcCrowdAgentData>(capacity, Allocator.Persistent);
            steeringResults = new NativeArray<float3>(capacity, Allocator.Persistent);
            spatialGrid = new NativeParallelMultiHashMap<int, int>(capacity * 2, Allocator.Persistent);
            groundCommands = new NativeArray<CapsulecastCommand>(capacity, Allocator.Persistent);
            groundHits = new NativeArray<RaycastHit>(capacity, Allocator.Persistent);
            groundOverlapCommands = new NativeArray<OverlapCapsuleCommand>(capacity, Allocator.Persistent);
            groundOverlapHits = new NativeArray<ColliderHit>(capacity * GroundOverlapHitsPerNpc, Allocator.Persistent);
            wallCommands = new NativeArray<CapsulecastCommand>(capacity * 3, Allocator.Persistent);
            wallHits = new NativeArray<RaycastHit>(capacity * 3, Allocator.Persistent);
            wallOwners = new NativeArray<int>(capacity, Allocator.Persistent);
            movementInputs = new NativeArray<NpcCrowdMovementData>(capacity, Allocator.Persistent);
            movementResults = new NativeArray<NpcCrowdMovementResult>(capacity, Allocator.Persistent);
        }

        private void OnDestroy()
        {
            DisposeNativeCollections();
            if (appliedPathfindingIterationsPerFrame != originalPathfindingIterationsPerFrame)
                NavMesh.pathfindingIterationsPerFrame = originalPathfindingIterationsPerFrame;
            if (instance == this)
                instance = null;
        }

        private void DisposeNativeCollections()
        {
            if (agents.IsCreated) agents.Dispose();
            if (steeringResults.IsCreated) steeringResults.Dispose();
            if (spatialGrid.IsCreated) spatialGrid.Dispose();
            if (groundCommands.IsCreated) groundCommands.Dispose();
            if (groundHits.IsCreated) groundHits.Dispose();
            if (groundOverlapCommands.IsCreated) groundOverlapCommands.Dispose();
            if (groundOverlapHits.IsCreated) groundOverlapHits.Dispose();
            if (wallCommands.IsCreated) wallCommands.Dispose();
            if (wallHits.IsCreated) wallHits.Dispose();
            if (wallOwners.IsCreated) wallOwners.Dispose();
            if (movementInputs.IsCreated) movementInputs.Dispose();
            if (movementResults.IsCreated) movementResults.Dispose();
        }
    }
}
