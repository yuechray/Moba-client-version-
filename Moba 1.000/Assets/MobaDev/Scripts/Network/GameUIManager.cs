using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    [RequireComponent(typeof(UIDocument))]
    public class GameUIManager : MonoBehaviour
    {
        private ProgressBar _hpBar;
        private ProgressBar _manaBar;
        private Label       _hpLabel;
        private Label       _manaLabel;

        private VisualElement _gameOverPanel;
        private Label         _gameOverTitle;
        private Label         _gameOverSubtitle;
        private Button        _exitBtn;

        private DbConnection _conn;
        private bool         _gameOverShown;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _hpBar           = root.Q<ProgressBar>("hp-bar");
            _manaBar         = root.Q<ProgressBar>("mana-bar");
            _hpLabel         = root.Q<Label>("hp-label");
            _manaLabel       = root.Q<Label>("mana-label");
            _gameOverPanel   = root.Q<VisualElement>("game-over-panel");
            _gameOverTitle   = root.Q<Label>("game-over-title");
            _gameOverSubtitle = root.Q<Label>("game-over-subtitle");
            _exitBtn         = root.Q<Button>("exit-btn");

            if (_gameOverPanel != null) _gameOverPanel.style.display = DisplayStyle.None;
            if (_exitBtn != null)       _exitBtn.clicked += () => Application.Quit();

            _conn = SpacetimeNetworkManager.Instance?.Conn;
            if (_conn == null) return;

            _conn.Db.Champion.OnUpdate  += OnChampionUpdated;
            _conn.Db.GameState.OnUpdate += OnGameStateUpdated;
        }

        void OnDisable()
        {
            if (_conn == null) return;
            _conn.Db.Champion.OnUpdate  -= OnChampionUpdated;
            _conn.Db.GameState.OnUpdate -= OnGameStateUpdated;
        }

        void Update()
        {
            if (_gameOverShown) return;
            var local = SpacetimeNetworkManager.Instance?.GetLocalChampion();
            if (local != null) UpdateHpDisplay(local.CurrentState);
        }

        private void OnChampionUpdated(EventContext ctx, Champion old, Champion updated)
        {
            if (_conn == null || !_conn.Identity.HasValue) return;
            if (updated.OwnerIdentity != _conn.Identity.Value) return;
            UpdateHpDisplay(updated);
        }

        private void OnGameStateUpdated(EventContext ctx, GameState old, GameState newState)
        {
            if (newState.Phase != GamePhase.GameOver || _gameOverShown) return;
            _gameOverShown = true;

            bool iWin = _conn != null && _conn.Identity.HasValue &&
                        _conn.Db.Champion.Iter()
                            .Any(c => c.OwnerIdentity == _conn.Identity.Value && c.Team == newState.WinnerTeam);

            ShowGameOver(newState.WinnerTeam, iWin);
        }

        private void UpdateHpDisplay(Champion c)
        {
            if (_hpBar != null)
            {
                _hpBar.lowValue  = 0;
                _hpBar.highValue = c.MaxHealth;
                _hpBar.value     = c.Health;
            }
            if (_hpLabel != null) _hpLabel.text = $"{Mathf.CeilToInt(c.Health)}/{Mathf.CeilToInt(c.MaxHealth)}";

            if (_manaBar != null)
            {
                _manaBar.lowValue  = 0;
                _manaBar.highValue = c.MaxMana;
                _manaBar.value     = c.Mana;
            }
            if (_manaLabel != null) _manaLabel.text = $"{Mathf.CeilToInt(c.Mana)}/{Mathf.CeilToInt(c.MaxMana)}";
        }

        private void ShowGameOver(Team winner, bool iWin)
        {
            if (_gameOverPanel == null) return;
            _gameOverPanel.style.display = DisplayStyle.Flex;

            string winTeam = winner == Team.Radiant ? "Radiant" : "Dire";

            if (_gameOverTitle != null)
            {
                _gameOverTitle.text        = iWin ? "ПОБЕДА!" : "ПОРАЖЕНИЕ";
                _gameOverTitle.style.color = iWin
                    ? new StyleColor(new Color(0.2f, 0.9f, 0.2f))
                    : new StyleColor(new Color(0.9f, 0.2f, 0.2f));
            }
            if (_gameOverSubtitle != null)
                _gameOverSubtitle.text = $"{winTeam} уничтожили трон противника";
        }
    }
}
