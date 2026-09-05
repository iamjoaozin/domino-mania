using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace Gemini.UI
{
    [RequireComponent(typeof(Image))]
    public class BannerCarousel : MonoBehaviour
    {
        public List<Sprite> banners;
        public float displayTime = 4f;
        public float fadeTime = 0.5f;

        private Image bannerImage;
        private CanvasGroup canvasGroup;
        private int currentIndex = 0;

        void Awake()
        {
            bannerImage = GetComponent<Image>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        void Start()
        {
            if (banners != null && banners.Count > 0)
            {
                bannerImage.sprite = banners[0];
                if (banners.Count > 1)
                {
                    StartCoroutine(CarouselRoutine());
                }
            }
        }

        IEnumerator CarouselRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(displayTime);

                // Fade out
                float t = 0;
                while (t < fadeTime)
                {
                    t += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
                    yield return null;
                }

                // Swap banner
                currentIndex = (currentIndex + 1) % banners.Count;
                bannerImage.sprite = banners[currentIndex];

                // Fade in
                t = 0;
                while (t < fadeTime)
                {
                    t += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeTime);
                    yield return null;
                }
            }
        }
    }
}
