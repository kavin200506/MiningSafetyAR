using UnityEngine.UIElements;
using UnityEngine;

namespace MiningSafetyAR.UI.Helpers
{
    public static class ScoreBarHelper
    {
        public static void Configure(VisualElement root, string label, int value, int maxValue = 100)
        {
            if (root == null) return;
            var labelEl = root.Q<Label>("score-label");
            if (labelEl != null) labelEl.text = label;
            var valueEl = root.Q<Label>("score-value");
            if (valueEl != null) valueEl.text = $"{value}%";

            float pct = (float)value / Mathf.Max(1, maxValue) * 100f;
            string color = pct >= 80f ? "#4CAF50" : pct >= 60f ? "#FF6D00" : "#F44336";
            var track = root.Q("track");
            if (track != null) ProgressBarHelper.SetProgress(track, pct, color);
        }
    }
}
