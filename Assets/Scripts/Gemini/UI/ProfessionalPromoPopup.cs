using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace Gemini.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ProfessionalPromoPopup : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform panelRect;
        public Button closeButton;
        public Button backgroundBlocker;
        public TextMeshProUGUI titleText;
        public Image bannerImage;

        [Header("Animation Settings")]
        public float animDuration = 0.4f;
        public AnimationCurve easeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve easeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        private CanvasGroup canvasGroup;
        private static bool hasShownThisSession = false;
        private bool isAnimating = false;

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
                
            if (backgroundBlocker != null)
                backgroundBlocker.onClick.AddListener(Close);

            // Initially hide
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            if (panelRect != null) panelRect.localScale = Vector3.zero;
        }

        void Start()
        {
            if (!hasShownThisSession)
            {
                Show();
                hasShownThisSession = true;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Show()
        {
            if (isAnimating) return;
            StartCoroutine(Animate(true));
        }

        public void Close()
        {
            if (isAnimating) return;
            StartCoroutine(Animate(false));
        }

        private IEnumerator Animate(bool show)
        {
            isAnimating = true;
            
            if (show)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
            else
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            float t = 0;
            Vector3 startScale = show ? Vector3.zero : Vector3.one;
            Vector3 endScale = show ? Vector3.one : Vector3.zero;
            float startAlpha = show ? 0f : 1f;
            float endAlpha = show ? 1f : 0f;
            AnimationCurve curve = show ? easeInCurve : easeOutCurve;

            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / animDuration);
                float curveValue = curve.Evaluate(progress);

                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
                if (panelRect != null)
                    panelRect.localScale = Vector3.LerpUnclamped(startScale, endScale, curveValue); // Unclamped for bounce
                
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
            if (panelRect != null) panelRect.localScale = endScale;
            
            if (!show)
            {
                Destroy(gameObject);
            }

            isAnimating = false;
        }

        // For testing/editor
        public void ResetSession()
        {
            hasShownThisSession = false;
        }
    }
}
