using UnityEngine;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    [RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
    public class CreepController : MobController
    {
        [Header("Анимация")]
        [SerializeField] private Animator animator;

        private static readonly int AnimMove   = Animator.StringToHash("Move");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDead   = Animator.StringToHash("Dead");

        private bool _isDead;

        protected override void ConfigureAgent()
        {
            Agent.angularSpeed     = 360f;
            Agent.acceleration     = 15f;
            Agent.stoppingDistance = 0.5f;

            if (animator == null) animator = GetComponent<Animator>();
        }

        public void ApplyServerState(Creep state)
        {
            if (!IsInitialized) return;

            Agent.speed = state.MoveSpeed;

            if (state.State == CreepState.Dead && !_isDead)
            {
                _isDead = true;
                StopNav();
                Agent.ResetPath();
                animator?.SetBool(AnimDead, true);
                HideHPBar();
                return;
            }

            if (_isDead) return;

            SyncPosition(state.Position, lerpSpeed: 10f);

            if (state.State == CreepState.Moving)
                SetNavDestination(state.Destination);
            else
                StopNav();

            if (animator != null)
            {
                bool moving = Agent.velocity.sqrMagnitude > 0.01f;
                animator.SetBool(AnimMove,   moving);
                animator.SetBool(AnimAttack, state.State == CreepState.Attacking && !moving);
            }

            SetHPBar(state.Health, state.MaxHealth);
        }

        void LateUpdate() => BillboardHPBar();
    }
}
