using UnityEngine;
using UnityEngine.AI;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    [RequireComponent(typeof(ChampionController))]
    public class ChampionAnimator : MonoBehaviour
    {
        private static readonly int IdleState    = Animator.StringToHash("Base Layer.idle");
        private static readonly int MoveState    = Animator.StringToHash("Base Layer.move");
        private static readonly int AttackState  = Animator.StringToHash("Base Layer.attack_shift");
        private static readonly int DissolveState = Animator.StringToHash("Base Layer.dissolve");

        private ChampionController    _ctrl;
        private Animator              _anim;
        private NavMeshAgent          _agent;
        private SkinnedMeshRenderer[] _meshRenderers;

        private bool  _isDissolving;
        private float _dissolveValue = 1f;

        void Awake()
        {
            _ctrl          = GetComponent<ChampionController>();
            _anim          = GetComponent<Animator>();
            _agent         = GetComponent<NavMeshAgent>();
            _meshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        void Update()
        {
            UpdateAnimations();
            UpdateDissolve();
        }

        public void PlayDeath()
        {
            _isDissolving  = true;
            _dissolveValue = 1f;
            _anim?.CrossFade(DissolveState, 0.1f);
        }

        public void PlayRespawn()
        {
            _isDissolving  = false;
            _dissolveValue = 1f;
            ResetDissolveOnMeshes();
            _anim?.CrossFade(IdleState, 0.1f);
        }

        private void UpdateAnimations()
        {
            if (_ctrl.IsDead || !_ctrl.StateReceived || _anim == null) return;

            var  info   = _anim.GetCurrentAnimatorStateInfo(0);
            bool moving = _agent.velocity.sqrMagnitude > 0.01f;

            if (moving)
            {
                if (!info.IsName("move")) _anim.CrossFade(MoveState, 0.1f);
            }
            else if (_ctrl.CurrentState.State == ChampionState.Attacking)
            {
                if (!info.IsTag("Attack")) _anim.CrossFade(AttackState, 0.1f);
            }
            else
            {
                if (!info.IsName("idle")) _anim.CrossFade(IdleState, 0.1f);
            }
        }

        private void UpdateDissolve()
        {
            if (!_isDissolving) return;
            _dissolveValue = Mathf.MoveTowards(_dissolveValue, 0f, Time.deltaTime * 0.8f);
            foreach (var mr in _meshRenderers)
                if (mr && mr.material.HasProperty("_Dissolve"))
                    mr.material.SetFloat("_Dissolve", _dissolveValue);
        }

        private void ResetDissolveOnMeshes()
        {
            foreach (var mr in _meshRenderers)
                if (mr && mr.material.HasProperty("_Dissolve"))
                    mr.material.SetFloat("_Dissolve", 1f);
        }
    }
}
