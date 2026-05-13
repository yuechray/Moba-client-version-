using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(EntityFactory))]
    public class SpacetimeNetworkManager : MonoBehaviour
    {
        public static SpacetimeNetworkManager Instance { get; private set; }

        [Header("SpacetimeDB")]
        [SerializeField] private string serverUri  = "wss://maincloud.spacetimedb.com";
        [SerializeField] private string moduleName = "moba-server";

        [Header("Сцены")]
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private string gameSceneName  = "Game";

        public DbConnection Conn                  { get; private set; }
        public event Action OnSubscriptionApplied;
        public bool         IsSubscriptionApplied { get; private set; }

        private EntityFactory _factory;

        private readonly Dictionary<ulong, ChampionController>  _champions  = new();
        private readonly Dictionary<ulong, StructureController> _structures = new();
        private readonly Dictionary<ulong, CreepController>     _creeps     = new();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Prefabs назначены в Game.unity — копируем в синглтон до уничтожения дубликата
                Instance._factory.CopyPrefabsFrom(GetComponent<EntityFactory>());
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _factory = GetComponent<EntityFactory>();
        }

        void Start()
        {
            if (Instance != this) return;
            Connect();
        }

        void Update()    => Conn?.FrameTick();
        void OnDestroy() => Conn?.Disconnect();

        private void Connect()
        {
            string token = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-stdb-token") token = args[i + 1];
            if (string.IsNullOrEmpty(token))
                token = PlayerPrefs.GetString("stdb_token", null);
            if (string.IsNullOrEmpty(token)) token = null;

            var builder = DbConnection.Builder()
                .WithUri(serverUri)
                .WithModuleName(moduleName)
                .OnConnect(HandleConnect)
                .OnConnectError(HandleConnectError)
                .OnDisconnect(HandleDisconnect);

            if (!string.IsNullOrEmpty(token))
                builder = builder.WithToken(token);

            Conn = builder.Build();

            Conn.Db.Champion.OnInsert  += HandleChampionInsert;
            Conn.Db.Champion.OnUpdate  += HandleChampionUpdate;
            Conn.Db.Champion.OnDelete  += HandleChampionDelete;
            Conn.Db.Structure.OnInsert += HandleStructureInsert;
            Conn.Db.Structure.OnUpdate += HandleStructureUpdate;
            Conn.Db.Structure.OnDelete += HandleStructureDelete;
            Conn.Db.Creep.OnInsert     += HandleCreepInsert;
            Conn.Db.Creep.OnUpdate     += HandleCreepUpdate;
            Conn.Db.Creep.OnDelete     += HandleCreepDelete;
            Conn.Db.GameState.OnUpdate += HandleGameStateUpdate;
        }

        private void HandleConnect(DbConnection conn, Identity identity, string token)
        {
            PlayerPrefs.SetString("stdb_token", token);
            PlayerPrefs.Save();
            Debug.Log($"[MOBA] Подключено — {identity}");
            conn.SubscriptionBuilder()
                .OnApplied(_ =>
                {
                    IsSubscriptionApplied = true;
                    OnSubscriptionApplied?.Invoke();

                    if (conn.Db.GameState.Id.Find(0) is { Phase: GamePhase.InGame })
                    {
                        Debug.Log("[MOBA] Игра уже идёт — загружаем Game сцену напрямую");
                        _champions.Clear(); _structures.Clear(); _creeps.Clear();
                        SceneManager.sceneLoaded += OnGameSceneLoaded;
                        SceneManager.LoadScene(gameSceneName);
                    }
                })
                .SubscribeToAllTables();
        }

        private void HandleConnectError(Exception err) =>
            Debug.LogError($"[MOBA] Ошибка подключения: {err.Message}");

        private void HandleDisconnect(DbConnection conn, Exception err) =>
            Debug.Log($"[MOBA] Отключено: {err?.Message ?? "graceful"}");

        private void HandleGameStateUpdate(EventContext ctx, GameState old, GameState newState)
        {
            if (newState.Phase == GamePhase.InGame && old.Phase != GamePhase.InGame)
            {
                Debug.Log("[MOBA] Матч начался — загружаем Game сцену");
                _champions.Clear(); _structures.Clear(); _creeps.Clear();
                SceneManager.sceneLoaded += OnGameSceneLoaded;
                SceneManager.LoadScene(gameSceneName);
            }
        }

        private void HandleChampionInsert(EventContext ctx, Champion champ)
        {
            if (_champions.ContainsKey(champ.Id)) return;
            if (SceneManager.GetActiveScene().name != gameSceneName) return;
            SpawnChampion(champ);
        }
        private void HandleChampionUpdate(EventContext ctx, Champion old, Champion updated)
        {
            if (_champions.TryGetValue(updated.Id, out var ctrl)) ctrl.ApplyServerState(updated);
        }
        private void HandleChampionDelete(EventContext ctx, Champion champ)
        {
            if (!_champions.TryGetValue(champ.Id, out var ctrl)) return;
            Destroy(ctrl.gameObject); _champions.Remove(champ.Id);
        }

        private void HandleStructureInsert(EventContext ctx, Structure s)
        {
            if (_structures.ContainsKey(s.Id)) return;
            if (SceneManager.GetActiveScene().name != gameSceneName) return;
            SpawnStructure(s);
        }
        private void HandleStructureUpdate(EventContext ctx, Structure old, Structure updated)
        {
            if (_structures.TryGetValue(updated.Id, out var ctrl)) ctrl.ApplyServerState(updated);
        }
        private void HandleStructureDelete(EventContext ctx, Structure s)
        {
            if (!_structures.TryGetValue(s.Id, out var ctrl)) return;
            _structures.Remove(s.Id);
            // Scene-bound objects stay in the scene; only destroy dynamically spawned ones
            if (ctrl.GetComponent<SceneStructureBinding>() == null)
                Destroy(ctrl.gameObject);
        }

        private void HandleCreepInsert(EventContext ctx, Creep c)
        {
            if (_creeps.ContainsKey(c.Id)) return;
            if (SceneManager.GetActiveScene().name != gameSceneName) return;
            SpawnCreep(c);
        }
        private void HandleCreepUpdate(EventContext ctx, Creep old, Creep updated)
        {
            if (_creeps.TryGetValue(updated.Id, out var ctrl)) ctrl.ApplyServerState(updated);
        }
        private void HandleCreepDelete(EventContext ctx, Creep c)
        {
            if (!_creeps.TryGetValue(c.Id, out var ctrl)) return;
            Destroy(ctrl.gameObject); _creeps.Remove(c.Id);
        }

        private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != gameSceneName) return;
            SceneManager.sceneLoaded -= OnGameSceneLoaded;
            Debug.Log("[MOBA] Game сцена загружена — спавним объекты");

            var placeholder = GameObject.Find("Character");
            if (placeholder != null) Destroy(placeholder);

            SpawnAllFromCache();
            StartCoroutine(LateSpawnCheck());
        }

        private void SpawnAllFromCache()
        {
            foreach (var champ in Conn.Db.Champion.Iter())
                if (!_champions.ContainsKey(champ.Id)) SpawnChampion(champ);

            foreach (var s in Conn.Db.Structure.Iter())
                if (!_structures.ContainsKey(s.Id)) SpawnStructure(s);

            foreach (var c in Conn.Db.Creep.Iter())
                if (!_creeps.ContainsKey(c.Id)) SpawnCreep(c);
        }

        private IEnumerator LateSpawnCheck()
        {
            yield return null;
            yield return null;
            yield return null;
            int before = _champions.Count + _structures.Count + _creeps.Count;
            SpawnAllFromCache();
            int after = _champions.Count + _structures.Count + _creeps.Count;
            Debug.Log($"[MOBA] LateSpawnCheck: champs={_champions.Count}, structs={_structures.Count}, creeps={_creeps.Count} (новых: {after - before})");
        }

        private void SpawnChampion(Champion champ)
        {
            if (!Conn.Identity.HasValue) return;
            var ctrl = _factory.SpawnChampion(champ, Conn.Identity.Value);
            _champions[champ.Id] = ctrl;
        }

        private void SpawnStructure(Structure s)
        {
            var ctrl = _factory.SpawnStructure(s);
            _structures[s.Id] = ctrl;
        }

        private void SpawnCreep(Creep c)
        {
            var ctrl = _factory.SpawnCreep(c);
            _creeps[c.Id] = ctrl;
        }

        public ChampionController GetLocalChampion()
        {
            foreach (var ctrl in _champions.Values)
                if (ctrl.IsLocalPlayer) return ctrl;
            return null;
        }

        public bool IsConnected => Conn != null && Conn.IsActive;
    }
}
