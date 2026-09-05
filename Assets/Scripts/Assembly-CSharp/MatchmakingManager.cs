using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GBTemplates.Domino.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the visual logic for Matchmaking. 
/// You should create the UI visually in the Editor and assign the references here.
/// </summary>
[DefaultExecutionOrder(34000)]
public sealed class MatchmakingManager : MonoBehaviour
{
    private const string CleanStripRootName = "Premium Matchmaking Strip Root";
    private const float BotMatchSearchDuration = 10f;
    [Header("Matchmaking Visual UI")]
    public GameObject matchmakingPanel; // The main canvas or panel to enable/disable
    public RectTransform topBanner; // The banner that slides down

    public TextMeshProUGUI statusText;
    public Image matchFoundFlash;
    public RectTransform opponentPanelToShake;
    public Image progressFill;
    public Image bannerGlow;
    public RectTransform scanLine;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI badgeText;
    public TextMeshProUGUI cancelText;

    private static MatchmakingManager instance;
    private static readonly Color DeepPanel = new Color(0.02f, 0.006f, 0.036f, 0.94f);
    private static readonly Color Gold = new Color(1f, 0.72f, 0.12f, 1f);
    private static readonly Color Pink = new Color(1f, 0.08f, 0.78f, 1f);
    private static readonly Color Cyan = new Color(0.25f, 0.88f, 1f, 1f);

    private Coroutine animRoutine;
    private Coroutine fastFallbackRoutine;
    private bool isMatching;
    private bool matchFoundTriggered;
    private float nextHookTime;
    private float nextOverlaySanitizeTime;
    private float minigameStartTime;
    private HashSet<Button> hookedButtons = new HashSet<Button>();
    private readonly List<PremiumUiParticle> premiumParticles = new List<PremiumUiParticle>();
    private readonly List<Image> queueBars = new List<Image>();
    private readonly List<CanvasGroup> hiddenLegacyGroups = new List<CanvasGroup>();
    private RectTransform particleRoot;
    private Sprite topStripSprite;
    private Sprite bannerPanelSprite;
    private Sprite neonGlowSprite;
    private Sprite scanLineSprite;
    private Sprite progressFillSprite;
    private Sprite particleStarSprite;
    private Sprite particleDiamondSprite;
    private Vector2 offscreenPos = new Vector2(0, 112);
    private Vector2 onscreenPos = new Vector2(0, -34);

    private sealed class PremiumUiParticle
    {
        public RectTransform rect;
        public Image image;
        public Vector2 basePosition;
        public float phase;
        public float speed;
        public float amplitude;
        public float size;
        public Color color;
    }

    private sealed class PersistentButtonCall
    {
        public Object target;
        public string methodName;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject runner = new GameObject(nameof(MatchmakingManager));
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<MatchmakingManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        nextHookTime = 0f;
        if (!matchFoundTriggered) 
        {
            HideInstant();
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextHookTime)
        {
            nextHookTime = Time.unscaledTime + 1f;
            HookCityButtons();
        }

        if (isMatching && !matchFoundTriggered)
        {
            if (Time.unscaledTime >= nextOverlaySanitizeTime)
            {
                nextOverlaySanitizeTime = Time.unscaledTime + 0.25f;
                SanitizeMatchmakingOverlay();
                HideLegacyNetworkLobbyUi();
            }

            if (DependencyCache.DominoController != null && DependencyCache.DominoController.IsInMatch)
            {
                matchFoundTriggered = true;
                Time.timeScale = 0f; // Pausa a partida no fundo
                StartCoroutine(CompleteMatchmakingRoutine());
            }
        }
    }

    public void StartMatchmaking()
    {
        if (isMatching) return;

        AudioClip clip = null;
#if UNITY_EDITOR
        clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/matchmaking_retro.wav");
#endif
        if (clip == null) clip = Resources.Load<AudioClip>("Audio/matchmaking_retro");
        if (clip != null && Camera.main != null) AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, 1f);

        isMatching = true;
        matchFoundTriggered = false;
        minigameStartTime = Time.unscaledTime;
        if (fastFallbackRoutine != null)
        {
            StopCoroutine(fastFallbackRoutine);
            fastFallbackRoutine = null;
        }
        EnsurePremiumBannerSkin();
        SanitizeMatchmakingOverlay();
        HideLegacyNetworkLobbyUi();
        ResetPremiumBannerState();

        if (matchmakingPanel != null) matchmakingPanel.SetActive(true);
        if (statusText != null) 
        {
            statusText.text = "PROCURANDO ADVERSARIO";
            statusText.color = Gold;
        }
        
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(BannerAnimationRoutine());
    }

    private IEnumerator BannerAnimationRoutine()
    {
        if (topBanner == null) yield break;

        // 1. Slide In (Ease Out Back)
        float t = 0;
        float duration = 0.2f;
        topBanner.localScale = Vector3.one;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / duration;
            // Ease out back
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float p = 1f + c3 * Mathf.Pow(progress - 1f, 3f) + c1 * Mathf.Pow(progress - 1f, 2f);
            
            topBanner.anchoredPosition = Vector2.LerpUnclamped(offscreenPos, onscreenPos, p);
            yield return null;
        }
        topBanner.anchoredPosition = onscreenPos;

        // 2. Pulse while searching
        t = 0;
        int dotCount = 0;
        float dotTimer = 0;
        while (!matchFoundTriggered && isMatching)
        {
            t += Time.unscaledDeltaTime;
            float scale = 1f + Mathf.Sin(t * 3f) * 0.004f;
            topBanner.localScale = new Vector3(1f, scale, 1f);

            // Animate text dots
            dotTimer += Time.unscaledDeltaTime;
            if (dotTimer > 0.4f)
            {
                dotTimer = 0;
                dotCount = (dotCount + 1) % 4;
                if (statusText != null)
                {
                    statusText.text = "PROCURANDO ADVERSARIO" + new string('.', dotCount);
                }
            }
            UpdatePremiumBannerSearch(t);
            yield return null;
        }
    }

    private IEnumerator CompleteMatchmakingRoutine()
    {
        if (statusText != null) 
        {
            statusText.text = "ADVERSARIO ENCONTRADO!";
            statusText.color = Cyan;
        }

        if (badgeText != null)
        {
            badgeText.text = "ENTRANDO";
        }

        if (timerText != null)
        {
            timerText.text = "MESA ENCONTRADA";
        }

        if (progressFill != null)
        {
            progressFill.fillAmount = 1f;
            progressFill.color = Cyan;
        }

        UpdateQueueBars(Time.unscaledTime - minigameStartTime, true);

        // Pop impact animation
        if (topBanner != null)
        {
            topBanner.localScale = new Vector3(1f, 1.03f, 1f);
        }

        if (matchFoundFlash != null) matchFoundFlash.gameObject.SetActive(false);

        // Return from pop smoothly
        float t = 0;
        const float foundImpactDuration = 0.16f;
        while (t < foundImpactDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / foundImpactDuration;
            if (topBanner != null)
            {
                topBanner.localScale = Vector3.Lerp(new Vector3(1f, 1.03f, 1f), Vector3.one, progress);
            }
            UpdatePremiumParticles(Time.unscaledTime - minigameStartTime, true);
            yield return null;
        }
        if (matchFoundFlash != null) matchFoundFlash.gameObject.SetActive(false);

        // Keep the confirmation readable without delaying entry into the match.
        yield return new WaitForSecondsRealtime(0.25f);

        // 3. Slide Out
        t = 0;
        float duration = 0.18f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / duration;
            // Ease in back
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float p = c3 * progress * progress * progress - c1 * progress * progress;
            
            if (topBanner != null)
                topBanner.anchoredPosition = Vector2.LerpUnclamped(onscreenPos, offscreenPos, p);
            
            yield return null;
        }

        HideInstant();
    }

    public void Hide()
    {
        HideInstant();
    }

    private void HideInstant()
    {
        isMatching = false;
        Time.timeScale = 1f;
        if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
        if (topBanner != null) topBanner.anchoredPosition = offscreenPos;
        ResetPremiumBannerState();
        RestoreLegacyNetworkLobbyUi();
    }

    private void EnsurePremiumBannerSkin()
    {
        LoadPremiumSprites();
        EnsureCleanStripRoot();

        topBanner.anchorMin = new Vector2(0f, 1f);
        topBanner.anchorMax = new Vector2(1f, 1f);
        topBanner.pivot = new Vector2(0.5f, 1f);
        topBanner.sizeDelta = new Vector2(-36f, 94f);
        onscreenPos = new Vector2(0f, -GetSafeTopOffset());

        Image bannerImage = topBanner.GetComponent<Image>();
        if (bannerImage == null) bannerImage = topBanner.gameObject.AddComponent<Image>();
        bannerImage.color = DeepPanel;
        bannerImage.raycastTarget = false;
        if (topStripSprite != null)
        {
            bannerImage.sprite = topStripSprite;
            bannerImage.type = Image.Type.Sliced;
        }
        else if (bannerPanelSprite != null)
        {
            bannerImage.sprite = bannerPanelSprite;
            bannerImage.type = Image.Type.Sliced;
        }

        Shadow shadow = topBanner.GetComponent<Shadow>();
        if (shadow == null) shadow = topBanner.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(0f, -8f);

        Outline outline = topBanner.GetComponent<Outline>();
        if (outline == null) outline = topBanner.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        bannerGlow = FindOrCreateImage(topBanner, "Banner Glow", new Color(Cyan.r, Cyan.g, Cyan.b, 0.1f), new Vector2(0f, -46f), new Vector2(940f, 64f));
        if (neonGlowSprite != null) bannerGlow.sprite = neonGlowSprite;
        Image topRail = FindOrCreateImage(topBanner, "Top Neon Rail", new Color(Gold.r, Gold.g, Gold.b, 0.5f), new Vector2(0f, -6f), new Vector2(940f, 3f));
        StretchHorizontally(topRail.rectTransform, 32f);
        Image bottomRail = FindOrCreateImage(topBanner, "Bottom Neon Rail", new Color(Cyan.r, Cyan.g, Cyan.b, 0.62f), new Vector2(0f, -88f), new Vector2(940f, 3f));
        StretchHorizontally(bottomRail.rectTransform, 32f);

        Image badgeBack = FindOrCreateImage(topBanner, "Status Badge", new Color(0f, 0f, 0f, 0f), new Vector2(0f, -44f), new Vector2(198f, 36f));
        ConfigureLeftChild(badgeBack.rectTransform, 46f, -44f, 220f, 36f);
        badgeText = FindOrCreateText(badgeBack.rectTransform, "Badge Text", "CANCELAR BUSCA", 17f, new Color(0.78f, 0.94f, 1f, 1f), Vector2.zero, new Vector2(198f, 30f), TextAlignmentOptions.Left);
        cancelText = badgeText;
        StretchInside(badgeText.rectTransform);

        if (statusText == null)
        {
            TMP_Text existing = topBanner.GetComponentInChildren<TMP_Text>(true);
            statusText = existing as TextMeshProUGUI;
        }

        if (statusText == null)
        {
            statusText = FindOrCreateText(topBanner, "Status Text", "PROCURANDO ADVERSARIO", 21f, Gold, new Vector2(-32f, -42f), new Vector2(320f, 36f), TextAlignmentOptions.Center);
        }
        else
        {
            RectTransform statusRect = statusText.rectTransform;
            statusRect.SetParent(topBanner, false);
            statusRect.anchorMin = new Vector2(0.5f, 1f);
            statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(-32f, -42f);
            statusRect.sizeDelta = new Vector2(320f, 36f);
            statusText.fontSize = 21f;
            statusText.fontSizeMax = 21f;
            statusText.fontSizeMin = 13f;
            statusText.fontStyle = FontStyles.Bold;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Gold;
            statusText.raycastTarget = false;
        }

        EnsureQueueBars();

        timerText = FindOrCreateText(topBanner, "Timer Text", "TEMPO ESTIMADO: 10s", 20f, Cyan, Vector2.zero, new Vector2(320f, 34f), TextAlignmentOptions.Right);
        ConfigureRightChild(timerText.rectTransform, 46f, -42f, 330f, 34f);

        Image progressTrack = FindOrCreateImage(topBanner, "Progress Track", new Color(0f, 0f, 0f, 0.38f), new Vector2(0f, -80f), new Vector2(760f, 6f));
        StretchHorizontally(progressTrack.rectTransform, 86f);
        progressFill = FindOrCreateImage(progressTrack.rectTransform, "Progress Fill", Pink, Vector2.zero, new Vector2(500f, 6f));
        if (progressFillSprite != null) progressFill.sprite = progressFillSprite;
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        StretchInside(progressFill.rectTransform);

        Image scanImage = FindOrCreateImage(topBanner, "Scan Line", new Color(Cyan.r, Cyan.g, Cyan.b, 0.15f), new Vector2(-340f, -46f), new Vector2(44f, 82f));
        if (scanLineSprite != null) scanImage.sprite = scanLineSprite;
        scanLine = scanImage.rectTransform;
        scanImage.enabled = scanLineSprite != null;
        scanLine.localRotation = Quaternion.Euler(0f, 0f, -10f);
        EnsurePremiumParticles();
        SanitizeMatchmakingOverlay();
    }

    private void LoadPremiumSprites()
    {
        if (topStripSprite == null) topStripSprite = Resources.Load<Sprite>("MatchmakingPremium/matchmaking_queue_strip");
        if (bannerPanelSprite == null) bannerPanelSprite = Resources.Load<Sprite>("MatchmakingPremium/matchmaking_banner_panel");
        if (neonGlowSprite == null) neonGlowSprite = Resources.Load<Sprite>("MatchmakingPremium/matchmaking_neon_glow");
        if (scanLineSprite == null) scanLineSprite = Resources.Load<Sprite>("MatchmakingPremium/matchmaking_scan_line");
        if (progressFillSprite == null) progressFillSprite = Resources.Load<Sprite>("MatchmakingPremium/matchmaking_progress_fill");
        if (particleStarSprite == null) particleStarSprite = Resources.Load<Sprite>("MatchmakingPremium/matchmaking_particle_star");
        if (particleDiamondSprite == null) particleDiamondSprite = Resources.Load<Sprite>("MatchmakingPremium/matchmaking_particle_diamond");
    }

    private void UpdatePremiumBannerSearch(float time)
    {
        float elapsed = Time.unscaledTime - minigameStartTime;

        if (progressFill != null)
        {
            progressFill.fillAmount = Mathf.Clamp01(elapsed / 10f);
        }

        if (timerText != null)
        {
            int remaining = Mathf.Clamp(10 - Mathf.FloorToInt(elapsed), 1, 99);
            timerText.text = "TEMPO ESTIMADO: " + remaining + "s";
        }

        if (badgeText != null)
        {
            badgeText.text = "CANCELAR BUSCA";
        }

        if (bannerGlow != null)
        {
            float glow = 0.055f + Mathf.Sin(time * 5f) * 0.025f;
            bannerGlow.color = new Color(Cyan.r, Cyan.g, Cyan.b, Mathf.Clamp01(glow));
        }

        if (scanLine != null)
        {
            float width = topBanner != null ? Mathf.Max(720f, topBanner.rect.width) : 1080f;
            float scanX = Mathf.PingPong(time * 520f, width + 160f) - width * 0.5f - 80f;
            scanLine.anchoredPosition = new Vector2(scanX, scanLine.anchoredPosition.y);
        }

        UpdateQueueBars(time, false);
        UpdatePremiumParticles(time, false);
    }

    private void EnsureQueueBars()
    {
        queueBars.Clear();
        for (int i = 0; i < 3; i++)
        {
            Image bar = FindOrCreateImage(topBanner, "Queue Pulse " + i, Gold, new Vector2(128f + i * 12f, -42f), new Vector2(7f, 26f));
            bar.color = new Color(Gold.r, Gold.g, Gold.b, 0.72f);
            queueBars.Add(bar);
        }
    }

    private void UpdateQueueBars(float time, bool found)
    {
        for (int i = 0; i < queueBars.Count; i++)
        {
            Image bar = queueBars[i];
            if (bar == null)
            {
                continue;
            }

            float wave = Mathf.Sin(time * 8f - i * 0.72f) * 0.5f + 0.5f;
            float height = found ? 30f : Mathf.Lerp(14f, 30f, wave);
            RectTransform rect = bar.rectTransform;
            rect.sizeDelta = new Vector2(7f, height);
            rect.anchoredPosition = new Vector2(128f + i * 12f, -42f);
            Color color = found ? Cyan : Gold;
            color.a = found ? 0.95f : Mathf.Lerp(0.42f, 1f, wave);
            bar.color = color;
        }
    }

    private void EnsurePremiumParticles()
    {
        if (topBanner == null)
        {
            return;
        }

        if (particleRoot == null)
        {
            Transform existingRoot = topBanner.Find("Premium Particles");
            if (existingRoot != null)
            {
                particleRoot = existingRoot as RectTransform;
            }
            else
            {
                GameObject rootObj = new GameObject("Premium Particles", typeof(RectTransform));
                particleRoot = rootObj.GetComponent<RectTransform>();
                particleRoot.SetParent(topBanner, false);
            }
        }

        particleRoot.anchorMin = new Vector2(0f, 1f);
        particleRoot.anchorMax = new Vector2(1f, 1f);
        particleRoot.pivot = new Vector2(0.5f, 1f);
        particleRoot.anchoredPosition = Vector2.zero;
        particleRoot.sizeDelta = new Vector2(0f, 94f);
        particleRoot.SetSiblingIndex(Mathf.Min(3, particleRoot.parent.childCount - 1));

        premiumParticles.Clear();
        int particleCount = 16;
        for (int i = 0; i < particleCount; i++)
        {
            string name = "Spark " + i.ToString("00");
            Transform child = particleRoot.Find(name);
            RectTransform rect;
            Image image;
            if (child == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform));
                rect = obj.GetComponent<RectTransform>();
                rect.SetParent(particleRoot, false);
                image = obj.AddComponent<Image>();
            }
            else
            {
                rect = child.GetComponent<RectTransform>();
                image = child.GetComponent<Image>();
                if (image == null) image = child.gameObject.AddComponent<Image>();
            }

            rect.gameObject.SetActive(true);
            float u = ((i * 37) % 100) / 100f;
            float v = ((i * 61) % 100) / 100f;
            float size = Mathf.Lerp(5f, 13f, ((i * 17) % 100) / 100f);
            Color color = i % 3 == 0 ? Cyan : (i % 3 == 1 ? Gold : Pink);
            color.a = 0.48f;

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            image.sprite = i % 4 == 0 ? particleDiamondSprite : particleStarSprite;
            image.enabled = image.sprite != null;
            image.color = color;
            image.raycastTarget = false;

            PremiumUiParticle particle = new PremiumUiParticle();
            particle.rect = rect;
            particle.image = image;
            particle.basePosition = new Vector2(Mathf.Lerp(-520f, 520f, u), Mathf.Lerp(-78f, -16f, v));
            particle.phase = i * 0.77f;
            particle.speed = Mathf.Lerp(1.1f, 2.8f, v);
            particle.amplitude = Mathf.Lerp(4f, 16f, u);
            particle.size = size;
            particle.color = color;
            premiumParticles.Add(particle);
        }

        for (int i = particleCount; i < 32; i++)
        {
            Transform extra = particleRoot.Find("Spark " + i.ToString("00"));
            if (extra != null)
            {
                extra.gameObject.SetActive(false);
            }
        }
    }

    private void UpdatePremiumParticles(float time, bool found)
    {
        for (int i = 0; i < premiumParticles.Count; i++)
        {
            PremiumUiParticle particle = premiumParticles[i];
            if (particle == null || particle.rect == null || particle.image == null)
            {
                continue;
            }

            float drift = Mathf.Sin(time * particle.speed + particle.phase);
            float lift = Mathf.Cos(time * (particle.speed * 0.72f) + particle.phase);
            float foundKick = found ? Mathf.Sin(time * 10f + particle.phase) * 10f : 0f;
            Vector2 pos = particle.basePosition + new Vector2(drift * particle.amplitude, lift * 5f + foundKick);
            particle.rect.anchoredPosition = pos;
            particle.rect.localRotation = Quaternion.Euler(0f, 0f, time * (found ? 160f : 42f) + particle.phase * 57f);

            float pulse = 0.45f + Mathf.Sin(time * 4f + particle.phase) * 0.28f;
            Color color = found ? Color.Lerp(particle.color, Color.white, 0.28f) : particle.color;
            color.a = Mathf.Clamp01(found ? 0.88f : pulse);
            particle.image.color = color;

            float scale = found ? 1.18f : 0.92f + Mathf.Sin(time * 3f + particle.phase) * 0.16f;
            particle.rect.sizeDelta = new Vector2(particle.size * scale, particle.size * scale);
        }
    }

    private void ResetPremiumBannerState()
    {
        if (matchFoundFlash != null)
        {
            matchFoundFlash.color = Color.clear;
            matchFoundFlash.raycastTarget = false;
            matchFoundFlash.gameObject.SetActive(false);
        }

        if (progressFill != null)
        {
            progressFill.fillAmount = 0f;
            progressFill.color = Pink;
        }

        if (timerText != null)
        {
            timerText.text = "TEMPO ESTIMADO: 10s";
        }

        if (badgeText != null)
        {
            badgeText.text = "CANCELAR BUSCA";
        }

        if (bannerGlow != null)
        {
            bannerGlow.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.065f);
        }

        UpdateQueueBars(0f, false);
        UpdatePremiumParticles(0f, false);
    }

    private void SanitizeMatchmakingOverlay()
    {
        if (matchmakingPanel == null)
        {
            return;
        }

        RectTransform panelRect = matchmakingPanel.transform as RectTransform;
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, 112f);
            panelRect.localScale = Vector3.one;
        }

        Graphic panelGraphic = matchmakingPanel.GetComponent<Graphic>();
        if (panelGraphic != null)
        {
            Color color = panelGraphic.color;
            color.a = 0f;
            panelGraphic.color = color;
            panelGraphic.raycastTarget = false;
        }

        RectTransform[] allRects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < allRects.Length; i++)
        {
            RectTransform rect = allRects[i];
            if (rect == null || !rect.gameObject.scene.IsValid())
            {
                continue;
            }

            if ((rect.name == "Matchmaking Panel" || rect.name == "MatchmakingPanel" || rect.name == "MatchmakingCanvas") &&
                rect.gameObject != matchmakingPanel && !rect.IsChildOf(matchmakingPanel.transform))
            {
                rect.gameObject.SetActive(false);
            }
        }

        Image[] images = matchmakingPanel.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            if (image.name == "Invisible Blocker")
            {
                image.color = Color.clear;
                image.raycastTarget = true;
            }
            else if (image.name == "MatchFound Flash")
            {
                image.color = Color.clear;
                image.raycastTarget = false;
                image.gameObject.SetActive(false);
            }
            else if ((image.name == "Scan Line" || image.name.StartsWith("Spark ")) && image.sprite == null)
            {
                image.enabled = false;
            }
        }
    }

    private void EnsureCleanStripRoot()
    {
        RectTransform cleanRoot = null;
        RectTransform[] allRects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < allRects.Length; i++)
        {
            RectTransform rect = allRects[i];
            if (rect != null && rect.name == CleanStripRootName && rect.gameObject.scene.IsValid())
            {
                cleanRoot = rect;
                break;
            }
        }

        if (cleanRoot == null)
        {
            Canvas mainMenuCanvas = null;
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.name == "Canvas - MainMenu")
                {
                    mainMenuCanvas = candidate;
                    break;
                }
            }

            Transform parent = mainMenuCanvas != null ? mainMenuCanvas.transform : null;
            GameObject rootObject;
            if (parent != null)
            {
                rootObject = new GameObject(CleanStripRootName, typeof(RectTransform));
                rootObject.transform.SetParent(parent, false);
            }
            else
            {
                rootObject = new GameObject(CleanStripRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas fallbackCanvas = rootObject.GetComponent<Canvas>();
                fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                fallbackCanvas.sortingOrder = 32000;
                CanvasScaler scaler = rootObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 2400f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            cleanRoot = rootObject.GetComponent<RectTransform>();
        }

        matchmakingPanel = cleanRoot.gameObject;
        matchmakingPanel.SetActive(true);
        cleanRoot.SetAsLastSibling();

        Transform existingBanner = cleanRoot.Find("Top Banner");
        if (existingBanner == null)
        {
            GameObject bannerObject = new GameObject("Top Banner", typeof(RectTransform));
            topBanner = bannerObject.GetComponent<RectTransform>();
            topBanner.SetParent(cleanRoot, false);
        }
        else
        {
            topBanner = existingBanner as RectTransform;
        }

        if (statusText != null && !statusText.transform.IsChildOf(cleanRoot)) statusText = null;
        if (matchFoundFlash != null && !matchFoundFlash.transform.IsChildOf(cleanRoot)) matchFoundFlash = null;
        if (progressFill != null && !progressFill.transform.IsChildOf(cleanRoot)) progressFill = null;
        if (bannerGlow != null && !bannerGlow.transform.IsChildOf(cleanRoot)) bannerGlow = null;
        if (scanLine != null && !scanLine.IsChildOf(cleanRoot)) scanLine = null;
        if (timerText != null && !timerText.transform.IsChildOf(cleanRoot)) timerText = null;
        if (badgeText != null && !badgeText.transform.IsChildOf(cleanRoot)) badgeText = null;
        if (cancelText != null && !cancelText.transform.IsChildOf(cleanRoot)) cancelText = null;

        particleRoot = null;
        premiumParticles.Clear();
        queueBars.Clear();
        RetireLegacyMatchmakingUi();
    }

    private void RetireLegacyMatchmakingUi()
    {
        RectTransform[] allRects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < allRects.Length; i++)
        {
            RectTransform rect = allRects[i];
            bool belongsToCleanStrip = rect != null && matchmakingPanel != null && rect.IsChildOf(matchmakingPanel.transform);
            if (rect == null || !rect.gameObject.scene.IsValid() || rect.name == CleanStripRootName || belongsToCleanStrip)
            {
                continue;
            }

            if (rect.name == "MatchmakingCanvas" || rect.name == "Matchmaking Panel" || rect.name == "MatchmakingPanel")
            {
                rect.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(rect.gameObject);
                }
            }
        }
    }

    private void HideLegacyNetworkLobbyUi()
    {
        RectTransform[] allRects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < allRects.Length; i++)
        {
            RectTransform rect = allRects[i];
            if (rect == null || !rect.gameObject.scene.IsValid() || rect.name != "NetLobbyView")
            {
                continue;
            }

            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (!hiddenLegacyGroups.Contains(group)) hiddenLegacyGroups.Add(group);
        }
    }

    private void RestoreLegacyNetworkLobbyUi()
    {
        for (int i = 0; i < hiddenLegacyGroups.Count; i++)
        {
            CanvasGroup group = hiddenLegacyGroups[i];
            if (group == null) continue;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        hiddenLegacyGroups.Clear();
    }

    private void StretchInside(RectTransform rect)
    {
        StretchInside(rect, Vector2.zero);
    }

    private void StretchInside(RectTransform rect, Vector2 padding)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = padding;
        rect.offsetMax = -padding;
        rect.anchoredPosition = Vector2.zero;
    }

    private void StretchHorizontally(RectTransform rect, float sidePadding)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(-sidePadding * 2f, rect.sizeDelta.y);
        rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
    }

    private void ConfigureLeftChild(RectTransform rect, float left, float y, float width, float height)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(left, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void ConfigureRightChild(RectTransform rect, float right, float y, float width, float height)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-right, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private Image FindOrCreateImage(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        Transform child = parent.Find(name);
        RectTransform rect;
        Image image;
        if (child == null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            image = obj.AddComponent<Image>();
        }
        else
        {
            rect = child.GetComponent<RectTransform>();
            image = child.GetComponent<Image>();
            if (image == null) image = child.gameObject.AddComponent<Image>();
        }

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI FindOrCreateText(Transform parent, string name, string value, float fontSize, Color color, Vector2 position, Vector2 size, TextAlignmentOptions alignment)
    {
        Transform child = parent.Find(name);
        RectTransform rect;
        TextMeshProUGUI text;
        if (child == null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            text = obj.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            rect = child.GetComponent<RectTransform>();
            text = child.GetComponent<TextMeshProUGUI>();
            if (text == null) text = child.gameObject.AddComponent<TextMeshProUGUI>();
        }

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.text = value;
        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(10f, fontSize - 12f);
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private IEnumerator HideLegacyUIRoutine()
    {
        while (isMatching)
        {
            TMP_Text[] allTexts = Object.FindObjectsOfType<TMP_Text>();
            UnityEngine.UI.Text[] oldTexts = Object.FindObjectsOfType<UnityEngine.UI.Text>();
            PremiumProfilePanel[] profilePanels = Object.FindObjectsOfType<PremiumProfilePanel>();

            List<Component> thingsToHide = new List<Component>();
            thingsToHide.AddRange(allTexts);
            thingsToHide.AddRange(oldTexts);
            thingsToHide.AddRange(profilePanels);

            foreach (var comp in thingsToHide)
            {
                if (comp == null || comp.gameObject == null || !comp.gameObject.activeInHierarchy) continue;
                if (matchmakingPanel != null && comp.transform.IsChildOf(matchmakingPanel.transform)) continue;

                bool shouldHide = false;
                if (comp is PremiumProfilePanel) shouldHide = true;
                else
                {
                    string textContent = "";
                    if (comp is TMP_Text tmp) textContent = tmp.text.ToLower();
                    else if (comp is UnityEngine.UI.Text txt) textContent = txt.text.ToLower();

                    if (textContent.Contains("procurando") || textContent.Contains("quick play") || textContent.Contains("aguardando") || textContent.Contains("cancelar") || textContent.Contains("searching"))
                        shouldHide = true;
                }

                if (shouldHide)
                {
                    Transform rootToHide = comp.transform;
                    while (rootToHide.parent != null && rootToHide.parent.GetComponent<Canvas>() == null)
                        rootToHide = rootToHide.parent;

                    CanvasGroup cg = rootToHide.GetComponent<CanvasGroup>();
                    if (cg == null) cg = rootToHide.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                    cg.interactable = false;

                    RectTransform rt = rootToHide.GetComponent<RectTransform>();
                    if (rt != null && rt.anchoredPosition.x < 4000f)
                        rt.anchoredPosition = new Vector2(5000f, 5000f);
                }
            }
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    private void HookCityButtons()
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            if (btn == null || hookedButtons.Contains(btn) || !btn.gameObject.activeInHierarchy || !btn.gameObject.scene.IsValid())
                continue;

            if (btn.name == "QuickPlay - Btn")
            {
                HookQuickPlayButton(btn);
                continue;
            }

            if (btn.name.StartsWith("City Choice - ") || btn.name == "City Back")
            {
                hookedButtons.Add(btn);
                continue;
            }

            bool isCity = false;
            Transform current = btn.transform;
            while (current != null)
            {
                string n = current.name.ToLower();
                if (n.Contains("lobbyitem") || n.Contains("room") || n.Contains("city"))
                {
                    isCity = true;
                    break;
                }
                current = current.parent;
            }

            if (!isCity)
            {
                TMP_Text[] texts = btn.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in texts)
                {
                    string txt = t.text.ToLower();
                    if (txt.Contains("metrópole") || txt.Contains("metropole") || txt.Contains("entrar") || txt.Contains("jogar"))
                    {
                        if (!txt.Contains("online"))
                        {
                            isCity = true;
                            break;
                        }
                    }
                }
            }

            if (isCity)
            {
                btn.onClick.AddListener(StartMatchmaking);
                hookedButtons.Add(btn);
            }
        }
    }

    private void HookQuickPlayButton(Button button)
    {
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OpenCitySelection);
        hookedButtons.Add(button);
    }

    private void OpenCitySelection()
    {
        Canvas mainMenuCanvas = FindMainMenuCanvas();
        CitySelectionOverlay.Show(mainMenuCanvas, OnCitySelected);
    }

    private void OnCitySelected(string cityId)
    {
        StartMatchmaking();

        NetworkLobbyController controller = Object.FindObjectOfType<NetworkLobbyController>();
        if (controller == null)
        {
            return;
        }

        // For now matchmaking is simulated locally: show the search for ten seconds,
        // then enter directly against a bot without starting/cancelling online work.
        fastFallbackRoutine = StartCoroutine(StartBotMatchAfterSearch(controller));
    }

    private IEnumerator StartBotMatchAfterSearch(NetworkLobbyController controller)
    {
        yield return new WaitForSecondsRealtime(BotMatchSearchDuration);

        if (!isMatching || matchFoundTriggered || controller == null)
        {
            fastFallbackRoutine = null;
            yield break;
        }

        if (DependencyCache.DominoController != null && DependencyCache.DominoController.IsInMatch)
        {
            fastFallbackRoutine = null;
            yield break;
        }

        if (statusText != null)
        {
            statusText.text = "PREPARANDO MESA";
            statusText.color = Cyan;
        }
        if (timerText != null)
        {
            timerText.text = "ENTRANDO...";
        }

        controller.StartBotFallbackMatch();
        fastFallbackRoutine = null;
    }

    private static Canvas FindMainMenuCanvas()
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.scene.IsValid() && canvas.name == "Canvas - MainMenu")
            {
                return canvas;
            }
        }

        return null;
    }

    private float GetSafeTopOffset()
    {
        float unsafeTopPixels = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);
        Canvas canvas = topBanner != null ? topBanner.GetComponentInParent<Canvas>() : null;
        float scaleFactor = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        return Mathf.Clamp(unsafeTopPixels / scaleFactor + 12f, 34f, 118f);
    }

    private static void InvokePersistentButtonCalls(List<PersistentButtonCall> calls)
    {
        for (int i = 0; i < calls.Count; i++)
        {
            PersistentButtonCall call = calls[i];
            if (call == null || call.target == null)
            {
                continue;
            }

            MethodInfo method = call.target.GetType().GetMethod(call.methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
            if (method != null)
            {
                method.Invoke(call.target, null);
            }
        }
    }
}
