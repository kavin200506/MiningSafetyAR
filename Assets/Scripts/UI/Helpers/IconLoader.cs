using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI.Helpers
{
    public static class IconLoader
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;
            if (cache.TryGetValue(iconName, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>("Icons/" + iconName);
            if (sprite != null) cache[iconName] = sprite;
            return sprite;
        }

        public static void ApplyTo(VisualElement element, string iconName)
        {
            if (element == null) return;
            var sprite = Get(iconName);
            if (sprite != null)
                element.style.backgroundImage = new StyleBackground(sprite);
        }

        static readonly Dictionary<string, string> ModuleIcons = new Dictionary<string, string>
        {
            { "fire_safety", "module_fire" },
            { "gas_safety", "module_gas" },
            { "machinery_safety", "module_machinery" },
            { "electrical_safety", "module_electrical" },
            { "heights_safety", "module_heights" }
        };

        static readonly Dictionary<string, string> SlideIcons = new Dictionary<string, string>
        {
            { "slide-icon-fire", "slide_fire" },
            { "slide-icon-extinguisher", "slide_extinguisher" },
            { "slide-icon-evacuation", "slide_evacuation" },
            { "slide-icon-gas", "slide_gas" },
            { "slide-icon-ppe", "slide_ppe" },
            { "slide-icon-confined", "slide_confined" },
            { "slide-icon-lockout", "slide_lockout" },
            { "slide-icon-guarding", "slide_guarding" },
            { "slide-icon-operation", "slide_operation" },
            { "slide-icon-electrical", "slide_electrical" },
            { "slide-icon-gloves", "slide_gloves" },
            { "slide-icon-fall", "slide_fall" },
            { "slide-icon-ladder", "slide_ladder" },
            { "slide-icon-tip", "slide_tip" }
        };

        static readonly Dictionary<string, string> ArIcons = new Dictionary<string, string>
        {
            { "ar-icon-scan", "ar_scan" },
            { "ar-icon-move", "ar_move" },
            { "ar-icon-check", "ar_check" },
            { "ar-icon-fire", "ar_fire" },
            { "ar-icon-extinguisher", "ar_extinguisher" },
            { "ar-icon-unlock", "ar_unlock" },
            { "ar-icon-spray", "ar_spray" }
        };

        public static string GetModuleName(string moduleId)
        {
            return ModuleIcons.TryGetValue(moduleId, out var name) ? name : "module_fire";
        }

        public static string GetSlideIcon(string iconClass)
        {
            return SlideIcons.TryGetValue(iconClass, out var name) ? name : "slide_fire";
        }

        public static string GetArIcon(string iconClass)
        {
            return ArIcons.TryGetValue(iconClass, out var name) ? name : "ar_check";
        }

        public static void ApplyModuleIcon(VisualElement element, string moduleId)
        {
            ApplyTo(element, GetModuleName(moduleId));
        }

        public static void ApplyByClass(VisualElement element, string iconClass)
        {
            if (SlideIcons.TryGetValue(iconClass, out var slideName))
                ApplyTo(element, slideName);
            else if (ArIcons.TryGetValue(iconClass, out var arName))
                ApplyTo(element, arName);
            else
                ApplyTo(element, iconClass);
        }

        public static void ApplyBottomNavIcons(VisualElement root)
        {
            if (root == null) return;
            ApplyTo(root.Q<VisualElement>("tab-home")?.Q<VisualElement>() ?? root.Q("icon-home"), "icon_home");
            ApplyTo(root.Q<VisualElement>("tab-training")?.Q<VisualElement>() ?? root.Q("icon-training"), "icon_training");
            ApplyTo(root.Q<VisualElement>("tab-progress")?.Q<VisualElement>() ?? root.Q("icon-progress"), "icon_progress");
            ApplyTo(root.Q<VisualElement>("tab-settings")?.Q<VisualElement>() ?? root.Q("icon-settings"), "icon_settings");
        }

        public static void ApplyCommonIcons(VisualElement root)
        {
            if (root == null) return;
            ApplyTo(root.Q("logo-shield"), "logo_shield");
            ApplyTo(root.Q("icon-back"), "icon_back");
            ApplyTo(root.Q("icon-eye"), "icon_eye");
            ApplyTo(root.Q("icon-search"), "icon_search");
            ApplyTo(root.Q("icon-lock"), "icon_lock");
            ApplyTo(root.Q("icon-trophy"), "icon_trophy");
            ApplyTo(root.Q("avatar-worker"), "avatar_worker");
            ApplyTo(root.Q("slide-icon-tip"), "slide_tip");
        }
    }
}
