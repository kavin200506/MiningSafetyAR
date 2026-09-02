using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI
{
    [RequireComponent(typeof(UIDocument))]
    public abstract class PageController : MonoBehaviour, Localization.IVoiceCommandTarget
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
            // If scene is directly played (not via NavigationManager), auto-call OnPageEnter once
            if (!hasEntered)
            {
                hasEntered = true;
                Debug.Log($"[PageController] Auto-entered page controller: {GetType().Name} in scene: {gameObject.scene.name}");
                OnPageEnter();
            }
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

        #region IVoiceCommandTarget Default Virtual Implementations
        /// <summary>Default voice 'Next' handler — can be overridden by subclasses.</summary>
        public virtual void VoiceNext() { }

        /// <summary>Default voice 'Option 1-4' handler — can be overridden by subclasses.</summary>
        public virtual void VoiceSelectOption(int oneBasedIndex) { }

        /// <summary>Default voice 'Start' handler — can be overridden by subclasses.</summary>
        public virtual void VoiceStart() { }

        /// <summary>Default voice 'Yes' / 'Confirm' handler — can be overridden by subclasses.</summary>
        public virtual void VoiceConfirm() { }

        /// <summary>Default voice 'No' / 'Cancel' handler — defaults to NavigationManager GoBack().</summary>
        public virtual void VoiceCancel()
        {
            if (Navigation.NavigationManager.Instance != null)
                Navigation.NavigationManager.Instance.GoBack();
        }

        /// <summary>Default voice 'Repeat' handler — defaults to re-triggering OnPageEnter().</summary>
        public virtual void VoiceRepeat() => OnPageEnter();

        /// <summary>Default voice PASS step handler (Pull/Aim/Spray/Sweep) — for AR modules.</summary>
        public virtual void VoicePassStep(string step) { }
        #endregion
    }
}
