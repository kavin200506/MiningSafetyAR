using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI
{
    [RequireComponent(typeof(UIDocument))]
    public abstract class PageController : MonoBehaviour
    {
        protected UIDocument document;
        protected VisualElement root;
        protected object navigationParameter;

        bool hasEntered = false;
        public bool HasEntered => hasEntered;

        protected virtual void OnEnable()
        {
            document = GetComponent<UIDocument>();
            // UIDocument may not be ready immediately — delay BindUI by one frame if needed
            if (document != null)
            {
                // Try immediate bind if root already has children (after domain reload Play)
                if (document.rootVisualElement != null && document.rootVisualElement.childCount > 0)
                {
                    root = document.rootVisualElement;
                    BindUI();
                    IconLoader.ApplyBottomNavIcons(root);
                    IconLoader.ApplyCommonIcons(root);
                    ApplyTheme(root);
                    TryAutoEnter();
                }
                else
                {
                    // Wait one frame for UIDocument to clone visualTreeAsset
                    Invoke(nameof(DelayedBind), 0.1f);
                }
            }
        }

        void DelayedBind()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document != null && document.rootVisualElement != null)
            {
                root = document.rootVisualElement;
                BindUI();
                IconLoader.ApplyBottomNavIcons(root);
                IconLoader.ApplyCommonIcons(root);
                ApplyTheme(root);
                TryAutoEnter();
            }
            else
            {
                Debug.LogWarning($"[PageController] {GetType().Name} root still null after delay");
            }
        }

        void TryAutoEnter()
        {
            // If scene is directly played (not via NavigationManager), auto-call OnPageEnter once
            if (!hasEntered)
            {
                hasEntered = true;
                // Let NavigationManager know current scene if it hasn't been set
                var nav = Navigation.NavigationManager.Instance;
                if (nav != null && string.IsNullOrEmpty(nav.CurrentScene))
                {
                    // NavigationManager will be set on next NavigateTo, but for direct play set via reflection
                }
                OnPageEnter();
            }
        }

        public void MarkEntered()
        {
            hasEntered = true;
        }

        protected void ApplyTheme(VisualElement rootElement)
        {
            if (rootElement == null) return;
            bool isDark = PlayerPrefs.GetInt("DarkMode", 0) == 1;
            if (isDark)
            {
                rootElement.AddToClassList("dark-theme");
            }
            else
            {
                rootElement.RemoveFromClassList("dark-theme");
            }
        }

        public virtual void SetNavigationParameter(object param)
        {
            navigationParameter = param;
        }

        protected abstract void BindUI();
        public virtual void OnPageEnter() { }
        public virtual void OnPageExit() { }
    }
}
