using UnityEngine;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    /// <summary>
    /// Synchronizes the champion GameObject with server state.
    /// Input and animation are handled by ChampionInputHandler and ChampionAnimator.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
    public class ChampionController : MobController
    {
        public bool     IsLocalPlayer { get; private set; }
        public Champion CurrentState  => _state;
        public bool     IsDead        => _isDead;
        public bool     StateReceived => _stateReceived;

        private Champion          _state;
        private bool              _stateReceived;
        private bool              _isDead;
        private ChampionAnimator  _animator;

        public void Init(ulong id, bool isLocal)
        {
            base.Init(id);
            IsLocalPlayer = isLocal;
        }

        protected override void ConfigureAgent()
        {
            Agent.angularSpeed     = 720f;
            Agent.acceleration     = 20f;
            Agent.stoppingDistance = 0.3f;

            _animator = GetComponent<ChampionAnimator>();
        }

        public void ApplyServerState(Champion state)
        {
            _state         = state;
            _stateReceived = true;
            Agent.speed    = state.MoveSpeed;

            var serverPos = ToVector3(state.Position);

            if (IsLocalPlayer)
                SyncLocalChampion(state, serverPos);
            else
                SyncRemoteChampion(state);

            if (state.State == ChampionState.Dead && !_isDead)
                StartDeath();
            else if (state.State != ChampionState.Dead && _isDead)
                StartRespawn(serverPos);
        }

        private void SyncLocalChampion(Champion state, Vector3 serverPos)
        {
            if (!Agent.isOnNavMesh) return;

            if (Vector3.Distance(transform.position, serverPos) > 1.5f)
                Agent.Warp(serverPos);

            if (state.State == ChampionState.Moving)
                SetNavDestination(state.Destination);
            else if (state.State == ChampionState.Idle)
                Agent.ResetPath();
        }

        private void SyncRemoteChampion(Champion state)
        {
            SyncPosition(state.Position, lerpSpeed: 12f);

            if (state.State == ChampionState.Moving)
                SetNavDestination(state.Destination);
            else
                StopNav();
        }

        private void StartDeath()
        {
            _isDead = true;
            StopNav();
            Agent.ResetPath();
            _animator?.PlayDeath();
        }

        private void StartRespawn(Vector3 spawnPos)
        {
            _isDead = false;

            Agent.enabled      = false;
            transform.position = spawnPos;
            Agent.enabled      = true;
            Agent.isStopped    = false;

            _animator?.PlayRespawn();
        }
    }
}
