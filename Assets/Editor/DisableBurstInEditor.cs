#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DisableBurstInEditor
{
	private const string DisableBurstEnvironmentKey = "UNITY_BURST_DISABLE_COMPILATION";

	static DisableBurstInEditor()
	{
		DisableBurst();
		EditorApplication.delayCall += DisableBurst;
		AssemblyReloadEvents.afterAssemblyReload += DisableBurst;
		EditorApplication.playModeStateChanged += _ => DisableBurst();
	}

	[MenuItem("Tools/Dominioes/Desligar Burst no Editor")]
	private static void DisableBurstMenu()
	{
		DisableBurst();
		UnityEngine.Debug.Log("Burst desligado para este Editor.");
	}

	private static void DisableBurst()
	{
		Environment.SetEnvironmentVariable(DisableBurstEnvironmentKey, "1");
		EditorPrefs.SetBool("BurstCompilation", false);
		EditorPrefs.SetBool("BurstSafetyChecks", false);
		EditorPrefs.SetBool("BurstEnableCompilation", false);
		EditorPrefs.SetBool("BurstEnableBurstCompilation", false);

		object options = GetBurstOptions();
		SetBurstOption(options, "EnableBurstCompilation", false);
		SetBurstOption(options, "EnableBurstCompileSynchronously", false);
		SetBurstOption(options, "CompileSynchronously", false);
		SetBurstOption(options, "EnableBurstSafetyChecks", false);
		SetBurstOption(options, "ForceEnableBurstSafetyChecks", false);
	}

	private static object GetBurstOptions()
	{
		try
		{
			Type compilerType = Type.GetType("Unity.Burst.BurstCompiler, Unity.Burst");
			PropertyInfo optionsProperty = compilerType?.GetProperty("Options", BindingFlags.Static | BindingFlags.Public);
			return optionsProperty?.GetValue(null, null);
		}
		catch
		{
			return null;
		}
	}

	private static void SetBurstOption(object options, string propertyName, bool value)
	{
		if (options == null)
		{
			return;
		}

		try
		{
			PropertyInfo property = options.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
			if (property != null && property.CanWrite)
			{
				property.SetValue(options, value, null);
			}
		}
		catch
		{
			// The recovered Burst assembly can be half imported; the environment flag still protects Play Mode.
		}
	}
}
#endif
