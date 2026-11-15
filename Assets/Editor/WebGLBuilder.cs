using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WebGLBuilder
{
    [MenuItem("Build/WebGL (docs)")]
    public static void Build()
    {
        bool dev = System.Environment.GetCommandLineArgs().Contains("-devbuild");
        PlayerSettings.WebGL.exceptionSupport = dev ? WebGLExceptionSupport.FullWithStacktrace : WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.productName = "Virtual Logic Labs";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = dev ? "Builds/dev" : "docs",
            target = BuildTarget.WebGL,
            options = dev ? BuildOptions.Development : BuildOptions.None
        });
        Debug.Log("BUILD RESULT: " + report.summary.result + " size=" + report.summary.totalSize);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
