using System;
using System.Collections;
using System.Collections.Generic;
using GBTemplates.Domino.Services;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ProfileCustomizationMenu : MonoBehaviour
{
	private enum ProfileIcon
	{
		Person = 0,
		Star = 1,
		Chat = 2,
		Camera = 3,
		Save = 4,
		Close = 5,
		Pencil = 6,
		Crown = 7,
		Check = 8
	}

	private readonly struct SwatchView
	{
		public GameObject Ring { get; }

		public GameObject Check { get; }

		public SwatchView(GameObject ring, GameObject check)
		{
			Ring = ring;
			Check = check;
		}
	}

	private sealed class IconTexture
	{
		private readonly Texture2D texture;

		private readonly Color color;

		public IconTexture(int size, Color lineColor)
		{
			texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
			texture.wrapMode = TextureWrapMode.Clamp;
			color = lineColor;
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					texture.SetPixel(j, i, Color.clear);
				}
			}
		}

		public Texture2D Apply()
		{
			texture.Apply();
			return texture;
		}

		public void Line(Vector2 start, Vector2 end, float width)
		{
			int num = Mathf.FloorToInt(Mathf.Min(start.x, end.x) - width);
			int num2 = Mathf.CeilToInt(Mathf.Max(start.x, end.x) + width);
			int num3 = Mathf.FloorToInt(Mathf.Min(start.y, end.y) - width);
			int num4 = Mathf.CeilToInt(Mathf.Max(start.y, end.y) + width);
			Vector2 vector = end - start;
			float sqrMagnitude = vector.sqrMagnitude;
			for (int i = num3; i <= num4; i++)
			{
				for (int j = num; j <= num2; j++)
				{
					Vector2 vector2 = new Vector2((float)j + 0.5f, (float)i + 0.5f);
					float num5 = ((sqrMagnitude <= 0.001f) ? 0f : Mathf.Clamp01(Vector2.Dot(vector2 - start, vector) / sqrMagnitude));
					Vector2 b = start + vector * num5;
					float num6 = Vector2.Distance(vector2, b);
					if (num6 <= width * 0.5f)
					{
						SetPixel(j, i, color);
					}
				}
			}
		}

		public void Circle(Vector2 center, float radius, float width, bool filled)
		{
			for (int i = Mathf.FloorToInt(center.y - radius - width); i <= Mathf.CeilToInt(center.y + radius + width); i++)
			{
				for (int j = Mathf.FloorToInt(center.x - radius - width); j <= Mathf.CeilToInt(center.x + radius + width); j++)
				{
					float num = Vector2.Distance(new Vector2((float)j + 0.5f, (float)i + 0.5f), center);
					bool num2;
					if (!filled)
					{
						if (!(num >= radius - width))
						{
							continue;
						}
						num2 = num <= radius + width * 0.5f;
					}
					else
					{
						num2 = num <= radius;
					}
					if (num2)
					{
						SetPixel(j, i, color);
					}
				}
			}
		}

		public void Arc(Vector2 center, float radius, float startAngle, float endAngle, float width)
		{
			Vector2 start = Vector2.zero;
			bool flag = false;
			for (float num = startAngle; num <= endAngle; num += 3f)
			{
				float f = num * (MathF.PI / 180f);
				Vector2 vector = center + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * radius;
				if (flag)
				{
					Line(start, vector, width);
				}
				start = vector;
				flag = true;
			}
		}

		public void Rect(Rect rect, float width)
		{
			Line(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), width);
			Line(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), width);
			Line(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), width);
			Line(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), width);
		}

		public void RoundedRect(Rect rect, float radius, float width)
		{
			Line(new Vector2(rect.xMin + radius, rect.yMin), new Vector2(rect.xMax - radius, rect.yMin), width);
			Line(new Vector2(rect.xMin + radius, rect.yMax), new Vector2(rect.xMax - radius, rect.yMax), width);
			Line(new Vector2(rect.xMin, rect.yMin + radius), new Vector2(rect.xMin, rect.yMax - radius), width);
			Line(new Vector2(rect.xMax, rect.yMin + radius), new Vector2(rect.xMax, rect.yMax - radius), width);
			Arc(new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, width);
			Arc(new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, width);
			Arc(new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, width);
			Arc(new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, width);
		}

		public void Star(Vector2 center, float outer, float inner, float width)
		{
			Vector2[] array = new Vector2[10];
			for (int i = 0; i < array.Length; i++)
			{
				float num = -90f + (float)i * 36f;
				float num2 = ((i % 2 == 0) ? outer : inner);
				array[i] = center + new Vector2(Mathf.Cos(num * (MathF.PI / 180f)), Mathf.Sin(num * (MathF.PI / 180f))) * num2;
			}
			for (int j = 0; j < array.Length; j++)
			{
				Line(array[j], array[(j + 1) % array.Length], width);
			}
		}

		private void SetPixel(int x, int y, Color pixelColor)
		{
			if (x >= 0 && y >= 0 && x < texture.width && y < texture.height)
			{
				texture.SetPixel(x, y, pixelColor);
			}
		}
	}

	private const float ReferenceWidth = 853f;

	private const float ReferenceHeight = 1844f;

	private const string PanelBackgroundResource = "Profile/profile_editor_panel_bg";

	private const string DefaultAvatarResource = "Profile/profile_default_avatar";

	private const string NeonIndexKey = "profile_neon_index";

	private const string PlayerBioKey = "player_bio";

	private readonly Color gold = new Color(1f, 0.86f, 0.28f, 1f);

	private readonly Color fieldFill = new Color(0.15f, 0.04f, 0.24f, 0.9f);

	private readonly Color fieldBorder = new Color(0.55f, 0.2f, 0.78f, 1f);

	private readonly Color purple = new Color(0.61f, 0.12f, 0.88f, 1f);

	private readonly Color softText = new Color(0.9f, 0.84f, 0.96f, 0.86f);

	private readonly Color[] neonColors = new Color[7]
	{
		new Color(0.96f, 0.09f, 0.58f, 1f),
		new Color(0.59f, 0.12f, 0.87f, 1f),
		new Color(0.11f, 0.66f, 0.86f, 1f),
		new Color(0.11f, 0.79f, 0.35f, 1f),
		new Color(1f, 0.78f, 0.14f, 1f),
		new Color(1f, 0.47f, 0.12f, 1f),
		new Color(1f, 0.23f, 0.17f, 1f)
	};

	private readonly List<SwatchView> swatches = new List<SwatchView>();

	private GameObject overlayRoot;

	private RectTransform screenRoot;

	private RectTransform panelRoot;

	private CanvasGroup overlayGroup;

	private PremiumProfilePanel profilePanel;

	private TMP_InputField nameInput;

	private TMP_InputField titleInput;

	private TMP_InputField bioInput;

	private TextMeshProUGUI statusText;

	private Image avatarImage;

	private Coroutine transitionRoutine;

	private bool opened;

	private int selectedNeonIndex = 1;

	private void Start()
	{
		profilePanel = UnityEngine.Object.FindObjectOfType<PremiumProfilePanel>();
		CreateMenu();
	}

	private void CreateMenu()
	{
		Canvas canvas = PrepareCanvas();
		overlayRoot = new GameObject("Premium Profile Editor");
		overlayRoot.transform.SetParent(canvas.transform, worldPositionStays: false);
		overlayRoot.SetActive(value: false);
		screenRoot = overlayRoot.AddComponent<RectTransform>();
		screenRoot.anchorMin = Vector2.zero;
		screenRoot.anchorMax = Vector2.one;
		screenRoot.offsetMin = Vector2.zero;
		screenRoot.offsetMax = Vector2.zero;
		overlayGroup = overlayRoot.AddComponent<CanvasGroup>();
		overlayGroup.alpha = 0f;
		overlayGroup.blocksRaycasts = false;
		Image image = CreateSolidImage(screenRoot, "Dismiss Background", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.52f));
		Stretch(image.rectTransform);
		Button button = image.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.onClick.AddListener(CloseMenu);
		panelRoot = CreateRect(screenRoot, "Profile Editor Panel", 0f, 0f, 853f, 1844f);
		Image image2 = panelRoot.gameObject.AddComponent<Image>();
		image2.sprite = LoadSprite("Profile/profile_editor_panel_bg");
		image2.raycastTarget = true;
		BuildHeader();
		BuildAvatarArea();
		BuildFields();
		BuildNeonSwatches();
		BuildFooterButtons();
	}

	private Canvas PrepareCanvas()
	{
		Canvas canvas = GetComponent<Canvas>();
		if (canvas == null)
		{
			canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
		}
		if (canvas == null)
		{
			GameObject gameObject = new GameObject("Premium Profile Canvas");
			canvas = gameObject.AddComponent<Canvas>();
			gameObject.AddComponent<GraphicRaycaster>();
		}
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.overrideSorting = true;
		canvas.sortingOrder = 950;
		CanvasScaler canvasScaler = canvas.GetComponent<CanvasScaler>();
		if (canvasScaler == null)
		{
			canvasScaler = canvas.gameObject.AddComponent<CanvasScaler>();
		}
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(853f, 1844f);
		canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		canvasScaler.matchWidthOrHeight = 0.5f;
		if (canvas.GetComponent<GraphicRaycaster>() == null)
		{
			canvas.gameObject.AddComponent<GraphicRaycaster>();
		}
		return canvas;
	}

	private void BuildHeader()
	{
		CreateText(panelRoot, "PERFIL PREMIUM", 47, FontStyles.Bold, 93f, 143f, 410f, 60f, gold);
		CreateIcon(panelRoot, ProfileIcon.Crown, 493f, 141f, 58f, 46f, gold);
		CreateText(panelRoot, "CUSTOMIZE SEU PERFIL", 25, FontStyles.Bold, 93f, 205f, 430f, 44f, softText);
	}

	private void BuildAvatarArea()
	{
		CreateGlowCircle(panelRoot, 211.5f, 368.5f, 266f, new Color(0.78f, 0.25f, 1f, 0.28f));
		RectTransform rectTransform = CreateRect(panelRoot, "Avatar Frame", 109f, 266f, 205f, 205f);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = CreateCircleSprite(224, new Color(0.07f, 0.02f, 0.11f, 1f), new Color(0.81f, 0.25f, 1f, 1f), 8);
		image.raycastTarget = false;
		RectTransform rectTransform2 = CreateCenteredChildRect(rectTransform, "Avatar Mask", 0f, 0f, 193f, 193f);
		Image image2 = rectTransform2.gameObject.AddComponent<Image>();
		image2.sprite = CreateCircleSprite(192, Color.white, Color.white, 0);
		Mask mask = rectTransform2.gameObject.AddComponent<Mask>();
		mask.showMaskGraphic = false;
		avatarImage = CreateCenteredChildImage(rectTransform2, "Avatar", 0f, 0f, 193f, 193f, null);
		avatarImage.sprite = LoadCurrentAvatar();
		avatarImage.preserveAspect = false;
		Button button = rectTransform.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.onClick.AddListener(ChangeAvatar);
		RectTransform rectTransform3 = CreateButton(panelRoot, "Edit Avatar", 268f, 417f, 72f, 72f, string.Empty, ProfileIcon.Pencil, primary: false, ChangeAvatar);
		rectTransform3.GetComponent<Image>().sprite = CreateRoundedSprite(96, 96, 48, new Color(0.36f, 0.08f, 0.59f, 1f), new Color(0.84f, 0.29f, 1f, 1f), 5);
		CreateButton(panelRoot, "Change Photo", 383f, 340f, 365f, 93f, "ALTERAR FOTO", ProfileIcon.Camera, primary: false, ChangeAvatar);
	}

	private void BuildFields()
	{
		nameInput = CreateProfileInput("NOME", ProfileIcon.Person, 524f, 91f, multiline: false);
		titleInput = CreateProfileInput("TÍTULO", ProfileIcon.Star, 709f, 91f, multiline: false);
		bioInput = CreateProfileInput("BIO", ProfileIcon.Chat, 894f, 181f, multiline: true);
	}

	private void BuildNeonSwatches()
	{
		CreateText(panelRoot, "COR NEON", 24, FontStyles.Bold, 93f, 1183f, 240f, 36f, gold);
		for (int i = 0; i < neonColors.Length; i++)
		{
			int index = i;
			RectTransform rectTransform = CreateRect(panelRoot, "Neon Swatch " + i, 96f + (float)i * 96f, 1232f, 76f, 79f);
			Image image = rectTransform.gameObject.AddComponent<Image>();
			image.sprite = CreateRoundedSprite(76, 79, 10, neonColors[i], Color.clear, 0);
			Button button = rectTransform.gameObject.AddComponent<Button>();
			button.transition = Selectable.Transition.None;
			button.onClick.AddListener(delegate
			{
				SelectNeon(index);
			});
			RectTransform rectTransform2 = CreateCenteredChildRect(rectTransform, "Selected Ring", 0f, 0f, 88f, 91f);
			Image image2 = rectTransform2.gameObject.AddComponent<Image>();
			image2.sprite = CreateRoundedSprite(88, 91, 16, Color.clear, new Color(0.94f, 0.79f, 1f, 1f), 4);
			image2.raycastTarget = false;
			Image image3 = CreateCenteredChildIcon(rectTransform2, ProfileIcon.Check, 0f, 0f, 47f, 47f, Color.white);
			image3.raycastTarget = false;
			swatches.Add(new SwatchView(rectTransform2.gameObject, image3.gameObject));
		}
	}

	private void BuildFooterButtons()
	{
		CreateButton(panelRoot, "Save Profile", 93f, 1393f, 655f, 107f, "SALVAR ALTERAÇÕES", ProfileIcon.Save, primary: true, SaveAll);
		CreateButton(panelRoot, "Close Profile", 93f, 1533f, 655f, 100f, "FECHAR", ProfileIcon.Close, primary: false, CloseMenu);
		statusText = CreateText(panelRoot, "Perfil salvo na nuvem quando logado com Google", 19, FontStyles.Normal, 170f, 1686f, 520f, 36f, new Color(0.86f, 0.8f, 0.94f, 0.6f));
		statusText.alignment = TextAlignmentOptions.Center;
	}

	private TMP_InputField CreateProfileInput(string label, ProfileIcon icon, float y, float height, bool multiline)
	{
		CreateText(panelRoot, label, 24, FontStyles.Bold, 93f, y, 240f, 36f, gold);
		RectTransform rectTransform = CreateRect(panelRoot, label + " Input", 93f, y + 46f, 655f, height);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = CreateRoundedSprite(655, Mathf.RoundToInt(height), 10, fieldFill, fieldBorder, 3);
		TMP_InputField tMP_InputField = rectTransform.gameObject.AddComponent<TMP_InputField>();
		tMP_InputField.targetGraphic = image;
		tMP_InputField.lineType = (multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine);
		tMP_InputField.characterLimit = (multiline ? 80 : 18);
		tMP_InputField.selectionColor = new Color(0.74f, 0.25f, 1f, 0.45f);
		tMP_InputField.caretColor = Color.white;
		CreateChildIcon(rectTransform, icon, 25f, height * 0.5f - 25f, 50f, 50f, new Color(0.82f, 0.78f, 0.91f, 0.82f));
		RectTransform rectTransform2 = CreateChildRect(rectTransform, "Text", 88f, 12f, 535f, height - 24f);
		TextMeshProUGUI textMeshProUGUI = rectTransform2.gameObject.AddComponent<TextMeshProUGUI>();
		textMeshProUGUI.fontSize = (multiline ? 28 : 30);
		textMeshProUGUI.color = Color.white;
		textMeshProUGUI.fontStyle = FontStyles.Normal;
		textMeshProUGUI.alignment = (multiline ? TextAlignmentOptions.Left : TextAlignmentOptions.MidlineLeft);
		textMeshProUGUI.enableWordWrapping = multiline;
		ApplySafeFont(textMeshProUGUI);
		textMeshProUGUI.raycastTarget = false;
		tMP_InputField.textComponent = textMeshProUGUI;
		return tMP_InputField;
	}

	private RectTransform CreateButton(RectTransform parent, string name, float x, float y, float width, float height, string label, ProfileIcon icon, bool primary, UnityAction action)
	{
		RectTransform rectTransform = CreateRect(parent, name, x, y, width, height);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = (primary ? CreateRoundedSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 13, new Color(0.39f, 0.08f, 0.66f, 0.94f), new Color(0.83f, 0.2f, 1f, 1f), 4) : CreateRoundedSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 13, new Color(0.07f, 0.02f, 0.13f, 0.92f), new Color(0.62f, 0.18f, 0.84f, 1f), 3));
		Button button = rectTransform.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.onClick.AddListener(action);
		ProfileButtonFeedback profileButtonFeedback = rectTransform.gameObject.AddComponent<ProfileButtonFeedback>();
		profileButtonFeedback.Initialize(image);
		if (!string.IsNullOrEmpty(label))
		{
			float x2 = ((name == "Change Photo") ? 40f : (width * 0.22f - 31f));
			float num = ((name == "Change Photo") ? 106f : (width * 0.32f));
			CreateChildIcon(rectTransform, icon, x2, height * 0.5f - 31f, 62f, 62f, new Color(0.91f, 0.82f, 1f, 1f));
			TextMeshProUGUI textMeshProUGUI = CreateChildText(rectTransform, label, (name == "Change Photo") ? 31 : 29, FontStyles.Bold, num, height * 0.5f - 29f, width - num - 24f, 58f, Color.white);
			textMeshProUGUI.alignment = TextAlignmentOptions.Left;
		}
		else
		{
			CreateChildIcon(rectTransform, icon, 16f, 16f, width - 32f, height - 32f, new Color(0.94f, 0.86f, 1f, 1f));
		}
		return rectTransform;
	}

	public void ToggleMenu()
	{
		if (opened)
		{
			CloseMenu();
		}
		else
		{
			OpenMenu();
		}
	}

	private void OpenMenu()
	{
		if (overlayRoot == null)
		{
			CreateMenu();
		}
		opened = true;
		RefreshInputsFromProfile();
		overlayRoot.SetActive(value: true);
		overlayGroup.blocksRaycasts = true;
		if (transitionRoutine != null)
		{
			StopCoroutine(transitionRoutine);
		}
		transitionRoutine = StartCoroutine(AnimateOpen());
	}

	private void CloseMenu()
	{
		if (opened || (!(overlayRoot == null) && overlayRoot.activeSelf))
		{
			opened = false;
			if (transitionRoutine != null)
			{
				StopCoroutine(transitionRoutine);
			}
			transitionRoutine = StartCoroutine(AnimateClose());
		}
	}

	private IEnumerator AnimateOpen()
	{
		panelRoot.localScale = Vector3.one * 0.94f;
		panelRoot.anchoredPosition = new Vector2(0f, -34f);
		for (float elapsed = 0f; elapsed < 0.28f; elapsed += Time.unscaledDeltaTime)
		{
			float progress = Mathf.Clamp01(elapsed / 0.28f);
			float eased = EaseOutBack(progress);
			overlayGroup.alpha = Mathf.SmoothStep(0f, 1f, progress);
			panelRoot.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, eased);
			panelRoot.anchoredPosition = Vector2.LerpUnclamped(new Vector2(0f, -34f), Vector2.zero, eased);
			yield return null;
		}
		overlayGroup.alpha = 1f;
		panelRoot.localScale = Vector3.one;
		panelRoot.anchoredPosition = Vector2.zero;
	}

	private IEnumerator AnimateClose()
	{
		overlayGroup.blocksRaycasts = false;
		for (float elapsed = 0f; elapsed < 0.2f; elapsed += Time.unscaledDeltaTime)
		{
			float progress = Mathf.Clamp01(elapsed / 0.2f);
			float eased = Mathf.SmoothStep(0f, 1f, progress);
			overlayGroup.alpha = 1f - eased;
			panelRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.97f, eased);
			panelRoot.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0f, -24f), eased);
			yield return null;
		}
		overlayGroup.alpha = 0f;
		overlayRoot.SetActive(value: false);
	}

	private void RefreshInputsFromProfile()
	{
		if (profilePanel == null)
		{
			profilePanel = UnityEngine.Object.FindObjectOfType<PremiumProfilePanel>();
		}
		string text = PlayerPrefs.GetString("player_name", (profilePanel != null) ? profilePanel.playerName : "Jogador");
		string text2 = PlayerPrefs.GetString("player_title", (profilePanel != null) ? profilePanel.playerTitle : "Jogador do Ano");
		string text3 = PlayerPrefs.GetString("player_bio", (profilePanel != null) ? profilePanel.playerBio : "Dominando as mesas.");
		nameInput.text = text;
		titleInput.text = text2;
		bioInput.text = text3;
		avatarImage.sprite = LoadCurrentAvatar();
		selectedNeonIndex = Mathf.Clamp(PlayerPrefs.GetInt("profile_neon_index", GetClosestNeonIndex((profilePanel != null) ? profilePanel.neonColor : neonColors[1])), 0, neonColors.Length - 1);
		SelectNeon(selectedNeonIndex);
		SetStatus("Perfil salvo na nuvem quando logado com Google", highlight: false);
	}

	private void SaveAll()
	{
		string text = CleanValue(nameInput.text, "Jogador", 18);
		string text2 = CleanValue(titleInput.text, "Jogador do Ano", 24);
		string text3 = CleanValue(bioInput.text, "Dominando as mesas.", 80);
		Color color = neonColors[Mathf.Clamp(selectedNeonIndex, 0, neonColors.Length - 1)];
		nameInput.text = text;
		titleInput.text = text2;
		bioInput.text = text3;
		PlayerPrefs.SetString("player_name", text);
		PlayerPrefs.SetString("player_title", text2);
		PlayerPrefs.SetString("player_bio", text3);
		PlayerPrefs.SetInt("profile_neon_index", selectedNeonIndex);
		PlayerPrefs.SetString("profile_neon_color", ColorUtility.ToHtmlStringRGBA(color));
		PlayerPrefs.Save();
		if (profilePanel != null)
		{
			profilePanel.ApplyProfileCustomization(text, text2, text3, color);
		}
		PlayerCloudSaveService.QueueSaveFromPlayerPrefs();
		PlayerCloudSaveService.TrySaveCurrentProfileAsync();
		SetStatus("Alterações salvas", highlight: true);
	}

	private void ChangeAvatar()
	{
		NativeGallery.GetImageFromGallery(delegate(string path)
		{
			if (!string.IsNullOrWhiteSpace(path))
			{
				Texture2D texture2D = NativeGallery.LoadImageAtPath(path);
				if (texture2D == null)
				{
					SetStatus("Não consegui carregar essa imagem", highlight: false);
				}
				else
				{
					Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
					avatarImage.sprite = sprite;
					PlayerPrefs.SetString("avatar_path", path);
					PlayerPrefs.Save();
					if (profilePanel != null)
					{
						profilePanel.UpdateAvatar(sprite);
					}
					PlayerCloudSaveService.QueueSaveFromPlayerPrefs();
					SetStatus("Foto atualizada", highlight: true);
				}
			}
		}, "Selecione um Avatar");
	}

	private void SelectNeon(int index)
	{
		selectedNeonIndex = Mathf.Clamp(index, 0, neonColors.Length - 1);
		for (int i = 0; i < swatches.Count; i++)
		{
			bool active = i == selectedNeonIndex;
			swatches[i].Ring.SetActive(active);
			swatches[i].Check.SetActive(active);
		}
	}

	private void SetStatus(string message, bool highlight)
	{
		if (!(statusText == null))
		{
			statusText.text = message;
			statusText.color = (highlight ? new Color(0.9f, 0.78f, 1f, 0.95f) : new Color(0.86f, 0.8f, 0.94f, 0.6f));
		}
	}

	private string CleanValue(string value, string fallback, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			value = fallback;
		}
		value = value.Trim();
		return (value.Length <= maxLength) ? value : value.Substring(0, maxLength);
	}

	private int GetClosestNeonIndex(Color color)
	{
		int result = 1;
		float num = float.MaxValue;
		for (int i = 0; i < neonColors.Length; i++)
		{
			Color color2 = neonColors[i];
			float num2 = Mathf.Pow(color2.r - color.r, 2f) + Mathf.Pow(color2.g - color.g, 2f) + Mathf.Pow(color2.b - color.b, 2f);
			if (num2 < num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	private Sprite LoadCurrentAvatar()
	{
		string text = PlayerPrefs.GetString("avatar_path", "");
		if (!string.IsNullOrWhiteSpace(text) && System.IO.File.Exists(text))
		{
			Texture2D texture2D;
			try
			{
				texture2D = NativeGallery.LoadImageAtPath(text);
			}
			catch
			{
				texture2D = null;
			}

			if (texture2D != null)
			{
				return Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
			}
		}
		else if (!string.IsNullOrWhiteSpace(text))
		{
			PlayerPrefs.DeleteKey("avatar_path");
			PlayerPrefs.Save();
		}

		Texture2D texture2D2 = Resources.Load<Texture2D>("Profile/profile_default_avatar");
		if (texture2D2 == null)
		{
			return null;
		}
		return Sprite.Create(texture2D2, new Rect(0f, 0f, texture2D2.width, texture2D2.height), new Vector2(0.5f, 0.5f));
	}

	private RectTransform CreateRect(RectTransform parent, string name, float x, float y, float width, float height, bool topLeft = true, Vector2? pivotOverride = null)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform));
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.SetParent(parent, worldPositionStays: false);
		component.anchorMin = new Vector2(0.5f, 0.5f);
		component.anchorMax = new Vector2(0.5f, 0.5f);
		Vector2 vector = (component.pivot = pivotOverride ?? new Vector2(0.5f, 0.5f));
		component.sizeDelta = new Vector2(width, height);
		if (topLeft)
		{
			component.anchoredPosition = new Vector2(x + width * vector.x - 426.5f, 922f - y - height * (1f - vector.y));
		}
		else
		{
			component.anchoredPosition = new Vector2(x, y);
		}
		return component;
	}

	private Image CreateImage(RectTransform parent, string name, float x, float y, float width, float height, Sprite sprite, bool topLeft = true)
	{
		RectTransform rectTransform = CreateRect(parent, name, x, y, width, height, topLeft);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = sprite;
		image.raycastTarget = false;
		return image;
	}

	private RectTransform CreateChildRect(RectTransform parent, string name, float x, float y, float width, float height)
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

	private RectTransform CreateCenteredChildRect(RectTransform parent, string name, float x, float y, float width, float height)
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

	private Image CreateCenteredChildImage(RectTransform parent, string name, float x, float y, float width, float height, Sprite sprite)
	{
		RectTransform rectTransform = CreateCenteredChildRect(parent, name, x, y, width, height);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = sprite;
		image.raycastTarget = false;
		return image;
	}

	private TextMeshProUGUI CreateChildText(RectTransform parent, string value, int size, FontStyles style, float x, float y, float width, float height, Color color)
	{
		RectTransform rectTransform = CreateChildRect(parent, value + " Text", x, y, width, height);
		TextMeshProUGUI textMeshProUGUI = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
		textMeshProUGUI.text = value;
		textMeshProUGUI.fontSize = size;
		textMeshProUGUI.fontStyle = style;
		textMeshProUGUI.color = color;
		textMeshProUGUI.alignment = TextAlignmentOptions.Left;
		textMeshProUGUI.enableAutoSizing = true;
		textMeshProUGUI.fontSizeMin = 10f;
		textMeshProUGUI.fontSizeMax = size;
		ApplySafeFont(textMeshProUGUI);
		textMeshProUGUI.raycastTarget = false;
		return textMeshProUGUI;
	}

	private Image CreateChildIcon(RectTransform parent, ProfileIcon icon, float x, float y, float width, float height, Color color)
	{
		RectTransform rectTransform = CreateChildRect(parent, icon.ToString() + " Icon", x, y, width, height);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = CreateIconSprite(icon, color);
		image.preserveAspect = true;
		image.raycastTarget = false;
		return image;
	}

	private Image CreateCenteredChildIcon(RectTransform parent, ProfileIcon icon, float x, float y, float width, float height, Color color)
	{
		RectTransform rectTransform = CreateCenteredChildRect(parent, icon.ToString() + " Icon", x, y, width, height);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = CreateIconSprite(icon, color);
		image.preserveAspect = true;
		image.raycastTarget = false;
		return image;
	}

	private Image CreateSolidImage(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
	{
		RectTransform rectTransform = CreateRect(parent, name, position.x, position.y, size.x, size.y, topLeft: false);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.color = color;
		return image;
	}

	private TextMeshProUGUI CreateText(RectTransform parent, string value, int size, FontStyles style, float x, float y, float width, float height, Color color, bool topLeft = true)
	{
		RectTransform rectTransform = CreateRect(parent, value + " Text", x, y, width, height, topLeft, new Vector2(0f, 1f));
		TextMeshProUGUI textMeshProUGUI = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
		textMeshProUGUI.text = value;
		textMeshProUGUI.fontSize = size;
		textMeshProUGUI.fontStyle = style;
		textMeshProUGUI.color = color;
		textMeshProUGUI.alignment = TextAlignmentOptions.Left;
		textMeshProUGUI.enableAutoSizing = true;
		textMeshProUGUI.fontSizeMin = 10f;
		textMeshProUGUI.fontSizeMax = size;
		ApplySafeFont(textMeshProUGUI);
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

	private Image CreateIcon(RectTransform parent, ProfileIcon icon, float x, float y, float width, float height, Color color, bool topLeft = true)
	{
		Image image = CreateImage(parent, icon.ToString() + " Icon", x, y, width, height, CreateIconSprite(icon, color), topLeft);
		image.preserveAspect = true;
		return image;
	}

	private void CreateGlowCircle(RectTransform parent, float centerX, float centerY, float size, Color color)
	{
		Image image = CreateImage(parent, "Avatar Glow", centerX - size * 0.5f, centerY - size * 0.5f, size, size, CreateCircleSprite(256, color, Color.clear, 0));
		image.raycastTarget = false;
	}

	private void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

	private Sprite LoadSprite(string resourceName)
	{
		Texture2D texture2D = Resources.Load<Texture2D>(resourceName);
		if (texture2D == null)
		{
			Debug.LogWarning("Profile asset não encontrado: " + resourceName);
			return null;
		}
		return Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
	}

	private Sprite CreateRoundedSprite(int width, int height, int radius, Color fill, Color border, int borderWidth)
	{
		Texture2D texture2D = new Texture2D(Mathf.Max(8, width), Mathf.Max(8, height), TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		Color clear = Color.clear;
		for (int i = 0; i < texture2D.height; i++)
		{
			for (int j = 0; j < texture2D.width; j++)
			{
				if (!IsInsideRoundedRect(j, i, texture2D.width, texture2D.height, radius))
				{
					texture2D.SetPixel(j, i, clear);
					continue;
				}
				bool flag = borderWidth > 0 && !IsInsideRoundedRect(j, i, texture2D.width, texture2D.height, Mathf.Max(0, radius - borderWidth), borderWidth);
				texture2D.SetPixel(j, i, flag ? border : fill);
			}
		}
		texture2D.Apply();
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		return Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
	}

	private bool IsInsideRoundedRect(int x, int y, int width, int height, int radius, int inset = 0)
	{
		float num = (float)x + 0.5f;
		float num2 = (float)y + 0.5f;
		float num3 = inset;
		float num4 = width - inset;
		float num5 = inset;
		float num6 = height - inset;
		float num7 = Mathf.Max(0, radius);
		if (num < num3 || num > num4 || num2 < num5 || num2 > num6)
		{
			return false;
		}
		float x2 = Mathf.Clamp(num, num3 + num7, num4 - num7);
		float y2 = Mathf.Clamp(num2, num5 + num7, num6 - num7);
		return Vector2.Distance(new Vector2(num, num2), new Vector2(x2, y2)) <= num7 + 0.5f;
	}

	private Sprite CreateCircleSprite(int size, Color fill, Color border, int borderWidth)
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
					continue;
				}
				Color color = ((borderWidth > 0 && num2 >= num - (float)borderWidth) ? border : fill);
				if (borderWidth == 0 && border == Color.clear)
				{
					color.a *= Mathf.Clamp01(1f - num2 / num);
				}
				texture2D.SetPixel(j, i, color);
			}
		}
		texture2D.Apply();
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		return Sprite.Create(texture2D, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
	}

	private Sprite CreateIconSprite(ProfileIcon icon, Color color)
	{
		IconTexture iconTexture = new IconTexture(128, color);
		switch (icon)
		{
		case ProfileIcon.Person:
			iconTexture.Circle(new Vector2(64f, 42f), 15f, 5f, filled: false);
			iconTexture.Arc(new Vector2(64f, 88f), 42f, 205f, 335f, 6f);
			break;
		case ProfileIcon.Star:
			iconTexture.Star(new Vector2(64f, 66f), 42f, 18f, 6f);
			break;
		case ProfileIcon.Chat:
			iconTexture.RoundedRect(new Rect(25f, 34f, 78f, 52f), 9f, 6f);
			iconTexture.Line(new Vector2(47f, 86f), new Vector2(31f, 105f), 6f);
			iconTexture.Line(new Vector2(43f, 52f), new Vector2(86f, 52f), 5f);
			iconTexture.Line(new Vector2(43f, 68f), new Vector2(76f, 68f), 5f);
			break;
		case ProfileIcon.Camera:
			iconTexture.RoundedRect(new Rect(25f, 39f, 78f, 58f), 10f, 6f);
			iconTexture.Rect(new Rect(47f, 25f, 34f, 16f), 5f);
			iconTexture.Circle(new Vector2(64f, 68f), 19f, 6f, filled: false);
			break;
		case ProfileIcon.Save:
			iconTexture.RoundedRect(new Rect(27f, 20f, 74f, 88f), 5f, 7f);
			iconTexture.Rect(new Rect(42f, 20f, 40f, 34f), 6f);
			iconTexture.Line(new Vector2(43f, 75f), new Vector2(85f, 75f), 7f);
			iconTexture.Line(new Vector2(43f, 75f), new Vector2(43f, 108f), 7f);
			iconTexture.Line(new Vector2(85f, 75f), new Vector2(85f, 108f), 7f);
			break;
		case ProfileIcon.Close:
			iconTexture.Line(new Vector2(34f, 34f), new Vector2(94f, 94f), 8f);
			iconTexture.Line(new Vector2(94f, 34f), new Vector2(34f, 94f), 8f);
			break;
		case ProfileIcon.Pencil:
			iconTexture.Line(new Vector2(36f, 91f), new Vector2(85f, 42f), 8f);
			iconTexture.Line(new Vector2(83f, 42f), new Vector2(99f, 58f), 8f);
			iconTexture.Line(new Vector2(29f, 100f), new Vector2(53f, 93f), 7f);
			break;
		case ProfileIcon.Crown:
			iconTexture.Line(new Vector2(15f, 85f), new Vector2(28f, 42f), 7f);
			iconTexture.Line(new Vector2(28f, 42f), new Vector2(52f, 68f), 7f);
			iconTexture.Line(new Vector2(52f, 68f), new Vector2(64f, 27f), 7f);
			iconTexture.Line(new Vector2(64f, 27f), new Vector2(76f, 68f), 7f);
			iconTexture.Line(new Vector2(76f, 68f), new Vector2(100f, 42f), 7f);
			iconTexture.Line(new Vector2(100f, 42f), new Vector2(113f, 85f), 7f);
			iconTexture.Line(new Vector2(20f, 94f), new Vector2(108f, 94f), 7f);
			break;
		case ProfileIcon.Check:
			iconTexture.Line(new Vector2(30f, 68f), new Vector2(54f, 92f), 10f);
			iconTexture.Line(new Vector2(54f, 92f), new Vector2(98f, 36f), 10f);
			break;
		}
		Texture2D texture2D = iconTexture.Apply();
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		return Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
	}

	private float EaseOutBack(float value)
	{
		float num = value - 1f;
		return 1f + num * num * (1.8199999f * num + 0.82f);
	}
}
