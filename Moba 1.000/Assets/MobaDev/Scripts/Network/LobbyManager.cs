using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    [RequireComponent(typeof(UIDocument))]
    public class LobbyManager : MonoBehaviour
    {
        private Label      _statusLabel;
        private Button     _radiantBtn;
        private Button     _direBtn;
        private ScrollView _playerList;
        private Button     _readyBtn;
        private Button     _resetBtn;

        private bool         _isReady;
        private DbConnection _conn;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _statusLabel = root.Q<Label>("status-label");
            _radiantBtn  = root.Q<Button>("radiant-btn");
            _direBtn     = root.Q<Button>("dire-btn");
            _playerList  = root.Q<ScrollView>("player-list");
            _readyBtn    = root.Q<Button>("ready-btn");
            _resetBtn    = root.Q<Button>("reset-btn");

            _radiantBtn.clicked += () => OnSelectTeam(Team.Radiant);
            _direBtn   .clicked += () => OnSelectTeam(Team.Dire);
            _readyBtn  .clicked += OnReadyClicked;
            _resetBtn  .clicked += OnResetClicked;

            _conn = SpacetimeNetworkManager.Instance?.Conn;
            if (_conn == null) { Debug.LogError("[Lobby] SpacetimeNetworkManager not connected!"); return; }

            _conn.Db.LobbyPlayer.OnInsert += OnLobbyChanged;
            _conn.Db.LobbyPlayer.OnUpdate += OnLobbyUpdated;
            _conn.Db.LobbyPlayer.OnDelete += OnLobbyRemoved;

            _conn.Reducers.OnJoinLobby  += (ctx, _) => LogReducerError(ctx, "JoinLobby");
            _conn.Reducers.OnSelectTeam += (ctx, _) => LogReducerError(ctx, "SelectTeam");
            _conn.Reducers.OnSetReady   += (ctx, _) => LogReducerError(ctx, "SetReady");
            _conn.Reducers.OnResetMatch += ctx       => { LogReducerError(ctx, "ResetMatch"); AutoJoin(); };

            AutoJoin();
        }

        void OnDisable()
        {
            if (_conn == null) return;
            _conn.Db.LobbyPlayer.OnInsert -= OnLobbyChanged;
            _conn.Db.LobbyPlayer.OnUpdate -= OnLobbyUpdated;
            _conn.Db.LobbyPlayer.OnDelete -= OnLobbyRemoved;
        }

        private void AutoJoin()
        {
            if (!_conn.Identity.HasValue) { Debug.LogWarning("[Lobby] Identity not ready."); return; }

            var profile = _conn.Db.UserProfile.PlayerId.Find(_conn.Identity.Value);
            if (profile is not { } p) { Debug.LogWarning("[Lobby] No UserProfile — login first."); return; }

            Debug.Log($"[Lobby] AutoJoin as '{p.Username}'");
            _conn.Reducers.JoinLobby(p.Username);
            RefreshPlayerList();
        }

        private void OnSelectTeam(Team team)
        {
            Debug.Log($"[Lobby] SelectTeam → {team}");
            _conn?.Reducers.SelectTeam(team);
            _isReady = false;
            UpdateReadyButton();
            UpdateTeamButtons(team);
        }

        private void OnReadyClicked()
        {
            _isReady = !_isReady;
            _conn?.Reducers.SetReady(_isReady);
            UpdateReadyButton();
        }

        private void OnResetClicked()
        {
            Debug.Log("[Lobby] Requesting match reset...");
            _conn?.Reducers.ResetMatch();
        }

        private void OnLobbyChanged(EventContext ctx, LobbyPlayer lp)                    => RefreshPlayerList();
        private void OnLobbyUpdated(EventContext ctx, LobbyPlayer o, LobbyPlayer n)      => RefreshPlayerList();
        private void OnLobbyRemoved(EventContext ctx, LobbyPlayer lp)                    => RefreshPlayerList();

        private void RefreshPlayerList()
        {
            _playerList.Clear();
            int connected = 0, ready = 0;

            foreach (var lp in _conn.Db.LobbyPlayer.Iter())
            {
                if (!lp.IsConnected) continue;
                connected++;
                if (lp.IsReady) ready++;

                var entry = new Label(FormatPlayer(lp));
                entry.AddToClassList("player-entry");
                _playerList.Add(entry);
            }

            _statusLabel.text = connected < 2
                ? $"Waiting for players… ({connected}/2)"
                : $"Players ready: {ready}/{connected}";
        }

        private void UpdateReadyButton()
        {
            _readyBtn.text = _isReady ? "CANCEL" : "READY";
            if (_isReady) _readyBtn.AddToClassList("is-ready");
            else          _readyBtn.RemoveFromClassList("is-ready");
        }

        private void UpdateTeamButtons(Team selected)
        {
            if (selected == Team.Radiant)
            {
                _radiantBtn.AddToClassList("selected");
                _direBtn   .RemoveFromClassList("selected");
            }
            else
            {
                _direBtn   .AddToClassList("selected");
                _radiantBtn.RemoveFromClassList("selected");
            }
        }

        private static void LogReducerError(ReducerEventContext ctx, string name)
        {
            if (ctx.Event.Status is Status.Failed(var reason))
                Debug.LogError($"[Lobby] {name} failed: {reason}");
        }

        private static string FormatPlayer(LobbyPlayer lp)
        {
            string team  = lp.Team switch { Team.Radiant => "Radiant", Team.Dire => "Dire", _ => "—" };
            string ready = lp.IsReady ? "  ✓" : "";
            return $"{lp.Name}   [{team}]{ready}";
        }
    }
}
