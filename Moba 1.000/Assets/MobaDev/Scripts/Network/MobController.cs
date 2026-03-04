using UnityEngine;
using UnityEngine.AI;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    /// <summary>Base class for moving entities that use NavMeshAgent (champions and creeps).</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class MobController : EntityController
    {
        protected NavMeshAgent Agent { get; private set; }

        protected override void OnInit()
        {
            Agent = GetComponent<NavMeshAgent>();
            ConfigureAgent();
        }

        protected abstract void ConfigureAgent();

        protected void SyncPosition(DbVec3 serverPos, float lerpSpeed)
        {
            transform.position = Vector3.Lerp(
                transform.position, ToVector3(serverPos), Time.deltaTime * lerpSpeed
            );
        }

        protected void SetNavDestination(DbVec3 dest)
        {
            if (!Agent.isOnNavMesh) return;
            Agent.SetDestination(ToVector3(dest));
            Agent.isStopped = false;
        }

        protected void StopNav()
        {
            if (Agent.isOnNavMesh)
                Agent.isStopped = true;
        }
    }
}
