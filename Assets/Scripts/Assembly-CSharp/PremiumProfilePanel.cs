using System;
using GBTemplates.Domino.Model;
using GBTemplates.Domino.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PremiumProfilePanel : MonoBehaviour
{
	private const string DefaultAvatarResource = "Profile/profile_default_avatar";

	private const string PlayerNameKey = "player_name";

	private const string PlayerTitleKey = "player_title";

	private const string PlayerBioKey = "player_bio";

	private const string AvatarPathKey = "avatar_path";

	private const string ProfileNeonColorKey = "profile_neon_color";

	private static readonly Color Gold = new Color(1f, 0.75f, 0.13f, 1f);

	private static readonly Color SoftGold = new Color(1f, 0.89f, 0.42f, 1f);

	private static readonly Color Purple = new Color(0.95f, 0.08f, 1f, 1f);

	private static readonly Color DeepPurple = new Color(0.035f, 0.006f, 0.07f, 0.96f);

	[Header("PLAYER")]
	public string playerName = "JOAO VICTOR";

	public string playerRank = "Nivel";

	[Range(0f, 1f)]
	public float xpProgress;

	[Header("TITLE")]
	public string playerTitle = "Jogador do Ano";

	[Header("BIO")]
	public string playerBio = "Dominando as mesas.";

	[Header("FLAG")]
	public Sprite countryFlag;

	[Header("NEON")]
	public Color neonColor = new Color(0.95f, 0.08f, 1f, 1f);

	[Header("AVATAR")]
	public Sprite avatarSprite;

	[Header("PROFILE MENU")]
	public ProfileCustomizationMenu customizationMenu;

	public TextMeshProUGUI playerNameText;

	public TextMeshProUGUI playerTitleText;

	public TextMeshProUGUI playerBioText;

	public Image avatarImage;

	private RectTransform mainPanel;

	private RectTransform xpFillRect;

	private Image panelGlow;

	private Image xpFillImage;

	private Outline panelOutline;

	private Outline avatarOutline;

	private Outline xpGlow;

	private TextMeshProUGUI levelText;

	private TextMeshProUGUI levelBadgeText;

	private TextMeshProUGUI streakText;

	private TextMeshProUGUI percentText;

	private float nextRefreshAt;

	private void Start()
	{
		LoadProfileFromPrefs();
		ProfileProgressionService.EnsureDefaults();
		CreatePremiumPanel();
		LoadSavedAvatar();
		RefreshProfileVisuals();
	}

	private void Update()
	{
		AnimatePanel();
		if (Time.unscaledTime >= nextRefreshAt)
		{
			nextRefreshAt = Time.unscaledTime + 0.5f;
			RefreshProgressVisuals();
		}
	}

	private void LoadProfileFromPrefs()
	{
		playerName = PlayerPrefs.GetString("player_name", playerName);
		playerTitle = PlayerPrefs.GetString("player_title", playerTitle);
		playerBio = PlayerPrefs.GetString("player_bio", playerBio);
		if (PlayerPrefs.HasKey("profile_neon_color") && ColorUtility.TryParseHtmlString("#" + PlayerPrefs.GetString("profile_neon_color"), out var color))
		{
			neonColor = color;
		}
	}

	private void CreatePremiumPanel()
	{
		Canvas canvas = GetComponentInParent<Canvas>();
		if (canvas == null)
		{
			canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
		}
		if (canvas == null)
		{
			UnityEngine.Debug.LogError("Canvas nao encontrado.");
			return;
		}
		Transform transform = canvas.transform.Find("PREMIUM_PLAYER_PANEL");
		if (transform != null)
		{
			UnityEngine.Object.Destroy(transform.gameObject);
		}
		GameObject gameObject = new GameObject("PREMIUM_PLAYER_PANEL", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		gameObject.transform.SetParent(canvas.transform, worldPositionStays: false);
		gameObject.transform.SetAsLastSibling();
		mainPanel = gameObject.GetComponent<RectTransform>();
		mainPanel.anchorMin = new Vector2(0f, 1f);
		mainPanel.anchorMax = new Vector2(0f, 1f);
		mainPanel.pivot = new Vector2(0f, 1f);
		mainPanel.sizeDelta = new Vector2(690f, 230f);
		mainPanel.anchoredPosition = new Vector2(24f, -22f);
		Image component = gameObject.GetComponent<Image>();
		component.sprite = LoadResourceSprite("Profile/premium_profile_panel_frame", () => CreateBeveledPanelSprite(690, 230, 30, 9, DeepPurple, new Color(1f, 0.52f, 0.03f, 1f), Purple), new Vector4(30f, 30f, 30f, 30f));
		component.type = Image.Type.Sliced;
		panelOutline = gameObject.AddComponent<Outline>();
		panelOutline.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0.85f);
		panelOutline.effectDistance = new Vector2(3f, -3f);
		Shadow shadow = gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.86f);
		shadow.effectDistance = new Vector2(0f, -8f);
		Button button = gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.onClick.AddListener(OpenProfileMenu);
		panelGlow = AddImage(mainPanel, "Panel Glow", 7f, 7f, 676f, 216f, LoadResourceSprite("Profile/premium_profile_panel_glow", () => CreateBeveledPanelSprite(676, 216, 28, 4, new Color(neonColor.r, neonColor.g, neonColor.b, 0.08f), Color.clear, Color.clear), new Vector4(28f, 28f, 28f, 28f)));
		panelGlow.raycastTarget = false;
		AddCrown(new Vector2(0f, -4f), new Vector2(70f, 48f));
		BuildAvatarBlock();
		BuildProfileTexts();
		BuildEditButton();
		BuildProgressBar();
	}

	private void BuildAvatarBlock()
	{
		Image image = AddImage(mainPanel, "Avatar Glow", 22f, 20f, 172f, 172f, LoadResourceSprite("Profile/premium_profile_avatar_glow", () => CreateCircleSprite(192, new Color(neonColor.r, neonColor.g, neonColor.b, 0.24f), Color.clear, 0)));
		image.raycastTarget = false;
		RectTransform rectTransform = CreateRect(mainPanel, "Avatar Frame", 31f, 27f, 154f, 154f);
		Image image2 = rectTransform.gameObject.AddComponent<Image>();
		image2.sprite = LoadResourceSprite("Profile/premium_profile_avatar_frame", () => CreateCircleSprite(192, new Color(0.055f, 0.012f, 0.085f, 1f), Gold, 8));
		image2.raycastTarget = false;
		avatarOutline = rectTransform.gameObject.AddComponent<Outline>();
		avatarOutline.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0.8f);
		avatarOutline.effectDistance = new Vector2(3f, -3f);
		RectTransform rectTransform2 = CreateCenteredChildRect(rectTransform, "Avatar Mask", 0f, 0f, 138f, 138f);
		Image image3 = rectTransform2.gameObject.AddComponent<Image>();
		image3.sprite = LoadResourceSprite("Profile/premium_profile_avatar_mask", () => CreateCircleSprite(160, Color.white, Color.clear, 0));
		Mask mask = rectTransform2.gameObject.AddComponent<Mask>();
		mask.showMaskGraphic = false;
		avatarImage = AddImage(rectTransform2, "Avatar", 0f, 0f, 138f, 138f, LoadDefaultAvatar());
		avatarImage.rectTransform.anchorMin = Vector2.zero;
		avatarImage.rectTransform.anchorMax = Vector2.one;
		avatarImage.rectTransform.offsetMin = Vector2.zero;
		avatarImage.rectTransform.offsetMax = Vector2.zero;
		avatarImage.preserveAspect = false;
		RectTransform rectTransform3 = CreateRect(mainPanel, "Level Badge", 73f, 146f, 74f, 74f);
		Image image4 = rectTransform3.gameObject.AddComponent<Image>();
		image4.sprite = LoadResourceSprite("Profile/premium_profile_level_badge", () => CreateBeveledPanelSprite(96, 96, 22, 5, new Color(0.12f, 0.045f, 0f, 0.98f), Gold, Gold), new Vector4(22f, 22f, 22f, 22f));
		image4.raycastTarget = false;
		levelBadgeText = CreateText(rectTransform3, "1", 34, FontStyles.Bold, 0f, 11f, 74f, 42f, SoftGold, TextAlignmentOptions.Center);
	}

	private void BuildProfileTexts()
	{
		playerNameText = CreateText(mainPanel, playerName.ToUpperInvariant(), 39, FontStyles.Bold, 220f, 35f, 330f, 45f, Color.white, TextAlignmentOptions.Left);
		RectTransform rectTransform = CreateRect(mainPanel, "Title Pill", 219f, 84f, 285f, 38f);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = LoadResourceSprite("Profile/premium_profile_title_pill", () => CreateBeveledPanelSprite(300, 44, 12, 4, new Color(0.13f, 0.045f, 0f, 0.95f), Gold, Gold), new Vector4(12f, 12f, 12f, 12f));
		image.raycastTarget = false;
		AddIcon(rectTransform, "Title Crown", 13f, 7f, 26f, 22f, CreateCrownSprite(64, Gold));
		playerTitleText = CreateText(rectTransform, playerTitle.ToUpperInvariant(), 22, FontStyles.Bold, 48f, 5f, 218f, 27f, SoftGold, TextAlignmentOptions.Left);
		AddIcon(mainPanel, "Level Star", 221f, 132f, 35f, 35f, LoadResourceSprite("Profile/premium_profile_star", () => CreateStarSprite(64, Purple)));
		levelText = CreateText(mainPanel, "NIVEL 1", 23, FontStyles.Bold, 268f, 132f, 140f, 32f, Color.white, TextAlignmentOptions.Left);
		streakText = CreateText(mainPanel, "WIN STREAK 0", 18, FontStyles.Bold, 404f, 135f, 190f, 27f, SoftGold, TextAlignmentOptions.Left);
	}

	private void BuildEditButton()
	{
		RectTransform rectTransform = CreateRect(mainPanel, "Edit Profile", 592f, 38f, 62f, 62f);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = LoadResourceSprite("Profile/premium_profile_edit_button", () => CreateBeveledPanelSprite(80, 80, 14, 4, new Color(0.12f, 0.045f, 0f, 0.95f), Gold, Purple), new Vector4(14f, 14f, 14f, 14f));
		Button button = rectTransform.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.ColorTint;
		button.targetGraphic = image;
		button.onClick.AddListener(OpenProfileMenu);
		Image image2 = AddIcon(rectTransform, "Pencil", 15f, 15f, 32f, 32f, LoadResourceSprite("Profile/premium_profile_pencil", () => CreatePencilSprite(64, SoftGold)));
		image2.raycastTarget = false;
	}

	private void BuildProgressBar()
	{
		RectTransform rectTransform = CreateRect(mainPanel, "XP Track", 219f, 178f, 385f, 27f);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = LoadResourceSprite("Profile/premium_profile_xp_track", () => CreateRoundedSprite(420, 32, 14, new Color(0f, 0f, 0f, 0.82f), Purple, 3), new Vector4(14f, 14f, 14f, 14f));
		image.raycastTarget = false;
		xpFillRect = CreateRect(rectTransform, "XP Fill", 4f, 4f, 190f, 19f);
		Image image2 = xpFillRect.gameObject.AddComponent<Image>();
		image2.sprite = LoadResourceSprite("Profile/premium_profile_xp_fill", () => CreateRoundedSprite(420, 28, 11, Purple, Color.clear, 0), new Vector4(11f, 11f, 11f, 11f));
		image2.raycastTarget = false;
		xpFillImage = image2;
		xpGlow = xpFillRect.gameObject.AddComponent<Outline>();
		xpGlow.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0.75f);
		xpGlow.effectDistance = new Vector2(2f, -2f);
		percentText = CreateText(mainPanel, "0%", 22, FontStyles.Bold, 610f, 177f, 60f, 28f, Color.white, TextAlignmentOptions.Left);
	}

	private void AddCrown(Vector2 anchoredPosition, Vector2 size)
	{
		RectTransform rectTransform = CreateRect(mainPanel, "Top Crown", anchoredPosition.x, 0f - anchoredPosition.y, size.x, size.y);
		rectTransform.anchorMin = new Vector2(0.5f, 1f);
		rectTransform.anchorMax = new Vector2(0.5f, 1f);
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.anchoredPosition = anchoredPosition;
		rectTransform.gameObject.AddComponent<Image>().sprite = LoadResourceSprite("Profile/premium_profile_top_crown", () => CreateCrownSprite(128, Gold));
	}

	private void AnimatePanel()
	{
		if (!(mainPanel == null))
		{
			float num = 1f + Mathf.Sin(Time.unscaledTime * 2f) * 0.006f;
			mainPanel.localScale = new Vector3(num, num, 1f);
			if (panelGlow != null)
			{
				Color color = panelGlow.color;
				color.a = 0.05f + Mathf.Sin(Time.unscaledTime * 2.4f) * 0.025f;
				panelGlow.color = color;
			}
		}
	}

	private void OpenProfileMenu()
	{
		if (customizationMenu == null)
		{
			customizationMenu = UnityEngine.Object.FindObjectOfType<ProfileCustomizationMenu>();
		}
		customizationMenu?.ToggleMenu();
	}

	public void UpdatePlayerName(string newName)
	{
		playerName = newName;
		PlayerPrefs.SetString("player_name", newName);
		RefreshProfileVisuals();
	}

	public void ApplyProfileCustomization(string newName, string newTitle, string newBio, Color newNeonColor)
	{
		playerName = newName;
		playerTitle = newTitle;
		playerBio = newBio;
		neonColor = newNeonColor;
		RefreshProfileVisuals();
	}

	public void UpdateAvatar(Sprite newAvatar)
	{
		avatarSprite = newAvatar;
		if (avatarImage != null && newAvatar != null)
		{
			avatarImage.sprite = newAvatar;
			avatarImage.color = Color.white;
		}
	}

	public void ChangeAvatar()
	{
		NativeGallery.GetImageFromGallery(delegate(string path)
		{
			if (!string.IsNullOrEmpty(path))
			{
				Texture2D texture2D = NativeGallery.LoadImageAtPath(path);
				if (!(texture2D == null))
				{
					Sprite newAvatar = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
					UpdateAvatar(newAvatar);
					PlayerPrefs.SetString("avatar_path", path);
					PlayerPrefs.Save();
					PlayerCloudSaveService.QueueSaveFromPlayerPrefs();
				}
			}
		}, "Selecione um Avatar");
	}

	private void LoadSavedAvatar()
	{
		string text = PlayerPrefs.GetString("avatar_path", string.Empty);
		if (string.IsNullOrEmpty(text) || !System.IO.File.Exists(text))
		{
			if (!string.IsNullOrEmpty(text))
			{
				PlayerPrefs.DeleteKey("avatar_path");
				PlayerPrefs.Save();
			}

			if (avatarSprite != null)
			{
				UpdateAvatar(avatarSprite);
			}
			return;
		}

		Texture2D texture2D;
		try
		{
			texture2D = NativeGallery.LoadImageAtPath(text);
		}
		catch
		{
			PlayerPrefs.DeleteKey("avatar_path");
			PlayerPrefs.Save();
			if (avatarSprite != null)
			{
				UpdateAvatar(avatarSprite);
			}
			return;
		}

		if (!(texture2D == null))
		{
			Sprite newAvatar = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
			UpdateAvatar(newAvatar);
		}
	}

	private void RefreshProfileVisuals()
	{
		if (playerNameText != null)
		{
			playerNameText.text = CleanText(playerName, "JOGADOR", 16).ToUpperInvariant();
		}
		if (playerTitleText != null)
		{
			playerTitleText.text = CleanText(playerTitle, "JOGADOR DO ANO", 18).ToUpperInvariant();
		}
		if (playerBioText != null)
		{
			playerBioText.text = playerBio;
		}
		if (panelOutline != null)
		{
			panelOutline.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0.85f);
		}
		if (avatarOutline != null)
		{
			avatarOutline.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0.8f);
		}
		if (xpFillImage != null)
		{
			xpFillImage.color = neonColor;
		}
		if (xpGlow != null)
		{
			xpGlow.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0.75f);
		}
		RefreshProgressVisuals();
	}

	private void RefreshProgressVisuals()
	{
		int level = ProfileProgressionService.Level;
		int winStreak = ProfileProgressionService.WinStreak;
		float t = (xpProgress = ProfileProgressionService.Progress01());
		if (levelText != null)
		{
			levelText.text = "NIVEL " + level;
		}
		if (levelBadgeText != null)
		{
			levelBadgeText.text = level.ToString();
		}
		if (streakText != null)
		{
			streakText.text = "WIN STREAK " + winStreak;
		}
		if (percentText != null)
		{
			percentText.text = ProfileProgressionService.ProgressPercent() + "%";
		}
		if (xpFillRect != null)
		{
			float x = Mathf.Lerp(12f, 377f, t);
			xpFillRect.sizeDelta = new Vector2(x, xpFillRect.sizeDelta.y);
		}
	}

	private Sprite LoadDefaultAvatar()
	{
		if (avatarSprite != null)
		{
			return avatarSprite;
		}
		Sprite sprite = Resources.Load<Sprite>("Profile/profile_default_avatar");
		if (sprite != null)
		{
			avatarSprite = sprite;
			return sprite;
		}
		Texture2D texture2D = Resources.Load<Texture2D>("Profile/profile_default_avatar");
		if (texture2D != null)
		{
			avatarSprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
			return avatarSprite;
		}
		return CreateCircleSprite(128, new Color(0.12f, 0.02f, 0.16f, 1f), Gold, 4);
	}

	private static string CleanText(string value, string fallback, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			value = fallback;
		}
		value = value.Trim();
		return (value.Length <= maxLength) ? value : value.Substring(0, maxLength);
	}

	private TextMeshProUGUI CreateText(RectTransform parent, string text, int size, FontStyles style, float x, float y, float width, float height, Color color, TextAlignmentOptions alignment)
	{
		RectTransform rectTransform = CreateRect(parent, text, x, y, width, height);
		TextMeshProUGUI textMeshProUGUI = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
		textMeshProUGUI.text = text;
		textMeshProUGUI.fontSize = size;
		textMeshProUGUI.fontStyle = style;
		textMeshProUGUI.alignment = alignment;
		textMeshProUGUI.color = color;
		ApplySafeFont(textMeshProUGUI);
		textMeshProUGUI.enableAutoSizing = true;
		textMeshProUGUI.fontSizeMin = Mathf.Max(8, size - 13);
		textMeshProUGUI.fontSizeMax = size;
		textMeshProUGUI.raycastTarget = false;
		return textMeshProUGUI;
	}

	private static void ApplySafeFont(TextMeshProUGUI text)
	{
		if (text == null)
		{
			return;
		}

		TMP_FontAsset safeFont = TMPFontSafetyNet.SafeFontAsset;
		if (safeFont != null)
		{
			text.font = safeFont;
		}

		Material safeMaterial = TMPFontSafetyNet.SafeFontMaterial;
		if (safeMaterial != null)
		{
			text.fontSharedMaterial = safeMaterial;
		}
	}

	private Image AddImage(RectTransform parent, string name, float x, float y, float width, float height, Sprite sprite)
	{
		RectTransform rectTransform = CreateRect(parent, name, x, y, width, height);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = sprite;
		image.type = Image.Type.Simple;
		image.color = Color.white;
		return image;
	}

	private Image AddIcon(RectTransform parent, string name, float x, float y, float width, float height, Sprite sprite)
	{
		Image image = AddImage(parent, name, x, y, width, height, sprite);
		image.raycastTarget = false;
		return image;
	}

	private RectTransform CreateRect(Transform parent, string name, float x, float y, float width, float height)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform));
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.SetParent(parent, worldPositionStays: false);
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(0f, 1f);
		component.pivot = new Vector2(0f, 1f);
		component.anchoredPosition = new Vector2(x, 0f - y);
		component.sizeDelta = new Vector2(width, height);
		return component;
	}

	private RectTransform CreateCenteredChildRect(Transform parent, string name, float x, float y, float width, float height)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform));
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.SetParent(parent, worldPositionStays: false);
		component.anchorMin = new Vector2(0.5f, 0.5f);
		component.anchorMax = new Vector2(0.5f, 0.5f);
		component.pivot = new Vector2(0.5f, 0.5f);
		component.anchoredPosition = new Vector2(x, y);
		component.sizeDelta = new Vector2(width, height);
		return component;
	}

	private static Sprite LoadResourceSprite(string resourcePath, Func<Sprite> fallbackFactory, Vector4 border = default(Vector4))
	{
		Sprite sprite = Resources.Load<Sprite>(resourcePath);
		if (sprite != null)
		{
			return sprite;
		}
		Texture2D texture2D = Resources.Load<Texture2D>(resourcePath);
		if (texture2D != null)
		{
			return Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
		}
		return fallbackFactory();
	}

	private static Sprite CreateBeveledPanelSprite(int width, int height, int cut, int border, Color fill, Color gold, Color neon)
	{
		Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				if (!IsInsideBeveledRect(j, i, width, height, cut))
				{
					texture2D.SetPixel(j, i, Color.clear);
					continue;
				}
				bool flag = j >= border && j < width - border && i >= border && i < height - border && IsInsideBeveledRect(j - border, i - border, width - border * 2, height - border * 2, Mathf.Max(0, cut - border));
				Color color = (flag ? fill : Color.Lerp(gold, neon, Mathf.PingPong((float)(j + i) * 0.015f, 1f)));
				if (flag)
				{
					float num = Mathf.Clamp01(Vector2.Distance(new Vector2((float)j / (float)width, (float)i / (float)height), new Vector2(0.5f, 0.5f)) * 1.2f);
					color = Color.Lerp(color, new Color(0.01f, 0f, 0.02f, fill.a), num * 0.35f);
				}
				texture2D.SetPixel(j, i, color);
			}
		}
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(cut, cut, cut, cut));
	}

	private static bool IsInsideBeveledRect(int x, int y, int width, int height, int cut)
	{
		if (x < 0 || y < 0 || x >= width || y >= height)
		{
			return false;
		}
		if (x + y < cut)
		{
			return false;
		}
		if (width - 1 - x + y < cut)
		{
			return false;
		}
		if (x + (height - 1 - y) < cut)
		{
			return false;
		}
		if (width - 1 - x + (height - 1 - y) < cut)
		{
			return false;
		}
		return true;
	}

	private static Sprite CreateRoundedSprite(int width, int height, int radius, Color fill, Color borderColor, int border)
	{
		Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				float num = Mathf.Max(radius - j, j - (width - 1 - radius), 0);
				float num2 = Mathf.Max(radius - i, i - (height - 1 - radius), 0);
				if (!(num * num + num2 * num2 <= (float)(radius * radius)))
				{
					texture2D.SetPixel(j, i, Color.clear);
					continue;
				}
				bool flag = border > 0 && (j < border || i < border || j >= width - border || i >= height - border || num * num + num2 * num2 > (float)((radius - border) * (radius - border)));
				texture2D.SetPixel(j, i, flag ? borderColor : fill);
			}
		}
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
	}

	private static Sprite CreateCircleSprite(int size, Color fill, Color borderColor, int border)
	{
		Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		Vector2 b = new Vector2((float)(size - 1) * 0.5f, (float)(size - 1) * 0.5f);
		float num = (float)size * 0.5f - 1f;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num2 = Vector2.Distance(new Vector2(j, i), b);
				if (num2 > num)
				{
					texture2D.SetPixel(j, i, Color.clear);
				}
				else if (border > 0 && num2 > num - (float)border)
				{
					texture2D.SetPixel(j, i, borderColor);
				}
				else
				{
					texture2D.SetPixel(j, i, fill);
				}
			}
		}
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
	}

	private static Sprite CreateStarSprite(int size, Color color)
	{
		Vector2[] array = new Vector2[10];
		Vector2 vector = new Vector2((float)size * 0.5f, (float)size * 0.5f);
		for (int i = 0; i < array.Length; i++)
		{
			float num = ((i % 2 == 0) ? ((float)size * 0.43f) : ((float)size * 0.19f));
			float f = (-90f + (float)i * 36f) * (MathF.PI / 180f);
			array[i] = vector + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
		}
		return CreatePolygonSprite(size, size, array, color);
	}

	private static Sprite CreateCrownSprite(int size, Color color)
	{
		Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		Vector2[] polygon = new Vector2[7]
		{
			new Vector2((float)size * 0.12f, (float)size * 0.68f),
			new Vector2((float)size * 0.18f, (float)size * 0.28f),
			new Vector2((float)size * 0.36f, (float)size * 0.55f),
			new Vector2((float)size * 0.5f, (float)size * 0.14f),
			new Vector2((float)size * 0.64f, (float)size * 0.55f),
			new Vector2((float)size * 0.82f, (float)size * 0.28f),
			new Vector2((float)size * 0.88f, (float)size * 0.68f)
		};
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				bool flag = PointInPolygon(new Vector2(j, i), polygon);
				bool flag2 = (float)j > (float)size * 0.15f && (float)j < (float)size * 0.85f && (float)i > (float)size * 0.66f && (float)i < (float)size * 0.82f;
				texture2D.SetPixel(j, i, (flag || flag2) ? color : Color.clear);
			}
		}
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
	}

	private static Sprite CreatePencilSprite(int size, Color color)
	{
		Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num = Mathf.Abs((float)(j - i) - (float)size * 0.08f);
				bool flag = num < (float)size * 0.08f && (float)j > (float)size * 0.22f && (float)j < (float)size * 0.78f && (float)i > (float)size * 0.14f && (float)i < (float)size * 0.7f;
				bool flag2 = (float)j > (float)size * 0.66f && (float)i > (float)size * 0.58f && (float)(j + i) < (float)size * 1.55f;
				bool flag3 = (float)j > (float)size * 0.14f && (float)j < (float)size * 0.72f && (float)i > (float)size * 0.2f && (float)i < (float)size * 0.82f && ((float)j < (float)size * 0.2f || (float)i < (float)size * 0.26f || (float)j > (float)size * 0.66f || (float)i > (float)size * 0.76f);
				texture2D.SetPixel(j, i, (flag || flag2 || flag3) ? color : Color.clear);
			}
		}
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
	}

	private static Sprite CreatePolygonSprite(int width, int height, Vector2[] points, Color color)
	{
		Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				texture2D.SetPixel(j, i, PointInPolygon(new Vector2(j, i), points) ? color : Color.clear);
			}
		}
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
	}

	private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
	{
		bool flag = false;
		int num = 0;
		int num2 = polygon.Length - 1;
		while (num < polygon.Length)
		{
			if (polygon[num].y > point.y != polygon[num2].y > point.y && point.x < (polygon[num2].x - polygon[num].x) * (point.y - polygon[num].y) / (polygon[num2].y - polygon[num].y + 0.0001f) + polygon[num].x)
			{
				flag = !flag;
			}
			num2 = num++;
		}
		return flag;
	}
}
