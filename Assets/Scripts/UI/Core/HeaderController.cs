using UnityEngine.UIElements;
using MiningSafetyAR.UI.Navigation;

namespace MiningSafetyAR.UI.Core
{
    public class HeaderController
    {
        readonly VisualElement root;
        readonly Button backButton;
        readonly Label titleLabel;
        readonly VisualElement rightActionSlot;

        public HeaderController(VisualElement headerRoot)
        {
            root = headerRoot;
            if (root == null) return;
            backButton = headerRoot.Q<Button>("back-button");
            titleLabel = headerRoot.Q<Label>("title");
            rightActionSlot = headerRoot.Q("right-action");

            if (backButton != null)
                backButton.RegisterCallback<ClickEvent>(evt =>
                {
                    if (NavigationManager.Instance != null)
                        NavigationManager.Instance.GoBack();
                });
        }

        public void Configure(string title, bool showBack, VisualElement rightAction = null)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (backButton != null)
                backButton.style.display = showBack ? DisplayStyle.Flex : DisplayStyle.None;
            if (rightActionSlot != null)
            {
                rightActionSlot.Clear();
                if (rightAction != null)
                    rightActionSlot.Add(rightAction);
            }
        }
    }
}
