using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI.Navigation;

namespace MiningSafetyAR.UI.Navigation
{
    public class BottomNavController : MonoBehaviour
    {
        [SerializeField] UIDocument document;

        VisualElement root;
        Button[] tabs;
        int activeIndex = 0;

        static readonly string[] tabScenes = {
            "UI_Dashboard", "UI_TrainingCatalogue",
            "UI_Progress", "UI_Settings"
        };

        void OnEnable()
        {
            var doc = document != null ? document : GetComponent<UIDocument>();
            if (doc == null) return;
            root = doc.rootVisualElement;
            tabs = new Button[4];
            tabs[0] = root.Q<Button>("tab-home");
            tabs[1] = root.Q<Button>("tab-training");
            tabs[2] = root.Q<Button>("tab-progress");
            tabs[3] = root.Q<Button>("tab-settings");

            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i]?.RegisterCallback<ClickEvent>(evt => OnTabClicked(index));
            }
        }

        void OnTabClicked(int index)
        {
            if (NavigationManager.Instance != null)
                NavigationManager.Instance.NavigateToTab(tabScenes[index]);
            else
                Debug.LogWarning($"[BottomNav] NavigationManager missing, click {index} ignored");
        }

        public void SetActiveTab(int index)
        {
            if (index < 0 || index >= tabs.Length) return;
            if (tabs[activeIndex] != null) tabs[activeIndex].RemoveFromClassList("bottom-nav__tab--active");
            activeIndex = index;
            if (tabs[activeIndex] != null) tabs[activeIndex].AddToClassList("bottom-nav__tab--active");
        }

        public void SetVisibility(bool visible)
        {
            if (root == null) return;
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
