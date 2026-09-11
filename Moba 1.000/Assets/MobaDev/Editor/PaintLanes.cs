using UnityEngine;
using UnityEditor;

public static class PaintLanes
{
    // Road half-width in world units
    private const float RoadHalf  = 3.5f;
    // Base plaza radius around spawn/throne
    private const float PlazaHalf = 8f;

    // Lane waypoints — mirror of Module.MapConfig.cs
    private static readonly Vector2[] MidWaypoints =
        { V(20,20), V(40,40), V(64,64), V(87,87), V(111,111) };

    private static readonly Vector2[] TopWaypoints =
        { V(16,20), V(16,64), V(16,107), V(64,111), V(107,111) };

    private static readonly Vector2[] BotWaypoints =
        { V(20,16), V(64,16), V(107,16), V(111,64), V(111,107) };

    // Base plazas: Radiant spawn, Radiant throne, Dire spawn, Dire throne
    private static readonly Vector2[] Plazas =
        { V(8,8), V(20,20), V(120,120), V(108,108) };

    [MenuItem("Tools/Paint Lanes")]
    private static void Execute()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("[PaintLanes] No active terrain found."); return; }

        var td  = terrain.terrainData;
        int res = td.alphamapResolution;
        int layerCount = td.terrainLayers.Length;

        // Find Sand layer index
        int sandIdx = -1;
        for (int i = 0; i < layerCount; i++)
            if (td.terrainLayers[i] != null && td.terrainLayers[i].name == "Sand")
            { sandIdx = i; break; }

        if (sandIdx < 0) { Debug.LogError("[PaintLanes] 'Sand' terrain layer not found on terrain."); return; }

        float[,,] maps = td.GetAlphamaps(0, 0, res, res);

        Vector3 origin = terrain.transform.position;
        Vector2 size   = new Vector2(td.size.x, td.size.z);

        // Paint lanes
        PaintPath(maps, res, layerCount, sandIdx, origin, size, MidWaypoints, RoadHalf);
        PaintPath(maps, res, layerCount, sandIdx, origin, size, TopWaypoints, RoadHalf);
        PaintPath(maps, res, layerCount, sandIdx, origin, size, BotWaypoints, RoadHalf);

        // Paint base plazas
        foreach (var p in Plazas)
            PaintCircle(maps, res, layerCount, sandIdx, origin, size, p, PlazaHalf);

        td.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(td);
        Debug.Log($"[PaintLanes] Done. Sand layer index = {sandIdx}, alphamap res = {res}.");
    }

    private static void PaintPath(float[,,] maps, int res, int layers, int sandIdx,
        Vector3 origin, Vector2 size, Vector2[] waypoints, float halfWidth)
    {
        for (int i = 0; i < waypoints.Length - 1; i++)
            PaintSegment(maps, res, layers, sandIdx, origin, size, waypoints[i], waypoints[i + 1], halfWidth);
    }

    private static void PaintSegment(float[,,] maps, int res, int layers, int sandIdx,
        Vector3 origin, Vector2 size, Vector2 a, Vector2 b, float halfWidth)
    {
        float segLen  = Vector2.Distance(a, b);
        int   steps   = Mathf.CeilToInt(segLen * res / size.x * 2f);
        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            PaintCircle(maps, res, layers, sandIdx, origin, size, Vector2.Lerp(a, b, t), halfWidth);
        }
    }

    private static void PaintCircle(float[,,] maps, int res, int layers, int sandIdx,
        Vector3 origin, Vector2 size, Vector2 worldXZ, float halfWidth)
    {
        int cx = WorldToMap(worldXZ.x - origin.x, size.x, res);
        int cz = WorldToMap(worldXZ.y - origin.z, size.y, res);
        int r  = Mathf.CeilToInt(halfWidth / size.x * res);

        for (int dz = -r; dz <= r; dz++)
        for (int dx = -r; dx <= r; dx++)
        {
            if (dx * dx + dz * dz > r * r) continue;

            int xi = cx + dx;
            int zi = cz + dz;
            if (xi < 0 || xi >= res || zi < 0 || zi >= res) continue;

            // Smooth falloff toward edges
            float dist   = Mathf.Sqrt(dx * dx + dz * dz);
            float weight = Mathf.Clamp01(1f - dist / r);

            float existing = maps[zi, xi, sandIdx];
            float target   = Mathf.Max(existing, weight);
            float delta    = target - existing;

            if (delta <= 0f) continue;

            // Reduce other layers proportionally
            float otherSum = 0f;
            for (int l = 0; l < layers; l++)
                if (l != sandIdx) otherSum += maps[zi, xi, l];

            if (otherSum > 0f)
                for (int l = 0; l < layers; l++)
                    if (l != sandIdx) maps[zi, xi, l] *= 1f - delta / otherSum * otherSum / (otherSum + existing);

            maps[zi, xi, sandIdx] = target;

            // Renormalize
            float total = 0f;
            for (int l = 0; l < layers; l++) total += maps[zi, xi, l];
            if (total > 0f)
                for (int l = 0; l < layers; l++) maps[zi, xi, l] /= total;
        }
    }

    private static int WorldToMap(float worldCoord, float terrainSize, int res)
        => Mathf.RoundToInt(worldCoord / terrainSize * res);

    private static Vector2 V(float x, float z) => new Vector2(x, z);
}
