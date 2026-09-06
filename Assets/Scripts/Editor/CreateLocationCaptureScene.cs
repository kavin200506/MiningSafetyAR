using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using System.IO;

public static class CreateLocationCaptureScene
{
    [MenuItem("Mining Safety AR/Setup Location Capture Scene")]
    public static void CreateSceneAndAddToBuildSettings()
    {
        string sceneName = "UI_LocationCapture";
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        string uxmlPath = "Assets/UI/Templates/Pages/UI_LocationCapture.uxml";

        if (File.Exists(scenePath))
        {
            File.Delete(scenePath);
            if (File.Exists(scenePath + ".meta")) File.Delete(scenePath + ".meta");
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 0, -10);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;
        cam.orthographic = false; // Perspective projection to avoid XR warnings

        var uiGo = new GameObject($"{sceneName}_Controller");
        var doc = uiGo.AddComponent<UIDocument>();

        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings/DefaultPanelSettings.asset");
        if (panelSettings != null) doc.panelSettings = panelSettings;

        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        if (uxml != null) doc.visualTreeAsset = uxml;

        uiGo.AddComponent<MiningSafetyAR.UI.Pages.LocationCapturePageController>();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);

        AddSceneToBuildSettings(scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[LocationCaptureSeeder] Successfully created {scenePath} and updated EditorBuildSettings.");
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool exists = false;
        foreach (var s in list)
        {
            if (s.path == scenePath) { exists = true; break; }
        }
        if (!exists)
        {
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
