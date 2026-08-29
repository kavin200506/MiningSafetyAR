using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.IO;

public static class Phase4SceneSeeder
{
    [MenuItem("Mining Safety AR/Phase 4 - Create Main Flow Scenes")]
    public static void CreateMainFlowScenes()
    {
        CreateScene("UI_Dashboard", "Assets/UI/Templates/Pages/DashboardPage.uxml", typeof(MiningSafetyAR.UI.Pages.DashboardPageController), true);
        CreateScene("UI_TrainingCatalogue", "Assets/UI/Templates/Pages/TrainingCataloguePage.uxml", typeof(MiningSafetyAR.UI.Pages.TrainingCataloguePageController), false);
        CreateScene("UI_ModuleDetail", "Assets/UI/Templates/Pages/ModuleDetailPage.uxml", typeof(MiningSafetyAR.UI.Pages.ModuleDetailPageController), false);
        CreateScene("UI_Progress", "Assets/UI/Templates/Pages/ProgressPage.uxml", typeof(MiningSafetyAR.UI.Pages.ProgressPageController), false);
        CreateScene("UI_Settings", "Assets/UI/Templates/Pages/SettingsPage.uxml", typeof(MiningSafetyAR.UI.Pages.SettingsPageController), false);

        // Assign templates via serialized fields
        AssignModuleCardTemplate("UI_Dashboard", "Assets/UI/Templates/Components/ModuleCard.uxml");
        AssignModuleCardTemplate("UI_TrainingCatalogue", "Assets/UI/Templates/Components/ModuleCard.uxml");
        AssignScoreBarTemplate("UI_ModuleDetail", "Assets/UI/Templates/Components/ScoreBar.uxml");
        AssignScoreBarTemplate("UI_Progress", "Assets/UI/Templates/Components/ScoreBar.uxml");

        AddScenesToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[Phase4Seeder] Main flow scenes created");
    }

    static void CreateScene(string sceneName, string uxmlPath, System.Type controllerType, bool overwrite)
    {
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        if (File.Exists(scenePath) && !overwrite)
        {
            Debug.Log($"[Phase4Seeder] {sceneName} exists, skipping (overwrite=false)");
            return;
        }
        // Allow overwrite for Dashboard placeholder
        if (File.Exists(scenePath)) File.Delete(scenePath);
        if (File.Exists(scenePath + ".meta")) File.Delete(scenePath + ".meta");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera");
        camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 0, -10);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;
        cam.orthographic = true;

        var uiGo = new GameObject($"{sceneName}_Controller");
        var doc = uiGo.AddComponent<UIDocument>();
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings/DefaultPanelSettings.asset");
        if (panelSettings != null) doc.panelSettings = panelSettings;
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        if (uxml != null) doc.visualTreeAsset = uxml;
        else Debug.LogWarning($"[Phase4Seeder] UXML not found: {uxmlPath}");
        uiGo.AddComponent(controllerType);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Phase4Seeder] Created {scenePath}");
    }

    static void AssignModuleCardTemplate(string sceneName, string templatePath)
    {
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
        if (template == null) return;
        // Open scene, find controller, assign field
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        // Find and assign via SerializedObject
        var all = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in all)
        {
            if (mb is MiningSafetyAR.UI.Pages.DashboardPageController d)
            {
                var so = new SerializedObject(d);
                var prop = so.FindProperty("moduleCardTemplate");
                if (prop != null) { prop.objectReferenceValue = template; so.ApplyModifiedProperties(); EditorUtility.SetDirty(d); }
            }
            if (mb is MiningSafetyAR.UI.Pages.TrainingCataloguePageController t)
            {
                var so = new SerializedObject(t);
                var prop = so.FindProperty("moduleCardTemplate");
                if (prop != null) { prop.objectReferenceValue = template; so.ApplyModifiedProperties(); EditorUtility.SetDirty(t); }
            }
        }
        EditorSceneManager.SaveScene(scene);
    }

    static void AssignScoreBarTemplate(string sceneName, string templatePath)
    {
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
        if (template == null) return;
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var all = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in all)
        {
            if (mb is MiningSafetyAR.UI.Pages.ModuleDetailPageController m)
            {
                var so = new SerializedObject(m);
                var prop = so.FindProperty("scoreBarTemplate");
                if (prop != null) { prop.objectReferenceValue = template; so.ApplyModifiedProperties(); EditorUtility.SetDirty(m); }
            }
            if (mb is MiningSafetyAR.UI.Pages.ProgressPageController p)
            {
                var so = new SerializedObject(p);
                var prop = so.FindProperty("scoreBarTemplate");
                if (prop != null) { prop.objectReferenceValue = template; so.ApplyModifiedProperties(); EditorUtility.SetDirty(p); }
            }
        }
        EditorSceneManager.SaveScene(scene);
    }

    static void AddScenesToBuildSettings()
    {
        string[] newScenes = { "Assets/Scenes/UI_Dashboard.unity", "Assets/Scenes/UI_TrainingCatalogue.unity", "Assets/Scenes/UI_ModuleDetail.unity", "Assets/Scenes/UI_Progress.unity", "Assets/Scenes/UI_Settings.unity" };
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var path in newScenes)
        {
            bool found = false;
            foreach (var s in list) if (s.path == path) { found = true; s.enabled = true; break; }
            if (!found) list.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = list.ToArray();
    }
}
