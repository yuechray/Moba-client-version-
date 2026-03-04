using UnityEngine;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    /// <summary>
    /// Creates and initializes Unity GameObjects for server-side entities.
    /// Separates spawning logic from network connection management.
    /// </summary>
    public class EntityFactory : MonoBehaviour
    {
        [Header("Prefabs — Чемпионы (оставить пустым для fallback-капсул)")]
        [SerializeField] public GameObject localChampionPrefab;
        [SerializeField] public GameObject remoteChampionPrefab;

        [Header("Prefabs — Структуры (оставить пустым для fallback-кубов)")]
        [SerializeField] public GameObject radiantTowerPrefab;
        [SerializeField] public GameObject direTowerPrefab;
        [SerializeField] public GameObject radiantThronePrefab;
        [SerializeField] public GameObject direThroneGO;

        [Header("Prefabs — Крипы (оставить пустым для fallback-капсул)")]
        [SerializeField] public GameObject radiantCreepPrefab;
        [SerializeField] public GameObject direCreepPrefab;
        [SerializeField] public GameObject neutralCreepPrefab;

        public void CopyPrefabsFrom(EntityFactory src)
        {
            if (src.localChampionPrefab  != null) localChampionPrefab  = src.localChampionPrefab;
            if (src.remoteChampionPrefab != null) remoteChampionPrefab = src.remoteChampionPrefab;
            if (src.radiantTowerPrefab   != null) radiantTowerPrefab   = src.radiantTowerPrefab;
            if (src.direTowerPrefab      != null) direTowerPrefab      = src.direTowerPrefab;
            if (src.radiantThronePrefab  != null) radiantThronePrefab  = src.radiantThronePrefab;
            if (src.direThroneGO         != null) direThroneGO         = src.direThroneGO;
            if (src.radiantCreepPrefab   != null) radiantCreepPrefab   = src.radiantCreepPrefab;
            if (src.direCreepPrefab      != null) direCreepPrefab      = src.direCreepPrefab;
            if (src.neutralCreepPrefab   != null) neutralCreepPrefab   = src.neutralCreepPrefab;
        }

        public ChampionController SpawnChampion(Champion champ, SpacetimeDB.Identity localIdentity)
        {
            bool isLocal = champ.OwnerIdentity == localIdentity;
            var  prefab  = isLocal ? localChampionPrefab : remoteChampionPrefab;
            var  pos     = SampleHeight(ToVector3(champ.Position));

            var go = CreateOrFallback(prefab, pos, () =>
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.transform.localScale = new Vector3(1f, 1.2f, 1f);
                var r = obj.GetComponent<Renderer>();
                if (r != null) { r.material = SafeMaterial(); r.material.color = isLocal ? Color.blue : Color.magenta; }
                Debug.LogWarning($"[MOBA] Префаб чемпиона не назначен ({(isLocal ? "LOCAL" : "REMOTE")})");
                return obj;
            });

            go.name = $"Champion_{champ.Name}_{(isLocal ? "LOCAL" : "REMOTE")}";

            EnsureComponent<UnityEngine.AI.NavMeshAgent>(go);
            EnsureComponent<Animator>(go);

            var ctrl = go.GetComponent<ChampionController>() ?? go.AddComponent<ChampionController>();
            ctrl.Init(champ.Id, isLocal);
            ctrl.ApplyServerState(champ);

            Debug.Log($"[MOBA] Спавн чемпиона '{champ.Name}' в позиции {pos} (local={isLocal})");
            return ctrl;
        }

        public StructureController SpawnStructure(Structure s)
        {
            var pos    = SampleHeight(ToVector3(s.Position));
            var prefab = (s.Type, s.Team) switch
            {
                (StructureType.Tower,  Team.Radiant) => radiantTowerPrefab,
                (StructureType.Tower,  Team.Dire)    => direTowerPrefab,
                (StructureType.Throne, Team.Radiant) => radiantThronePrefab,
                (StructureType.Throne, Team.Dire)    => direThroneGO,
                _                                    => (GameObject)null
            };

            var go = CreateOrFallback(prefab, pos, () =>
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.localScale = s.Type == StructureType.Throne
                    ? new Vector3(4f, 6f, 4f) : new Vector3(2f, 4f, 2f);
                var r = obj.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material       = SafeMaterial();
                    r.material.color = s.Type == StructureType.Throne
                        ? (s.Team == Team.Radiant ? new Color(0.1f, 1f, 0.1f) : new Color(1f, 0.1f, 0.1f))
                        : (s.Team == Team.Radiant ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.7f, 0.2f, 0.2f));
                }
                return obj;
            });

            go.name = $"Structure_{s.Team}_{s.Type}_{s.Lane}_{s.Id}";
            EnsureComponent<BoxCollider>(go);

            var ctrl = go.GetComponent<StructureController>() ?? go.AddComponent<StructureController>();
            ctrl.Init(s.Id);
            ctrl.ApplyServerState(s);

            Debug.Log($"[MOBA] Спавн структуры {s.Team} {s.Type} (Lane={s.Lane}) в позиции {pos}");
            return ctrl;
        }

        public CreepController SpawnCreep(Creep c)
        {
            var pos    = SampleHeight(ToVector3(c.Position));
            var prefab = c.Type == CreepType.Neutral ? neutralCreepPrefab
                       : c.Team == Team.Radiant      ? radiantCreepPrefab : direCreepPrefab;

            var go = CreateOrFallback(prefab, pos, () =>
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.transform.localScale = Vector3.one * 0.6f;
                var r = obj.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material       = SafeMaterial();
                    r.material.color = c.Type == CreepType.Neutral ? Color.yellow
                                     : c.Team == Team.Radiant      ? Color.green : Color.red;
                }
                return obj;
            });

            go.name = $"Creep_{c.Team}_{c.Type}_{c.Id}";
            EnsureComponent<UnityEngine.AI.NavMeshAgent>(go);
            EnsureComponent<CapsuleCollider>(go);

            var ctrl = go.GetComponent<CreepController>() ?? go.AddComponent<CreepController>();
            ctrl.Init(c.Id);
            ctrl.ApplyServerState(c);

            return ctrl;
        }

        private static GameObject CreateOrFallback(
            GameObject prefab, Vector3 pos, System.Func<GameObject> fallback)
        {
            if (prefab != null)
                return Instantiate(prefab, pos, Quaternion.identity);

            var go = fallback();
            go.transform.position = pos;
            return go;
        }

        private static void EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null) go.AddComponent<T>();
        }

        private static Vector3 SampleHeight(Vector3 pos)
        {
            if (Terrain.activeTerrain != null)
                pos.y = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
            return pos;
        }

        private static Material SafeMaterial()
        {
            var shader = Shader.Find("Standard")
                      ?? Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("HDRP/Lit")
                      ?? Shader.Find("Diffuse");
            return shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        private static Vector3 ToVector3(DbVec3 v) => new(v.X, v.Y, v.Z);
    }
}
