using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using System.IO;

public static class Phase6SceneSeeder
{
    [MenuItem("Mining Safety AR/Phase 6 - Create Results & Certs Scenes")]
    public static void CreateResultsScenes()
    {
        CreateScene("UI_Results", "Assets/UI/Templates/Pages/ResultsPage.uxml", typeof(MiningSafetyAR.UI.Pages.ResultsPageController), true);
        CreateScene("UI_Certificate", "Assets/UI/Templates/Pages/CertificatePage.uxml", typeof(MiningSafetyAR.UI.Pages.CertificatePageController), true);
        CreateScene("UI_QRVerify", "Assets/UI/Templates/Pages/QRVerifyPage.uxml", typeof(MiningSafetyAR.UI.Pages.QRVerifyPageController), true);
        CreateScene("UI_LocationCapture", "Assets/UI/Templates/Pages/UI_LocationCapture.uxml", typeof(MiningSafetyAR.UI.Pages.LocationCapturePageController), true);

        // Assign scoreBar template for Results
        AssignTemplate("UI_Results", "scoreBarTemplate", "Assets/UI/Templates/Components/ScoreBar.uxml");

        AddScenesToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[Phase6Seeder] Results & Certs scenes created");
    }

    static void CreateScene(string sceneName, string uxmlPath, System.Type controllerType, bool overwrite)
    {
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        if (File.Exists(scenePath))
        {
            if (!overwrite) { Debug.Log($"[Phase6Seeder] {sceneName} exists, skipping"); return; }
            File.Delete(scenePath);
            if (File.Exists(scenePath + ".meta")) File.Delete(scenePath + ".meta");
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera");
        camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0,0,-10);
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
        else Debug.LogWarning($"[Phase6Seeder] UXML not found: {uxmlPath}");
        uiGo.AddComponent(controllerType);
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Phase6Seeder] Created {scenePath}");
    }

    static void AssignTemplate(string sceneName, string fieldName, string templatePath)
    {
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
        if (template == null) return;
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var all = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in all)
        {
            var so = new SerializedObject(mb);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = template;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mb);
                Debug.Log($"[Phase6Seeder] Assigned {fieldName} for {mb.GetType().Name} in {sceneName}");
            }
        }
        EditorSceneManager.SaveScene(scene);
    }

    static void AddScenesToBuildSettings()
    {
        string[] newScenes = { "Assets/Scenes/UI_LocationCapture.unity", "Assets/Scenes/UI_Results.unity", "Assets/Scenes/UI_Certificate.unity", "Assets/Scenes/UI_QRVerify.unity" };
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
