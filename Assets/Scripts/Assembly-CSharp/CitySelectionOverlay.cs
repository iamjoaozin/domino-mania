using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class CitySelectionOverlay : MonoBehaviour
{
    private const string RootName = "Premium City Selection";
    private const float CardWidth = 1320f;
    private const float CardHeight = 540f;
    private const float FirstCardY = -760f;
    private const float CardStep = 620f;

    private static CitySelectionOverlay instance;

    private readonly List<CityCard> cityCards = new List<CityCard>(3);
    private RectTransform root;
    private CanvasGroup canvasGroup;
    private RectTransform headerRect;
    private CanvasGroup headerGroup;
    private Vector2 headerHome;
    private RectTransform backRect;
    private CanvasGroup backGroup;
    private Vector2 backHome;
    private Action<string> onSelected;
    private bool transitioning;

    private sealed class CityCard
    {
        public string Id;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Image Image;
        public Button Button;
        public Vector2 Home;
    }

    public static void Show(Canvas parentCanvas, Action<string> callback)
    {
        if (parentCanvas == null)
        {
            callback?.Invoke("metropole");
            return;
        }

        DisableLegacySelection(parentCanvas);

        if (instance == null)
        {
            Transform existing = parentCanvas.transform.Find(RootName);
            if (existing != null)
            {
                instance = existing.GetComponent<CitySelectionOverlay>();
            }

            if (instance == null)
            {
                GameObject host = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup), typeof(CitySelectionOverlay));
                host.transform.SetParent(parentCanvas.transform, false);
                instance = host.GetComponent<CitySelectionOverlay>();
                instance.Build();
            }
        }

        instance.onSelected = callback;
        instance.root.SetAsLastSibling();
        instance.root.gameObject.SetActive(true);
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.OpenRoutine());
    }

    private static void DisableLegacySelection(Canvas parentCanvas)
    {
        Transform legacy = parentCanvas.transform.Find("Root/City Selection Screen");
        if (legacy != null)
        {
            legacy.gameObject.SetActive(false);
        }
    }

    private void Build()
    {
        root = transform as RectTransform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Sprite background = Resources.Load<Sprite>("cities/city_select_background");
        Sprite header = Resources.Load<Sprite>("cities/city_select_header");
        Sprite praia = Resources.Load<Sprite>("cities/city_card_praia_clean");
        Sprite metropole = Resources.Load<Sprite>("cities/city_card_metropole_clean");
        Sprite imperio = Resources.Load<Sprite>("cities/city_card_imperio_clean");
        Sprite back = Resources.Load<Sprite>("cities/city_back_button");

        Image backdrop = CreateImage(root, "City Background", background, Color.white, Vector2.zero, Vector2.zero);
        Stretch(backdrop.rectTransform);

        Image headerImage = CreateImage(root, "City Header", header, Color.white, new Vector2(0f, -285f), new Vector2(1420f, 520f));
        AnchorTop(headerImage.rectTransform);
        headerImage.preserveAspect = true;
        headerRect = headerImage.rectTransform;
        headerHome = headerRect.anchoredPosition;
        headerGroup = headerImage.gameObject.AddComponent<CanvasGroup>();

        CreateCityButton("praia", praia, 0);
        CreateCityButton("metropole", metropole, 1);
        CreateCityButton("imperio", imperio, 2);

        GameObject backObject = new GameObject("City Back", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
        backObject.transform.SetParent(root, false);
        backRect = backObject.GetComponent<RectTransform>();
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.zero;
        backRect.pivot = Vector2.zero;
        backRect.anchoredPosition = new Vector2(42f, 44f);
        backRect.sizeDelta = new Vector2(320f, 132f);
        backHome = backRect.anchoredPosition;
        backGroup = backObject.GetComponent<CanvasGroup>();
        Image backImage = backObject.GetComponent<Image>();
        backImage.sprite = back;
        backImage.color = Color.white;
        backImage.preserveAspect = true;
        backObject.GetComponent<Button>().onClick.AddListener(Hide);
    }

    private void CreateCityButton(string cityId, Sprite sprite, int index)
    {
        GameObject buttonObject = new GameObject("City Choice - " + cityId, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        AnchorTop(rect);
        rect.anchoredPosition = new Vector2(0f, FirstCardY - (CardStep * index));
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = false;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.82f, 1f);
        colors.pressedColor = new Color(1f, 0.82f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        CityCard card = new CityCard
        {
            Id = cityId,
            Rect = rect,
            Group = buttonObject.GetComponent<CanvasGroup>(),
            Image = image,
            Button = button,
            Home = rect.anchoredPosition
        };
        cityCards.Add(card);
        button.onClick.AddListener(delegate { SelectCity(card); });
    }

    private void SelectCity(CityCard selectedCard)
    {
        if (transitioning || selectedCard == null)
        {
            return;
        }

        StartCoroutine(SelectRoutine(selectedCard));
    }

    private IEnumerator SelectRoutine(CityCard selectedCard)
    {
        transitioning = true;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        string normalizedCity = string.IsNullOrEmpty(selectedCard.Id) ? "praia" : selectedCard.Id.ToLowerInvariant();
        string boardResource = "cities/city_table_" + normalizedCity + "_clean";

        PlayerPrefs.SetString("SelectedCity", normalizedCity);
        PlayerPrefs.SetString("selected_city", normalizedCity);
        PlayerPrefs.SetString("selected_online_city", normalizedCity);
        PlayerPrefs.SetString("selected_city_name", normalizedCity);
        PlayerPrefs.SetString("selected_city_board_resource", boardResource);
        PlayerPrefs.Save();

        float elapsed = 0f;
        const float duration = 0.24f;
        Color selectedTint = new Color(1f, 0.88f, 0.55f, 1f);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI);

            for (int i = 0; i < cityCards.Count; i++)
            {
                CityCard card = cityCards[i];
                if (card == selectedCard)
                {
                    card.Rect.localScale = Vector3.one * (1f + (0.045f * pulse));
                    card.Image.color = Color.Lerp(Color.white, selectedTint, pulse);
                }
                else
                {
                    card.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.97f, t);
                    card.Group.alpha = Mathf.Lerp(1f, 0.42f, t);
                }
            }

            yield return null;
        }

        selectedCard.Rect.localScale = Vector3.one;
        selectedCard.Image.color = Color.white;
        Action<string> callback = onSelected;
        onSelected = null;
        yield return StartCoroutine(CloseRoutine(delegate { callback?.Invoke(normalizedCity); }));
    }

    private void Hide()
    {
        if (transitioning || root == null || !root.gameObject.activeSelf)
        {
            return;
        }

        StartCoroutine(CloseRoutine(null));
    }

    private IEnumerator OpenRoutine()
    {
        transitioning = true;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        root.localScale = Vector3.one;

        headerRect.anchoredPosition = headerHome + new Vector2(0f, 95f);
        headerGroup.alpha = 0f;
        backRect.anchoredPosition = backHome + new Vector2(0f, -35f);
        backGroup.alpha = 0f;

        for (int i = 0; i < cityCards.Count; i++)
        {
            CityCard card = cityCards[i];
            float side = (i % 2 == 0) ? -1f : 1f;
            card.Rect.anchoredPosition = card.Home + new Vector2(155f * side, 35f);
            card.Rect.localScale = new Vector3(0.94f, 0.94f, 1f);
            card.Group.alpha = 0f;
            card.Image.color = Color.white;
        }

        float elapsed = 0f;
        const float totalDuration = 0.58f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float rootT = Mathf.Clamp01(elapsed / 0.22f);
            canvasGroup.alpha = EaseOutCubic(rootT);

            float headerT = EaseOutCubic(Mathf.Clamp01(elapsed / 0.32f));
            headerRect.anchoredPosition = Vector2.Lerp(headerHome + new Vector2(0f, 95f), headerHome, headerT);
            headerGroup.alpha = headerT;

            for (int i = 0; i < cityCards.Count; i++)
            {
                CityCard card = cityCards[i];
                float delay = 0.08f + (i * 0.075f);
                float t = Mathf.Clamp01((elapsed - delay) / 0.34f);
                float moveT = EaseOutCubic(t);
                float scaleT = EaseOutBack(t);
                float side = (i % 2 == 0) ? -1f : 1f;
                Vector2 start = card.Home + new Vector2(155f * side, 35f);
                card.Rect.anchoredPosition = Vector2.Lerp(start, card.Home, moveT);
                card.Rect.localScale = Vector3.LerpUnclamped(new Vector3(0.94f, 0.94f, 1f), Vector3.one, scaleT);
                card.Group.alpha = moveT;
            }

            float backT = EaseOutCubic(Mathf.Clamp01((elapsed - 0.30f) / 0.22f));
            backRect.anchoredPosition = Vector2.Lerp(backHome + new Vector2(0f, -35f), backHome, backT);
            backGroup.alpha = backT;
            yield return null;
        }

        canvasGroup.alpha = 1f;
        headerRect.anchoredPosition = headerHome;
        headerGroup.alpha = 1f;
        backRect.anchoredPosition = backHome;
        backGroup.alpha = 1f;
        for (int i = 0; i < cityCards.Count; i++)
        {
            cityCards[i].Rect.anchoredPosition = cityCards[i].Home;
            cityCards[i].Rect.localScale = Vector3.one;
            cityCards[i].Group.alpha = 1f;
        }

        transitioning = false;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private IEnumerator CloseRoutine(Action afterClose)
    {
        transitioning = true;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        const float duration = 0.28f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            canvasGroup.alpha = 1f - eased;
            headerRect.anchoredPosition = Vector2.Lerp(headerHome, headerHome + new Vector2(0f, 70f), eased);
            headerGroup.alpha = 1f - eased;
            backRect.anchoredPosition = Vector2.Lerp(backHome, backHome + new Vector2(0f, -28f), eased);
            backGroup.alpha = 1f - eased;

            for (int i = 0; i < cityCards.Count; i++)
            {
                CityCard card = cityCards[i];
                float side = (i % 2 == 0) ? -1f : 1f;
                card.Rect.anchoredPosition = Vector2.Lerp(card.Home, card.Home + new Vector2(130f * side, 22f), eased);
                card.Rect.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.96f, 0.96f, 1f), eased);
                card.Group.alpha = Mathf.Min(card.Group.alpha, 1f - eased);
            }

            yield return null;
        }

        root.gameObject.SetActive(false);
        transitioning = false;
        afterClose?.Invoke();
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + (c3 * p * p * p) + (c1 * p * p);
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void AnchorTop(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
