using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Command-line entry points. Example:
///   Unity.exe -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod BuildScript.BuildWebGL
public static class BuildScript
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Shiftside/Setup Scene")]
    public static void SetupScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 7.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.17f, 0.09f, 0.12f);
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.AddComponent<AudioListener>();

        new GameObject("Game").AddComponent<GameManager>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        ConfigurePlayer();
        AssetDatabase.SaveAssets();
        Debug.Log("[Shiftside] Scene set up at " + ScenePath);
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "Agneay B Nair";
        PlayerSettings.productName = "Shiftside";
        PlayerSettings.runInBackground = false; // pause (and go silent) when not focused
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 540;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true; // required for itch.io hosting
        PlayerSettings.WebGL.dataCaching = true;
    }

    public static void BuildWebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Shiftside.exe");

    static void Build(BuildTarget target, string path)
    {
        SetupScene();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = target,
            options = BuildOptions.None
        });
        var s = report.summary;
        Debug.Log($"[Shiftside] Build {target}: {s.result}, {s.totalSize / (1024 * 1024f):F1} MB, {s.totalErrors} errors -> {path}");
        if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
