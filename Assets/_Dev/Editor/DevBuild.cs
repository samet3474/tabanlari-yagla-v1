// Teşhis için geliştirme derlemesi (yalnızca editör; yayın derlemesine girmez).
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

    // iPhone test sürümü: optimize (sıkıştırılmış) derleme + TY_DEVTOOLS sembolü; zaman çizelgesi katmanı web'de otomatik açılır.
    // Geliştirme derlemesi kullanılmıyor: WebGL'de sıkıştırmasız ~118 MB wasm üretiyor (GitHub sınırı 100 MB, iPhone için fazla).
    public static void BuildWebDev()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.template = "PROJECT:TabanlariYagla";
        UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var addr);
        Debug.Log("DEV ADDRESSABLES: " + (string.IsNullOrEmpty(addr.Error) ? "OK" : addr.Error));
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
        FileUtil.DeleteFileOrDirectory("Build/WebDev"); // önceki çıktıdan dosya kalmasın
        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = "Build/WebDev",
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
            extraScriptingDefines = new[] { "TY_DEVTOOLS" },
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("DEV BUILD RESULT: " + report.summary.result);
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
