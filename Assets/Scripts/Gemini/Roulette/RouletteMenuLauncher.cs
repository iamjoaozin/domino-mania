using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Gemini.Roulette
{
    [DefaultExecutionOrder(1000)]
    public class RouletteMenuLauncher : MonoBehaviour
    {
        [SerializeField] private string buttonName = "presente";
        [SerializeField] private string rouletteCanvasName = "ReactRouletteCanvas";
        [SerializeField] private string legacyDailyRewardName = "Daily Reward Overlay";

        private BootReactRoulette roulette;
        private Button targetButton;
        private int hookFramesRemaining = 180;

        private void Start()
        {
            HideLegacyDailyReward();
            EnsureRoulette();
            StartCoroutine(HookButtonAfterMenuSetup());
        }

        private void LateUpdate()
        {
            if (hookFramesRemaining > 0)
            {
                hookFramesRemaining--;
                HookButton();
                return;
            }

            if (Time.frameCount % 60 == 0)
                HookButton();
        }

        public void OpenRoulette()
        {
            HideLegacyDailyReward();
            EnsureRoulette();
            roulette.OpenRoulette();
        }

        private IEnumerator HookButtonAfterMenuSetup()
        {
            yield return null;
            HookButton();
        }

        private void EnsureRoulette()
        {
            if (roulette != null) return;

            roulette = BootReactRoulette.Instance;
            if (roulette != null) return;

            GameObject existing = GameObject.Find(rouletteCanvasName);
            if (existing == null)
            {
                existing = new GameObject(rouletteCanvasName, typeof(RectTransform));
            }

            roulette = existing.GetComponent<BootReactRoulette>();
            if (roulette == null) roulette = existing.AddComponent<BootReactRoulette>();
        }

        private void HookButton()
        {
            GameObject buttonObject = GameObject.Find(buttonName);
            if (buttonObject == null)
            {
                Debug.LogWarning($"[RouletteMenuLauncher] Botao '{buttonName}' nao encontrado.");
                return;
            }

            targetButton = buttonObject.GetComponent<Button>();
            if (targetButton == null)
            {
                Debug.LogWarning($"[RouletteMenuLauncher] '{buttonName}' nao possui Button.");
                return;
            }

            targetButton.onClick.RemoveAllListeners();
            targetButton.onClick.AddListener(OpenRoulette);
        }

        private void HideLegacyDailyReward()
        {
            GameObject legacy = GameObject.Find(legacyDailyRewardName);
            if (legacy != null && legacy != roulette?.gameObject)
                legacy.SetActive(false);
        }
    }
}
