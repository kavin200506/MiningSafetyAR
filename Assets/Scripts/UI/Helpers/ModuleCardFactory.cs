using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.UI.Helpers
{
    public static class ModuleCardFactory
    {
        public static readonly Dictionary<string, string> ModuleIconClasses = new Dictionary<string, string>
        {
            { "fire_safety", "module-icon-fire" },
            { "gas_safety", "module-icon-gas" },
            { "machinery_safety", "module-icon-machinery" },
            { "electrical_safety", "module-icon-electrical" },
            { "heights_safety", "module-icon-heights" }
        };

        public static VisualElement Create(VisualTreeAsset template, ModuleData mod, Action<ModuleData> onClick)
        {
            if (template == null || mod == null) return new VisualElement();
            var card = template.Instantiate();

            var iconElement = card.Q<VisualElement>("icon-emoji");
            if (iconElement != null)
            {
                IconLoader.ApplyModuleIcon(iconElement, mod.id);
            }

            var title = card.Q<Label>("module-title");
            if (title != null) title.text = mod.title ?? "";

            var meta = card.Q<Label>("module-meta");
            if (meta != null) meta.text = $"{mod.duration} · {mod.difficulty}";

            var badge = card.Q<Label>("status-badge");
            if (badge != null)
            {
                badge.text = GetStatusText(mod.status);
                badge.AddToClassList($"badge--{mod.status.ToString().ToLower()}");
            }

            var fill = card.Q("progress-fill");
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp(mod.progress, 0, 100));

            var bestScore = card.Q<Label>("best-score");
            if (bestScore != null)
            {
                bestScore.style.display = DisplayStyle.None;
            }

            var iconBox = card.Q("icon-box");
            if (iconBox != null && !string.IsNullOrEmpty(mod.color))
            {
                try
                {
                    string hex = mod.color.Trim();
                    if (ColorUtility.TryParseHtmlString(hex, out var c))
                    {
                        c.a = 0.2f;
                        iconBox.style.backgroundColor = c;
                    }
                }
                catch { }
            }

            if (mod.status == ModuleStatus.Locked)
            {
                card.AddToClassList("module-card--locked");
                card.style.opacity = 0.5f;
                card.pickingMode = PickingMode.Ignore;
            }
            else
            {
                card.RegisterCallback<ClickEvent>(_ => onClick?.Invoke(mod));
                card.AddToClassList("pressable");
            }

            return card;
        }

        static string GetStatusText(ModuleStatus status) => status switch
        {
            ModuleStatus.Completed => "Completed",
            ModuleStatus.InProgress => "In Progress",
            ModuleStatus.NotStarted => "Not Started",
            ModuleStatus.Locked => "Locked",
            _ => ""
        };
    }
}
