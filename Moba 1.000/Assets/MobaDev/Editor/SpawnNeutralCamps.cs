using UnityEngine;
using UnityEditor;

public static class SpawnNeutralCamps
{
    // Camp positions — mirror of Module.MapConfig.cs NeutralCamps
    // River runs at x+z ≈ 115–132 → camps stay at x+z < 112 or x+z > 134
    // T3 exclusion: x+z < 58 (Radiant), x+z > 200 (Dire)
    private static readonly (int id, Vector2 pos)[] Camps =
    {
        // Left jungle (Radiant → center)
        (1,  new Vector2(24f,  50f)),   // x+z=74
        (2,  new Vector2(26f,  74f)),   // x+z=100
        (3,  new Vector2(30f,  78f)),   // x+z=108
        // Right jungle — mirror of 1–3
        (4,  new Vector2(50f,  24f)),
        (5,  new Vector2(74f,  26f)),
        (6,  new Vector2(78f,  30f)),
        // Left jungle (Dire side, past river)
        (7,  new Vector2(36f,  100f)),  // x+z=136
        (8,  new Vector2(46f,  102f)),  // x+z=148
        (9,  new Vector2(56f,  96f)),   // x+z=152
        // Right jungle — mirror of 7–9
        (10, new Vector2(100f, 36f)),
        (11, new Vector2(102f, 46f)),
        (12, new Vector2(96f,  56f)),
    };

    private static readonly string[] RockPrefabs =
    {
        "Assets/MobaDev/Resources/Prefabs/Rock1.prefab",
        "Assets/MobaDev/Resources/Prefabs/Rock2.prefab",
        "Assets/MobaDev/Resources/Prefabs/Rock3.prefab",
        "Assets/MobaDev/Resources/Prefabs/Rock4.prefab",
        "Assets/MobaDev/Resources/Prefabs/Rock5.prefab",
        "Assets/MobaDev/Resources/Prefabs/Rock6.prefab",
        "Assets/MobaDev/Resources/Prefabs/Rock7.prefab",
        "Assets/MobaDev/Resources/Prefabs/Rock8.prefab",
    };

    private static readonly string[] TreePrefabs =
    {
        "Assets/MobaDev/Resources/Prefabs/Tree1.prefab",
        "Assets/MobaDev/Resources/Prefabs/Tree2.prefab",
        "Assets/MobaDev/Resources/Prefabs/Tree3.prefab",
        "Assets/MobaDev/Resources/Prefabs/TreeYellow1.prefab",
        "Assets/MobaDev/Resources/Prefabs/TreeYellow3.prefab",
        "Assets/MobaDev/Resources/Prefabs/TreePink1.prefab",
    };

    private const string CampFirePrefab = "Assets/MobaDev/Resources/Prefabs/CampFire.prefab";
    private const float  RoadThreshold  = 0.08f;
    private const float  CheckBuffer    = 2.0f;

    private static float[,,] _maps;
    private static int        _res;
    private static Vector3    _origin;
    private static Vector3    _size;
    private static int        _sandIdx  = -1;
    private static int        _riverIdx = -1;

    [MenuItem("Tools/Spawn Neutral Camps")]
    private static void Execute()
    {
        var existing = GameObject.Find("NeutralCamps");
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        CacheAlphamap();

        var root = new GameObject("NeutralCamps");
        Undo.RegisterCreatedObjectUndo(root, "Spawn Neutral Camps");

        var rng = new System.Random(42);

        // ── Camp circles ────────────────────────────────────────────────────
        foreach (var (id, pos) in Camps)
        {
            var campGO = new GameObject($"Camp_{id}");
            campGO.transform.SetParent(root.transform);
            Undo.RegisterCreatedObjectUndo(campGO, "Spawn Neutral Camps");

            Vector3 center = SampleHeight(new Vector3(pos.x, 0f, pos.y));
            campGO.transform.position = center;

            // Campfire at center
            PlacePrefab(CampFirePrefab, center, 0f, 1f, campGO.transform);

            // 3 small rocks scattered near the campfire (radius 1.5–3)
            for (int i = 0; i < 3; i++)
            {
                float angle  = Rnd(rng) * 360f;
                float radius = 1.5f + Rnd(rng) * 1.5f;
                var   wp     = Offset(center, angle, radius);
                if (IsOnRoadOrRiver(wp)) continue;
                PlacePrefab(RockPrefabs[rng.Next(RockPrefabs.Length)], wp,
                    Rnd360(rng), 0.6f + Rnd(rng) * 0.4f, campGO.transform);
            }

            // 8 rocks evenly in a ring (camp perimeter markers, radius 5–6.5)
            // Small angle jitter keeps the ring shape recognisable
            const int ringCount = 8;
            for (int i = 0; i < ringCount; i++)
            {
                if (!TryRingPos(center, i, ringCount, 5f, 1.5f, 15f, rng, out var wp)) continue;
                PlacePrefab(RockPrefabs[rng.Next(RockPrefabs.Length)], wp,
                    Rnd360(rng), 0.9f + Rnd(rng) * 0.5f, campGO.transform);
            }
        }

        // ── Forest zones (trees stay outside camp circles via scatter spacing) ─
        var forest = new GameObject("Forest");
        forest.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(forest, "Spawn Neutral Camps");

        // Outer strips — outside lane boundaries
        Scatter(forest.transform, rng,  0f,  15f,  18f, 110f, 5f);
        Scatter(forest.transform, rng, 18f, 110f,   0f,  15f, 5f);
        Scatter(forest.transform, rng, 113f, 128f, 18f, 110f, 5f);
        Scatter(forest.transform, rng, 18f, 110f, 113f, 128f, 5f);

        // Inner strips — jungle side of lane borders
        Scatter(forest.transform, rng, 18f,  36f, 22f, 106f, 6f);
        Scatter(forest.transform, rng, 22f, 106f, 18f,  36f, 6f);
        Scatter(forest.transform, rng, 18f, 106f, 96f, 110f, 6f);
        Scatter(forest.transform, rng, 96f, 110f, 18f, 106f, 6f);

        // Full jungle area — alphamap filters roads & river automatically
        Scatter(forest.transform, rng, 26f, 118f, 26f, 118f, 7f);

        Selection.activeGameObject = root;
        Debug.Log($"[SpawnNeutralCamps] Done — {Camps.Length} camps + forest.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void Scatter(Transform parent, System.Random rng,
        float xMin, float xMax, float zMin, float zMax, float spacing)
    {
        int xSteps = Mathf.CeilToInt((xMax - xMin) / spacing);
        int zSteps = Mathf.CeilToInt((zMax - zMin) / spacing);
        for (int ix = 0; ix <= xSteps; ix++)
        for (int iz = 0; iz <= zSteps; iz++)
        {
            float jx = (float)(rng.NextDouble() * spacing * 0.9f - spacing * 0.45f);
            float jz = (float)(rng.NextDouble() * spacing * 0.9f - spacing * 0.45f);
            float x  = Mathf.Clamp(xMin + ix * spacing + jx, xMin, xMax);
            float z  = Mathf.Clamp(zMin + iz * spacing + jz, zMin, zMax);
            var pos  = SampleHeight(new Vector3(x, 0f, z));
            if (IsOnRoadOrRiver(pos)) continue;
            PlacePrefab(TreePrefabs[rng.Next(TreePrefabs.Length)], pos,
                Rnd360(rng), 0.8f + Rnd(rng) * 0.6f, parent);
        }
    }

    // Tries up to 8 angle variations to find a non-road position in the ring slot.
    private static bool TryRingPos(Vector3 center, int slot, int total,
        float baseRadius, float radiusJitter, float angleJitter,
        System.Random rng, out Vector3 result)
    {
        float baseAngle = slot * (360f / total);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle  = baseAngle + Rnd(rng) * angleJitter - angleJitter * 0.5f;
            float radius = baseRadius + Rnd(rng) * radiusJitter;
            var   pos    = Offset(center, angle, radius);
            if (!IsOnRoadOrRiver(pos)) { result = pos; return true; }
        }
        result = default;
        return false;
    }

    private static bool IsOnRoadOrRiver(Vector3 worldPos)
    {
        if (_maps == null) return false;
        float uBuf  = CheckBuffer / _size.x;
        float vBuf  = CheckBuffer / _size.z;
        float uBase = (worldPos.x - _origin.x) / _size.x;
        float vBase = (worldPos.z - _origin.z) / _size.z;
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            float u = uBase + dx * uBuf;
            float v = vBase + dz * vBuf;
            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
            int xi = Mathf.Clamp(Mathf.RoundToInt(u * _res), 0, _res - 1);
            int zi = Mathf.Clamp(Mathf.RoundToInt(v * _res), 0, _res - 1);
            if (_sandIdx  >= 0 && _maps[zi, xi, _sandIdx]  > RoadThreshold) return true;
            if (_riverIdx >= 0 && _maps[zi, xi, _riverIdx] > RoadThreshold) return true;
        }
        return false;
    }

    private static void CacheAlphamap()
    {
        _maps = null; _sandIdx = -1; _riverIdx = -1;
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        var td  = terrain.terrainData;
        _res    = td.alphamapResolution;
        _origin = terrain.transform.position;
        _size   = td.size;
        _maps   = td.GetAlphamaps(0, 0, _res, _res);
        var layers = td.terrainLayers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            if (layers[i].name == "Sand")  _sandIdx  = i;
            if (layers[i].name == "River") _riverIdx = i;
        }
    }

    private static void PlacePrefab(string path, Vector3 pos, float rotY, float scale, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning($"[SpawnNeutralCamps] Prefab not found: {path}"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(go, "Spawn Neutral Camps");
        go.transform.position   = pos;
        go.transform.rotation   = Quaternion.Euler(0f, rotY, 0f);
        go.transform.localScale = Vector3.one * scale;
    }

    private static Vector3 Offset(Vector3 center, float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return SampleHeight(new Vector3(
            center.x + Mathf.Cos(rad) * radius,
            0f,
            center.z + Mathf.Sin(rad) * radius));
    }

    private static Vector3 SampleHeight(Vector3 pos)
    {
        if (Terrain.activeTerrain != null)
            pos.y = Terrain.activeTerrain.SampleHeight(pos)
                  + Terrain.activeTerrain.transform.position.y;
        return pos;
    }

    private static float Rnd(System.Random rng)    => (float)rng.NextDouble();
    private static float Rnd360(System.Random rng) => (float)rng.NextDouble() * 360f;
}
