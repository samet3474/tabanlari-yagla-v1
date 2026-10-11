// Faz 0.2 teşhis derlemesi (geliştirme derlemesi). Commit edilmez.
using UnityEditor;
using UnityEngine;

public static class DevBuild
{
    public static void BuildWindowsDev()
    {
        UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var addr);
        Debug.Log("DEV ADDRESSABLES: " + (string.IsNullOrEmpty(addr.Error) ? "OK" : addr.Error));
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
        var report = BuildPipeline.BuildPlayer(scenes.ToArray(), "Build/WindowsDev/TabanlariYaglaDev.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.Development);
        Debug.Log("DEV BUILD RESULT: " + report.summary.result);
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
