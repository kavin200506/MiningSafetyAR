using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI.Pages
{
    public class TrainingCataloguePageController : PageController
    {
        [SerializeField] VisualTreeAsset moduleCardTemplate;

        Button[] filterButtons;
        VisualElement moduleList;
        Label moduleCount;
        string activeFilter = "all";
        TextField searchField;
        string searchQuery = "";

        protected override void BindUI()
        {
            moduleList = root.Q("module-list");
            moduleCount = root.Q<Label>("module-count");
            
            searchField = root.Q<TextField>("search-field");
            var searchPlaceholder = root.Q<Label>("search-placeholder");
            var searchIconBtn = root.Q<VisualElement>("search-icon-btn");
            
            if (searchField != null)
            {
                if (searchIconBtn != null)
                {
                    searchIconBtn.RegisterCallback<PointerDownEvent>(e => searchField.Focus());
                }
                
                searchField.RegisterValueChangedCallback(e => {
                    searchQuery = e.newValue?.ToLower() ?? "";
                    if (searchPlaceholder != null)
                        searchPlaceholder.style.display = string.IsNullOrEmpty(e.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                    Refresh();
                });
            }

            if (moduleCardTemplate == null)
            {
                moduleCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/Components/ModuleCard");
#if UNITY_EDITOR
                if (moduleCardTemplate == null)
                    moduleCardTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Templates/Components/ModuleCard.uxml");
#endif
            }

            filterButtons = new Button[]
            {
                root.Q<Button>("filter-all"),
                root.Q<Button>("filter-completed"),
                root.Q<Button>("filter-in-progress"),
                root.Q<Button>("filter-not-started")
            };

            string[] filters = { "all", "completed", "in_progress", "not_started" };
            for (int i = 0; i < filterButtons.Length; i++)
            {
                int idx = i;
                if (filterButtons[i] != null)
                    filterButtons[i].RegisterCallback<ClickEvent>(e => SetFilter(filters[idx]));
            }

            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
        }

        public override void OnPageEnter() => Refresh();

        void SetFilter(string filter)
        {
            activeFilter = filter;
            for (int i = 0; i < filterButtons.Length; i++)
            {
                if (filterButtons[i] == null) continue;
                filterButtons[i].RemoveFromClassList("filter-pill--active");
                
                // Reset to inactive styles
                filterButtons[i].style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f));
                filterButtons[i].style.color = new StyleColor(new Color(102f/255f, 102f/255f, 102f/255f));
                filterButtons[i].style.borderTopWidth = 1;
                filterButtons[i].style.borderRightWidth = 1;
                filterButtons[i].style.borderBottomWidth = 1;
                filterButtons[i].style.borderLeftWidth = 1;
                filterButtons[i].style.borderTopColor = new StyleColor(new Color(230f/255f, 230f/255f, 230f/255f));
                filterButtons[i].style.borderRightColor = new StyleColor(new Color(230f/255f, 230f/255f, 230f/255f));
                filterButtons[i].style.borderBottomColor = new StyleColor(new Color(230f/255f, 230f/255f, 230f/255f));
                filterButtons[i].style.borderLeftColor = new StyleColor(new Color(230f/255f, 230f/255f, 230f/255f));
            }
            
            int activeIdx = 0;
            if (activeFilter == "completed") activeIdx = 1;
            else if (activeFilter == "in_progress") activeIdx = 2;
            else if (activeFilter == "not_started") activeIdx = 3;
            
            if (filterButtons[activeIdx] != null)
            {
                filterButtons[activeIdx].AddToClassList("filter-pill--active");
                filterButtons[activeIdx].style.backgroundColor = new StyleColor(new Color(1f, 109f/255f, 0f));
                filterButtons[activeIdx].style.color = new StyleColor(new Color(1f, 1f, 1f));
                filterButtons[activeIdx].style.borderTopWidth = 0;
                filterButtons[activeIdx].style.borderRightWidth = 0;
                filterButtons[activeIdx].style.borderBottomWidth = 0;
                filterButtons[activeIdx].style.borderLeftWidth = 0;
            }

            Refresh();
        }

        void Refresh()
        {
            var app = AppDataService.Instance;
            if (app == null) return;
            var modules = app.GetAllModulesWithProgress();
            if (activeFilter != "all")
            {
                ModuleStatus status = activeFilter switch
                {
                    "completed" => ModuleStatus.Completed,
                    "in_progress" => ModuleStatus.InProgress,
                    "not_started" => ModuleStatus.NotStarted,
                    _ => ModuleStatus.NotStarted
                };
                modules = modules.FindAll(m => m.status == status);
            }
            if (!string.IsNullOrEmpty(searchQuery))
            {
                modules = modules.FindAll(m => m.title.ToLower().Contains(searchQuery) || m.description.ToLower().Contains(searchQuery));
            }
            if (moduleCount != null) moduleCount.text = $"{modules.Count} modules";
            if (moduleList == null) return;
            moduleList.Clear();
            if (modules.Count == 0)
            {
                var empty = new Label("No modules found");
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.color = new StyleColor(new Color(0.44f, 0.44f, 0.44f));
                empty.style.marginTop = 40;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                moduleList.Add(empty);
                return;
            }
            foreach (var mod in modules)
            {
                var card = ModuleCardFactory.Create(moduleCardTemplate, mod, OnModuleClicked);
                moduleList.Add(card);
            }
        }

        void OnModuleClicked(ModuleData mod) => NavigationManager.Instance.NavigateTo("UI_ModuleDetail", mod.id);
    }
}
