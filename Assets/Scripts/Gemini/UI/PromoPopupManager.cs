using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Gemini.UI
{
    public class PromoPopupManager : MonoBehaviour
    {
        public Sprite[] banners;
        public float animDuration = 0.4f;

        private GameObject popupRoot;
        private Image bannerImage;
        private CanvasGroup canvasGroup;
        private static bool hasShownPopup = false;

        void Start()
        {
            if (banners == null || banners.Length == 0 || hasShownPopup) return;
            
            // Show only once per session
            hasShownPopup = true;
            CreatePopupUI();
            StartCoroutine(ShowPopupAnim());
        }

        void CreatePopupUI()
        {
            // Blocker (dark background)
            popupRoot = new GameObject("PromoPopupRoot");
            popupRoot.transform.SetParent(transform, false);
            var rect = popupRoot.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            var blockerImg = popupRoot.AddComponent<Image>();
            blockerImg.color = new Color(0, 0, 0, 0.85f); // Dark semi-transparent
            
            // Add block click
            var blockerBtn = popupRoot.AddComponent<Button>();
            blockerBtn.onClick.AddListener(ClosePopup);

            // Banner Container
            var bannerContainer = new GameObject("BannerImage");
            bannerContainer.transform.SetParent(popupRoot.transform, false);
            var bRect = bannerContainer.AddComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0.5f, 0.5f);
            bRect.anchorMax = new Vector2(0.5f, 0.5f);
            bRect.sizeDelta = new Vector2(600, 600); // Popup size
            
            bannerImage = bannerContainer.AddComponent<Image>();
            bannerImage.sprite = banners[0];
            bannerImage.preserveAspect = true;

            // Close Button (X)
            var closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(bannerContainer.transform, false);
            var cRect = closeBtnObj.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(1f, 1f);
            cRect.anchorMax = new Vector2(1f, 1f);
            cRect.anchoredPosition = new Vector2(-20, -20);
            cRect.sizeDelta = new Vector2(60, 60);

            var closeImg = closeBtnObj.AddComponent<Image>();
            closeImg.color = new Color(0.8f, 0, 0, 1f); // Red box
            
            var textObj = new GameObject("X");
            textObj.transform.SetParent(closeBtnObj.transform, false);
            var tRect = textObj.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
            var txt = textObj.AddComponent<Text>();
            txt.text = "X";
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 40;

            var closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(ClosePopup);

            canvasGroup = popupRoot.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            bannerContainer.transform.localScale = Vector3.zero;
        }

        IEnumerator ShowPopupAnim()
        {
            var bannerContainer = bannerImage.transform;
            float t = 0;
            while (t < animDuration)
            {
                t += Time.deltaTime;
                float progress = t / animDuration;
                
                // Elastic effect
                float scale = progress;
                if (progress > 0.7f) scale = 1f + Mathf.Sin((progress - 0.7f) * Mathf.PI * 3f) * 0.05f;
                else scale = progress / 0.7f;
                
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
                bannerContainer.localScale = Vector3.one * scale;
                yield return null;
            }
            bannerContainer.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
        }

        public void ClosePopup()
        {
            StartCoroutine(HidePopupAnim());
        }

        IEnumerator HidePopupAnim()
        {
            var bannerContainer = bannerImage.transform;
            float t = 0;
            while (t < animDuration)
            {
                t += Time.deltaTime;
                float progress = t / animDuration;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
                bannerContainer.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, progress);
                yield return null;
            }
            Destroy(popupRoot);
        }
    }
}
