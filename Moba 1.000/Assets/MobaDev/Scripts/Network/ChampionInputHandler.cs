using UnityEngine;
using UnityEngine.AI;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    [RequireComponent(typeof(ChampionController))]
    public class ChampionInputHandler : MonoBehaviour
    {
        private ChampionController _ctrl;
        private NavMeshAgent       _agent;

        void Awake()
        {
            _ctrl  = GetComponent<ChampionController>();
            _agent = GetComponent<NavMeshAgent>();
        }

        void Update()
        {
            if (!_ctrl.IsLocalPlayer || _ctrl.IsDead) return;

            var conn = SpacetimeNetworkManager.Instance?.Conn;
            if (conn == null) return;
            if (!Input.GetMouseButtonDown(1)) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f)) return;

            var enemyChamp = hit.collider.GetComponentInParent<ChampionController>();
            if (enemyChamp != null && !enemyChamp.IsLocalPlayer && !enemyChamp.IsDead)
            {
                conn.Reducers.IssueAttack(TargetKind.Champion, enemyChamp.EntityId);
                return;
            }

            var creepCtrl = hit.collider.GetComponentInParent<CreepController>();
            if (creepCtrl != null)
            {
                conn.Reducers.IssueAttack(TargetKind.Creep, creepCtrl.EntityId);
                return;
            }

            var structCtrl = hit.collider.GetComponentInParent<StructureController>();
            if (structCtrl != null)
            {
                conn.Reducers.IssueAttack(TargetKind.Structure, structCtrl.EntityId);
                return;
            }

            conn.Reducers.MoveToPosition(hit.point.x, hit.point.y, hit.point.z);
            _agent.SetDestination(hit.point);
            _agent.isStopped = false;
        }
    }
}
