using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.IO;

public static class Phase3SceneSeeder
{
    [MenuItem("Mining Safety AR/Phase 3 - Create Auth Scenes")]
    public static void CreateAuthScenes()
    {
        CreateScene("UI_Splash", "Assets/UI/Templates/Pages/SplashPage.uxml", typeof(MiningSafetyAR.UI.Pages.SplashPageController));
        CreateScene("UI_Login", "Assets/UI/Templates/Pages/LoginPage.uxml", typeof(MiningSafetyAR.UI.Pages.LoginPageController));
        CreateScene("UI_Register", "Assets/UI/Templates/Pages/RegisterPage.uxml", typeof(MiningSafetyAR.UI.Pages.RegisterPageController));
        // Add Dashboard placeholder for navigation target (so Login can navigate)
        // Dashboard will be fully built in Phase 4, but create empty scene now to avoid missing scene error
        CreatePlaceholderDashboard();

        AddScenesToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[Phase3Seeder] Auth scenes created and added to Build Settings");
    }

    static void CreateScene(string sceneName, string uxmlPath, System.Type controllerType)
    {
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        // Delete existing if present
        if (File.Exists(scenePath)) File.Delete(scenePath);
        if (File.Exists(scenePath + ".meta")) File.Delete(scenePath + ".meta");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Add main camera (required though UI Toolkit doesn't need it, keep for consistency)
        var camGo = new GameObject("Main Camera");
        camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 0, -10);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;
        cam.orthographic = true;
        cam.orthographicSize = 5f;

        // Add EventSystem for UI Toolkit? Not needed but harmless
        // Create UI root
        var uiGo = new GameObject($"{sceneName}_Controller");
        var doc = uiGo.AddComponent<UIDocument>();

        // Assign PanelSettings
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings/DefaultPanelSettings.asset");
        if (panelSettings != null) doc.panelSettings = panelSettings;
        else Debug.LogWarning($"[Phase3Seeder] PanelSettings not found for {sceneName}");

        // Assign UXML
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        if (uxml != null) doc.visualTreeAsset = uxml;
        else Debug.LogWarning($"[Phase3Seeder] UXML not found: {uxmlPath}");

        // Add controller
        uiGo.AddComponent(controllerType);

        // Save scene
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Phase3Seeder] Created {scenePath} with {controllerType.Name}");
    }

    static void CreatePlaceholderDashboard()
    {
        string sceneName = "UI_Dashboard";
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        if (File.Exists(scenePath)) { Debug.Log($"[Phase3Seeder] {sceneName} already exists, skipping placeholder"); return; }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera");
        camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 0, -10);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;

        var uiGo = new GameObject("Dashboard_Placeholder");
        var doc = uiGo.AddComponent<UIDocument>();
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings/DefaultPanelSettings.asset");
        if (panelSettings != null) doc.panelSettings = panelSettings;

        // Simple placeholder UXML via code? Create a label
        // For now leave visualTreeAsset null, add a text via UIDocument root in placeholder controller
        var placeholder = uiGo.AddComponent<DashboardPlaceholderController>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Phase3Seeder] Created placeholder {scenePath}");
    }

    static void AddScenesToBuildSettings()
    {
        var newScenes = new string[]
        {
            "Assets/Scenes/UI_Splash.unity",
            "Assets/Scenes/UI_Login.unity",
            "Assets/Scenes/UI_Register.unity",
            "Assets/Scenes/UI_Dashboard.unity",
        };

        var existing = EditorBuildSettings.scenes;
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(existing);
        foreach (var path in newScenes)
        {
            bool found = false;
            foreach (var s in list) if (s.path == path) { found = true; break; }
            if (!found)
            {
                list.Add(new EditorBuildSettingsScene(path, true));
                Debug.Log($"[Phase3Seeder] Added to BuildSettings: {path}");
            }
            else
            {
                // Ensure enabled
                foreach (var s in list) if (s.path == path) s.enabled = true;
            }
        }
        EditorBuildSettings.scenes = list.ToArray();
    }
}

// Minimal placeholder for Dashboard so Login can navigate without Phase 4 error
public class DashboardPlaceholderController : MonoBehaviour
{
    void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc != null)
        {
            // Delay to allow UIDocument to initialize
            Invoke(nameof(BuildUI), 0.1f);
        }
    }

    void BuildUI()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        var root = doc.rootVisualElement;
        root.Clear();
        root.style.backgroundColor = Color.white;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        var title = new Label("Dashboard - Phase 4 coming soon");
        title.style.fontSize = 18;
        title.style.color = new Color(0.1f, 0.1f, 0.1f);
        title.style.marginBottom = 12;
        root.Add(title);
        var sub = new Label("Login/Register successful! You are here because Auth works.");
        sub.style.fontSize = 12;
        sub.style.color = new Color(0.4f, 0.4f, 0.4f);
        sub.style.marginBottom = 12;
        root.Add(sub);
        var userLabel = new Label($"Logged in as: {(MiningSafetyAR.Firebase.FirebaseAuthManager.Instance?.CurrentUserEmail ?? "unknown")}");
        userLabel.style.fontSize = 11;
        userLabel.style.color = new Color(0.2f, 0.2f, 0.2f);
        userLabel.style.marginBottom = 12;
        root.Add(userLabel);
        var btn = new Button(() => {
            // Proper logout for testing
            var auth = MiningSafetyAR.Firebase.FirebaseAuthManager.Instance;
            if (auth != null) auth.Logout();
            var nav = FindFirstObjectByType<MiningSafetyAR.UI.Navigation.NavigationManager>();
            if (nav != null) nav.NavigateTo("UI_Login");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("UI_Login");
        });
        btn.text = "Logout & Back to Login";
        btn.style.backgroundColor = new Color(1f, 0.42f, 0f);
        btn.style.color = Color.white;
        btn.style.width = 220;
        btn.style.height = 48;
        root.Add(btn);
        var splashBtn = new Button(() => {
            var nav = FindFirstObjectByType<MiningSafetyAR.UI.Navigation.NavigationManager>();
            if (nav != null) nav.NavigateTo("UI_Splash");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("UI_Splash");
        });
        splashBtn.text = "Test Splash Again";
        splashBtn.style.backgroundColor = Color.white;
        splashBtn.style.color = new Color(1f, 0.42f, 0f);
        splashBtn.style.borderLeftColor = new Color(1f, 0.42f, 0f);
        splashBtn.style.borderRightColor = new Color(1f, 0.42f, 0f);
        splashBtn.style.borderTopColor = new Color(1f, 0.42f, 0f);
        splashBtn.style.borderBottomColor = new Color(1f, 0.42f, 0f);
        splashBtn.style.borderLeftWidth = 1;
        splashBtn.style.borderRightWidth = 1;
        splashBtn.style.borderTopWidth = 1;
        splashBtn.style.borderBottomWidth = 1;
        splashBtn.style.width = 220;
        splashBtn.style.height = 48;
        splashBtn.style.marginTop = 8;
        root.Add(splashBtn);
    }
}
