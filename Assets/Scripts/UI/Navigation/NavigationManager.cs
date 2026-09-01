using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using MiningSafetyAR.UI;

namespace MiningSafetyAR.UI.Navigation
{
    public class NavigationManager : MonoBehaviour
    {
        public static NavigationManager Instance { get; private set; }

        private Stack<string> backStack = new Stack<string>();
        private string currentScene;

        private static readonly HashSet<string> bottomNavVisibleScenes = new HashSet<string>
        {
            "UI_Dashboard", "UI_TrainingCatalogue",
            "UI_ModuleDetail",
            "UI_Progress", "UI_Settings"
        };

        private static readonly Dictionary<string, int> sceneTabMap = new Dictionary<string, int>
        {
            { "UI_Dashboard", 0 },
            { "UI_TrainingCatalogue", 1 },
            { "UI_ModuleDetail", 1 },
            { "UI_Progress", 2 },
            { "UI_Settings", 3 }
        };

        public string CurrentScene => currentScene;
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

        public void NavigateTo(string sceneName, object param = null, bool pushToStack = true)
        {
            Debug.Log($"[Nav] >>> NAVIGATING TO: '{sceneName}' (param: {param ?? "none"})");

            if (!SceneExists(sceneName))
            {
                Debug.LogError($"[Nav] FAILED: Scene not found in BuildSettings: '{sceneName}'");
                return;
            }

            if (!string.IsNullOrEmpty(currentScene) && pushToStack)
            {
                backStack.Push(currentScene);
            }

            LoadScene(sceneName, () =>
            {
                var page = FindFirstObjectByType<PageController>();
                Debug.Log($"[Nav] <<< SCENE LOADED OK: '{sceneName}' -> Active Controller: {(page != null ? page.GetType().Name : "None")}");

                if (page != null)
                {
                    if (param != null) page.SetNavigationParameter(param);
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
            if (backStack.Count == 0)
            {
                Debug.LogWarning("[Nav] Back stack empty.");
                return;
            }
            string previousScene = backStack.Pop();
            NavigateTo(previousScene, pushToStack: false);
        }

        public void NavigateToRoot(string sceneName)
        {
            backStack.Clear();
            NavigateTo(sceneName, pushToStack: false);
        }

        public void NavigateToTab(string sceneName)
        {
            backStack.Clear();
            NavigateTo(sceneName, pushToStack: false);
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
