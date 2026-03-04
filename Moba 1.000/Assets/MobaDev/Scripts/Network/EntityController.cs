using UnityEngine;
using UnityEngine.UI;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    /// <summary>Base class for all networked game entities (champions, creeps, structures).</summary>
    public abstract class EntityController : MonoBehaviour
    {
        [Header("HP Bar")]
        [SerializeField] protected Canvas hpCanvas;
        [SerializeField] protected Image  hpFill;

        public ulong EntityId        { get; private set; }
        protected bool IsInitialized { get; private set; }

        public virtual void Init(ulong id)
        {
            EntityId      = id;
            IsInitialized = true;
            OnInit();
        }

        protected virtual void OnInit() { }

        protected void SetHPBar(float hp, float maxHp)
        {
            if (hpFill != null && maxHp > 0)
                hpFill.fillAmount = hp / maxHp;
        }

        protected void HideHPBar()
        {
            if (hpCanvas != null)
                hpCanvas.gameObject.SetActive(false);
        }

        // Call from LateUpdate to keep the HP canvas facing the camera.
        protected void BillboardHPBar()
        {
            if (hpCanvas != null && Camera.main != null)
                hpCanvas.transform.LookAt(
                    hpCanvas.transform.position + Camera.main.transform.rotation * Vector3.forward
                );
        }

        protected static Vector3 ToVector3(DbVec3 v) => new(v.X, v.Y, v.Z);
    }
}
