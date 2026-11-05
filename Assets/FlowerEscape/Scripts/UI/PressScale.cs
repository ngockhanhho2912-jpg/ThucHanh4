using UnityEngine;
using UnityEngine.EventSystems;

namespace FlowerEscape.UI
{
    /// <summary>Small "squash" feedback when a button is pressed.</summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressedScale = 0.92f;
        public float speed = 18f;

        float _target = 1f;

        public void OnPointerDown(PointerEventData e) => _target = pressedScale;
        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;

        void OnDisable()
        {
            _target = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, _target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
