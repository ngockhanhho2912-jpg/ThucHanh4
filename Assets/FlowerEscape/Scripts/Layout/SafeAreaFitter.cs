using UnityEngine;

namespace FlowerEscape.UI
{
    /// <summary>
    /// Shrinks this RectTransform to Screen.safeArea so HUD elements stay clear of notches / punch holes
    /// on tall (9:21) phones. The parent is expected to be the full-screen canvas.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Ignore the safe area (used by the editor preview renderer).")]
        public bool ignoreSafeArea;

        Rect _lastSafeArea;
        Vector2Int _lastScreen;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != _lastSafeArea || _lastScreen.x != Screen.width || _lastScreen.y != Screen.height)
                Apply();
        }

        public void Apply()
        {
            var rt = (RectTransform)transform;
            _lastSafeArea = Screen.safeArea;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);

            if (ignoreSafeArea || !Application.isPlaying || Screen.width <= 0 || Screen.height <= 0)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
            }
            else
            {
                Rect sa = Screen.safeArea;
                rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
                rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            }
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
