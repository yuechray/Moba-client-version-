using UnityEngine;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    public class StructureController : EntityController
    {
        [SerializeField] private Transform hpBarOffset;

        private Renderer[] _renderers;

        protected override void OnInit()
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        public void ApplyServerState(Structure state)
        {
            if (!IsInitialized) return;

            SetHPBar(state.Health, state.MaxHealth);

            if (state.IsDestroyed) OnStructureDestroyed();
        }

        private void OnStructureDestroyed()
        {
            HideHPBar();

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                foreach (var mat in r.materials)
                    mat.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }
        }

        void LateUpdate() => BillboardHPBar();
    }
}
