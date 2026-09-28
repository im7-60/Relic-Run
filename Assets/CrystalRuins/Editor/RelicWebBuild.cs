using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only build entry point. It never regenerates or saves game scenes.
public static class RelicWebBuild
{
    private static string DefaultOutput => Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/RelicRun-Web"));
    private static readonly string[] ExpectedScenes =
    {
        "Assets/CrystalRuins/Scenes/RelicRun.unity",
        "Assets/CrystalRuins/Scenes/RelicRun2.unity",
        "Assets/CrystalRuins/Scenes/RelicRun3.unity"
    };

    [MenuItem("Relic Run/Build itch.io Web player")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play mode before building.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save edited scenes before building.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("Install Web Build Support for Unity " + Application.unityVersion + ".");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new InvalidOperationException("Launch Unity with -buildTarget WebGL, or select Web before running this command.");

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (!scenes.SequenceEqual(ExpectedScenes) || scenes.Any(s => !File.Exists(s)))
            throw new InvalidOperationException("Expected the three existing Relic Run scenes, in order, in Build Settings.");
        if (!File.Exists("Assets/WebGLTemplates/RelicRun/index.html"))
            throw new InvalidOperationException("Install the RelicRun Web template before building.");

        string output = Path.GetFullPath(CommandLineValue("-relicWebOutput") ?? DefaultOutput);
        string playerFolder = Path.Combine(output, "Web");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(playerFolder);

        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 540;
        PlayerSettings.WebGL.template = "PROJECT:RelicRun";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.threadsSupport = false;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        PlayerSettings.WebGL.showDiagnostics = false;
        PlayerSettings.WebGL.closeOnQuit = false;
        PlayerSettings.WebGL.initialMemorySize = 128;
        PlayerSettings.WebGL.maximumMemorySize = 2048;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
        AssetDatabase.SaveAssets();

        var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            // Avoid importing the authoring scene's Global Light into build validation.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = playerFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            var summary = new StringBuilder();
            summary.AppendLine("Result: " + report.summary.result);
            summary.AppendLine("Unity: " + Application.unityVersion);
            summary.AppendLine("Errors: " + report.summary.totalErrors);
            summary.AppendLine("Warnings: " + report.summary.totalWarnings);
            summary.AppendLine("Bytes: " + report.summary.totalSize);
            summary.AppendLine("Duration: " + report.summary.totalTime);
            summary.AppendLine("Target: WebGL / IL2CPP / release");
            summary.AppendLine("Canvas: responsive 16:9, default 960 x 540");
            summary.AppendLine("Compression: Gzip with JavaScript decompression fallback");
            summary.AppendLine("Threads: disabled; memory: 128 MB initial, growing to 2048 MB maximum");
            summary.AppendLine("Scenes:\n" + string.Join("\n", scenes));
            File.WriteAllText(Path.Combine(output, "build-report.txt"), summary.ToString());

            var messages = new StringBuilder();
            foreach (BuildStep step in report.steps)
                foreach (BuildStepMessage message in step.messages)
                    if (message.type == LogType.Warning || message.type == LogType.Error || message.type == LogType.Exception)
                        messages.AppendLine(message.type + " | " + step.name + " | " + message.content);
            File.WriteAllText(Path.Combine(output, "build-warnings.txt"), messages.ToString());

            if (report.summary.result != BuildResult.Succeeded || !File.Exists(Path.Combine(playerFolder, "index.html")))
                throw new InvalidOperationException("Web build did not succeed. See " + Path.Combine(output, "build-report.txt"));
            Debug.Log("RELIC_WEB_BUILD_SUCCEEDED " + playerFolder);
        }
        finally
        {
            // Batch mode can start without a loaded authoring scene to restore.
            if (sceneSetup.Any(scene => scene.isLoaded) && sceneSetup.Count(scene => scene.isActive) == 1)
                EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
    }

    private static string CommandLineValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}
