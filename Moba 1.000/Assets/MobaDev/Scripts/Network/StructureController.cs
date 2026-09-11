using UnityEngine;
using UnityEngine.UI;
using SpacetimeDB.Types;

namespace MobaDev.Network
{
    public class StructureController : EntityController
    {
        [SerializeField] private Vector3 hpBarOffset = new Vector3(0f, 6f, 0f);

        private Renderer[] _renderers;
        private bool       _teamColorApplied;

        protected override void OnInit()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            if (hpCanvas == null)
                BuildHPBar();
        }

        public void ApplyServerState(Structure state)
        {
            if (!IsInitialized) return;

            if (!_teamColorApplied && hpFill != null)
            {
                hpFill.color = state.Team == Team.Radiant
                    ? new Color(0.2f, 0.8f, 0.2f, 1f)
                    : new Color(0.9f, 0.2f, 0.2f, 1f);
                _teamColorApplied = true;
            }

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

        private void BuildHPBar()
        {
            var canvasGO = new GameObject("HPCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = hpBarOffset;
            canvasGO.transform.localScale    = Vector3.one * 0.02f;
            canvasGO.layer = 5;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasRenderer>();

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 20f);

            var bgGO  = new GameObject("BG");
            bgGO.transform.SetParent(canvasGO.transform, false);
            bgGO.layer = 5;
            var bgRT  = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg  = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            var fillGO  = new GameObject("Fill");
            fillGO.transform.SetParent(canvasGO.transform, false);
            fillGO.layer = 5;
            var fillRT  = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg  = fillGO.AddComponent<Image>();
            fillImg.color      = Color.white;
            fillImg.type       = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;

            hpCanvas = canvas;
            hpFill   = fillImg;
        }
    }
}
