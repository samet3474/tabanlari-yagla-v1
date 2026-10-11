using UnityEditor;
using UnityEngine;

public static class V1Build
{
    static string[] Scenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
        return scenes.ToArray();
    }

    static void Build(BuildTarget target, string path)
    {
        UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var addr);
        Debug.Log("V1 ADDRESSABLES: " + (string.IsNullOrEmpty(addr.Error) ? "OK" : addr.Error));
        var report = BuildPipeline.BuildPlayer(Scenes(), path, target, BuildOptions.None);
        Debug.Log("V1 BUILD RESULT: " + report.summary.result + " size=" + report.summary.totalSize);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }

    [MenuItem("Tabanları Yağla/Windows V1 Derle")]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64, "Build/Windows/TabanlariYagla.exe");
    }

    // GitHub Pages sıkıştırılmış dosyalara Content-Encoding eklemediği için decompressionFallback açık.
    [MenuItem("Tabanları Yağla/Web V1 Derle")]
    public static void BuildWeb()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        Build(BuildTarget.WebGL, "Build/Web");
    }
}
