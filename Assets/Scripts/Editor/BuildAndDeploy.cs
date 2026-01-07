using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using UnityEngine;

public class BuildAndDeploy
{
    // Path to your .NET server's game Build folder
    private const string TargetPath = "/Users/christopherheld/Development/Game Development/WebSocketServerDotNet/wwwroot/game/Build";

    [MenuItem("Build/Update WebGL on Server")]
    public static void PerformBuild()
    {
        // 1. Configure the build
        string buildPath = "Builds/WebGL"; // Temporary local folder in Unity project
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();

        // This will find all scenes currently in your Build Settings
        buildPlayerOptions.scenes = GetEnabledScenes();
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.WebGL;
        buildPlayerOptions.options = BuildOptions.None;

        // 2. Run the build
        Debug.Log("Starting WebGL Build...");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Build succeeded! Syncing files to .NET Server...");

            // 3. Move only the 'Build' folder contents to your web server
            // Note: This matches the "Build" directory Unity generates
            string sourceBuildDir = Path.Combine(buildPath, "Build");

            if (Directory.Exists(sourceBuildDir))
            {
                SyncDirectories(sourceBuildDir, TargetPath);
                Debug.Log($"SUCCESS: WebGL build files updated at: {TargetPath}");
            }
            else
            {
                Debug.LogError("Could not find the 'Build' folder in the output. Check Player Settings > Product Name is set correctly.");
            }
        }
        else
        {
            Debug.LogError("Unity Build Failed. Check the console for errors.");
        }
    }

    private static string[] GetEnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        var enabledScenes = new System.Collections.Generic.List<string>();
        foreach (var scene in scenes)
        {
            if (scene.enabled) enabledScenes.Add(scene.path);
        }
        return enabledScenes.ToArray();
    }

    private static void SyncDirectories(string source, string target)
    {
        if (!Directory.Exists(target)) Directory.CreateDirectory(target);

        // Delete old files in target to ensure a clean sync
        foreach (string file in Directory.GetFiles(target))
        {
            File.Delete(file);
        }

        // Copy new files
        foreach (string file in Directory.GetFiles(source))
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(target, fileName);
            File.Copy(file, destFile, true);
        }
    }
}