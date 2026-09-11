using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    [RequireComponent(typeof(UIDocument))]
    public class LoginManager : MonoBehaviour
    {
        [SerializeField] private string lobbySceneName = "Lobby";

        private VisualElement _connectingPanel;
        private VisualElement _registerPanel;
        private VisualElement _welcomePanel;
        private TextField     _usernameField;
        private Button        _registerBtn;
        private Label         _errorLabel;
        private Label         _welcomeLabel;
        private Button        _enterLobbyBtn;

        private DbConnection _conn;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _connectingPanel = root.Q<VisualElement>("connecting-panel");
            _registerPanel   = root.Q<VisualElement>("register-panel");
            _welcomePanel    = root.Q<VisualElement>("welcome-panel");
            _usernameField   = root.Q<TextField>("username-field");
            _registerBtn     = root.Q<Button>("register-btn");
            _errorLabel      = root.Q<Label>("error-label");
            _welcomeLabel    = root.Q<Label>("welcome-label");
            _enterLobbyBtn   = root.Q<Button>("enter-lobby-btn");

            _registerBtn.clicked   += OnRegisterClicked;
            _enterLobbyBtn.clicked += OnEnterLobbyClicked;

            ShowPanel(_connectingPanel);

            var mgr = SpacetimeNetworkManager.Instance;
            if (mgr == null) { Debug.LogError("[Login] SpacetimeNetworkManager not found!"); return; }

            if (mgr.IsSubscriptionApplied)
                SetupAfterSubscription(mgr.Conn);
            else
                mgr.OnSubscriptionApplied += () => SetupAfterSubscription(mgr.Conn);
        }

        void OnDisable()
        {
            if (_conn == null) return;
            _conn.Db.UserProfile.OnInsert    -= OnProfileInserted;
            _conn.Reducers.OnRegisterProfile -= OnRegisterProfileResult;
        }

        private void SetupAfterSubscription(DbConnection conn)
        {
            _conn = conn;
            _conn.Db.UserProfile.OnInsert    += OnProfileInserted;
            _conn.Reducers.OnRegisterProfile += OnRegisterProfileResult;
            CheckExistingProfile();
        }

        private void CheckExistingProfile()
        {
            if (_conn == null || !_conn.Identity.HasValue) { ShowPanel(_registerPanel); return; }

            var profile = _conn.Db.UserProfile.PlayerId.Find(_conn.Identity.Value);
            if (profile is { } p)
            {
                _welcomeLabel.text = $"Welcome back,\n{p.Username}";
                ShowPanel(_welcomePanel);
            }
            else
            {
                ShowPanel(_registerPanel);
            }
        }

        private void OnRegisterClicked()
        {
            _errorLabel.text = "";
            var username = _usernameField?.value.Trim() ?? "";
            if (username.Length == 0 || username.Length > 24)
            {
                _errorLabel.text = "Username must be 1–24 characters.";
                return;
            }
            _registerBtn.SetEnabled(false);
            _conn?.Reducers.RegisterProfile(username);
        }

        private void OnEnterLobbyClicked() => SceneManager.LoadScene(lobbySceneName);

        private void OnProfileInserted(EventContext ctx, UserProfile profile)
        {
            if (!_conn.Identity.HasValue) return;
            if (profile.PlayerId != _conn.Identity.Value) return;
            SceneManager.LoadScene(lobbySceneName);
        }

        private void OnRegisterProfileResult(ReducerEventContext ctx, string username)
        {
            if (ctx.Event.Status is Status.Failed(var reason))
            {
                _registerBtn.SetEnabled(true);
                _errorLabel.text = reason;
            }
        }

        private void ShowPanel(VisualElement panel)
        {
            _connectingPanel.style.display = DisplayStyle.None;
            _registerPanel  .style.display = DisplayStyle.None;
            _welcomePanel   .style.display = DisplayStyle.None;
            panel           .style.display = DisplayStyle.Flex;
        }
    }
}
