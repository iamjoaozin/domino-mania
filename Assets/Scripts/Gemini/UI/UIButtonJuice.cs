using UnityEngine;
using UnityEngine.EventSystems;

namespace Gemini.UI
{
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.05f;
        public float clickScale = 0.95f;
        public float animationSpeed = 10f;
        
        public bool breathing = true;
        public float breathSpeed = 2f;
        public float breathAmount = 0.02f;

        private Vector3 originalScale;
        private Vector3 targetScale;
        private bool isHovering = false;
        private bool isPressed = false;

        void Awake()
        {
            originalScale = transform.localScale;
            targetScale = originalScale;
        }

        void Update()
        {
            if (isPressed)
            {
                targetScale = originalScale * clickScale;
            }
            else if (isHovering)
            {
                targetScale = originalScale * hoverScale;
            }
            else
            {
                if (breathing)
                {
                    float wave = Mathf.Sin(Time.unscaledTime * breathSpeed) * breathAmount;
                    targetScale = originalScale * (1f + wave);
                }
                else
                {
                    targetScale = originalScale;
                }
            }

            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }
    }
}
