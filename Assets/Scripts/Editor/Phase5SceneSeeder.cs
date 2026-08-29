using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using System.IO;

public static class Phase5SceneSeeder
{
    [MenuItem("Mining Safety AR/Phase 5 - Create AR Flow Scenes")]
    public static void CreateARFlowScenes()
    {
        CreateScene("UI_LearningContent", "Assets/UI/Templates/Pages/LearningContentPage.uxml", typeof(MiningSafetyAR.UI.Pages.LearningContentPageController));
        CreateScene("UI_ARSimulation", "Assets/UI/Templates/Pages/ARSimulationPage.uxml", typeof(MiningSafetyAR.UI.Pages.ARSimulationPageController));
        CreateScene("UI_Assessment", "Assets/UI/Templates/Pages/AssessmentPage.uxml", typeof(MiningSafetyAR.UI.Pages.AssessmentPageController));
        // Placeholder Results for Phase 5 verification (will be overwritten in Phase 6)
        CreatePlaceholderResults();

        AddScenesToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[Phase5Seeder] AR Flow scenes created");
    }

    static void CreateScene(string sceneName, string uxmlPath, System.Type controllerType)
    {
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
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
        else Debug.LogWarning($"[Phase5Seeder] UXML not found: {uxmlPath}");
        uiGo.AddComponent(controllerType);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Phase5Seeder] Created {scenePath}");
    }

    static void CreatePlaceholderResults()
    {
        string sceneName = "UI_Results";
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        if (File.Exists(scenePath)) { Debug.Log($"[Phase5Seeder] {sceneName} already exists, skipping placeholder"); return; }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera");
        camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 0, -10);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;
        var uiGo = new GameObject("Results_Placeholder");
        var doc = uiGo.AddComponent<UIDocument>();
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings/DefaultPanelSettings.asset");
        if (panelSettings != null) doc.panelSettings = panelSettings;
        uiGo.AddComponent<ResultsPlaceholderController>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Phase5Seeder] Created placeholder {scenePath}");
    }

    static void AddScenesToBuildSettings()
    {
        string[] newScenes = { "Assets/Scenes/UI_LearningContent.unity", "Assets/Scenes/UI_ARSimulation.unity", "Assets/Scenes/UI_Assessment.unity", "Assets/Scenes/UI_Results.unity" };
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

public class ResultsPlaceholderController : MonoBehaviour
{
    void OnEnable() => Invoke(nameof(BuildUI), 0.1f);
    void BuildUI()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        var root = doc.rootVisualElement;
        root.Clear();
        root.style.backgroundColor = Color.white;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        root.style.paddingTop = 40;
        var title = new Label("Results - Phase 6 coming soon");
        title.style.fontSize = 18; title.style.color = new Color(0.1f,0.1f,0.1f);
        root.Add(title);
        var sub = new Label("Assessment Complete! Score saved to Firestore.");
        sub.style.fontSize = 12; sub.style.color = new Color(0.4f,0.4f,0.4f);
        sub.style.marginTop = 8;
        root.Add(sub);
        var btn = new Button(() => {
            var nav = FindFirstObjectByType<MiningSafetyAR.UI.Navigation.NavigationManager>();
            if (nav != null) nav.NavigateTo("UI_Dashboard");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("UI_Dashboard");
        });
        btn.text = "Back to Dashboard";
        btn.style.backgroundColor = new Color(1f,0.42f,0f);
        btn.style.color = Color.white;
        btn.style.width = 200; btn.style.height = 48; btn.style.marginTop = 16;
        root.Add(btn);
    }
}
