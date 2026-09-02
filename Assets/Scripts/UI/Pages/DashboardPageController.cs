using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI.Pages
{
    public class DashboardPageController : PageController, MiningSafetyAR.Localization.IVoiceCommandTarget
    {
        [SerializeField] VisualTreeAsset moduleCardTemplate;

        Label greetingText, progressValue;
        Label completedCount, certCount, attemptsCount;
        Label certBannerSub;
        VisualElement moduleList;
        VisualElement overallTrack;
        Button seeAllBtn, viewCertsBtn;

        protected override void BindUI()
        {
            greetingText = root.Q<Label>("greeting-text");
            progressValue = root.Q<Label>("progress-value");
            completedCount = root.Q<Label>("completed-count");
            certCount = root.Q<Label>("cert-count");
            attemptsCount = root.Q<Label>("attempts-count");
            certBannerSub = root.Q<Label>("cert-banner-sub");
            moduleList = root.Q("module-list");
            overallTrack = root.Q("overall-track");
            seeAllBtn = root.Q<Button>("see-all-btn");
            viewCertsBtn = root.Q<Button>("view-certs-btn");

            if (moduleCardTemplate == null)
            {
                moduleCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/Components/ModuleCard");
#if UNITY_EDITOR
                if (moduleCardTemplate == null)
                    moduleCardTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Templates/Components/ModuleCard.uxml");
#endif
            }

            // BottomNav tab clicks
            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));

            if (seeAllBtn != null) seeAllBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateTo("UI_TrainingCatalogue"));
            if (viewCertsBtn != null) viewCertsBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateTo("UI_Progress"));

            // Subscribe to worker loaded event — re-Refresh when async Firestore load completes
            if (AppDataService.Instance != null)
                AppDataService.Instance.OnWorkerLoaded += OnWorkerLoaded;
        }

        void OnDisable()
        {
            if (AppDataService.Instance != null)
                AppDataService.Instance.OnWorkerLoaded -= OnWorkerLoaded;
        }

        void OnWorkerLoaded(Data.WorkerData worker)
        {
            // Async Firestore load completed — refresh the dashboard with real data
            if (moduleList != null) Refresh();
        }

        public override void OnPageEnter()
        {
            Refresh();
        }

        void Refresh()
        {
            var app = AppDataService.Instance;
            var worker = app != null ? app.CurrentWorker : null;
            if (worker == null)
            {
                if (greetingText != null) greetingText.text = "Hello, Worker";
                return;
            }

            string firstName = worker.name.Split(' ')[0];
            if (greetingText != null) greetingText.text = $"Hello, {firstName}";
            if (progressValue != null) progressValue.text = $"{worker.overallProgress}%";
            if (overallTrack != null) ProgressBarHelper.SetProgress(overallTrack, worker.overallProgress);

            int completed = app.GetModulesByStatusDynamic(ModuleStatus.Completed).Count;
            if (completedCount != null) completedCount.text = completed.ToString();
            if (certCount != null) certCount.text = worker.certificatesEarned.ToString();
            if (attemptsCount != null) attemptsCount.text = worker.totalAttempts.ToString();
            if (certBannerSub != null) certBannerSub.text = $"{worker.certificatesEarned} certificates earned";

            if (moduleList != null)
            {
                moduleList.Clear();
                var modules = app.GetAllModulesWithProgress();
                int count = Mathf.Min(3, modules.Count);
                for (int i = 0; i < count; i++)
                {
                    var card = ModuleCardFactory.Create(moduleCardTemplate, modules[i], OnModuleClicked);
                    moduleList.Add(card);
                }
            }
        }

        void OnModuleClicked(ModuleData mod)
        {
            NavigationManager.Instance.NavigateTo("UI_ModuleDetail", mod.id);
        }

        #region IVoiceCommandTarget Implementation
        public void VoiceNext() => NavigationManager.Instance.NavigateTo("UI_TrainingCatalogue");
        public void VoiceSelectOption(int oneBasedIndex)
        {
            var app = AppDataService.Instance;
            if (app == null) return;
            var modules = app.GetAllModulesWithProgress();
            int idx = oneBasedIndex - 1;
            if (idx >= 0 && idx < modules.Count)
                OnModuleClicked(modules[idx]);
        }
        public void VoiceStart() => NavigationManager.Instance.NavigateTo("UI_TrainingCatalogue");
        public void VoiceConfirm() => VoiceNext();
        public void VoiceCancel() { }
        public void VoiceRepeat() => Refresh();
        public void VoicePassStep(string step) { }
        #endregion
    }
}
