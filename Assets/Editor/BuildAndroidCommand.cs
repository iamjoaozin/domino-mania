using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildAndroidCommand
{
    private const string DefaultOutputPath = "Builds/Android/Dominioes.apk";
    private const string BundleIdentifier = "com.doxyh.dominioes";

    public static void BuildApk()
    {
        string outputPath = GetArgument("-outputPath", DefaultOutputPath);
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = false;

        PlayerSettings.productName = "Dominioes";
        PlayerSettings.bundleVersion = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion) ? "1.0" : PlayerSettings.bundleVersion;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleIdentifier);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.Mono2x);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);
        PlayerSettings.Android.useCustomKeystore = false;

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            UnityEngine.Debug.LogError("Android build failed: no enabled scenes in Build Settings.");
            EditorApplication.Exit(1);
            return;
        }

        var buildOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.Development
        };

        UnityEngine.Debug.Log("Starting Android APK build: " + outputPath);
        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            UnityEngine.Debug.Log("Android APK build succeeded: " + outputPath + " (" + summary.totalSize + " bytes)");
            EditorApplication.Exit(0);
            return;
        }

        UnityEngine.Debug.LogError("Android APK build failed with result: " + summary.result);
        EditorApplication.Exit(1);
    }

    private static string GetArgument(string name, string defaultValue)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return defaultValue;
    }
}
