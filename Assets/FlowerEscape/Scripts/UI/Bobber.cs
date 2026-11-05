using UnityEngine;

namespace FlowerEscape.UI
{
    /// <summary>Idle floating motion for units on the water (boat, crew, monsters).</summary>
    public class Bobber : MonoBehaviour
    {
        public float amplitude = 2.5f;
        public float frequency = 1.2f;
        public float phase;
        public float driftX;   // px per second, wraps inside driftRange
        public float driftRange = 40f;

        RectTransform _rt;
        Vector2 _base;
        float _drift;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _base = _rt.anchoredPosition;
        }

        void OnDisable()
        {
            if (_rt != null) _rt.anchoredPosition = _base;
        }

        void Update()
        {
            _drift = driftX == 0f ? 0f : Mathf.PingPong(Time.time * Mathf.Abs(driftX), driftRange) * Mathf.Sign(driftX);
            float y = Mathf.Sin((Time.time * frequency + phase) * Mathf.PI * 2f) * amplitude;
            _rt.anchoredPosition = _base + new Vector2(_drift, y);
        }
    }
}
