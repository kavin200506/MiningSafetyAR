# 06 — Navigation System

## NavigationManager.cs

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

namespace MiningSafetyAR.UI
{
    public class NavigationManager : MonoBehaviour
    {
        public static NavigationManager Instance { get; private set; }

        // Back stack stores scene names
        private Stack<string> backStack = new Stack<string>();
        private string currentScene;

        // Bottom nav visibility
        private static readonly HashSet<string> bottomNavVisibleScenes
            = new HashSet<string>
        {
            "UI_Dashboard", "UI_TrainingCatalogue",
            "UI_ModuleDetail", "UI_Results", "UI_Certificate",
            "UI_Progress", "UI_Settings", "UI_QRVerify"
        };

        private static readonly Dictionary<string, int> sceneTabMap
            = new Dictionary<string, int>
        {
            { "UI_Dashboard", 0 },
            { "UI_TrainingCatalogue", 1 },
            { "UI_ModuleDetail", 1 },
            { "UI_Progress", 2 },
            { "UI_Settings", 3 }
        };

        public string CurrentScene => currentScene;
        public event Action<string, object> OnSceneNavigated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Navigate to a page scene. Replaces current scene.
        /// </summary>
        public void NavigateTo(string sceneName, object param = null,
                                bool pushToStack = true)
        {
            // Validate scene exists
            if (!SceneExists(sceneName))
            {
                Debug.LogError($"[Nav] Scene not found: {sceneName}");
                return;
            }

            // Push current to stack
            if (!string.IsNullOrEmpty(currentScene) && pushToStack)
            {
                backStack.Push(currentScene);
            }

            // Load new scene (replaces current)
            LoadScene(sceneName, () =>
            {
                // Pass parameter to the new page
                var page = FindFirstObjectByType<PageController>();
                if (page != null && param != null)
                {
                    page.SetNavigationParameter(param);
                    page.OnPageEnter();
                }

                currentScene = sceneName;
                UpdateBottomNav(sceneName);
                OnSceneNavigated?.Invoke(sceneName, param);
            });
        }

        /// <summary>
        /// Go back to previous scene.
        /// </summary>
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

        /// <summary>
        /// Navigate to root (clear stack). For logout.
        /// </summary>
        public void NavigateToRoot(string sceneName)
        {
            backStack.Clear();
            NavigateTo(sceneName, pushToStack: false);
        }

        /// <summary>
        /// For bottom nav tabs — clear stack.
        /// </summary>
        public void NavigateToTab(string sceneName)
        {
            backStack.Clear();
            NavigateTo(sceneName, pushToStack: false);
        }

        private void LoadScene(string sceneName, Action onComplete)
        {
            // Async load with callback
            StartCoroutine(LoadSceneAsync(sceneName, onComplete));
        }

        private System.Collections.IEnumerator LoadSceneAsync(
            string sceneName, Action onComplete)
        {
            var op = SceneManager.LoadSceneAsync(sceneName,
                LoadSceneMode.Single);
            while (!op.isDone)
                yield return null;
            onComplete?.Invoke();
        }

        private void UpdateBottomNav(string sceneName)
        {
            bool visible = bottomNavVisibleScenes.Contains(sceneName);

            // BottomNav exists in each scene that needs it
            var bottomNav = FindFirstObjectByType<BottomNavController>();
            if (bottomNav != null)
            {
                bottomNav.SetVisibility(visible);
                if (visible && sceneTabMap.TryGetValue(sceneName,
                    out int tab))
                {
                    bottomNav.SetActiveTab(tab);
                }
            }
        }

        private bool SceneExists(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility
                    .GetScenePathByBuildIndex(i);
                string name = System.IO.Path
                    .GetFileNameWithoutExtension(path);
                if (name == sceneName) return true;
            }
            return false;
        }
    }
}
```

## PageController.cs (Base Class)

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI
{
    [RequireComponent(typeof(UIDocument))]
    public abstract class PageController : MonoBehaviour
    {
        protected UIDocument document;
        protected VisualElement root;
        protected object navigationParameter;

        protected virtual void OnEnable()
        {
            document = GetComponent<UIDocument>();
            root = document.rootVisualElement;
            BindUI();
        }

        public virtual void SetNavigationParameter(object param)
        {
            navigationParameter = param;
        }

        protected abstract void BindUI();
        public virtual void OnPageEnter() { }
    }
}
```

## BottomNavController.cs

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI
{
    public class BottomNavController : MonoBehaviour
    {
        private VisualElement root;
        private Button[] tabs;
        private int activeIndex = 0;

        private static readonly string[] tabScenes = {
            "UI_Dashboard", "UI_TrainingCatalogue",
            "UI_Progress", "UI_Settings"
        };

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            tabs = new Button[4];
            tabs[0] = root.Q<Button>("tab-home");
            tabs[1] = root.Q<Button>("tab-training");
            tabs[2] = root.Q<Button>("tab-progress");
            tabs[3] = root.Q<Button>("tab-settings");

            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i]?.RegisterCallback<ClickEvent>(evt =>
                    OnTabClicked(index));
            }
        }

        private void OnTabClicked(int index)
        {
            NavigationManager.Instance
                .NavigateToTab(tabScenes[index]);
        }

        public void SetActiveTab(int index)
        {
            if (index < 0 || index >= tabs.Length) return;
            tabs[activeIndex]?.RemoveFromClassList(
                "bottom-nav__tab--active");
            activeIndex = index;
            tabs[activeIndex]?.AddToClassList(
                "bottom-nav__tab--active");
        }

        public void SetVisibility(bool visible)
        {
            root.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
```

## Scene Loading Notes

- Each scene is loaded with `LoadSceneMode.Single` (replaces current)
- This means only one scene is loaded at a time (except AR additive)
- The `NavigationManager` persists across scenes via `DontDestroyOnLoad`
- Each scene has its own `UIDocument` + `PageController`
- The `BottomNav` only appears in scenes that need it

## Passing Data Between Scenes

Since we use `LoadSceneMode.Single`, data must be passed through:
1. **Navigation parameter** — `NavigateTo("scene", dataObject)`
2. **AppDataService singleton** — Persistent data service
3. **PlayerPrefs** — Simple key-value persistence
4. **Static references** — For small temporary data

```csharp
// Example: Navigate to module detail with module ID
NavigationManager.Instance.NavigateTo(
    "UI_ModuleDetail", "fire_safety");

// In ModuleDetailPageController:
public override void SetNavigationParameter(object param)
{
    moduleId = param as string;
}

public override void OnPageEnter()
{
    var mod = AppDataService.Instance.GetModule(moduleId);
    RefreshUI(mod);
}
```
