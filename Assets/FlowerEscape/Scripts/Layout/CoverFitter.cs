using UnityEngine;

namespace FlowerEscape.UI
{
    /// <summary>
    /// Scales this RectTransform (centered in its parent) so it fully covers the parent while keeping
    /// its aspect ratio — like CSS "background-size: cover". Used for the environment background so it
    /// fills both 9:21 phones and 3:4 tablets without stretching.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class CoverFitter : MonoBehaviour
    {
        public Vector2 designSize = new Vector2(874f, 874f);
        [Tooltip("Normalised point of the image that stays pinned to the same point of the parent (0.5,0.5 = centre, 0.5,1 = top).")]
        public Vector2 focus = new Vector2(0.5f, 1f);

        Vector2 _lastParentSize;

        void OnEnable() => Apply();

        void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent != null && parent.rect.size != _lastParentSize) Apply();
        }

        public void Apply()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null || designSize.x <= 0f || designSize.y <= 0f) return;
            Vector2 p = parent.rect.size;
            _lastParentSize = p;
            float scale = Mathf.Max(p.x / designSize.x, p.y / designSize.y);
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = focus;
            rt.pivot = focus;
            rt.sizeDelta = designSize * scale;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
