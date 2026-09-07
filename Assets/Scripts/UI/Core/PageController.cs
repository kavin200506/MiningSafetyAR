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
                TryAutoEnter();
            }
            else
            {
                Debug.LogWarning($"[PageController] {GetType().Name} root still null after delay");
            }
        }

        void TryAutoEnter()
        {
            // If scene is directly played (not via NavigationManager), auto-call OnPageEnter once.
            if (hasEntered) return;

            // A NavigateTo() call is currently loading this exact scene and is about to set the
            // real navigation parameter and call OnPageEnter() itself once its coroutine resumes.
            // That resume always happens a frame AFTER this OnEnable()/TryAutoEnter() runs (scene
            // activation calls Awake/OnEnable before LoadSceneAsync.isDone flips), so calling
            // OnPageEnter() here first would fire it with no parameter yet, then again moments
            // later with the correct one. Harmless for pages that just refresh a label, but
            // LocationCapturePageController starts a 5s coroutine from OnPageEnter() — firing
            // twice meant two of them running concurrently, racing on the same UI and on which one
            // actually gets to call NavigateTo("ar_fire_safety", ...) before the other's GameObject
            // is torn down. Defer to NavigationManager's own call in that case.
            if (Navigation.NavigationManager.Instance != null && Navigation.NavigationManager.IsNavigating)
            {
                return;
            }

            hasEntered = true;
            OnPageEnter();
        }

        public void MarkEntered()
        {
            hasEntered = true;
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
