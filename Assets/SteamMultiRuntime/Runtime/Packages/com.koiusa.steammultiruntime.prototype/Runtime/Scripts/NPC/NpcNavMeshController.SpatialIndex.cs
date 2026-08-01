using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public partial class NpcNavMeshController
    {
        private const float SpatialCellSize = 2f;
        private static readonly List<NpcNavMeshController> ActiveNpcs = new();
        private static readonly Dictionary<Vector3Int, List<NpcNavMeshController>> SpatialCells = new();
        private static int spatialIndexFrame = -1;

        private static void RegisterSpatialNpc(NpcNavMeshController npc)
        {
            if (npc != null && !ActiveNpcs.Contains(npc))
                ActiveNpcs.Add(npc);
            spatialIndexFrame = -1;
        }

        private static void UnregisterSpatialNpc(NpcNavMeshController npc)
        {
            ActiveNpcs.Remove(npc);
            spatialIndexFrame = -1;
        }

        private static int GetSpatialNeighbors(NpcNavMeshController self, float radius, NpcNavMeshController[] results)
        {
            RebuildSpatialIndexOncePerFrame();
            var position = self.transform.position;
            var center = ToSpatialCell(position);
            var cellRange = Mathf.CeilToInt(radius / SpatialCellSize);
            var radiusSqr = radius * radius;
            var count = 0;
            for (var y = -cellRange; y <= cellRange && count < results.Length; y++)
            for (var z = -cellRange; z <= cellRange && count < results.Length; z++)
            for (var x = -cellRange; x <= cellRange && count < results.Length; x++)
            {
                var key = new Vector3Int(center.x + x, center.y + y, center.z + z);
                if (!SpatialCells.TryGetValue(key, out var cell))
                    continue;
                for (var i = 0; i < cell.Count && count < results.Length; i++)
                {
                    var candidate = cell[i];
                    if (candidate == null || candidate == self || !candidate.isActiveAndEnabled)
                        continue;
                    if ((candidate.transform.position - position).sqrMagnitude <= radiusSqr)
                        results[count++] = candidate;
                }
            }
            return count;
        }

        private static void RebuildSpatialIndexOncePerFrame()
        {
            if (spatialIndexFrame == Time.frameCount)
                return;
            spatialIndexFrame = Time.frameCount;
            foreach (var cell in SpatialCells.Values)
                cell.Clear();
            for (var i = ActiveNpcs.Count - 1; i >= 0; i--)
            {
                var npc = ActiveNpcs[i];
                if (npc == null)
                {
                    ActiveNpcs.RemoveAt(i);
                    continue;
                }
                if (!npc.isActiveAndEnabled)
                    continue;
                var key = ToSpatialCell(npc.transform.position);
                if (!SpatialCells.TryGetValue(key, out var cell))
                {
                    cell = new List<NpcNavMeshController>(8);
                    SpatialCells.Add(key, cell);
                }
                cell.Add(npc);
            }
        }

        private static Vector3Int ToSpatialCell(Vector3 position) => new(
            Mathf.FloorToInt(position.x / SpatialCellSize),
            Mathf.FloorToInt(position.y / SpatialCellSize),
            Mathf.FloorToInt(position.z / SpatialCellSize));
    }
}
