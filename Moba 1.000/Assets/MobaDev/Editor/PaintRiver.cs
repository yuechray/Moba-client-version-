using UnityEngine;
using UnityEditor;

public static class PaintRiver
{
    private const string LayerPath   = "Assets/River.terrainlayer";
    private const string WaterTexPath = "Assets/MobaDev/Resources/Textures/Water_D.png";
    private const float  RiverHalf   = 5f;   // half-width in world units
    private const float  TileSize    = 4f;   // texture tile size

    // River runs perpendicular to Mid lane (diagonal top-left → bottom-right)
    // with slight meanders for a natural look
    private static readonly Vector2[] RiverPath =
    {
        V(  8, 112),
        V( 18, 102),
        V( 32,  92),
        V( 44,  80),
        V( 52,  72),
        V( 60,  67),
        V( 64,  64),
        V( 67,  60),
        V( 72,  52),
        V( 80,  44),
        V( 92,  32),
        V(102,  18),
        V(112,   8),
    };

    [MenuItem("Tools/Paint River")]
    private static void Execute()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("[PaintRiver] No active terrain."); return; }

        var td = terrain.terrainData;

        // Find or create the Water terrain layer
        int riverIdx = FindOrCreateRiverLayer(td);
        if (riverIdx < 0) { Debug.LogError("[PaintRiver] Failed to set up river terrain layer."); return; }

        int res = td.alphamapResolution;
        int layerCount = td.terrainLayers.Length;
        float[,,] maps = td.GetAlphamaps(0, 0, res, res);

        Vector3 origin = terrain.transform.position;
        Vector2 size   = new Vector2(td.size.x, td.size.z);

        PaintPath(maps, res, layerCount, riverIdx, origin, size, RiverPath, RiverHalf);

        td.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PaintRiver] Done. River layer index = {riverIdx}.");
    }

    private static int FindOrCreateRiverLayer(TerrainData td)
    {
        // Check if already exists on terrain
        var layers = td.terrainLayers;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i] != null && layers[i].name == "River") return i;

        // Load or create TerrainLayer asset
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
        if (layer == null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterTexPath);
            if (tex == null)
            {
                Debug.LogError($"[PaintRiver] Water texture not found at: {WaterTexPath}");
                return -1;
            }

            layer = new TerrainLayer
            {
                name            = "River",
                diffuseTexture  = tex,
                tileSize        = new Vector2(TileSize, TileSize),
                tileOffset      = Vector2.zero,
            };
            AssetDatabase.CreateAsset(layer, LayerPath);
            AssetDatabase.SaveAssets();
        }

        // Add layer to terrain
        var newLayers = new TerrainLayer[layers.Length + 1];
        layers.CopyTo(newLayers, 0);
        newLayers[layers.Length] = layer;
        td.terrainLayers = newLayers;

        return layers.Length; // index of newly added layer
    }

    private static void PaintPath(float[,,] maps, int res, int layers, int riverIdx,
        Vector3 origin, Vector2 size, Vector2[] waypoints, float halfWidth)
    {
        for (int i = 0; i < waypoints.Length - 1; i++)
            PaintSegment(maps, res, layers, riverIdx, origin, size,
                         waypoints[i], waypoints[i + 1], halfWidth);
    }

    private static void PaintSegment(float[,,] maps, int res, int layers, int riverIdx,
        Vector3 origin, Vector2 size, Vector2 a, Vector2 b, float halfWidth)
    {
        float segLen = Vector2.Distance(a, b);
        int   steps  = Mathf.CeilToInt(segLen * res / size.x * 2f);
        for (int s = 0; s <= steps; s++)
            PaintCircle(maps, res, layers, riverIdx, origin, size,
                        Vector2.Lerp(a, b, (float)s / steps), halfWidth);
    }

    private static void PaintCircle(float[,,] maps, int res, int layers, int riverIdx,
        Vector3 origin, Vector2 size, Vector2 worldXZ, float halfWidth)
    {
        int cx = Mathf.RoundToInt((worldXZ.x - origin.x) / size.x * res);
        int cz = Mathf.RoundToInt((worldXZ.y - origin.z) / size.y * res);
        int r  = Mathf.CeilToInt(halfWidth / size.x * res);

        for (int dz = -r; dz <= r; dz++)
        for (int dx = -r; dx <= r; dx++)
        {
            if (dx * dx + dz * dz > r * r) continue;

            int xi = cx + dx;
            int zi = cz + dz;
            if (xi < 0 || xi >= res || zi < 0 || zi >= res) continue;

            float dist   = Mathf.Sqrt(dx * dx + dz * dz);
            float weight = Mathf.Clamp01(1f - dist / r);
            float target = Mathf.Max(maps[zi, xi, riverIdx], weight);

            maps[zi, xi, riverIdx] = target;

            // Scale all other layers down so sum = 1
            float remaining = 1f - target;
            float otherSum  = 0f;
            for (int l = 0; l < layers; l++)
                if (l != riverIdx) otherSum += maps[zi, xi, l];

            if (otherSum > 0f)
                for (int l = 0; l < layers; l++)
                    if (l != riverIdx)
                        maps[zi, xi, l] = maps[zi, xi, l] / otherSum * remaining;
        }
    }

    private static Vector2 V(float x, float z) => new Vector2(x, z);
}
