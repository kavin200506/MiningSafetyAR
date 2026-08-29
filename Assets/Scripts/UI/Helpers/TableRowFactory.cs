using UnityEngine.UIElements;
using UnityEngine;

namespace MiningSafetyAR.UI.Helpers
{
    public static class TableRowFactory
    {
        public static VisualElement Create(VisualTreeAsset template, int index, string date, string score, string status, bool isPass)
        {
            if (template == null) return new Label($"Row {index}");
            var row = template.Instantiate();
            var num = row.Q<Label>("row-num");
            if (num != null) num.text = index.ToString();
            var dateEl = row.Q<Label>("row-date");
            if (dateEl != null) dateEl.text = date;
            var scoreEl = row.Q<Label>("row-score");
            if (scoreEl != null) scoreEl.text = score;
            var statusEl = row.Q<Label>("row-status");
            if (statusEl != null)
            {
                statusEl.text = status;
                statusEl.style.color = isPass ? new Color(0.18f, 0.49f, 0.20f) : new Color(0.77f, 0.15f, 0.15f);
            }
            return row;
        }
    }
}
