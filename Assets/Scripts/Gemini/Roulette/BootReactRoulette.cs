using ReactUnity;
using ReactUnity.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace Gemini.Roulette
{
    [DisallowMultipleComponent]
    public class BootReactRoulette : MonoBehaviour
    {
        public static BootReactRoulette Instance { get; private set; }

        [SerializeField] private string sourceResourcePath = "react/roulette/index_bundle";
        [SerializeField] private int sortingOrder = 25000;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 2400f);

        private Canvas canvas;
        private CanvasScaler canvasScaler;
        private ReactRendererUGUI renderer;
        private DailyRouletteAPI api;
        private bool rendered;

        private void Awake()
        {
            Instance = this;
            api = new DailyRouletteAPI();
            EnsureCanvas();
            EnsureRenderer();
            SetVisible(false);
        }

        private void Start()
        {
            RenderIfNeeded();
        }

        public void OpenRoulette()
        {
            RenderIfNeeded();
            SetVisible(true);
        }

        public void CloseRoulette()
        {
            SetVisible(false);
        }

        private void RenderIfNeeded()
        {
            EnsureCanvas();
            EnsureRenderer();

            if (rendered)
            {
                renderer.Globals.Set("RouletteOpened", Time.realtimeSinceStartup);
                return;
            }

            string sourcePath = sourceResourcePath;
            if (string.IsNullOrWhiteSpace(sourcePath) || sourcePath == "react/roulette/index")
                sourcePath = "react/roulette/index_bundle";

            renderer.Source = ScriptSource.Resource(sourcePath);
            renderer.AdvancedOptions.AutoRender = false;
            renderer.Globals["RouletteAPI"] = api;
            renderer.Globals["CloseRoulette"] = new System.Action(CloseRoulette);
            renderer.Globals["PlayClickSound"] = new System.Action(PlayClickSound);
            renderer.Render();
            rendered = true;
        }

        private void EnsureCanvas()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            canvasScaler = GetComponent<CanvasScaler>();
            if (canvasScaler == null) canvasScaler = gameObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = referenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        private void EnsureRenderer()
        {
            renderer = GetComponent<ReactRendererUGUI>();
            if (renderer == null) renderer = gameObject.AddComponent<ReactRendererUGUI>();
        }

        private void SetVisible(bool visible)
        {
            EnsureCanvas();
            canvas.enabled = visible;

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = visible;
        }

        private void PlayClickSound()
        {
            var clip = Resources.Load<AudioClip>("Audio/ui_click");
            if (clip == null) return;

            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.ignoreListenerPause = true;
            source.clip = clip;
            source.Play();
            Destroy(source, clip.length + 0.05f);
        }
    }
}
