#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class DominoesCrashSmokeTest
{
    private const string ScenePath = "Assets/DominoTemplate/Scenes/DominoTemplate.unity";
    private const double InvokeDelaySeconds = 2.0d;
    private const double SuccessDelaySeconds = 8.0d;

    private static int phase;
    private static double phaseStartedAt;

    [MenuItem("Tools/Dominioes/Testar Crash Ao Iniciar Partida")]
    public static void Run()
    {
        Environment.SetEnvironmentVariable("UNITY_BURST_DISABLE_COMPILATION", "1");

        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[DominoesCrashSmokeTest] Ja esta em Play Mode.");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath);
        phase = 0;
        phaseStartedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (phase == 0)
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            phase = 1;
            phaseStartedAt = EditorApplication.timeSinceStartup;
            return;
        }

        if (phase == 1)
        {
            if (EditorApplication.timeSinceStartup - phaseStartedAt < InvokeDelaySeconds)
            {
                return;
            }

            InvokeMainMenuPlayButton();
            phase = 2;
            phaseStartedAt = EditorApplication.timeSinceStartup;
            return;
        }

        if (phase == 2 && EditorApplication.timeSinceStartup - phaseStartedAt >= SuccessDelaySeconds)
        {
            Debug.Log("[DominoesCrashSmokeTest] Start de partida ficou vivo sem SIGSEGV/Bug Reporter.");
            EditorApplication.update -= Update;

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
            else
            {
                EditorApplication.isPlaying = false;
            }
        }
    }

    private static void InvokeMainMenuPlayButton()
    {
        var type = Type.GetType("GBTemplates.Domino.View.MainMenuView, GBTemplates.Domino.View");
        if (type == null)
        {
            Fail("Nao encontrei o tipo MainMenuView.");
            return;
        }

        var view = UnityEngine.Object.FindObjectOfType(type);
        if (view == null)
        {
            Fail("Nao encontrei MainMenuView na cena.");
            return;
        }

        var method = type.GetMethod("PlayButton", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            Fail("Nao encontrei o metodo PlayButton.");
            return;
        }

        Debug.Log("[DominoesCrashSmokeTest] Chamando MainMenuView.PlayButton().");
        method.Invoke(view, null);
    }

    private static void Fail(string message)
    {
        Debug.LogError("[DominoesCrashSmokeTest] " + message);
        EditorApplication.update -= Update;

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(2);
        }
    }
}
#endif
