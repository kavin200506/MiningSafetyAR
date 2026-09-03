using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Helpers;
using System.Collections.Generic;

namespace MiningSafetyAR.UI.Pages
{
    public class SubModuleListPageController : PageController
    {
        [SerializeField] VisualTreeAsset moduleCardTemplate;

        VisualElement moduleList;
        Label moduleCount;
        Label parentModuleTitle;
        Label parentModuleDesc;
        string parentModuleId;

        protected override void BindUI()
        {
            moduleList = root.Q("module-list");
            moduleCount = root.Q<Label>("module-count");
            parentModuleTitle = root.Q<Label>("parent-module-title");
            parentModuleDesc = root.Q<Label>("parent-module-desc");
            
            if (moduleCardTemplate == null)
            {
                moduleCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/Components/ModuleCard");
#if UNITY_EDITOR
                if (moduleCardTemplate == null)
                    moduleCardTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Templates/Components/ModuleCard.uxml");
#endif
            }

            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
            
            var backBtn = root.Q<Button>("back-btn");
            if (backBtn != null) backBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.GoBack());
        }

        public override void SetNavigationParameter(object param)
        {
            base.SetNavigationParameter(param);
            if (param is string id)
            {
                parentModuleId = id;
            }
        }

        public override void OnPageEnter() => Refresh();

        void Refresh()
        {
            var app = AppDataService.Instance;
            if (app == null) return;
            
            if (!string.IsNullOrEmpty(parentModuleId))
            {
                var parentModule = app.GetModule(parentModuleId);
                if (parentModule != null)
                {
                    if (parentModuleTitle != null) parentModuleTitle.text = parentModule.title;
                    if (parentModuleDesc != null) parentModuleDesc.text = "Select a specific hazard scenario.";
                }
            }

            List<ModuleData> subModules = app.GetSubModulesWithProgress(parentModuleId);
            
            if (moduleCount != null) moduleCount.text = $"{subModules.Count} scenarios";
            
            if (moduleList == null) return;
            moduleList.Clear();
            
            if (subModules.Count == 0)
            {
                var empty = new Label("No sub-modules found");
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.color = new StyleColor(new Color(0.44f, 0.44f, 0.44f));
                empty.style.marginTop = 40;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                moduleList.Add(empty);
                return;
            }
            
            foreach (var mod in subModules)
            {
                var card = ModuleCardFactory.Create(moduleCardTemplate, mod, OnSubModuleClicked);
                moduleList.Add(card);
            }
        }

        void OnSubModuleClicked(ModuleData mod) => NavigationManager.Instance.NavigateTo("UI_ModuleDetail", mod.id);
    }
}
