using System;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI.Helpers
{
    public class ToggleSwitchController
    {
        readonly VisualElement toggle;
        bool isOn;
        public event Action<bool> OnToggled;

        public ToggleSwitchController(VisualElement toggleRoot)
        {
            toggle = toggleRoot;
            if (toggle == null) return;
            toggle.RegisterCallback<ClickEvent>(evt =>
            {
                isOn = !isOn;
                UpdateVisual();
                OnToggled?.Invoke(isOn);
            });
            UpdateVisual();
        }

        public void SetValue(bool value)
        {
            isOn = value;
            UpdateVisual();
        }

        public bool GetValue() => isOn;

        void UpdateVisual()
        {
            if (toggle == null) return;
            if (isOn) toggle.AddToClassList("toggle-switch--on");
            else toggle.RemoveFromClassList("toggle-switch--on");
        }
    }
}
