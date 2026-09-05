using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TMPFontSafetyNet : MonoBehaviour
{
	private static TMPFontSafetyNet instance;
	private static TMP_FontAsset safeFontAsset;
	private static Material safeFontMaterial;
	private static bool safeFontCreationFailed;
	private float nextRepairTime;
	private float intensiveRepairUntil;
	private static int lastCanvasRepairFrame = -1;

	public static TMP_FontAsset SafeFontAsset
	{
		get
		{
			EnsureSafeFont();
			return safeFontAsset;
		}
	}

	public static Material SafeFontMaterial
	{
		get
		{
			EnsureSafeFont();
			return safeFontMaterial;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Boot()
	{
		if (instance != null)
		{
			return;
		}

		GameObject gameObject = new GameObject("TMP Font Safety Net");
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
		instance = gameObject.AddComponent<TMPFontSafetyNet>();
	}

	private void Awake()
	{
		EnsureSafeFont();
		PatchTMPSettings();
		SceneManager.sceneLoaded += OnSceneLoaded;
		Canvas.willRenderCanvases += RepairAll;
		intensiveRepairUntil = Time.realtimeSinceStartup + 20f;
		RepairAll();
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		Canvas.willRenderCanvases -= RepairAll;
	}

	private void Update()
	{
		float interval = Time.realtimeSinceStartup < intensiveRepairUntil ? 0.35f : 3f;
		if (Time.realtimeSinceStartup < nextRepairTime)
		{
			return;
		}

		nextRepairTime = Time.realtimeSinceStartup + interval;
		RepairAll();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		intensiveRepairUntil = Time.realtimeSinceStartup + 10f;
		RepairAll();
	}

	private static void EnsureSafeFont()
	{
		if (safeFontAsset != null)
		{
			return;
		}

		if (safeFontCreationFailed)
		{
			return;
		}

		safeFontAsset = CreateRuntimeFontAsset();
		if (safeFontAsset != null)
		{
			EnableDynamicAtlas(safeFontAsset);
			WarmUpCommonCharacters(safeFontAsset);
			safeFontMaterial = CreateReadableMaterial(safeFontAsset.material);
			return;
		}

		safeFontAsset = LoadExistingFontAsset();
		if (safeFontAsset != null && IsFontAssetUsable(safeFontAsset))
		{
			safeFontMaterial = CreateReadableMaterial(LoadExistingMaterial() ?? safeFontAsset.material);
			EnableDynamicAtlas(safeFontAsset);
			WarmUpCommonCharacters(safeFontAsset);
			return;
		}

		safeFontAsset = null;
		safeFontCreationFailed = true;
	}

	private static TMP_FontAsset CreateRuntimeFontAsset()
	{
		Font sourceFont = Resources.Load<Font>("RuntimeFonts/LiberationSans");
		if (sourceFont == null)
		{
			sourceFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Roboto", "Liberation Sans", "sans-serif" }, 90);
		}

		if (sourceFont == null)
		{
			sourceFont = LoadBuiltinFont("LegacyRuntime.ttf");
		}

		if (sourceFont == null)
		{
			return null;
		}

		try
		{
			TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
			fontAsset.name = "Runtime TMP Safe Font";
			fontAsset.hideFlags = HideFlags.HideAndDontSave;
			return fontAsset;
		}
		catch (Exception exception)
		{
			Debug.LogWarning("Nao foi possivel criar a fonte segura do TMP: " + exception.Message);
			return null;
		}
	}

	private static TMP_FontAsset LoadExistingFontAsset()
	{
		string[] paths =
		{
			"fonts & materials/LiberationSans SDF",
			"Fonts & Materials/LiberationSans SDF",
			"fonts & materials/Roboto-Bold SDF",
			"fonts & materials/Oswald Bold SDF"
		};

		for (int i = 0; i < paths.Length; i++)
		{
			TMP_FontAsset fontAsset = Resources.Load<TMP_FontAsset>(paths[i]);
			if (fontAsset != null)
			{
				return fontAsset;
			}
		}

		return null;
	}

	private static bool IsFontAssetUsable(TMP_FontAsset fontAsset)
	{
		if (fontAsset == null)
		{
			return false;
		}

		try
		{
			Texture2D[] atlasTextures = fontAsset.atlasTextures;
			return atlasTextures != null && atlasTextures.Length > 0 && atlasTextures[0] != null;
		}
		catch
		{
			return false;
		}
	}

	private static Material LoadExistingMaterial()
	{
		string[] paths =
		{
			"fonts & materials/LiberationSans SDF Material_0",
			"fonts & materials/LiberationSans SDF Material",
			"Fonts & Materials/LiberationSans SDF Material_0",
			"Fonts & Materials/LiberationSans SDF Material"
		};

		for (int i = 0; i < paths.Length; i++)
		{
			Material material = Resources.Load<Material>(paths[i]);
			if (material != null)
			{
				return material;
			}
		}

		return null;
	}

	private static Font LoadBuiltinFont(string fontName)
	{
		try
		{
			return Resources.GetBuiltinResource<Font>(fontName);
		}
		catch
		{
			return null;
		}
	}

	private static void WarmUpCommonCharacters(TMP_FontAsset fontAsset)
	{
		if (fontAsset == null)
		{
			return;
		}

		try
		{
			string missingCharacters;
			fontAsset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,;:!?+-*/()[]{}#$%&@_<>|\\\"' Joao Victor Lara Poker JOGAR ENTRAR NA SALA CRIAR CONFIGURACOES SUPORTE TREINAR IA LOJA Boa sorte de NIVEL MOEDAS", out missingCharacters);
		}
		catch
		{
			// The first TMP render can still populate the dynamic atlas if this old build skips warmup.
		}
	}

	private static Material CreateReadableMaterial(Material sourceMaterial)
	{
		if (sourceMaterial == null)
		{
			return null;
		}

		Material material = new Material(sourceMaterial);
		material.name = sourceMaterial.name + " Runtime Readable";
		material.hideFlags = HideFlags.HideAndDontSave;
		SetColorIfPresent(material, "_FaceColor", Color.white);
		SetColorIfPresent(material, "_OutlineColor", new Color(0f, 0f, 0f, 1f));
		SetColorIfPresent(material, "_UnderlayColor", new Color(0f, 0f, 0f, 0f));
		SetColorIfPresent(material, "_GlowColor", Color.white);
		SetFloatIfPresent(material, "_FaceDilate", 0f);
		SetFloatIfPresent(material, "_OutlineWidth", 0f);
		SetFloatIfPresent(material, "_UnderlaySoftness", 0f);
		SetFloatIfPresent(material, "_GlowPower", 0f);
		return material;
	}

	private static void SetColorIfPresent(Material material, string propertyName, Color color)
	{
		if (material != null && material.HasProperty(propertyName))
		{
			material.SetColor(propertyName, color);
		}
	}

	private static void SetFloatIfPresent(Material material, string propertyName, float value)
	{
		if (material != null && material.HasProperty(propertyName))
		{
			material.SetFloat(propertyName, value);
		}
	}

	private static void PatchTMPSettings()
	{
		if (safeFontAsset == null)
		{
			return;
		}

		try
		{
			TMP_Settings settings = TMP_Settings.instance;
			if (settings == null)
			{
				return;
			}

			SetPrivateField(settings, "m_defaultFontAsset", safeFontAsset);
			SetPrivateField(settings, "m_fallbackFontAssets", new List<TMP_FontAsset> { safeFontAsset });
			SetPrivateField(settings, "m_warningsDisabled", true);
		}
		catch (Exception exception)
		{
			Debug.LogWarning("Nao foi possivel ajustar TMP Settings em runtime: " + exception.Message);
		}
	}

	private static void EnableDynamicAtlas(TMP_FontAsset fontAsset)
	{
		PropertyInfo property = typeof(TMP_FontAsset).GetProperty("atlasPopulationMode", BindingFlags.Instance | BindingFlags.Public);
		if (property == null || !property.CanWrite)
		{
			return;
		}

		try
		{
			object dynamicMode = Enum.Parse(property.PropertyType, "Dynamic");
			property.SetValue(fontAsset, dynamicMode, null);
		}
		catch
		{
			// Some recovered TMP builds expose the property but keep the enum internal.
		}
	}

	private static void SetPrivateField(object target, string fieldName, object value)
	{
		FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (field != null)
		{
			field.SetValue(target, value);
		}
	}

	private static void RepairAll()
	{
		if (Time.frameCount == lastCanvasRepairFrame)
		{
			return;
		}

		lastCanvasRepairFrame = Time.frameCount;
		EnsureSafeFont();
		if (safeFontAsset == null)
		{
			return;
		}

		TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
		for (int i = 0; i < texts.Length; i++)
		{
			RepairText(texts[i]);
		}

		Text[] legacyTexts = Resources.FindObjectsOfTypeAll<Text>();
		for (int i = 0; i < legacyTexts.Length; i++)
		{
			RepairLegacyText(legacyTexts[i]);
		}
	}

	private static void RepairText(TMP_Text text)
	{
		if (text == null || text.gameObject == null || !text.gameObject.scene.IsValid())
		{
			return;
		}

		try
		{
			text.font = safeFontAsset;
			if (safeFontMaterial != null)
			{
				text.fontSharedMaterial = safeFontMaterial;
			}

			RepairTextColor(text);
			text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
		}
		catch
		{
			// Some inactive recovered TMP objects still point at old serialized font data.
			// Active texts are repaired continuously, so keep this quiet to avoid console spam.
		}
	}

	private static void RepairLegacyText(Text text)
	{
		if (text == null || text.gameObject == null || !text.gameObject.scene.IsValid())
		{
			return;
		}

		if (text.font == null)
		{
			Font font = Resources.Load<Font>("RuntimeFonts/LiberationSans");
			if (font == null)
			{
				font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Roboto", "Liberation Sans", "sans-serif" }, Mathf.Max(32, text.fontSize));
			}

			if (font != null)
			{
				text.font = font;
			}
		}

		Color color = text.color;
		float luminance = (color.r * 0.2126f) + (color.g * 0.7152f) + (color.b * 0.0722f);
		if (color.a < 0.35f)
		{
			color.a = 1f;
		}

		if (luminance < 0.2f && !HasLightParentGraphic(text.transform))
		{
			color.r = 1f;
			color.g = 1f;
			color.b = 1f;
		}

		text.color = color;
	}

	private static void RepairTextColor(TMP_Text text)
	{
		Color color = text.color;
		float luminance = (color.r * 0.2126f) + (color.g * 0.7152f) + (color.b * 0.0722f);
		if (color.a < 0.35f)
		{
			color.a = 1f;
		}

		if (luminance < 0.2f && !HasLightParentGraphic(text.transform))
		{
			color.r = 1f;
			color.g = 1f;
			color.b = 1f;
		}

		text.color = color;
	}

	private static bool HasLightParentGraphic(Transform transform)
	{
		for (int i = 0; i < 4 && transform != null; i++)
		{
			Graphic graphic = transform.GetComponent<Graphic>();
			if (graphic != null && graphic.color.a > 0.45f)
			{
				Color color = graphic.color;
				float luminance = (color.r * 0.2126f) + (color.g * 0.7152f) + (color.b * 0.0722f);
				if (luminance > 0.65f)
				{
					return true;
				}
			}

			transform = transform.parent;
		}

		return false;
	}
}
