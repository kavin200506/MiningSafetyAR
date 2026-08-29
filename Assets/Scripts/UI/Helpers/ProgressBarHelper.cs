using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI.Helpers
{
    public static class ProgressBarHelper
    {
        public static void SetProgress(VisualElement track, float value, string color = null)
        {
            if (track == null) return;
            var fill = track.Q("progress-fill");
            if (fill == null) return;
            float clamped = Mathf.Clamp(value, 0f, 100f);
            fill.style.width = Length.Percent(clamped);
            if (!string.IsNullOrEmpty(color))
            {
                if (ColorUtility.TryParseHtmlString(color, out var c)) fill.style.backgroundColor = c;
            }
            if (clamped >= 100f) fill.AddToClassList("progress-fill--complete");
            else fill.RemoveFromClassList("progress-fill--complete");
        }

        public static void SetProgressImmediate(VisualElement track, float value)
        {
            if (track == null) return;
            var fill = track.Q("progress-fill");
            if (fill == null) return;
            float clamped = Mathf.Clamp(value, 0f, 100f);
            fill.style.width = Length.Percent(clamped);
        }
    }
}
