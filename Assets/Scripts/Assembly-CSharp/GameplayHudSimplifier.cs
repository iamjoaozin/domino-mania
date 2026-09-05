using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GBTemplates.Domino.Model;

public sealed class GameplayHudSimplifier : MonoBehaviour
{
	private const float TurnDurationSeconds = 7f;
	private const string HostName = "GameplayHudSimplifier";
	private const string OverlayName = "Gameplay Clean Match HUD";
	private const string TurnControllerTypeName = "GBTemplates.Domino.Controller.TurnController";
	private const string DominoControllerTypeName = "GBTemplates.Domino.Controller.DominoController";
	private const string NetworkManagerTypeName = "Unity.Netcode.NetworkManager";
	private const string DominoTileViewTypeName = "GBTemplates.Domino.View.DominoTileView";
	private const string HandSlotName = "Lara Hand Tile Slot";
	private const string HandHouseName = "Lara Hand House";
	private const float HandTileScale = 0.92f;
	private static readonly Vector2 HandHouseSize = new Vector2(690f, 164f);
	private const float ChatBubbleLifetime = 3.4f;

	private static GameplayHudSimplifier instance;

	private readonly HashSet<GameObject> hiddenObjects = new HashSet<GameObject>();
	private readonly HashSet<GameObject> transparentHandBackgrounds = new HashSet<GameObject>();
	private readonly HashSet<string> activeHandSlotNames = new HashSet<string>();
	private readonly List<RectTransform> activeBottomHandTiles = new List<RectTransform>(10);
	private RectTransform handHouse;
	private readonly string[] quickChatMessages =
	{
		"Boa!",
		"Bora!",
		"Valeu!",
		"Penso",
		"Joguei",
		"Top!"
	};

	private readonly Vector3[] worldCorners = new Vector3[4];
	private readonly Vector2[] screenCorners = new Vector2[4];
	private Canvas overlayCanvas;
	private CanvasGroup overlayGroup;
	private RectTransform leftAvatarRoot;
	private RectTransform rightAvatarRoot;
	private RectTransform centerBadge;
	private RectTransform leftRail;
	private RectTransform rightRail;
	private Image leftRingFill;
	private Image rightRingFill;
	private Image leftGlow;
	private Image rightGlow;
	private Image leftActiveLine;
	private Image rightActiveLine;
	private Sprite circleSprite;
	private Sprite ringSprite;
	private Sprite roundedSprite;
	private Sprite squareSprite;
	private Sprite spadeSprite;
	private Sprite chatIconSprite;
	private Sprite[] reactionSprites;
	private Sprite avatarFallbackSprite;
	private RectTransform chatButtonRoot;
	private RectTransform quickChatPanel;
	private RectTransform[] quickChatOptionRects;
	private CanvasGroup quickChatGroup;
	private CanvasGroup leftChatBubbleGroup;
	private CanvasGroup rightChatBubbleGroup;
	private RectTransform leftChatBubble;
	private RectTransform rightChatBubble;
	private Image leftChatBubbleIcon;
	private Image rightChatBubbleIcon;
	private TMP_Text leftChatBubbleText;
	private TMP_Text rightChatBubbleText;
	private Component cachedDominoController;
	private Component cachedTurnController;
	private float nextScanTime;
	private float nextControllerLookupTime;
	private float overlayFade;
	private float quickChatOpen;
	private float leftBubbleUntil;
	private float rightBubbleUntil;
	private float pendingBotReplyAt = -1f;
	private float turnStartTime;
	private float lastTurnCountdown = 1f;
	private string pendingBotReply = string.Empty;
	private int pendingBotReplyIndex;
	private bool gameplayVisible;
	private bool hasReliableTurnSide;
	private bool quickChatVisible;
	private int activeSide;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Bootstrap()
	{
		if (instance != null)
		{
			return;
		}

		GameObject host = new GameObject(HostName);
		UnityEngine.Object.DontDestroyOnLoad(host);
		instance = host.AddComponent<GameplayHudSimplifier>();
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void Start()
	{
		turnStartTime = Time.unscaledTime;
		EnsureOverlay();
		ScanScene();
	}

	private void Update()
	{
		EnsureOverlay();
		DismissNoMovePopup();

		if (Time.unscaledTime >= nextScanTime)
		{
			nextScanTime = Time.unscaledTime + 0.18f;
			ScanScene();
		}

		UpdateOverlayFade();

		if (gameplayVisible)
		{
			PolishHandArea();
			UpdateQuickChat();
			UpdateTurnVisual();
		}
		else
		{
			quickChatVisible = false;
			UpdateQuickChat();
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		hiddenObjects.Clear();
		transparentHandBackgrounds.Clear();
		activeHandSlotNames.Clear();
		gameplayVisible = false;
		hasReliableTurnSide = false;
		overlayFade = 0f;
		lastTurnCountdown = 1f;
		leftBubbleUntil = 0f;
		rightBubbleUntil = 0f;
		pendingBotReplyAt = -1f;
		pendingBotReply = string.Empty;
		quickChatVisible = false;
		quickChatOpen = 0f;
		turnStartTime = Time.unscaledTime;
		nextScanTime = 0f;
		nextControllerLookupTime = 0f;
		cachedDominoController = null;
		cachedTurnController = null;

		if (overlayCanvas != null)
		{
			overlayCanvas.gameObject.SetActive(false);
			overlayGroup.alpha = 0f;
		}
	}

	private void EnsureOverlay()
	{
		if (overlayCanvas != null)
		{
			return;
		}

		circleSprite = CreateCircleSprite(192);
		ringSprite = CreateRingSprite(224);
		roundedSprite = CreateRoundedRectSprite(128, 34);
		squareSprite = CreateSquareSprite();
		spadeSprite = CreateSpadeSprite(128);
		chatIconSprite = CreateChatIconSprite(128);
		reactionSprites = CreateReactionSprites(128);
		avatarFallbackSprite = CreateAvatarFallbackSprite(192);

		GameObject canvasObject = new GameObject(OverlayName, typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
		UnityEngine.Object.DontDestroyOnLoad(canvasObject);

		overlayCanvas = canvasObject.GetComponent<Canvas>();
		overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
		overlayCanvas.sortingOrder = 7000;

		CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1080f, 2400f);
		scaler.matchWidthOrHeight = 1f;

		overlayGroup = canvasObject.GetComponent<CanvasGroup>();
		overlayGroup.alpha = 0f;
		overlayGroup.interactable = false;
		overlayGroup.blocksRaycasts = false;

		CreateMatchHeader(canvasObject.transform);
		CreateQuickChat(canvasObject.transform);
		overlayCanvas.gameObject.SetActive(false);
	}

	private void CreateMatchHeader(Transform parent)
	{
		GameObject root = new GameObject("Clean Match Header", typeof(RectTransform));
		root.transform.SetParent(parent, false);

		RectTransform rootRect = root.GetComponent<RectTransform>();
		rootRect.anchorMin = new Vector2(0.5f, 1f);
		rootRect.anchorMax = new Vector2(0.5f, 1f);
		rootRect.pivot = new Vector2(0.5f, 1f);
		rootRect.anchoredPosition = new Vector2(0f, -22f);
		rootRect.sizeDelta = new Vector2(740f, 188f);

		Image backGlow = CreateImage(root.transform, "Header Soft Glow", roundedSprite, new Color(0.65f, 0f, 1f, 0.14f), new Vector2(0f, -72f), new Vector2(720f, 120f));
		backGlow.type = Image.Type.Sliced;

		Image backPanel = CreateImage(root.transform, "Header Glass Panel", roundedSprite, new Color(0.02f, 0f, 0.04f, 0.58f), new Vector2(0f, -72f), new Vector2(690f, 104f));
		backPanel.type = Image.Type.Sliced;

		leftRail = CreateLine(root.transform, "Left Gold Rail", new Vector2(-236f, -72f), new Vector2(212f, 5f), new Color(1f, 0.72f, 0.14f, 0.75f));
		rightRail = CreateLine(root.transform, "Right Gold Rail", new Vector2(236f, -72f), new Vector2(212f, 5f), new Color(1f, 0.72f, 0.14f, 0.75f));
		leftActiveLine = CreateLine(root.transform, "Left Active Rail", new Vector2(-236f, -72f), new Vector2(212f, 9f), new Color(0.22f, 1f, 0.18f, 0.0f)).GetComponent<Image>();
		rightActiveLine = CreateLine(root.transform, "Right Active Rail", new Vector2(236f, -72f), new Vector2(212f, 9f), new Color(0.22f, 1f, 0.18f, 0.0f)).GetComponent<Image>();

		leftAvatarRoot = CreateAvatarChip(root.transform, "Player Avatar", new Vector2(-250f, -72f), true);
		rightAvatarRoot = CreateAvatarChip(root.transform, "Opponent Avatar", new Vector2(250f, -72f), false);
		centerBadge = CreateCenterBadge(root.transform, new Vector2(0f, -72f));

		leftChatBubble = CreateChatBubble(root.transform, "Player Chat Bubble", new Vector2(-250f, -168f), true, out leftChatBubbleGroup, out leftChatBubbleIcon, out leftChatBubbleText);
		rightChatBubble = CreateChatBubble(root.transform, "Opponent Chat Bubble", new Vector2(250f, -168f), false, out rightChatBubbleGroup, out rightChatBubbleIcon, out rightChatBubbleText);
	}

	private RectTransform CreateLine(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
	{
		Image line = CreateImage(parent, name, squareSprite, color, anchoredPosition, size);
		return line.rectTransform;
	}

	private RectTransform CreateAvatarChip(Transform parent, string name, Vector2 anchoredPosition, bool localPlayer)
	{
		GameObject root = new GameObject(name, typeof(RectTransform));
		root.transform.SetParent(parent, false);

		RectTransform rootRect = root.GetComponent<RectTransform>();
		rootRect.anchorMin = new Vector2(0.5f, 0.5f);
		rootRect.anchorMax = new Vector2(0.5f, 0.5f);
		rootRect.pivot = new Vector2(0.5f, 0.5f);
		rootRect.anchoredPosition = anchoredPosition;
		rootRect.sizeDelta = new Vector2(156f, 156f);

		Image softGlow = CreateImage(root.transform, "Avatar Glow", circleSprite, new Color(1f, 0.67f, 0.12f, 0.18f), Vector2.zero, new Vector2(172f, 172f));
		Image outerGold = CreateImage(root.transform, "Outer Gold", ringSprite, new Color(1f, 0.72f, 0.16f, 0.86f), Vector2.zero, new Vector2(154f, 154f));
		Image ringFill = CreateImage(root.transform, "Turn Progress Ring", ringSprite, new Color(0.25f, 1f, 0.14f, 1f), Vector2.zero, new Vector2(164f, 164f));
		ringFill.type = Image.Type.Filled;
		ringFill.fillMethod = Image.FillMethod.Radial360;
		ringFill.fillOrigin = (int)Image.Origin360.Top;
		ringFill.fillClockwise = false;
		ringFill.fillAmount = 1f;

		Image avatarBack = CreateImage(root.transform, "Avatar Back", circleSprite, new Color(0.025f, 0.003f, 0.035f, 0.98f), Vector2.zero, new Vector2(126f, 126f));

		GameObject maskObject = new GameObject("Avatar Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
		maskObject.transform.SetParent(root.transform, false);
		RectTransform maskRect = maskObject.GetComponent<RectTransform>();
		maskRect.anchorMin = new Vector2(0.5f, 0.5f);
		maskRect.anchorMax = new Vector2(0.5f, 0.5f);
		maskRect.pivot = new Vector2(0.5f, 0.5f);
		maskRect.anchoredPosition = Vector2.zero;
		maskRect.sizeDelta = new Vector2(112f, 112f);

		Image maskImage = maskObject.GetComponent<Image>();
		maskImage.sprite = circleSprite;
		maskImage.color = Color.white;
		maskImage.raycastTarget = false;
		maskObject.GetComponent<Mask>().showMaskGraphic = false;

		Image avatar = CreateImage(maskObject.transform, "Avatar Image", localPlayer ? LoadLocalAvatarSprite() : avatarFallbackSprite, Color.white, Vector2.zero, new Vector2(112f, 112f));
		avatar.preserveAspect = true;

		Image shine = CreateImage(root.transform, "Avatar Shine", circleSprite, new Color(1f, 1f, 1f, 0.09f), new Vector2(-24f, 24f), new Vector2(82f, 44f));
		shine.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);

		DisableRaycasts(softGlow, outerGold, ringFill, avatarBack, avatar, shine);

		if (localPlayer)
		{
			leftGlow = softGlow;
			leftRingFill = ringFill;
		}
		else
		{
			rightGlow = softGlow;
			rightRingFill = ringFill;
		}

		return rootRect;
	}

	private RectTransform CreateCenterBadge(Transform parent, Vector2 anchoredPosition)
	{
		GameObject badge = new GameObject("Turn Spade Badge", typeof(RectTransform));
		badge.transform.SetParent(parent, false);

		RectTransform rect = badge.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = new Vector2(112f, 112f);

		Image glow = CreateImage(badge.transform, "Badge Glow", circleSprite, new Color(1f, 0.68f, 0.1f, 0.24f), Vector2.zero, new Vector2(112f, 112f));
		Image back = CreateImage(badge.transform, "Badge Back", circleSprite, new Color(0.035f, 0.008f, 0.035f, 0.98f), Vector2.zero, new Vector2(82f, 82f));
		Image border = CreateImage(badge.transform, "Badge Ring", ringSprite, new Color(1f, 0.78f, 0.18f, 0.9f), Vector2.zero, new Vector2(88f, 88f));
		Image spade = CreateImage(badge.transform, "Badge Spade", spadeSprite, new Color(1f, 0.86f, 0.22f, 1f), Vector2.zero, new Vector2(44f, 44f));

		DisableRaycasts(glow, back, border, spade);
		return rect;
	}

	private RectTransform CreateChatBubble(Transform parent, string name, Vector2 anchoredPosition, bool localPlayer, out CanvasGroup group, out Image icon, out TMP_Text text)
	{
		GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
		root.transform.SetParent(parent, false);

		RectTransform rect = root.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = new Vector2(214f, 78f);

		group = root.GetComponent<CanvasGroup>();
		group.alpha = 0f;
		group.interactable = false;
		group.blocksRaycasts = false;

		Image glow = CreateImage(root.transform, "Bubble Glow", roundedSprite, localPlayer ? new Color(0.84f, 0f, 1f, 0.18f) : new Color(1f, 0.72f, 0.12f, 0.18f), Vector2.zero, new Vector2(232f, 94f));
		glow.type = Image.Type.Sliced;

		Image back = CreateImage(root.transform, "Bubble Back", roundedSprite, localPlayer ? new Color(0.08f, 0f, 0.12f, 0.92f) : new Color(0.10f, 0.055f, 0f, 0.92f), Vector2.zero, new Vector2(214f, 78f));
		back.type = Image.Type.Sliced;

		Image edge = CreateImage(root.transform, "Bubble Edge", roundedSprite, localPlayer ? new Color(0.95f, 0.13f, 1f, 0.72f) : new Color(1f, 0.76f, 0.14f, 0.72f), Vector2.zero, new Vector2(218f, 82f));
		edge.type = Image.Type.Sliced;
		edge.transform.SetAsFirstSibling();

		Image tail = CreateImage(root.transform, "Bubble Tail", squareSprite, localPlayer ? new Color(0.95f, 0.13f, 1f, 0.82f) : new Color(1f, 0.76f, 0.14f, 0.82f), localPlayer ? new Vector2(-48f, 38f) : new Vector2(48f, 38f), new Vector2(28f, 28f));
		tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

		Sprite defaultIcon = reactionSprites != null && reactionSprites.Length > 0 ? reactionSprites[0] : circleSprite;
		icon = CreateImage(root.transform, "Bubble Icon", defaultIcon, Color.white, new Vector2(-62f, 0f), new Vector2(46f, 46f));
		text = CreateText(root.transform, "Bubble Text", string.Empty, 26f, FontStyles.Bold, TextAlignmentOptions.Left, Color.white, new Vector2(34f, -1f), new Vector2(118f, 52f));
		DisableRaycasts(glow, back, edge, tail, icon, text);
		return rect;
	}

	private void CreateQuickChat(Transform parent)
	{
		GameObject button = new GameObject("Clean Quick Chat Button", typeof(RectTransform));
		button.transform.SetParent(parent, false);
		chatButtonRoot = button.GetComponent<RectTransform>();
		chatButtonRoot.anchorMin = new Vector2(1f, 0f);
		chatButtonRoot.anchorMax = new Vector2(1f, 0f);
		chatButtonRoot.pivot = new Vector2(1f, 0f);
		chatButtonRoot.anchoredPosition = new Vector2(-34f, 520f);
		chatButtonRoot.sizeDelta = new Vector2(76f, 76f);

		Image buttonGlow = CreateImage(button.transform, "Chat Button Glow", circleSprite, new Color(0.88f, 0f, 1f, 0.22f), Vector2.zero, new Vector2(92f, 92f));
		Image buttonBack = CreateImage(button.transform, "Chat Button Back", circleSprite, new Color(0.045f, 0f, 0.065f, 0.95f), Vector2.zero, new Vector2(76f, 76f));
		Image buttonRing = CreateImage(button.transform, "Chat Button Ring", ringSprite, new Color(1f, 0.75f, 0.12f, 0.88f), Vector2.zero, new Vector2(82f, 82f));
		Image icon = CreateImage(button.transform, "Chat Button Icon", chatIconSprite, new Color(1f, 0.91f, 0.35f, 1f), Vector2.zero, new Vector2(42f, 42f));
		DisableRaycasts(buttonGlow, buttonBack, buttonRing, icon);

		GameObject panel = new GameObject("Clean Quick Chat Panel", typeof(RectTransform), typeof(CanvasGroup));
		panel.transform.SetParent(parent, false);
		quickChatPanel = panel.GetComponent<RectTransform>();
		quickChatPanel.anchorMin = new Vector2(1f, 0f);
		quickChatPanel.anchorMax = new Vector2(1f, 0f);
		quickChatPanel.pivot = new Vector2(1f, 0f);
		quickChatPanel.anchoredPosition = new Vector2(-34f, 612f);
		quickChatPanel.sizeDelta = new Vector2(420f, 178f);

		quickChatGroup = panel.GetComponent<CanvasGroup>();
		quickChatGroup.alpha = 0f;
		quickChatGroup.interactable = false;
		quickChatGroup.blocksRaycasts = false;

		Image panelGlow = CreateImage(panel.transform, "Quick Chat Glow", roundedSprite, new Color(0.78f, 0f, 1f, 0.13f), Vector2.zero, new Vector2(438f, 196f));
		panelGlow.type = Image.Type.Sliced;
		Image panelBack = CreateImage(panel.transform, "Quick Chat Back", roundedSprite, new Color(0.022f, 0f, 0.04f, 0.92f), Vector2.zero, new Vector2(420f, 178f));
		panelBack.type = Image.Type.Sliced;

		quickChatOptionRects = new RectTransform[quickChatMessages.Length];
		for (int i = 0; i < quickChatMessages.Length; i++)
		{
			int column = i % 3;
			int row = i / 3;
			Vector2 position = new Vector2(-136f + column * 136f, 40f - row * 78f);
			quickChatOptionRects[i] = CreateQuickChatOption(panel.transform, i, quickChatMessages[i], position);
		}
	}

	private RectTransform CreateQuickChatOption(Transform parent, int index, string label, Vector2 anchoredPosition)
	{
		GameObject option = new GameObject("Quick Chat Option " + index, typeof(RectTransform));
		option.transform.SetParent(parent, false);

		RectTransform rect = option.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = new Vector2(108f, 64f);

		Image glow = CreateImage(option.transform, "Option Glow", roundedSprite, new Color(1f, 0.72f, 0.14f, 0.13f), Vector2.zero, new Vector2(118f, 74f));
		glow.type = Image.Type.Sliced;
		Image back = CreateImage(option.transform, "Option Back", roundedSprite, new Color(0.07f, 0f, 0.1f, 0.95f), Vector2.zero, new Vector2(108f, 64f));
		back.type = Image.Type.Sliced;
		Image edge = CreateImage(option.transform, "Option Edge", roundedSprite, new Color(0.94f, 0.08f, 1f, 0.58f), Vector2.zero, new Vector2(110f, 66f));
		edge.type = Image.Type.Sliced;
		edge.transform.SetAsFirstSibling();

		Sprite optionSprite = reactionSprites != null && reactionSprites.Length > 0 ? reactionSprites[index % reactionSprites.Length] : circleSprite;
		Image optionIcon = CreateImage(option.transform, "Option Icon", optionSprite, Color.white, new Vector2(0f, 10f), new Vector2(36f, 36f));
		TMP_Text text = CreateText(option.transform, "Option Text", label, 15f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, new Vector2(0f, -23f), new Vector2(92f, 24f));
		DisableRaycasts(glow, back, edge, optionIcon, text);
		return rect;
	}

	private TMP_Text CreateText(Transform parent, string name, string value, float size, FontStyles style, TextAlignmentOptions alignment, Color color, Vector2 anchoredPosition, Vector2 dimensions)
	{
		GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		textObject.transform.SetParent(parent, false);

		RectTransform rect = textObject.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = dimensions;

		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.text = value;
		text.fontSize = size;
		text.fontStyle = style;
		text.alignment = alignment;
		text.color = color;
		text.enableWordWrapping = true;
		text.raycastTarget = false;
		if (TMP_Settings.defaultFontAsset != null)
		{
			text.font = TMP_Settings.defaultFontAsset;
		}

		return text;
	}

	private Image CreateImage(Transform parent, string name, Sprite sprite, Color color, Vector2 anchoredPosition, Vector2 size)
	{
		GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
		imageObject.transform.SetParent(parent, false);

		RectTransform rect = imageObject.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = size;

		Image image = imageObject.GetComponent<Image>();
		image.sprite = sprite;
		image.color = color;
		image.raycastTarget = false;
		return image;
	}

	private void DisableRaycasts(params Graphic[] graphics)
	{
		for (int i = 0; i < graphics.Length; i++)
		{
			if (graphics[i] != null)
			{
				graphics[i].raycastTarget = false;
			}
		}
	}

	private void ScanScene()
	{
		if (overlayCanvas == null)
		{
			return;
		}

		int sideHint;
		gameplayVisible = DetectGameplayVisible(out sideHint);
		HideLegacyMatchHudEverywhere();

		if (gameplayVisible && !overlayCanvas.gameObject.activeSelf)
		{
			overlayCanvas.gameObject.SetActive(true);
		}

		if (!gameplayVisible)
		{
			return;
		}

		HideOldHudByName();
		HideOldHudByText();
		HideTurnBanners();
		HideLegacyChatUi();
	}

	private void PolishHandArea()
	{
		HideHandBackground();
		activeHandSlotNames.Clear();
		activeBottomHandTiles.Clear();

		MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
		for (int i = 0; i < behaviours.Length; i++)
		{
			MonoBehaviour behaviour = behaviours[i];
			if (behaviour == null || behaviour.GetType().FullName != DominoTileViewTypeName || !CanTouch(behaviour.gameObject))
			{
				continue;
			}

			RectTransform tileRect = behaviour.transform as RectTransform;
			if (tileRect == null || !IsBottomHandTile(tileRect))
			{
				continue;
			}

			ApplyHandTileScale(tileRect);
			activeBottomHandTiles.Add(tileRect);
		}

		EnsureHandHouse(activeBottomHandTiles);
		RemoveOrphanHandSlots();
	}

	private void HideHandBackground()
	{
		SetHandBackgroundTransparent("BottomTokens - UI");
		SetHandBackgroundTransparent("BottomTokens");
		SetHandBackgroundTransparent("TileCollectionsView - UI");
	}

	private void SetHandBackgroundTransparent(string objectName)
	{
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
		for (int i = 0; i < objects.Length; i++)
		{
			GameObject go = objects[i];
			if (go == null || go.name != objectName || !CanTouch(go))
			{
				continue;
			}

			Graphic graphic = go.GetComponent<Graphic>();
			if (graphic == null || transparentHandBackgrounds.Contains(go))
			{
				continue;
			}

			Color color = graphic.color;
			color.a = 0f;
			graphic.color = color;
			graphic.raycastTarget = false;
			transparentHandBackgrounds.Add(go);
		}
	}

	private bool IsBottomHandTile(RectTransform rect)
	{
		if (!TryGetScreenBounds(rect, out Rect bounds))
		{
			return false;
		}

		if (bounds.center.y > Screen.height * 0.36f)
		{
			return false;
		}

		Transform current = rect;
		for (int i = 0; i < 8 && current != null; i++)
		{
			string name = current.name;
			if (name == "BottomTokens" || name == "BottomTokens - UI" || name == "TileCollectionsView - UI" || name == "AllTokens - UI")
			{
				return true;
			}

			current = current.parent;
		}

		return bounds.center.y <= Screen.height * 0.22f;
	}

	private void ApplyHandTileScale(RectTransform tileRect)
	{
		if (Mathf.Abs(tileRect.localScale.x - HandTileScale) > 0.02f ||
			Mathf.Abs(tileRect.localScale.y - HandTileScale) > 0.02f)
		{
			tileRect.localScale = new Vector3(HandTileScale, HandTileScale, tileRect.localScale.z);
		}
	}

	private void EnsureHandTileSlot(RectTransform tileRect)
	{
		RectTransform slot = null;
		string slotName = HandSlotName + " " + tileRect.GetInstanceID();
		Transform parent = tileRect.parent;
		if (parent != null)
		{
			for (int i = 0; i < parent.childCount; i++)
			{
				Transform child = parent.GetChild(i);
				if (child != null && child.name == slotName)
				{
					slot = child as RectTransform;
					break;
				}
			}
		}

		if (slot == null)
		{
			GameObject slotObject = new GameObject(slotName, typeof(RectTransform), typeof(Image), typeof(Outline));
			slotObject.transform.SetParent(parent != null ? parent : tileRect, false);
			slot = slotObject.GetComponent<RectTransform>();
			Image slotImage = slotObject.GetComponent<Image>();
			slotImage.sprite = roundedSprite;
			slotImage.type = Image.Type.Sliced;
			slotImage.color = new Color(0.025f, 0f, 0.04f, 0.58f);
			slotImage.raycastTarget = false;

			Outline outline = slotObject.GetComponent<Outline>();
			outline.effectColor = new Color(1f, 0.74f, 0.18f, 0.72f);
			outline.effectDistance = new Vector2(1.4f, -1.4f);

			Shadow shadow = slotObject.AddComponent<Shadow>();
			shadow.effectColor = new Color(0.85f, 0f, 1f, 0.34f);
			shadow.effectDistance = new Vector2(0f, -2.5f);
			shadow.useGraphicAlpha = false;

			GameObject glowObject = new GameObject("Glow", typeof(RectTransform), typeof(Image));
			glowObject.transform.SetParent(slotObject.transform, false);
			RectTransform createdGlowRect = glowObject.GetComponent<RectTransform>();
			createdGlowRect.anchorMin = new Vector2(0.5f, 0.5f);
			createdGlowRect.anchorMax = new Vector2(0.5f, 0.5f);
			createdGlowRect.pivot = new Vector2(0.5f, 0.5f);
			createdGlowRect.anchoredPosition = Vector2.zero;
			Image glow = glowObject.GetComponent<Image>();
			glow.sprite = roundedSprite;
			glow.type = Image.Type.Sliced;
			glow.color = new Color(0.84f, 0f, 1f, 0.18f);
			glow.raycastTarget = false;
		}

		slot.anchorMin = tileRect.anchorMin;
		slot.anchorMax = tileRect.anchorMax;
		slot.pivot = tileRect.pivot;
		slot.anchoredPosition = tileRect.anchoredPosition;
		slot.localRotation = tileRect.localRotation;
		slot.localScale = Vector3.one;
		slot.sizeDelta = new Vector2(Mathf.Max(54f, tileRect.rect.width * HandTileScale + 12f), Mathf.Max(104f, tileRect.rect.height * HandTileScale + 12f));
		slot.SetSiblingIndex(Mathf.Max(0, tileRect.GetSiblingIndex() - 1));

		RectTransform glowRect = slot.Find("Glow") as RectTransform;
		if (glowRect != null)
		{
			glowRect.sizeDelta = slot.sizeDelta + new Vector2(18f, 16f);
		}

		activeHandSlotNames.Add(slotName);
	}

	private void EnsureHandHouse(IReadOnlyList<RectTransform> tiles)
	{
		if (tiles == null || tiles.Count == 0)
		{
			HideHandHouse();
			return;
		}

		RectTransform parent = null;
		for (int i = 0; i < tiles.Count; i++)
		{
			if (tiles[i] != null && tiles[i].parent is RectTransform tileParent)
			{
				parent = tileParent;
				break;
			}
		}

		if (parent == null)
		{
			return;
		}

		RectTransform house = GetOrCreateSingleHandHouse(parent);
		if (house == null)
		{
			GameObject houseObject = new GameObject(HandHouseName, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
			houseObject.transform.SetParent(parent, false);
			house = houseObject.GetComponent<RectTransform>();
			house.anchorMin = new Vector2(0.5f, 0.5f);
			house.anchorMax = new Vector2(0.5f, 0.5f);
			house.pivot = new Vector2(0.5f, 0.5f);

			Image back = houseObject.GetComponent<Image>();
			back.sprite = roundedSprite;
			back.type = Image.Type.Sliced;
			back.color = new Color(0.025f, 0.004f, 0.045f, 0.82f);
			back.raycastTarget = false;

			Outline outline = houseObject.GetComponent<Outline>();
			outline.effectColor = new Color(1f, 0.72f, 0.15f, 0.78f);
			outline.effectDistance = new Vector2(2f, -2f);

			Shadow shadow = houseObject.GetComponent<Shadow>();
			shadow.effectColor = new Color(0.9f, 0f, 1f, 0.32f);
			shadow.effectDistance = new Vector2(0f, -4f);
			shadow.useGraphicAlpha = false;

			CreateHouseChild(house, "House Glow", roundedSprite, new Color(0.92f, 0f, 1f, 0.16f), Vector2.zero, Vector2.zero, true);
			CreateHouseChild(house, "House Inner Felt", roundedSprite, new Color(0.11f, 0.02f, 0.14f, 0.42f), Vector2.zero, Vector2.zero, true);
			CreateHouseChild(house, "House Top Rail", squareSprite, new Color(1f, 0.76f, 0.18f, 0.86f), Vector2.zero, Vector2.zero, false);
			CreateHouseChild(house, "House Bottom Rail", squareSprite, new Color(0.98f, 0.08f, 1f, 0.72f), Vector2.zero, Vector2.zero, false);
			handHouse = house;
		}

		if (!TryGetLocalTileBounds(parent, tiles, out Vector2 min, out Vector2 max))
		{
			return;
		}

		Vector2 tileCenter = (min + max) * 0.5f;
		Vector2 size = HandHouseSize;
		if (parent.rect.width > 0f)
		{
			size.x = Mathf.Min(size.x, Mathf.Max(320f, parent.rect.width - 24f));
		}

		house.gameObject.SetActive(true);
		house.localRotation = Quaternion.identity;
		house.localScale = Vector3.one;
		house.anchorMin = new Vector2(0.5f, 0.5f);
		house.anchorMax = new Vector2(0.5f, 0.5f);
		house.pivot = new Vector2(0.5f, 0.5f);
		house.localPosition = new Vector3(parent.rect.center.x, tileCenter.y - 2f, 0f);
		house.sizeDelta = size;
		house.SetSiblingIndex(0);

		UpdateHouseChild(house, "House Glow", Vector2.zero, size + new Vector2(48f, 32f));
		UpdateHouseChild(house, "House Inner Felt", new Vector2(0f, -1f), size - new Vector2(24f, 22f));
		UpdateHouseChild(house, "House Top Rail", new Vector2(0f, size.y * 0.5f - 13f), new Vector2(size.x - 64f, 5f));
		UpdateHouseChild(house, "House Bottom Rail", new Vector2(0f, -size.y * 0.5f + 13f), new Vector2(size.x - 64f, 5f));
	}

	private RectTransform GetOrCreateSingleHandHouse(RectTransform parent)
	{
		RectTransform keeper = handHouse;
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
		for (int i = 0; i < objects.Length; i++)
		{
			GameObject go = objects[i];
			if (go == null || go.name != HandHouseName || !CanTouch(go))
			{
				continue;
			}

			RectTransform candidate = go.transform as RectTransform;
			if (keeper == null)
			{
				keeper = candidate;
				continue;
			}

			if (candidate != keeper)
			{
				UnityEngine.Object.Destroy(go);
			}
		}

		if (keeper != null && keeper.parent != parent)
		{
			keeper.SetParent(parent, false);
		}

		handHouse = keeper;
		return keeper;
	}

	private void HideHandHouse()
	{
		if (handHouse != null)
		{
			handHouse.gameObject.SetActive(false);
		}

		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
		for (int i = 0; i < objects.Length; i++)
		{
			GameObject go = objects[i];
			if (go != null && go.name == HandHouseName && CanTouch(go))
			{
				go.SetActive(false);
			}
		}
	}

	private static RectTransform FindChildRect(RectTransform parent, string childName)
	{
		if (parent == null)
		{
			return null;
		}

		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if (child != null && child.name == childName)
			{
				return child as RectTransform;
			}
		}

		return null;
	}

	private Image CreateHouseChild(RectTransform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size, bool sliced)
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
		image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
		image.color = color;
		image.raycastTarget = false;
		return image;
	}

	private static void UpdateHouseChild(RectTransform parent, string name, Vector2 position, Vector2 size)
	{
		RectTransform child = FindChildRect(parent, name);
		if (child == null)
		{
			return;
		}

		child.anchoredPosition = position;
		child.sizeDelta = size;
	}

	private bool TryGetLocalTileBounds(RectTransform parent, IReadOnlyList<RectTransform> tiles, out Vector2 min, out Vector2 max)
	{
		min = new Vector2(float.MaxValue, float.MaxValue);
		max = new Vector2(float.MinValue, float.MinValue);
		bool found = false;

		for (int i = 0; i < tiles.Count; i++)
		{
			RectTransform tile = tiles[i];
			if (tile == null || !tile.gameObject.activeInHierarchy)
			{
				continue;
			}

			tile.GetWorldCorners(worldCorners);
			for (int j = 0; j < worldCorners.Length; j++)
			{
				Vector3 local = parent.InverseTransformPoint(worldCorners[j]);
				min.x = Mathf.Min(min.x, local.x);
				min.y = Mathf.Min(min.y, local.y);
				max.x = Mathf.Max(max.x, local.x);
				max.y = Mathf.Max(max.y, local.y);
				found = true;
			}
		}

		return found;
	}

	private void RemoveOrphanHandSlots()
	{
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
		for (int i = 0; i < objects.Length; i++)
		{
			GameObject go = objects[i];
			if (go == null || !go.name.StartsWith(HandSlotName) || !CanTouch(go) || activeHandSlotNames.Contains(go.name))
			{
				continue;
			}

			UnityEngine.Object.Destroy(go);
		}
	}

	private void HideLegacyMatchHudEverywhere()
	{
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

		for (int i = 0; i < objects.Length; i++)
		{
			GameObject go = objects[i];
			if (go == null || !CanTouch(go))
			{
				continue;
			}

			string name = go.name;
			if (name == "Premium Match HUD" ||
				name == "Bot Premium HUD" ||
				name == "Voce Premium HUD" ||
				name == "PlayerPanel" ||
				name == "PlayerIAPanel" ||
				name == "Player2ViewInfo" ||
				name == "Timer360" ||
				name == "Timer360Fill" ||
				name == "TextTimer")
			{
				HideBlock(go);
			}
		}
	}

	private bool DetectGameplayVisible(out int sideHint)
	{
		sideHint = -1;

		if (IsMainMenuVisible())
		{
			return false;
		}

		int visibleDominoSlots = CountVisibleNamedUi("Domino_UISlot_OnGame");
		bool hasVisibleHand = visibleDominoSlots >= 2 || IsNamedUiVisible("BottomTokens - UI") || IsNamedUiVisible("TileCollectionsView - UI");
		bool hasVisibleTimer = IsNamedUiVisible("Timer360") || IsNamedUiVisible("TextTimer");
		bool hasVisiblePlayers = IsNamedUiVisible("PlayerPanel") || IsNamedUiVisible("PlayerIAPanel") || IsNamedUiVisible("Player2ViewInfo");
		bool hasVisibleBoardLabel = IsNamedUiVisible("BoardTXT") || IsNamedUiVisible("BoneyardTXT");
		int gameplayTextSignals = ScanGameplayTextSignals(out sideHint);

		if (hasVisibleHand)
		{
			return true;
		}

		if (hasVisibleTimer && hasVisiblePlayers)
		{
			return true;
		}

		return hasVisiblePlayers && (hasVisibleBoardLabel || gameplayTextSignals >= 2);
	}

	private int ScanGameplayTextSignals(out int sideHint)
	{
		sideHint = -1;
		int signals = 0;
		TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();

		for (int i = 0; i < texts.Length; i++)
		{
			TMP_Text text = texts[i];
			if (text == null || !CanTouch(text.gameObject) || !IsVisible(text))
			{
				continue;
			}

			string value = Normalize(text.text);
			if (value.Contains("SUA VEZ"))
			{
				signals++;
				sideHint = GetSideFromScreenPosition(text.rectTransform);
			}
			else if (value.Contains("AGUARD") || value.Contains("VAI RESPONDER"))
			{
				signals++;
			}
			else if (value.Contains("TEMPO") || value.Contains("PARTIDA") || value == "MESA" || value.Contains("MONTE") || value.Contains("PECAS") || value.StartsWith("P:"))
			{
				signals++;
			}
		}

		return signals;
	}

	private bool IsMainMenuVisible()
	{
		if (IsNamedUiVisible("QuickPlay - Btn") ||
			IsNamedUiVisible("Playocal - Btn") ||
			IsNamedUiVisible("presente") ||
			IsNamedUiVisible("config 1") ||
			IsNamedUiVisible("mensagens") ||
			IsNamedUiVisible("ConnectPrivate - Btn "))
		{
			return true;
		}

		TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
		for (int i = 0; i < texts.Length; i++)
		{
			TMP_Text text = texts[i];
			if (text == null || !CanTouch(text.gameObject) || !IsVisible(text))
			{
				continue;
			}

			string value = Normalize(text.text);
			if (value == "JOGAR" || value.Contains("ENTRAR NA SALA") || value.Contains("CRIAR SALA") || value == "LOJA" || value.Contains("TREINAR IA"))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsNamedUiVisible(string objectName)
	{
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
		for (int i = 0; i < objects.Length; i++)
		{
			GameObject go = objects[i];
			if (go == null || go.name != objectName || !CanTouch(go))
			{
				continue;
			}

			if (HasVisibleGraphic(go))
			{
				return true;
			}
		}

		return false;
	}

	private int CountVisibleNamedUi(string namePrefix)
	{
		int count = 0;
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

		for (int i = 0; i < objects.Length; i++)
		{
			GameObject go = objects[i];
			if (go == null || !go.name.StartsWith(namePrefix) || !CanTouch(go))
			{
				continue;
			}

			if (HasVisibleGraphic(go))
			{
				count++;
				if (count >= 2)
				{
					return count;
				}
			}
		}

		return count;
	}

	private bool HasVisibleGraphic(GameObject target)
	{
		Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
		for (int i = 0; i < graphics.Length; i++)
		{
			if (graphics[i] != null && IsVisible(graphics[i]))
			{
				return true;
			}
		}

		return false;
	}

	private void HideOldHudByName()
	{
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

		for (int i = 0; i < objects.Length; i++)
		{
			GameObject go = objects[i];
			if (go == null || !CanTouch(go))
			{
				continue;
			}

			string name = go.name;
			if (name == "Timer360" ||
				name == "Timer360Fill" ||
				name == "TextTimer" ||
				name == "PlayerPanel" ||
				name == "PlayerIAPanel" ||
				name == "Player2ViewInfo" ||
				name == "PlayerScoreTxt" ||
				name == "PlayerNameTxt" ||
				name == "IAName" ||
				name == "IAScore" ||
				name == "BoardTXT" ||
				name == "BoneyardTXT" ||
				name == "Boneyard - UI" ||
				name == "SelectionFeedback")
			{
				HideBlock(go);
			}
		}
	}

	private void HideOldHudByText()
	{
		TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();

		for (int i = 0; i < texts.Length; i++)
		{
			TMP_Text text = texts[i];
			if (text == null || !CanTouch(text.gameObject) || !IsVisible(text))
			{
				continue;
			}

			string value = Normalize(text.text);
			if (value.Contains("TEMPO") ||
				value.Contains("PARTIDA") ||
				value == "MESA" ||
				value.Contains("MONTE") ||
				value.Contains("BONEYARD") ||
				value.Contains("PECAS") ||
				value.StartsWith("P:") ||
				value.StartsWith("V:") ||
				value.Contains("SUA VEZ") ||
				value.Contains("AGUARD") ||
				value.Contains("VAI RESPONDER"))
			{
				HideAncestorPanel(text.rectTransform);
			}
		}
	}

	private void HideTurnBanners()
	{
		TurnBannerAnim[] banners = Resources.FindObjectsOfTypeAll<TurnBannerAnim>();
		for (int i = 0; i < banners.Length; i++)
		{
			if (banners[i] == null || !CanTouch(banners[i].gameObject))
			{
				continue;
			}

			HideBlock(banners[i].gameObject);
		}
	}

	private void HideLegacyChatUi()
	{
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
		for (int i = 0; i < objects.Length; i++)
		{
			GameObject go = objects[i];
			if (go == null || !CanTouch(go))
			{
				continue;
			}

			string name = Normalize(go.name);
			if ((name.Contains("CHAT") || name.Contains("BATE PAPO") || name.Contains("MENSAGEM")) && HasVisibleGraphic(go))
			{
				RectTransform rect = go.transform as RectTransform;
				if (rect != null)
				{
					HideAncestorPanel(rect);
				}
				else
				{
					HideBlock(go);
				}
			}
		}

		TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
		for (int i = 0; i < texts.Length; i++)
		{
			TMP_Text text = texts[i];
			if (text == null || !CanTouch(text.gameObject) || !IsVisible(text))
			{
				continue;
			}

			string value = Normalize(text.text);
			if (value == "CHAT" || value.Contains("CHAT") || value.Contains("BATE PAPO"))
			{
				HideAncestorPanel(text.rectTransform);
			}
		}
	}

	private void HideAncestorPanel(RectTransform start)
	{
		if (start == null)
		{
			return;
		}

		RectTransform current = start;
		RectTransform candidate = start;

		for (int i = 0; i < 8 && current != null; i++)
		{
			float width = Mathf.Abs(current.rect.width);
			float height = Mathf.Abs(current.rect.height);

			if (width >= 40f && width <= 820f && height >= 16f && height <= 290f)
			{
				candidate = current;
			}

			if (current.GetComponent<Canvas>() != null)
			{
				break;
			}

			current = current.parent as RectTransform;
		}

		HideBlock(candidate.gameObject);
	}

	private void HideBlock(GameObject target)
	{
		if (target == null || hiddenObjects.Contains(target) || !CanTouch(target))
		{
			return;
		}

		CanvasGroup group = target.GetComponent<CanvasGroup>();
		if (group == null)
		{
			group = target.AddComponent<CanvasGroup>();
		}

		group.alpha = 0f;
		group.interactable = false;
		group.blocksRaycasts = false;

		Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
		for (int i = 0; i < graphics.Length; i++)
		{
			graphics[i].raycastTarget = false;
		}

		hiddenObjects.Add(target);
	}

	private bool CanTouch(GameObject target)
	{
		if (target == null || !target.scene.IsValid() || !target.scene.isLoaded || !target.activeInHierarchy)
		{
			return false;
		}

		Transform root = target.transform.root;
		if (target.name == HostName || target.name == OverlayName || root.name == HostName || root.name == OverlayName)
		{
			return false;
		}

		return target.hideFlags == HideFlags.None;
	}

	private bool IsVisible(Graphic graphic)
	{
		if (graphic == null || !graphic.enabled || !CanTouch(graphic.gameObject) || IsHiddenBySimplifier(graphic.transform))
		{
			return false;
		}

		if (!IsRectOnScreen(graphic.rectTransform))
		{
			return false;
		}

		float alpha = graphic.color.a * graphic.canvasRenderer.GetAlpha() * GetCanvasGroupAlpha(graphic.transform);
		return alpha > 0.045f;
	}

	private bool IsRectOnScreen(RectTransform rect)
	{
		Rect bounds;
		if (!TryGetScreenBounds(rect, out bounds))
		{
			return false;
		}

		return bounds.xMax >= -40f &&
			bounds.xMin <= Screen.width + 40f &&
			bounds.yMax >= -40f &&
			bounds.yMin <= Screen.height + 40f &&
			bounds.width > 1f &&
			bounds.height > 1f;
	}

	private bool TryGetScreenBounds(RectTransform rect, out Rect bounds)
	{
		bounds = default(Rect);
		if (rect == null)
		{
			return false;
		}

		Canvas canvas = rect.GetComponentInParent<Canvas>();
		Camera camera = null;
		if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
		{
			camera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
		}

		rect.GetWorldCorners(worldCorners);
		for (int i = 0; i < 4; i++)
		{
			screenCorners[i] = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay
				? new Vector2(worldCorners[i].x, worldCorners[i].y)
				: RectTransformUtility.WorldToScreenPoint(camera, worldCorners[i]);
		}

		float minX = screenCorners[0].x;
		float maxX = screenCorners[0].x;
		float minY = screenCorners[0].y;
		float maxY = screenCorners[0].y;

		for (int i = 1; i < 4; i++)
		{
			minX = Mathf.Min(minX, screenCorners[i].x);
			maxX = Mathf.Max(maxX, screenCorners[i].x);
			minY = Mathf.Min(minY, screenCorners[i].y);
			maxY = Mathf.Max(maxY, screenCorners[i].y);
		}

		if (float.IsNaN(minX) || float.IsNaN(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
		{
			return false;
		}

		bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
		return true;
	}

	private float GetCanvasGroupAlpha(Transform start)
	{
		float alpha = 1f;
		Transform current = start;

		while (current != null)
		{
			CanvasGroup group = current.GetComponent<CanvasGroup>();
			if (group != null)
			{
				alpha *= group.alpha;
				if (alpha <= 0.04f)
				{
					return 0f;
				}
			}

			current = current.parent;
		}

		return alpha;
	}

	private bool IsHiddenBySimplifier(Transform start)
	{
		Transform current = start;

		while (current != null)
		{
			if (hiddenObjects.Contains(current.gameObject))
			{
				return true;
			}

			current = current.parent;
		}

		return false;
	}

	private int GetSideFromScreenPosition(RectTransform rect)
	{
		Rect bounds;
		if (!TryGetScreenBounds(rect, out bounds))
		{
			return -1;
		}

		return bounds.center.x >= Screen.width * 0.5f ? 1 : 0;
	}

	private void UpdateOverlayFade()
	{
		if (overlayGroup == null || overlayCanvas == null)
		{
			return;
		}

		float target = gameplayVisible ? 1f : 0f;
		overlayFade = Mathf.MoveTowards(overlayFade, target, Time.unscaledDeltaTime * 7f);
		overlayGroup.alpha = Mathf.SmoothStep(0f, 1f, overlayFade);

		if (!gameplayVisible && overlayFade <= 0.01f && overlayCanvas.gameObject.activeSelf)
		{
			overlayCanvas.gameObject.SetActive(false);
		}
	}

	private void UpdateQuickChat()
	{
		if (quickChatGroup == null || quickChatPanel == null || chatButtonRoot == null)
		{
			return;
		}

		if (!gameplayVisible)
		{
			quickChatVisible = false;
			quickChatOpen = Mathf.MoveTowards(quickChatOpen, 0f, Time.unscaledDeltaTime * 8f);
			quickChatGroup.alpha = Mathf.SmoothStep(0f, 1f, quickChatOpen);
			UpdateChatBubble(leftChatBubbleGroup, leftChatBubble, leftBubbleUntil);
			UpdateChatBubble(rightChatBubbleGroup, rightChatBubble, rightBubbleUntil);
			return;
		}

		PollQuickChatInput();
		UpdatePendingBotReply();

		float target = quickChatVisible ? 1f : 0f;
		quickChatOpen = Mathf.MoveTowards(quickChatOpen, target, Time.unscaledDeltaTime * 9f);
		float eased = Mathf.SmoothStep(0f, 1f, quickChatOpen);
		quickChatGroup.alpha = eased;
		quickChatPanel.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, eased);
		quickChatPanel.gameObject.SetActive(eased > 0.01f);

		float buttonPulse = 1f + Mathf.Sin(Time.unscaledTime * 4.8f) * 0.035f;
		chatButtonRoot.localScale = quickChatVisible ? Vector3.one * 1.08f : Vector3.one * buttonPulse;

		UpdateChatBubble(leftChatBubbleGroup, leftChatBubble, leftBubbleUntil);
		UpdateChatBubble(rightChatBubbleGroup, rightChatBubble, rightBubbleUntil);
	}

	private void PollQuickChatInput()
	{
		Vector2 position;
		if (!TryGetPointerDown(out position))
		{
			return;
		}

		if (RectTransformUtility.RectangleContainsScreenPoint(chatButtonRoot, position, null))
		{
			quickChatVisible = !quickChatVisible;
			return;
		}

		if (!quickChatVisible || quickChatOptionRects == null)
		{
			return;
		}

		for (int i = 0; i < quickChatOptionRects.Length; i++)
		{
			RectTransform optionRect = quickChatOptionRects[i];
			if (optionRect != null && RectTransformUtility.RectangleContainsScreenPoint(optionRect, position, null))
			{
				SendQuickChat(i);
				return;
			}
		}

		if (!RectTransformUtility.RectangleContainsScreenPoint(quickChatPanel, position, null))
		{
			quickChatVisible = false;
		}
	}

	private bool TryGetPointerDown(out Vector2 position)
	{
		position = Vector2.zero;
		if (Input.touchCount > 0)
		{
			Touch touch = Input.GetTouch(0);
			if (touch.phase != TouchPhase.Began)
			{
				return false;
			}

			position = touch.position;
			return true;
		}

		if (Input.GetMouseButtonDown(0))
		{
			position = Input.mousePosition;
			return true;
		}

		return false;
	}

	private void SendQuickChat(int index)
	{
		if (index < 0 || index >= quickChatMessages.Length)
		{
			return;
		}

		quickChatVisible = false;
		ShowChatBubble(leftChatBubbleGroup, leftChatBubble, leftChatBubbleIcon, leftChatBubbleText, quickChatMessages[index], index, ref leftBubbleUntil);

		pendingBotReplyIndex = UnityEngine.Random.Range(0, quickChatMessages.Length);
		pendingBotReply = quickChatMessages[pendingBotReplyIndex];
		pendingBotReplyAt = Time.unscaledTime + UnityEngine.Random.Range(0.65f, 1.15f);
	}

	private void UpdatePendingBotReply()
	{
		if (pendingBotReplyAt < 0f || Time.unscaledTime < pendingBotReplyAt)
		{
			return;
		}

		ShowChatBubble(rightChatBubbleGroup, rightChatBubble, rightChatBubbleIcon, rightChatBubbleText, pendingBotReply, pendingBotReplyIndex, ref rightBubbleUntil);
		pendingBotReplyAt = -1f;
		pendingBotReply = string.Empty;
	}

	private void ShowChatBubble(CanvasGroup group, RectTransform bubble, Image icon, TMP_Text text, string message, int iconIndex, ref float until)
	{
		if (group == null || bubble == null || text == null)
		{
			return;
		}

		text.text = message;
		if (icon != null && reactionSprites != null && reactionSprites.Length > 0)
		{
			icon.sprite = reactionSprites[Mathf.Abs(iconIndex) % reactionSprites.Length];
			icon.color = Color.white;
		}

		group.alpha = 1f;
		bubble.localScale = Vector3.one * 0.82f;
		until = Time.unscaledTime + ChatBubbleLifetime;
	}

	private void UpdateChatBubble(CanvasGroup group, RectTransform bubble, float until)
	{
		if (group == null || bubble == null)
		{
			return;
		}

		float remaining = until - Time.unscaledTime;
		if (remaining <= 0f)
		{
			group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime * 5f);
			return;
		}

		float fadeIn = Mathf.Clamp01((ChatBubbleLifetime - remaining) / 0.18f);
		float fadeOut = Mathf.Clamp01(remaining / 0.42f);
		float alpha = Mathf.Min(fadeIn, fadeOut);
		group.alpha = Mathf.SmoothStep(0f, 1f, alpha);

		float pop = 1f + Mathf.Sin(Mathf.Clamp01((ChatBubbleLifetime - remaining) / 0.25f) * Mathf.PI) * 0.08f;
		bubble.localScale = Vector3.Lerp(bubble.localScale, Vector3.one * pop, Time.unscaledDeltaTime * 12f);
	}

	private void UpdateTurnVisual()
	{
		if (leftRingFill == null || rightRingFill == null || leftAvatarRoot == null || rightAvatarRoot == null)
		{
			return;
		}

		int syncedSide;
		float remaining;
		bool hasCountdown;
		bool hasSyncedSide;
		if (TryReadTurnState(out syncedSide, out remaining, out hasCountdown, out hasSyncedSide))
		{
			if (hasSyncedSide)
			{
				hasReliableTurnSide = true;
			}

			if (hasSyncedSide && syncedSide != activeSide)
			{
				activeSide = syncedSide;
				turnStartTime = Time.unscaledTime;
				lastTurnCountdown = 1f;
			}

			if (hasCountdown)
			{
				if (remaining > lastTurnCountdown + 0.18f)
				{
					turnStartTime = Time.unscaledTime - (1f - remaining) * TurnDurationSeconds;
				}

				lastTurnCountdown = remaining;
			}
			else
			{
				remaining = Mathf.Clamp01(1f - (Time.unscaledTime - turnStartTime) / TurnDurationSeconds);
				lastTurnCountdown = remaining;
			}
		}
		else
		{
			remaining = Mathf.Clamp01(1f - (Time.unscaledTime - turnStartTime) / TurnDurationSeconds);
			lastTurnCountdown = remaining;
		}

		bool leftActive = hasReliableTurnSide && activeSide == 0;
		bool rightActive = hasReliableTurnSide && activeSide == 1;

		leftRingFill.fillAmount = leftActive ? remaining : 1f;
		rightRingFill.fillAmount = rightActive ? remaining : 1f;
		leftRingFill.color = leftActive ? new Color(0.3f, 1f, 0.17f, 1f) : new Color(1f, 0.72f, 0.18f, 0.38f);
		rightRingFill.color = rightActive ? new Color(0.3f, 1f, 0.17f, 1f) : new Color(1f, 0.72f, 0.18f, 0.38f);

		float pulse = 1f + Mathf.Sin(Time.unscaledTime * 7.2f) * 0.035f;
		leftAvatarRoot.localScale = leftActive ? Vector3.one * pulse : Vector3.one * 0.96f;
		rightAvatarRoot.localScale = rightActive ? Vector3.one * pulse : Vector3.one * 0.96f;

		leftGlow.color = leftActive ? new Color(0.25f, 1f, 0.15f, 0.32f) : new Color(1f, 0.68f, 0.12f, 0.12f);
		rightGlow.color = rightActive ? new Color(0.25f, 1f, 0.15f, 0.32f) : new Color(1f, 0.68f, 0.12f, 0.12f);
		leftActiveLine.color = leftActive ? new Color(0.3f, 1f, 0.18f, 0.65f) : new Color(0.3f, 1f, 0.18f, 0f);
		rightActiveLine.color = rightActive ? new Color(0.3f, 1f, 0.18f, 0.65f) : new Color(0.3f, 1f, 0.18f, 0f);

		float railPulse = 1f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.018f;
		leftRail.localScale = leftActive ? new Vector3(railPulse, 1f, 1f) : Vector3.one;
		rightRail.localScale = rightActive ? new Vector3(railPulse, 1f, 1f) : Vector3.one;
		centerBadge.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.025f);
	}

	private bool TryReadTurnState(out int side, out float remaining, out bool hasCountdown, out bool hasSide)
	{
		side = activeSide;
		remaining = lastTurnCountdown;
		hasCountdown = false;
		hasSide = false;

		if (TryReadControllerTurnState(out side, out remaining, out hasCountdown))
		{
			hasSide = true;
			return true;
		}

		int textSide;
		if (TryReadTurnSideFromTexts(out textSide))
		{
			side = textSide;
			hasSide = true;
		}

		float seconds;
		if (TryReadTurnCountdownSeconds(out seconds))
		{
			remaining = Mathf.Clamp01(seconds / TurnDurationSeconds);
			hasCountdown = true;
		}

		return hasSide || hasCountdown;
	}

	private bool TryReadControllerTurnState(out int side, out float remaining, out bool hasCountdown)
	{
		side = activeSide;
		remaining = lastTurnCountdown;
		hasCountdown = false;

		Component turnController = GetCachedComponentByTypeName(ref cachedTurnController, TurnControllerTypeName);
		object currentTurnValue = null;
		if (turnController != null)
		{
			object currentTurnVariable = GetMemberValue(turnController, "CurrentTurn");
			currentTurnValue = GetMemberValue(currentTurnVariable, "Value");
		}
		else
		{
			Component dominoController = GetCachedComponentByTypeName(ref cachedDominoController, DominoControllerTypeName);
			currentTurnValue = dominoController != null ? GetMemberValue(dominoController, "CurrentTurn") : null;
		}

		string currentOwner = currentTurnValue != null ? currentTurnValue.ToString() : string.Empty;
		if (string.IsNullOrEmpty(currentOwner) || currentOwner == "None")
		{
			return false;
		}

		string localOwner = GetLocalOwnerName();
		side = currentOwner == localOwner ? 0 : 1;

		float seconds;
		if (turnController != null && TryReadFloat(GetMemberValue(turnController, "TurnTimeRemaining"), out seconds))
		{
			float totalSeconds = Mathf.Max(0.1f, GetConfiguredTurnSeconds());
			remaining = Mathf.Clamp01(seconds / totalSeconds);
			hasCountdown = seconds > 0f;
		}

		return true;
	}

	private string GetLocalOwnerName()
	{
		Component dominoController = GetCachedComponentByTypeName(ref cachedDominoController, DominoControllerTypeName);
		bool isLocalGame;
		if (dominoController != null && TryReadBool(GetMemberValue(dominoController, "IsLocalGame"), out isLocalGame) && isLocalGame)
		{
			return "Player1";
		}

		object networkManager = GetStaticPropertyValue(NetworkManagerTypeName, "Singleton");
		bool isListening;
		bool isHost;
		if (networkManager != null &&
			TryReadBool(GetMemberValue(networkManager, "IsListening"), out isListening) &&
			isListening &&
			TryReadBool(GetMemberValue(networkManager, "IsHost"), out isHost))
		{
			return isHost ? "Player1" : "Player2";
		}

		return "Player1";
	}

	private float GetConfiguredTurnSeconds()
	{
		Component dominoController = GetCachedComponentByTypeName(ref cachedDominoController, DominoControllerTypeName);
		object settings = dominoController != null ? GetMemberValue(dominoController, "Settings") : null;
		float seconds;
		if (settings != null && TryReadFloat(GetMemberValue(settings, "TurnSecsPerPlayer"), out seconds) && seconds > 0f)
		{
			return seconds;
		}

		return TurnDurationSeconds;
	}

	private Component GetCachedComponentByTypeName(ref Component cached, string fullTypeName)
	{
		if (cached != null && CanTouch(cached.gameObject) && cached.GetType().FullName == fullTypeName)
		{
			return cached;
		}

		if (Time.unscaledTime < nextControllerLookupTime && cachedTurnController != null)
		{
			return null;
		}

		nextControllerLookupTime = Time.unscaledTime + 0.25f;
		MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
		for (int i = 0; i < behaviours.Length; i++)
		{
			MonoBehaviour behaviour = behaviours[i];
			if (behaviour == null || !CanTouch(behaviour.gameObject))
			{
				continue;
			}

			string behaviourTypeName = behaviour.GetType().FullName;
			if (behaviourTypeName == TurnControllerTypeName)
			{
				cachedTurnController = behaviour;
			}
			else if (behaviourTypeName == DominoControllerTypeName)
			{
				cachedDominoController = behaviour;
			}

			if (behaviourTypeName == fullTypeName)
			{
				cached = behaviour;
				return cached;
			}
		}

		return null;
	}

	private object GetMemberValue(object target, string memberName)
	{
		if (target == null)
		{
			return null;
		}

		Type type = target.GetType();
		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
		PropertyInfo property = type.GetProperty(memberName, flags);
		if (property != null)
		{
			try
			{
				return property.GetValue(target, null);
			}
			catch
			{
				return null;
			}
		}

		FieldInfo field = type.GetField(memberName, flags);
		if (field == null)
		{
			return null;
		}

		try
		{
			return field.GetValue(target);
		}
		catch
		{
			return null;
		}
	}

	private object GetStaticPropertyValue(string fullTypeName, string propertyName)
	{
		Type type = FindType(fullTypeName);
		if (type == null)
		{
			return null;
		}

		PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
		if (property == null)
		{
			return null;
		}

		try
		{
			return property.GetValue(null, null);
		}
		catch
		{
			return null;
		}
	}

	private Type FindType(string fullTypeName)
	{
		Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (int i = 0; i < assemblies.Length; i++)
		{
			Type type = assemblies[i].GetType(fullTypeName, false);
			if (type != null)
			{
				return type;
			}
		}

		return null;
	}

	private bool TryReadFloat(object value, out float result)
	{
		result = 0f;
		if (value == null)
		{
			return false;
		}

		if (value is float)
		{
			result = (float)value;
			return true;
		}

		if (value is int)
		{
			result = (int)value;
			return true;
		}

		if (value is double)
		{
			result = (float)(double)value;
			return true;
		}

		return float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
	}

	private bool TryReadBool(object value, out bool result)
	{
		result = false;
		if (value == null)
		{
			return false;
		}

		if (value is bool)
		{
			result = (bool)value;
			return true;
		}

		return bool.TryParse(value.ToString(), out result);
	}

	private bool TryReadTurnSideFromTexts(out int side)
	{
		side = -1;
		TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();

		for (int i = 0; i < texts.Length; i++)
		{
			TMP_Text text = texts[i];
			if (text == null || !CanTouch(text.gameObject) || text.rectTransform == null || !IsVisible(text))
			{
				continue;
			}

			string value = Normalize(text.text);
			if (value.Contains("SUA VEZ"))
			{
				int textSide = GetSideFromScreenPosition(text.rectTransform);
				if (textSide >= 0)
				{
					side = textSide;
					return true;
				}
			}
		}

		return false;
	}

	private bool TryReadTurnCountdownSeconds(out float seconds)
	{
		seconds = 0f;
		TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();

		for (int i = 0; i < texts.Length; i++)
		{
			TMP_Text text = texts[i];
			if (text == null || !CanTouch(text.gameObject) || text.rectTransform == null || !IsVisible(text))
			{
				continue;
			}

			string value = Normalize(text.text);
			if (!value.Contains("TEMPO"))
			{
				continue;
			}

			float parsed;
			if (TryExtractFirstNumber(value, out parsed) && parsed >= 0f && parsed <= TurnDurationSeconds + 0.75f)
			{
				seconds = parsed;
				return true;
			}
		}

		return false;
	}

	private bool TryExtractFirstNumber(string value, out float number)
	{
		number = 0f;
		if (string.IsNullOrEmpty(value))
		{
			return false;
		}

		string buffer = string.Empty;
		bool started = false;
		for (int i = 0; i < value.Length; i++)
		{
			char current = value[i];
			if (char.IsDigit(current))
			{
				buffer += current;
				started = true;
			}
			else if (started && (current == '.' || current == ','))
			{
				buffer += '.';
			}
			else if (started)
			{
				break;
			}
		}

		return buffer.Length > 0 && float.TryParse(buffer, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
	}

	private Sprite LoadLocalAvatarSprite()
	{
		string path = PlayerPrefs.GetString("avatar_path", string.Empty);
		if (!string.IsNullOrEmpty(path) && File.Exists(path))
		{
			byte[] bytes = File.ReadAllBytes(path);
			Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (texture.LoadImage(bytes))
			{
				texture.name = "Runtime Local Avatar";
				return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
			}
		}

		Sprite resourceAvatar = Resources.Load<Sprite>("Profile/profile_default_avatar");
		if (resourceAvatar == null)
		{
			resourceAvatar = Resources.Load<Sprite>("profile/profile_default_avatar");
		}

		return resourceAvatar != null ? resourceAvatar : avatarFallbackSprite;
	}

	private string Normalize(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		string text = value.ToUpperInvariant();
		text = text.Replace((char)193, 'A').Replace((char)192, 'A').Replace((char)195, 'A').Replace((char)194, 'A');
		text = text.Replace((char)201, 'E').Replace((char)202, 'E');
		text = text.Replace((char)205, 'I');
		text = text.Replace((char)211, 'O').Replace((char)212, 'O').Replace((char)213, 'O');
		text = text.Replace((char)218, 'U');
		text = text.Replace((char)199, 'C');
		return text.Trim();
	}

	private Sprite CreateSquareSprite()
	{
		Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
		texture.name = "Runtime White Square";
		for (int y = 0; y < 4; y++)
		{
			for (int x = 0; x < 4; x++)
			{
				texture.SetPixel(x, y, Color.white);
			}
		}

		texture.Apply();
		return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
	}

	private Sprite CreateCircleSprite(int size)
	{
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
		texture.name = "Runtime Circle";
		float center = (size - 1) * 0.5f;
		float radius = size * 0.5f - 1f;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
				float alpha = Mathf.Clamp01(radius - distance + 1f);
				texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
			}
		}

		texture.Apply();
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
	}

	private Sprite CreateRingSprite(int size)
	{
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
		texture.name = "Runtime Ring";
		float center = (size - 1) * 0.5f;
		float outer = size * 0.5f - 2f;
		float inner = size * 0.405f;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
				float alpha = Mathf.Min(Mathf.Clamp01(outer - distance + 1f), Mathf.Clamp01(distance - inner + 1f));
				texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
			}
		}

		texture.Apply();
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
	}

	private Sprite CreateRoundedRectSprite(int size, int radius)
	{
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
		texture.name = "Runtime Rounded Rect";

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0f);
				float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0f);
				float distance = Mathf.Sqrt(dx * dx + dy * dy);
				float alpha = Mathf.Clamp01(radius - distance + 1f);
				texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
			}
		}

		texture.Apply();
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
	}

	private Sprite CreateAvatarFallbackSprite(int size)
	{
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
		texture.name = "Runtime Opponent Avatar";
		Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float distance = Vector2.Distance(new Vector2(x, y), center);
				float mask = Mathf.Clamp01(size * 0.49f - distance + 1f);
				float t = Mathf.Clamp01((float)y / size);
				Color color = Color.Lerp(new Color(0.02f, 0.01f, 0.05f, 1f), new Color(0.52f, 0.06f, 0.6f, 1f), t);
				texture.SetPixel(x, y, new Color(color.r, color.g, color.b, mask));
			}
		}

		DrawDisc(texture, size, new Vector2(0.5f, 0.63f), 0.145f, new Color(1f, 0.78f, 0.2f, 1f));
		DrawDisc(texture, size, new Vector2(0.5f, 0.34f), 0.25f, new Color(1f, 0.78f, 0.2f, 1f));
		texture.Apply();
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
	}

	private Sprite CreateSpadeSprite(int size)
	{
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
		texture.name = "Runtime Spade";

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				texture.SetPixel(x, y, Color.clear);
			}
		}

		DrawDisc(texture, size, new Vector2(0.38f, 0.50f), 0.20f, Color.white);
		DrawDisc(texture, size, new Vector2(0.62f, 0.50f), 0.20f, Color.white);
		DrawTriangle(texture, size, new Vector2(0.5f, 0.82f), new Vector2(0.25f, 0.48f), new Vector2(0.75f, 0.48f), Color.white);
		DrawTriangle(texture, size, new Vector2(0.5f, 0.46f), new Vector2(0.38f, 0.18f), new Vector2(0.62f, 0.18f), Color.white);
		DrawRect(texture, size, new Rect(0.44f, 0.12f, 0.12f, 0.2f), Color.white);
		texture.Apply();
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
	}

	private Sprite[] CreateReactionSprites(int size)
	{
		Sprite[] sprites = new Sprite[quickChatMessages.Length];
		for (int i = 0; i < sprites.Length; i++)
		{
			sprites[i] = CreateReactionSprite(size, i);
		}

		return sprites;
	}

	private Sprite CreateReactionSprite(int size, int index)
	{
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
		texture.name = "Runtime Quick Reaction " + index;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				texture.SetPixel(x, y, Color.clear);
			}
		}

		Color gold = new Color(1f, 0.72f, 0.14f, 1f);
		Color hotPink = new Color(1f, 0.10f, 0.88f, 1f);
		Color purple = new Color(0.58f, 0.12f, 1f, 1f);
		Color green = new Color(0.25f, 1f, 0.23f, 1f);
		Color dark = new Color(0.03f, 0.01f, 0.03f, 1f);

		switch (index)
		{
			case 0:
				DrawDisc(texture, size, new Vector2(0.5f, 0.5f), 0.38f, green);
				DrawLine(texture, size, new Vector2(0.28f, 0.50f), new Vector2(0.44f, 0.34f), 0.065f, Color.white);
				DrawLine(texture, size, new Vector2(0.43f, 0.34f), new Vector2(0.74f, 0.68f), 0.065f, Color.white);
				break;
			case 1:
				DrawDisc(texture, size, new Vector2(0.50f, 0.36f), 0.27f, new Color(1f, 0.34f, 0.05f, 1f));
				DrawTriangle(texture, size, new Vector2(0.50f, 0.90f), new Vector2(0.25f, 0.34f), new Vector2(0.75f, 0.34f), new Color(1f, 0.44f, 0.05f, 1f));
				DrawTriangle(texture, size, new Vector2(0.62f, 0.72f), new Vector2(0.42f, 0.35f), new Vector2(0.78f, 0.35f), hotPink);
				DrawDisc(texture, size, new Vector2(0.52f, 0.36f), 0.14f, new Color(1f, 0.82f, 0.12f, 1f));
				break;
			case 2:
				DrawDisc(texture, size, new Vector2(0.5f, 0.5f), 0.38f, gold);
				DrawDisc(texture, size, new Vector2(0.38f, 0.60f), 0.045f, dark);
				DrawDisc(texture, size, new Vector2(0.62f, 0.60f), 0.045f, dark);
				DrawLine(texture, size, new Vector2(0.32f, 0.42f), new Vector2(0.43f, 0.34f), 0.035f, dark);
				DrawLine(texture, size, new Vector2(0.43f, 0.34f), new Vector2(0.57f, 0.34f), 0.035f, dark);
				DrawLine(texture, size, new Vector2(0.57f, 0.34f), new Vector2(0.68f, 0.42f), 0.035f, dark);
				break;
			case 3:
				DrawDisc(texture, size, new Vector2(0.5f, 0.5f), 0.38f, purple);
				DrawLine(texture, size, new Vector2(0.38f, 0.68f), new Vector2(0.50f, 0.78f), 0.055f, Color.white);
				DrawLine(texture, size, new Vector2(0.50f, 0.78f), new Vector2(0.64f, 0.66f), 0.055f, Color.white);
				DrawLine(texture, size, new Vector2(0.64f, 0.66f), new Vector2(0.52f, 0.53f), 0.055f, Color.white);
				DrawLine(texture, size, new Vector2(0.52f, 0.53f), new Vector2(0.52f, 0.43f), 0.055f, Color.white);
				DrawDisc(texture, size, new Vector2(0.52f, 0.28f), 0.055f, Color.white);
				break;
			case 4:
				DrawDisc(texture, size, new Vector2(0.5f, 0.5f), 0.38f, gold);
				DrawRect(texture, size, new Rect(0.25f, 0.55f, 0.25f, 0.12f), dark);
				DrawRect(texture, size, new Rect(0.50f, 0.55f, 0.25f, 0.12f), dark);
				DrawLine(texture, size, new Vector2(0.46f, 0.60f), new Vector2(0.54f, 0.60f), 0.035f, dark);
				DrawLine(texture, size, new Vector2(0.35f, 0.36f), new Vector2(0.65f, 0.36f), 0.04f, dark);
				break;
			default:
				DrawDisc(texture, size, new Vector2(0.5f, 0.5f), 0.34f, hotPink);
				DrawLine(texture, size, new Vector2(0.50f, 0.20f), new Vector2(0.50f, 0.82f), 0.045f, Color.white);
				DrawLine(texture, size, new Vector2(0.20f, 0.50f), new Vector2(0.82f, 0.50f), 0.045f, Color.white);
				DrawLine(texture, size, new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f), 0.04f, gold);
				DrawLine(texture, size, new Vector2(0.70f, 0.30f), new Vector2(0.30f, 0.70f), 0.04f, gold);
				break;
		}

		texture.Apply();
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
	}

	private Sprite CreateChatIconSprite(int size)
	{
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
		texture.name = "Runtime Chat Icon";

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				texture.SetPixel(x, y, Color.clear);
			}
		}

		DrawDisc(texture, size, new Vector2(0.32f, 0.57f), 0.19f, Color.white);
		DrawDisc(texture, size, new Vector2(0.68f, 0.57f), 0.19f, Color.white);
		DrawRect(texture, size, new Rect(0.32f, 0.38f, 0.36f, 0.38f), Color.white);
		DrawRect(texture, size, new Rect(0.25f, 0.48f, 0.50f, 0.19f), Color.white);
		DrawTriangle(texture, size, new Vector2(0.40f, 0.39f), new Vector2(0.52f, 0.39f), new Vector2(0.36f, 0.23f), Color.white);
		DrawDisc(texture, size, new Vector2(0.35f, 0.55f), 0.045f, new Color(0.55f, 0.55f, 0.55f, 0.72f));
		DrawDisc(texture, size, new Vector2(0.50f, 0.55f), 0.045f, new Color(0.55f, 0.55f, 0.55f, 0.72f));
		DrawDisc(texture, size, new Vector2(0.65f, 0.55f), 0.045f, new Color(0.55f, 0.55f, 0.55f, 0.72f));

		texture.Apply();
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
	}

	private void DrawLine(Texture2D texture, int size, Vector2 from, Vector2 to, float normalizedWidth, Color color)
	{
		float distance = Vector2.Distance(from, to);
		int steps = Mathf.Max(2, Mathf.CeilToInt(distance * size * 1.5f));
		for (int i = 0; i <= steps; i++)
		{
			float t = (float)i / steps;
			DrawDisc(texture, size, Vector2.Lerp(from, to, t), normalizedWidth, color);
		}
	}

	private void DrawDisc(Texture2D texture, int size, Vector2 normalizedCenter, float normalizedRadius, Color color)
	{
		Vector2 center = new Vector2(normalizedCenter.x * size, normalizedCenter.y * size);
		float radius = normalizedRadius * size;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float distance = Vector2.Distance(new Vector2(x, y), center);
				float alpha = Mathf.Clamp01(radius - distance + 1f) * color.a;
				if (alpha <= 0f)
				{
					continue;
				}

				Color previous = texture.GetPixel(x, y);
				texture.SetPixel(x, y, Color.Lerp(previous, color, alpha));
			}
		}
	}

	private void DrawTriangle(Texture2D texture, int size, Vector2 a, Vector2 b, Vector2 c, Color color)
	{
		Vector2 pa = a * size;
		Vector2 pb = b * size;
		Vector2 pc = c * size;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				if (PointInTriangle(new Vector2(x, y), pa, pb, pc))
				{
					texture.SetPixel(x, y, color);
				}
			}
		}
	}

	private bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
	{
		float d1 = TriangleSign(p, a, b);
		float d2 = TriangleSign(p, b, c);
		float d3 = TriangleSign(p, c, a);
		bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
		bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
		return !(hasNegative && hasPositive);
	}

	private float TriangleSign(Vector2 p1, Vector2 p2, Vector2 p3)
	{
		return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
	}

	private void DrawRect(Texture2D texture, int size, Rect normalizedRect, Color color)
	{
		int xMin = Mathf.RoundToInt(normalizedRect.xMin * size);
		int xMax = Mathf.RoundToInt(normalizedRect.xMax * size);
		int yMin = Mathf.RoundToInt(normalizedRect.yMin * size);
		int yMax = Mathf.RoundToInt(normalizedRect.yMax * size);

		for (int y = yMin; y <= yMax; y++)
		{
			for (int x = xMin; x <= xMax; x++)
			{
				if (x >= 0 && x < size && y >= 0 && y < size)
				{
					texture.SetPixel(x, y, color);
				}
			}
		}
	}

	private void DismissNoMovePopup()
	{
		GameObject popupObj = GameObject.Find("UIPopup(Clone)");
		if (popupObj == null)
		{
			popupObj = GameObject.Find("UIPopup");
		}

		if (popupObj != null && popupObj.activeInHierarchy)
		{
			bool isNoMovePopup = false;

			TMP_Text[] tmpTexts = popupObj.GetComponentsInChildren<TMP_Text>(true);
			foreach (var t in tmpTexts)
			{
				if (t != null && (t.text.Contains("Sem jogada") || t.text.Contains("No move") || t.text.Contains("No play") || t.text.Contains("Passar a vez")))
				{
					isNoMovePopup = true;
					break;
				}
			}

			if (!isNoMovePopup)
			{
				UnityEngine.UI.Text[] uiTexts = popupObj.GetComponentsInChildren<UnityEngine.UI.Text>(true);
				foreach (var t in uiTexts)
				{
					if (t != null && (t.text.Contains("Sem jogada") || t.text.Contains("No move") || t.text.Contains("No play") || t.text.Contains("Passar a vez")))
					{
						isNoMovePopup = true;
						break;
					}
				}
			}

			if (isNoMovePopup)
			{
				Button[] buttons = popupObj.GetComponentsInChildren<Button>(true);
				foreach (var btn in buttons)
				{
					if (btn != null && btn.gameObject.activeInHierarchy)
					{
						btn.onClick.Invoke();
					}
				}

				popupObj.SetActive(false);
			}
		}
	}
}
