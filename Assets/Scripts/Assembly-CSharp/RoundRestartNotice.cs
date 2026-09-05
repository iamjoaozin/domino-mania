using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(35500)]
public sealed class RoundRestartNotice : MonoBehaviour
{
    private const float DuplicateWindow = 1.2f;
    private const float LifeSeconds = 2.25f;
    private static RoundRestartNotice instance;
    private static float lastShowTime = -999f;

    private CanvasGroup group;
    private RectTransform root;
    private RectTransform card;
    private Image glow;
    private Image cardBack;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private Coroutine routine;

    private static readonly Color Cyan = new Color(0.22f, 0.9f, 1f, 1f);
    private static readonly Color Gold = new Color(1f, 0.76f, 0.14f, 1f);

    public static void ShowDrawRestart(string reason)
    {
        if (Time.unscaledTime - lastShowTime < DuplicateWindow)
        {
            return;
        }

        lastShowTime = Time.unscaledTime;
        EnsureInstance().ShowInternal(reason);
    }

    public static void RemoveLegacyResultPopups()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null || !obj.scene.IsValid() || !obj.scene.isLoaded)
            {
                continue;
            }

            if (obj.name == "RoundWinnerPopup" || obj.name == "PremiumRoundResultBanner")
            {
                Destroy(obj);
            }
        }
    }

    private static RoundRestartNotice EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        RoundRestartNotice existing = FindObjectOfType<RoundRestartNotice>();
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject obj = new GameObject("Round Restart Notice");
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<RoundRestartNotice>();
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

    private void ShowInternal(string reason)
    {
        Build();
        RemoveLegacyResultPopups();
        bodyText.text = string.IsNullOrWhiteSpace(reason) ? "Rodada reiniciada sem alterar moedas" : reason;

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(PlayRoutine());
    }

    private void Build()
    {
        if (root != null)
        {
            return;
        }

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 34500;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        root = CreateRect(transform, "Draw Notice Root");
        Stretch(root);
        group = root.gameObject.AddComponent<CanvasGroup>();

        card = CreateRect(root, "Draw Restart Card");
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(760f, 154f);

        glow = CreateImage(card, "Glow", new Color(Cyan.r, Cyan.g, Cyan.b, 0.16f), Vector2.zero, new Vector2(820f, 198f));
        glow.sprite = CreateRoundedSprite(128, 64, 24f, Color.white);
        glow.type = Image.Type.Sliced;

        cardBack = CreateImage(card, "Card Back", new Color(0.02f, 0.01f, 0.05f, 0.94f), Vector2.zero, new Vector2(760f, 154f));
        cardBack.sprite = CreateRoundedSprite(128, 64, 22f, Color.white);
        cardBack.type = Image.Type.Sliced;

        Image topLine = CreateImage(card, "Top Line", Gold, new Vector2(0f, 66f), new Vector2(670f, 5f));
        Image bottomLine = CreateImage(card, "Bottom Line", Cyan, new Vector2(0f, -66f), new Vector2(670f, 3f));
        topLine.raycastTarget = false;
        bottomLine.raycastTarget = false;

        titleText = CreateText(card, "Title", "EMPATE", 42, Gold, new Vector2(0f, 24f), new Vector2(680f, 54f));
        bodyText = CreateText(card, "Body", "Rodada reiniciada sem alterar moedas", 24, new Color(0.86f, 0.92f, 1f, 1f), new Vector2(0f, -28f), new Vector2(690f, 42f));
    }

    private IEnumerator PlayRoutine()
    {
        float startY = 610f;
        float targetY = 500f;
        group.alpha = 0f;
        card.anchoredPosition = new Vector2(0f, startY);
        card.localScale = Vector3.one * 0.92f;

        float elapsed = 0f;
        while (elapsed < LifeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / LifeSeconds);
            float inT = EaseOutCubic(Mathf.Clamp01(t / 0.2f));
            float outT = EaseInCubic(Mathf.Clamp01((t - 0.78f) / 0.22f));

            group.alpha = Mathf.Lerp(1f, 0f, outT) * inT;
            card.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, targetY, inT) + Mathf.Lerp(0f, -32f, outT));
            card.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, inT);
            yield return null;
        }

        HideInstant();
    }

    private void HideInstant()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private static Image CreateImage(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(parent, name);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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
        tmp.fontSizeMin = Mathf.Max(12, size - 10);
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

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }
}
