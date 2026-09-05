using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(36000)]
public sealed class CoinFlowFeedback : MonoBehaviour
{
    private const float LifeSeconds = 1.65f;
    private static CoinFlowFeedback instance;

    private readonly List<GameObject> flyingCoins = new List<GameObject>(24);
    private readonly List<CoinParticle> coinParticles = new List<CoinParticle>(24);

    private Canvas canvas;
    private CanvasGroup labelGroup;
    private RectTransform root;
    private RectTransform labelRoot;
    private Image labelBack;
    private TextMeshProUGUI headingText;
    private TextMeshProUGUI amountText;
    private Sprite coinSprite;
    private Coroutine routine;
    private RectTransform hudPulseTarget;
    private Vector3 hudPulseBaseScale;

    private static readonly Color Gold = new Color(1f, 0.72f, 0.11f, 1f);
    private static readonly Color Cyan = new Color(0.2f, 0.9f, 1f, 1f);
    private static readonly Color Red = new Color(1f, 0.18f, 0.22f, 1f);
    private static readonly Color Deep = new Color(0.02f, 0.01f, 0.05f, 0.92f);

    public static void ShowPassCost(int amount)
    {
        if (amount <= 0) return;
        EnsureInstance().Play("PASSOU A VEZ", -amount, FlowMode.PassCost);
    }

    public static void ShowMatchGain(int amount)
    {
        if (amount <= 0) return;
        EnsureInstance().Play("MOEDAS RECEBIDAS", amount, FlowMode.MatchGain);
    }

    public static void ShowMatchLoss(int amount)
    {
        if (amount <= 0) return;
        EnsureInstance().Play("VOCE PERDEU", -amount, FlowMode.MatchLoss);
    }

    private enum FlowMode
    {
        PassCost,
        MatchGain,
        MatchLoss
    }

    private static CoinFlowFeedback EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        CoinFlowFeedback existing = FindObjectOfType<CoinFlowFeedback>();
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject obj = new GameObject("Coin Flow Feedback");
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<CoinFlowFeedback>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
        HideInstant();
    }

    private void Play(string heading, int signedAmount, FlowMode mode)
    {
        Build();
        ClearFlyingCoins();

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        Color accent = signedAmount >= 0 ? Gold : Red;
        headingText.text = heading;
        headingText.color = signedAmount >= 0 ? Cyan : new Color(1f, 0.78f, 0.78f, 1f);
        amountText.text = FormatSignedAmount(signedAmount) + " MOEDAS";
        amountText.color = accent;
        labelBack.color = new Color(Deep.r, Deep.g, Deep.b, Deep.a);

        Vector2 from;
        Vector2 to;
        Vector2 labelPosition;
        Vector2 coinHudPoint;
        RectTransform coinHudRect;
        if (!TryResolveCoinHudPoint(out coinHudPoint, out coinHudRect))
        {
            coinHudPoint = new Vector2(-390f, 780f);
        }

        hudPulseTarget = coinHudRect;
        hudPulseBaseScale = hudPulseTarget != null ? hudPulseTarget.localScale : Vector3.one;

        Vector2 opponentPoint;
        if (!TryResolveNamedPoint("Opponent Avatar", out opponentPoint))
        {
            opponentPoint = new Vector2(392f, 700f);
        }

        Vector2 playerPoint;
        if (!TryResolveNamedPoint("Player Avatar", out playerPoint))
        {
            playerPoint = new Vector2(0f, -610f);
        }

        if (mode == FlowMode.MatchGain)
        {
            from = opponentPoint;
            to = coinHudPoint;
            labelPosition = new Vector2(0f, 405f);
        }
        else if (mode == FlowMode.PassCost)
        {
            from = coinHudPoint;
            to = playerPoint + new Vector2(0f, -230f);
            labelPosition = new Vector2(0f, -425f);
        }
        else
        {
            from = coinHudPoint;
            to = opponentPoint;
            labelPosition = new Vector2(0f, 405f);
        }

        labelRoot.anchoredPosition = labelPosition;
        routine = StartCoroutine(PlayRoutine(from, to, accent, mode));
    }

    private void Build()
    {
        if (root != null)
        {
            return;
        }

        coinSprite = CreateCoinSprite();

        canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35000;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        root = CreateRect(transform, "Coin Flow Root");
        Stretch(root);

        labelRoot = CreateRect(root, "Coin Flow Label");
        labelRoot.anchorMin = new Vector2(0.5f, 0.5f);
        labelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        labelRoot.pivot = new Vector2(0.5f, 0.5f);
        labelRoot.sizeDelta = new Vector2(560f, 118f);
        labelGroup = labelRoot.gameObject.AddComponent<CanvasGroup>();

        labelBack = labelRoot.gameObject.AddComponent<Image>();
        labelBack.sprite = CreateRoundedSprite(96, 96, 20f, Color.white);
        labelBack.type = Image.Type.Sliced;
        labelBack.raycastTarget = false;

        headingText = CreateText(labelRoot, "Heading", "MOEDAS RECEBIDAS", 22, Cyan, new Vector2(0f, 28f), new Vector2(500f, 34f));
        amountText = CreateText(labelRoot, "Amount", "+1.000 MOEDAS", 46, Gold, new Vector2(0f, -24f), new Vector2(520f, 62f));
    }

    private IEnumerator PlayRoutine(Vector2 from, Vector2 to, Color accent, FlowMode mode)
    {
        labelRoot.localScale = Vector3.one * 0.82f;
        labelGroup.alpha = 0f;
        labelGroup.blocksRaycasts = false;
        labelGroup.interactable = false;

        int coinCount = mode == FlowMode.PassCost ? 8 : 16;
        for (int i = 0; i < coinCount; i++)
        {
            CreateFlyingCoin(i, coinCount, from, accent);
        }

        float elapsed = 0f;
        while (elapsed < LifeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / LifeSeconds);
            float labelIn = Mathf.Clamp01(t / 0.18f);
            float labelOut = Mathf.Clamp01((t - 0.78f) / 0.22f);

            labelGroup.alpha = Mathf.Lerp(1f, 0f, labelOut) * EaseOutCubic(labelIn);
            labelRoot.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.03f, EaseOutBack(labelIn));

            AnimateCoins(from, to, t);
            PulseCoinHud(t, mode);
            yield return null;
        }

        RestoreCoinHudScale();
        HideInstant();
        ClearFlyingCoins();
    }

    private void CreateFlyingCoin(int index, int total, Vector2 from, Color accent)
    {
        RectTransform rect = CreateRect(root, "Coin " + index.ToString("00"));
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = from + Random.insideUnitCircle * 34f;
        float size = Random.Range(24f, 42f);
        rect.sizeDelta = new Vector2(size, size);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = coinSprite;
        image.color = Color.Lerp(Gold, accent, 0.25f);
        image.raycastTarget = false;

        CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        flyingCoins.Add(rect.gameObject);
        coinParticles.Add(new CoinParticle
        {
            Rect = rect,
            Group = group,
            Index = index,
            Total = Mathf.Max(1, total),
            Offset = new Vector2(Random.Range(-110f, 110f), Random.Range(-88f, 130f)),
            Delay = Random.Range(0f, 0.2f),
            Spin = Random.Range(-320f, 320f)
        });
    }

    private void AnimateCoins(Vector2 from, Vector2 to, float globalT)
    {
        for (int i = 0; i < coinParticles.Count; i++)
        {
            CoinParticle motion = coinParticles[i];
            if (motion == null || motion.Rect == null) continue;

            float localT = Mathf.Clamp01((globalT - motion.Delay) / 0.72f);
            float eased = EaseInOutCubic(localT);
            Vector2 arc = Vector2.Lerp(from, to, eased);
            float height = Mathf.Sin(localT * Mathf.PI) * (180f + motion.Index * 5f);
            motion.Rect.anchoredPosition = arc + motion.Offset * Mathf.Sin(localT * Mathf.PI) + new Vector2(0f, height);
            motion.Rect.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.1f, Mathf.Sin(localT * Mathf.PI));
            motion.Rect.localRotation = Quaternion.Euler(0f, 0f, motion.Spin * globalT);

            if (motion.Group != null)
            {
                float fadeIn = Mathf.Clamp01(localT / 0.18f);
                float fadeOut = Mathf.Clamp01((localT - 0.78f) / 0.22f);
                motion.Group.alpha = Mathf.Lerp(1f, 0f, fadeOut) * fadeIn;
            }
        }
    }

    private void PulseCoinHud(float t, FlowMode mode)
    {
        if (hudPulseTarget == null)
        {
            return;
        }

        float start = mode == FlowMode.MatchGain ? 0.52f : 0.04f;
        float pulseT = Mathf.Clamp01((t - start) / 0.34f);
        float pulse = Mathf.Sin(pulseT * Mathf.PI);
        hudPulseTarget.localScale = hudPulseBaseScale * (1f + pulse * 0.16f);
    }

    private void RestoreCoinHudScale()
    {
        if (hudPulseTarget != null)
        {
            hudPulseTarget.localScale = hudPulseBaseScale;
        }
    }

    private void HideInstant()
    {
        if (labelGroup != null)
        {
            labelGroup.alpha = 0f;
            labelGroup.blocksRaycasts = false;
            labelGroup.interactable = false;
        }
    }

    private void ClearFlyingCoins()
    {
        for (int i = 0; i < flyingCoins.Count; i++)
        {
            if (flyingCoins[i] != null)
            {
                Destroy(flyingCoins[i]);
            }
        }

        flyingCoins.Clear();
        coinParticles.Clear();
        RestoreCoinHudScale();
        hudPulseTarget = null;
    }

    private bool TryResolveCoinHudPoint(out Vector2 point, out RectTransform rect)
    {
        point = Vector2.zero;
        rect = null;

        CoinHudUpdater[] huds = Resources.FindObjectsOfTypeAll<CoinHudUpdater>();
        for (int i = 0; i < huds.Length; i++)
        {
            CoinHudUpdater hud = huds[i];
            if (hud == null || !IsUsableSceneObject(hud.gameObject))
            {
                continue;
            }

            TMP_Text text = hud.GetComponentInChildren<TMP_Text>(true);
            if (TryResolveRectPoint(text != null ? text.rectTransform : hud.transform as RectTransform, out point))
            {
                rect = text != null ? text.rectTransform : hud.transform as RectTransform;
                return true;
            }
        }

        StoreManager[] stores = Resources.FindObjectsOfTypeAll<StoreManager>();
        for (int i = 0; i < stores.Length; i++)
        {
            StoreManager store = stores[i];
            if (store == null || store.coinsTexts == null)
            {
                continue;
            }

            for (int j = 0; j < store.coinsTexts.Length; j++)
            {
                TMP_Text text = store.coinsTexts[j];
                if (text == null || !IsUsableSceneObject(text.gameObject))
                {
                    continue;
                }

                if (TryResolveRectPoint(text.rectTransform, out point))
                {
                    rect = text.rectTransform;
                    return true;
                }
            }
        }

        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || !IsUsableSceneObject(text.gameObject) || text.transform.IsChildOf(root))
            {
                continue;
            }

            if (!LooksLikeCoinHudText(text))
            {
                continue;
            }

            if (TryResolveRectPoint(text.rectTransform, out point))
            {
                rect = text.rectTransform;
                return true;
            }
        }

        return false;
    }

    private bool TryResolveNamedPoint(string objectName, out Vector2 point)
    {
        point = Vector2.zero;
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null || obj.name != objectName || !IsUsableSceneObject(obj))
            {
                continue;
            }

            RectTransform rect = obj.transform as RectTransform;
            if (TryResolveRectPoint(rect, out point))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveRectPoint(RectTransform target, out Vector2 point)
    {
        point = Vector2.zero;
        if (target == null || root == null)
        {
            return false;
        }

        Canvas sourceCanvas = target.GetComponentInParent<Canvas>();
        Camera sourceCamera = null;
        if (sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            sourceCamera = sourceCanvas.worldCamera != null ? sourceCanvas.worldCamera : Camera.main;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, target.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out point);
    }

    private static bool LooksLikeCoinHudText(TMP_Text text)
    {
        string context = GetHierarchyText(text.transform).ToUpperInvariant();
        if (!context.Contains("COIN") && !context.Contains("MOEDA") && !context.Contains("SALDO") && !context.Contains("CASH"))
        {
            return false;
        }

        string value = text.text == null ? string.Empty : text.text.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsDigit(c))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetHierarchyText(Transform transform)
    {
        string value = string.Empty;
        int guard = 0;
        while (transform != null && guard < 8)
        {
            value += " " + transform.name;
            transform = transform.parent;
            guard++;
        }

        return value;
    }

    private static bool IsUsableSceneObject(GameObject obj)
    {
        return obj != null && obj.scene.IsValid() && obj.scene.isLoaded && obj.activeInHierarchy;
    }

    private static string FormatSignedAmount(int amount)
    {
        string sign = amount >= 0 ? "+" : "-";
        int abs = Mathf.Abs(amount);
        return sign + abs.ToString("N0").Replace(",", ".");
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int size, Color color, Vector2 position, Vector2 dimensions)
    {
        RectTransform rect = CreateRect(parent, name);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = Mathf.Max(12, size - 12);
        tmp.fontSizeMax = size;
        return tmp;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Sprite CreateCoinSprite()
    {
        int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        float inner = size * 0.32f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist > radius)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float shade = Mathf.InverseLerp(radius, 0f, dist);
                Color color = Color.Lerp(new Color(0.95f, 0.45f, 0.02f, 1f), new Color(1f, 0.95f, 0.3f, 1f), shade);
                if (dist > inner && dist < inner + 4f)
                {
                    color = new Color(1f, 0.82f, 0.12f, 1f);
                }
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateRoundedSprite(int width, int height, float radius, Color fill)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Max(radius - x, 0f) + Mathf.Max(x - (width - radius), 0f);
                float dy = Mathf.Max(radius - y, 0f) + Mathf.Max(y - (height - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius + 1f - dist);
                texture.SetPixel(x, y, new Color(fill.r, fill.g, fill.b, fill.a * alpha));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private sealed class CoinParticle
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public int Index;
        public int Total;
        public Vector2 Offset;
        public float Delay;
        public float Spin;
    }
}
