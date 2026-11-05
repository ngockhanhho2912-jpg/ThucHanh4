using UnityEngine;

namespace FlowerEscape.UI
{
    /// <summary>
    /// Lets an element that spans the full width of the 402px design column (bottom bar, ad strip)
    /// extend to the real screen edges on wide screens such as 3:4 tablets.
    /// The design column is centered in the canvas, so the element grows equally on both sides.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class HorizontalBleed : MonoBehaviour
    {
        public float designX;       // left edge inside the design frame (px)
        public float designWidth;   // width inside the design frame (px)
        public float frameWidth = 402f;

        float _lastCanvasWidth = -1f;

        void OnEnable() => Apply();

        void LateUpdate()
        {
            var c = CanvasRect();
            if (c != null && !Mathf.Approximately(c.rect.width, _lastCanvasWidth)) Apply();
        }

        RectTransform CanvasRect()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        }

        public void Apply()
        {
            var c = CanvasRect();
            if (c == null) return;
            _lastCanvasWidth = c.rect.width;
            float bleed = Mathf.Max(0f, (c.rect.width - frameWidth) * 0.5f);
            var rt = (RectTransform)transform;
            float left = designX <= 0.5f ? designX - bleed : designX;
            float right = designX + designWidth >= frameWidth - 0.5f ? designX + designWidth + bleed : designX + designWidth;
            // Leaves are anchored to the design frame's top-left with a centered pivot.
            var pos = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2((left + right) * 0.5f, pos.y);
            rt.sizeDelta = new Vector2(right - left, rt.sizeDelta.y);
        }
    }
}
