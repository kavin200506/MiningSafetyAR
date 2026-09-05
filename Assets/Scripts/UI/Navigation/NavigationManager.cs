using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using MiningSafetyAR.UI;

namespace MiningSafetyAR.UI.Navigation
{
    [Serializable]
    public class NavEntry
    {
        public string sceneName;
        public object parameter;
    }

    public class NavigationManager : MonoBehaviour
    {
        public static NavigationManager Instance { get; private set; }

        private Stack<NavEntry> backStack = new Stack<NavEntry>();
        private string currentScene;
        private object currentParam;

        private static readonly HashSet<string> bottomNavVisibleScenes = new HashSet<string>
        {
            "UI_Dashboard", "UI_TrainingCatalogue",
            "UI_ModuleDetail", "UI_SubModuleList",
            "UI_Progress", "UI_Settings"
        };

        private static readonly Dictionary<string, int> sceneTabMap = new Dictionary<string, int>
        {
            { "UI_Dashboard", 0 },
            { "UI_TrainingCatalogue", 1 },
            { "UI_ModuleDetail", 1 },
            { "UI_SubModuleList", 1 },
            { "UI_Progress", 2 },
            { "UI_Settings", 3 }
        };

        public string CurrentScene => currentScene;
        public object CurrentParam => currentParam;
        public event Action<string, object> OnSceneNavigated;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            currentScene = SceneManager.GetActiveScene().name;
        }

        void Update()
        {
            // Android hardware back button or desktop Escape key
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleHardwareBack();
            }
        }

        public void HandleHardwareBack()
        {
            // If on auth or splash scenes, let Android handle OS-level exit
            if (currentScene == "UI_Splash" || currentScene == "UI_Login")
            {
                return;
            }

            if (currentScene == "UI_Register")
            {
                NavigateToRoot("UI_Login");
                return;
            }

            // If at main dashboard root with nothing on back stack, don't re-navigate
            if (currentScene == "UI_Dashboard" && backStack.Count == 0)
            {
                return;
            }

            GoBack();
        }

        public void NavigateTo(string sceneName, object param = null, bool pushToStack = true)
        {
            if (!SceneExists(sceneName))
            {
                Debug.LogError($"[Nav] Scene not in BuildSettings: {sceneName}");
                return;
            }

            if (!string.IsNullOrEmpty(currentScene) && pushToStack)
            {
                backStack.Push(new NavEntry { sceneName = currentScene, parameter = currentParam });
            }

            currentParam = param;

            LoadScene(sceneName, () =>
            {
                var page = FindFirstObjectByType<PageController>();
                if (page != null)
                {
                    if (param != null) page.SetNavigationParameter(param);
                    // Avoid double OnPageEnter if already auto-entered without param
                    if (!page.HasEntered || param != null)
                    {
                        page.MarkEntered();
                        page.OnPageEnter();
                    }
                }

                currentScene = sceneName;
                UpdateBottomNav(sceneName);
                OnSceneNavigated?.Invoke(sceneName, param);
            });
        }

        public void GoBack()
        {
            if (currentScene == "UI_Register")
            {
                NavigateToRoot("UI_Login");
                return;
            }

            while (backStack.Count > 0)
            {
                var entry = backStack.Pop();
                if (entry != null && !string.IsNullOrEmpty(entry.sceneName))
                {
                    // Skip navigating back into auth or splash pages when the user is already inside the app
                    if (entry.sceneName == "UI_Login" || entry.sceneName == "UI_Register" || entry.sceneName == "UI_Splash")
                    {
                        continue;
                    }

                    NavigateTo(entry.sceneName, param: entry.parameter, pushToStack: false);
                    return;
                }
            }

            // Fallback: If stack is empty or had only auth scenes, safely return to Dashboard
            if (currentScene != "UI_Dashboard")
            {
                Debug.LogWarning("[Nav] Back stack empty or exhausted. Returning to Dashboard.");
                NavigateToRoot("UI_Dashboard");
            }
        }

        public void NavigateToRoot(string sceneName)
        {
            backStack.Clear();
            NavigateTo(sceneName, param: null, pushToStack: false);
        }

        public void NavigateToTab(string sceneName)
        {
            backStack.Clear();
            NavigateTo(sceneName, param: null, pushToStack: false);
        }

        void LoadScene(string sceneName, Action onComplete)
        {
            StartCoroutine(LoadSceneAsync(sceneName, onComplete));
        }

        System.Collections.IEnumerator LoadSceneAsync(string sceneName, Action onComplete)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone)
                yield return null;
            onComplete?.Invoke();
        }

        void UpdateBottomNav(string sceneName)
        {
            bool visible = bottomNavVisibleScenes.Contains(sceneName);
            var bottomNav = FindFirstObjectByType<BottomNavController>();
            if (bottomNav != null)
            {
                bottomNav.SetVisibility(visible);
                if (visible && sceneTabMap.TryGetValue(sceneName, out int tab))
                {
                    bottomNav.SetActiveTab(tab);
                }
            }
        }

        bool SceneExists(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == sceneName) return true;
            }
            return false;
        }
    }
}
