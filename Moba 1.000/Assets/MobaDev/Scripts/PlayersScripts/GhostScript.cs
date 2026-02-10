using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;           // ← обязательно для NavMeshAgent
using UnityEngine.UI;

namespace Sample
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class GhostScript : MonoBehaviour
    {
        private Animator Anim;
        private NavMeshAgent Agent;

        // Cache animator hashes
        private static readonly int IdleState = Animator.StringToHash("Base Layer.idle");
        private static readonly int MoveState = Animator.StringToHash("Base Layer.move");
        private static readonly int SurprisedState = Animator.StringToHash("Base Layer.surprised");
        private static readonly int AttackState = Animator.StringToHash("Base Layer.attack_shift");
        private static readonly int DissolveState = Animator.StringToHash("Base Layer.dissolve");
        private static readonly int AttackTag = Animator.StringToHash("Attack");

        // Dissolve
        [SerializeField] private SkinnedMeshRenderer[] MeshR;
        private float Dissolve_value = 1f;
        private bool DissolveFlg = false;

        private const int maxHP = 3;
        private int HP = maxHP;
        private Text HP_text;

        // Скорость движения (передаётся в NavMeshAgent)
        [SerializeField] private float Speed = 4f;

        // Для анимации — порог, когда считать что персонаж движется
        private const float movingThreshold = 0.1f;

        void Awake()
        {
            Anim = GetComponent<Animator>();
            Agent = GetComponent<NavMeshAgent>();

            Agent.speed = Speed;
            Agent.angularSpeed = 720f;          // быстрое поворачивание
            Agent.acceleration = 20f;
            Agent.stoppingDistance = 0.3f;      // можно подкрутить под вкус
        }

        void Start()
        {
            HP_text = GameObject.Find("Canvas/HP")?.GetComponent<Text>();
            if (HP_text) HP_text.text = "HP " + HP;

            // Начальное состояние
            Anim.CrossFade(IdleState, 0.1f);
        }

        void Update()
        {
            STATUS();
            HandleClickToMove();
            HandleAnimations();
            HandleAttack();
            HandleDamage();
            HandleDissolve();
            HandleRespawn();
        }

        // ────────────────────────────────────────────────
        // Клик правой кнопкой → движение по NavMesh
        // ────────────────────────────────────────────────
        private void HandleClickToMove()
        {
            if (Input.GetMouseButtonDown(1))    // 1 = правая кнопка мыши
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    // Можно добавить слой только для NavMesh поверхности, если нужно
                    // Сейчас просто любой hit → но лучше всего hit по NavMesh (см. ниже)

                    Agent.SetDestination(hit.point);
                }
            }
        }

        // Более точный вариант (только по NavMesh)
        // private void HandleClickToMove()
        // {
        //     if (Input.GetMouseButtonDown(1))
        //     {
        //         Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        //         if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        //         {
        //             if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        //             {
        //                 Agent.SetDestination(navHit.position);
        //             }
        //         }
        //     }
        // }

        private void HandleAnimations()
        {
            bool isMoving = Agent.velocity.magnitude > movingThreshold;

            if (isMoving)
            {
                if (!Anim.GetCurrentAnimatorStateInfo(0).IsName("move"))
                {
                    Anim.CrossFade(MoveState, 0.1f);
                }
            }
            else
            {
                if (!Anim.GetCurrentAnimatorStateInfo(0).IsName("idle"))
                {
                    Anim.CrossFade(IdleState, 0.1f);
                }
            }
        }

        // ────────────────────────────────────────────────
        // Атака (осталась на A)
        // ────────────────────────────────────────────────
        private void HandleAttack()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                Anim.CrossFade(AttackState, 0.1f);
            }
        }

        // ────────────────────────────────────────────────
        // Урон (для теста — S, можно потом заменить на триггер/коллизию)
        // ────────────────────────────────────────────────
        private void HandleDamage()
        {
            if (Input.GetKeyUp(KeyCode.S))
            {
                if (HP > 0)
                {
                    HP--;
                    if (HP_text) HP_text.text = "HP " + HP;

                    Anim.CrossFade(SurprisedState, 0.1f);

                    if (HP <= 0)
                    {
                        DissolveFlg = true;
                    }
                }
            }
        }

        // ────────────────────────────────────────────────
        // Dissolve (растворение)
        // ────────────────────────────────────────────────
        private void HandleDissolve()
        {
            if (DissolveFlg && Dissolve_value > 0)
            {
                Dissolve_value -= Time.deltaTime;
                foreach (var mesh in MeshR)
                {
                    if (mesh && mesh.material) mesh.material.SetFloat("_Dissolve", Dissolve_value);
                }

                if (Dissolve_value <= 0)
                {
                    Agent.enabled = false;          // останавливаем движение
                }
            }
        }

        // ────────────────────────────────────────────────
        // Respawn (Space)
        // ────────────────────────────────────────────────
        private void HandleRespawn()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                HP = maxHP;
                if (HP_text) HP_text.text = "HP " + HP;

                Agent.enabled = false;
                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
                Agent.enabled = true;

                Dissolve_value = 1f;
                foreach (var mesh in MeshR)
                {
                    if (mesh && mesh.material) mesh.material.SetFloat("_Dissolve", 1f);
                }

                DissolveFlg = false;
                Anim.CrossFade(IdleState, 0.1f);
            }
        }

        // ────────────────────────────────────────────────
        // Состояния (адаптировано под новый подход)
        // ────────────────────────────────────────────────
        private const int Dissolve = 1;
        private const int Attack = 2;
        private const int Surprised = 3;

        private Dictionary<int, bool> PlayerStatus = new Dictionary<int, bool>
        {
            { Dissolve,   false },
            { Attack,     false },
            { Surprised,  false }
        };

        private void STATUS()
        {
            // Dissolve
            PlayerStatus[Dissolve] = DissolveFlg && HP <= 0;

            // Attack (по тегу анимации)
            PlayerStatus[Attack] = Anim.GetCurrentAnimatorStateInfo(0).tagHash == AttackTag;

            // Surprised / damaged
            PlayerStatus[Surprised] = Anim.GetCurrentAnimatorStateInfo(0).fullPathHash == SurprisedState;
        }
    }
}