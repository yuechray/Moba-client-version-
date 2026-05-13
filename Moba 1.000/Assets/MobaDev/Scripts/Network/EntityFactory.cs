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

        [Header("Prefabs — Крипы (оставить пустым для fallback-капсул)")]
        [SerializeField] public GameObject radiantCreepPrefab;
        [SerializeField] public GameObject direCreepPrefab;
        [SerializeField] public GameObject neutralCreepPrefab;

        [Header("Prefabs — Нейтральные мобы по лагерям (индекс 0 = лагерь 1 … 11 = лагерь 12)")]
        [SerializeField] public GameObject[] campNeutralPrefabs = new GameObject[12];

        public void CopyPrefabsFrom(EntityFactory src)
        {
            if (src.localChampionPrefab  != null) localChampionPrefab  = src.localChampionPrefab;
            if (src.remoteChampionPrefab != null) remoteChampionPrefab = src.remoteChampionPrefab;
            if (src.radiantCreepPrefab   != null) radiantCreepPrefab   = src.radiantCreepPrefab;
            if (src.direCreepPrefab      != null) direCreepPrefab      = src.direCreepPrefab;
            if (src.neutralCreepPrefab != null) neutralCreepPrefab = src.neutralCreepPrefab;
            for (int i = 0; i < src.campNeutralPrefabs.Length; i++)
                if (src.campNeutralPrefabs[i] != null) campNeutralPrefabs[i] = src.campNeutralPrefabs[i];
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
            var binding = FindSceneBinding(s.Id);
            if (binding == null)
            {
                Debug.LogError($"[MOBA] Сцен-объект для структуры id={s.Id} ({s.Team} {s.Type}) не найден — добавь SceneStructureBinding.");
                return null;
            }

            var go = binding.gameObject;
            go.name = $"Structure_{s.Team}_{s.Type}_{s.Lane}_{s.Id}";
            EnsureComponent<BoxCollider>(go);

            var ctrl = go.GetComponent<StructureController>() ?? go.AddComponent<StructureController>();
            ctrl.Init(s.Id);
            ctrl.ApplyServerState(s);
            return ctrl;
        }

        private static SceneStructureBinding FindSceneBinding(ulong id)
        {
            foreach (var b in FindObjectsByType<SceneStructureBinding>(FindObjectsSortMode.None))
                if (b.structureId == id) return b;
            return null;
        }

        public CreepController SpawnCreep(Creep c)
        {
            var pos    = SampleHeight(ToVector3(c.Position));
            GameObject prefab;
            if (c.Type == CreepType.Neutral && c.CampId >= 1 && c.CampId <= 12)
            {
                int idx = (int)c.CampId - 1;
                prefab = campNeutralPrefabs[idx] ?? neutralCreepPrefab;
            }
            else
            {
                prefab = c.Team == Team.Radiant ? radiantCreepPrefab : direCreepPrefab;
            }

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
