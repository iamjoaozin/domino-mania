#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Automatically adds UNITY_DISABLE_COLLECTIONS_CHECKS to scripting defines.
/// This prevents InvalidOperationException spam from UnityTransport/NetworkDriver
/// when Burst is disabled in the Editor, which causes severe frame rate drops and screen trembling.
/// </summary>
[InitializeOnLoad]
internal static class DisableCollectionsChecks
{
    private const string Define = "UNITY_DISABLE_COLLECTIONS_CHECKS";

    static DisableCollectionsChecks()
    {
        Apply();
        EditorApplication.playModeStateChanged += _ => Apply();
    }

    [MenuItem("Tools/Dominioes/Aplicar Define AntiTremor")]
    private static void ApplyMenu()
    {
        Apply();
        UnityEngine.Debug.Log("[DisableCollectionsChecks] Define aplicado com sucesso.");
    }

    private static void Apply()
    {
        var targets = new[]
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.Android,
        };

        foreach (var group in targets)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            var list = new List<string>(defines.Split(';'));
            if (!list.Contains(Define))
            {
                list.Add(Define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
            }
        }
    }
}
#endif
