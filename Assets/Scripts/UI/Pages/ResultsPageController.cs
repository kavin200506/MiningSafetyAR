using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI.Pages
{
    public class ResultsPageController : PageController
    {
        [SerializeField] VisualTreeAsset scoreBarTemplate;

        Dictionary<string, object> resultsData;
        VisualElement resultIcon;
        Label resultTitle, resultSubtitle;
        Label simScore, mcqScore, correctCount, finalScore;
        Label resultBadge, moduleName, attemptNum, personalBest;
        VisualElement competencyBars;
        Button certBtn, retryBtn, moduleBtn, dashboardBtn;

        protected override void BindUI()
        {
            resultIcon = root.Q<VisualElement>("result-icon");
            resultTitle = root.Q<Label>("result-title");
            resultSubtitle = root.Q<Label>("result-subtitle");
            simScore = root.Q<Label>("sim-score");
            mcqScore = root.Q<Label>("mcq-score");
            correctCount = root.Q<Label>("correct-count");
            finalScore = root.Q<Label>("final-score");
            resultBadge = root.Q<Label>("result-badge");
            moduleName = root.Q<Label>("module-name");
            attemptNum = root.Q<Label>("attempt-num");
            personalBest = root.Q<Label>("personal-best");
            competencyBars = root.Q("competency-bars");
            certBtn = root.Q<Button>("cert-btn");
            retryBtn = root.Q<Button>("retry-btn");
            moduleBtn = root.Q<Button>("module-btn");
            dashboardBtn = root.Q<Button>("dashboard-btn");

            if (scoreBarTemplate == null)
            {
                scoreBarTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/Components/ScoreBar");
#if UNITY_EDITOR
                if (scoreBarTemplate == null)
                    scoreBarTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Templates/Components/ScoreBar.uxml");
#endif
            }

            if (certBtn != null) certBtn.RegisterCallback<ClickEvent>(e => OnViewCertificate());
            if (retryBtn != null) retryBtn.RegisterCallback<ClickEvent>(e => OnRetry());
            if (moduleBtn != null) moduleBtn.RegisterCallback<ClickEvent>(e => OnBackToModule());
            if (dashboardBtn != null) dashboardBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToRoot("UI_Dashboard"));

            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
        }

        public override void SetNavigationParameter(object param)
        {
            resultsData = param as Dictionary<string, object>;
            // Also handle direct string for testing
            if (param is string s) resultsData = new Dictionary<string, object> { { "moduleId", s }, { "simulationScore", 80 }, { "mcqScore", 75 }, { "correct", 4 }, { "total", 5 }, { "finalScore", 78 }, { "passed", true } };
        }

        public override void OnPageEnter()
        {
            if (resultsData == null)
            {
                // Demo fallback for direct Play
                resultsData = new Dictionary<string, object> { { "moduleId", "fire_safety" }, { "simulationScore", 80 }, { "mcqScore", 80 }, { "correct", 4 }, { "total", 5 }, { "finalScore", 80 }, { "passed", true } };
            }
            Refresh();
        }

        void Refresh()
        {
            int sim = resultsData.ContainsKey("simulationScore") ? System.Convert.ToInt32(resultsData["simulationScore"]) : 80;
            int mcq = resultsData.ContainsKey("mcqScore") ? System.Convert.ToInt32(resultsData["mcqScore"]) : 80;
            int correct = resultsData.ContainsKey("correct") ? System.Convert.ToInt32(resultsData["correct"]) : 4;
            int total = resultsData.ContainsKey("total") ? System.Convert.ToInt32(resultsData["total"]) : 5;
            int final = resultsData.ContainsKey("finalScore") ? System.Convert.ToInt32(resultsData["finalScore"]) : 80;
            bool passed = resultsData.ContainsKey("passed") ? (bool)resultsData["passed"] : true;
            string moduleId = resultsData.ContainsKey("moduleId") ? resultsData["moduleId"] as string : "fire_safety";

            if (resultIcon != null)
            {
                IconLoader.ApplyTo(resultIcon, passed ? "icon_trophy" : "ar_check");
            }
            if (resultTitle != null) resultTitle.text = passed ? "Congratulations!" : "Keep Trying!";
            if (resultSubtitle != null) resultSubtitle.text = passed ? "You've passed the assessment!" : "You need 60% to pass. Review and try again.";
            if (simScore != null) simScore.text = $"{sim}/100";
            if (mcqScore != null) mcqScore.text = $"{mcq}/100";
            if (correctCount != null) correctCount.text = $"{correct}/{total}";
            if (finalScore != null) { finalScore.text = final.ToString(); finalScore.style.color = new StyleColor(passed ? new Color(0.30f,0.68f,0.31f) : new Color(0.96f,0.26f,0.21f)); }
            if (resultBadge != null)
            {
                resultBadge.text = passed ? "PASSED" : "FAILED";
                resultBadge.RemoveFromClassList("badge--pass"); resultBadge.RemoveFromClassList("badge--fail");
                resultBadge.AddToClassList(passed ? "badge--pass" : "badge--fail");
            }
            if (moduleName != null)
            {
                var mod = AppDataService.Instance != null ? AppDataService.Instance.GetModule(moduleId) : null;
                moduleName.text = mod?.title ?? moduleId;
            }
            if (attemptNum != null)
            {
                var app = AppDataService.Instance;
                if (app != null && app.CurrentWorker != null)
                    attemptNum.text = app.CurrentWorker.totalAttempts.ToString();
                else attemptNum.text = "1";
            }
            if (personalBest != null)
            {
                var prog = AppDataService.Instance != null ? AppDataService.Instance.GetModuleProgress(moduleId) : null;
                personalBest.text = prog != null ? $"{prog.bestScore}%" : $"{final}%";
            }
            if (competencyBars != null)
            {
                competencyBars.Clear();
                // This attempt's own breakdown (not the ratcheted personal-best-ever values) —
                // matches what the worker just did, avoiding a "70% total but 100% bars" mismatch
                // from an earlier, better attempt. See documents/technical_scoring_explained.md §6.
                bool hasBreakdown = resultsData.ContainsKey("hazardRecognitionPct");
                if (hasBreakdown)
                {
                    int hazardPct = System.Convert.ToInt32(resultsData["hazardRecognitionPct"]);
                    int extPct = System.Convert.ToInt32(resultsData["extinguisherUsePct"]);
                    int timePct = System.Convert.ToInt32(resultsData["timeManagementPct"]);
                    int evacPct = System.Convert.ToInt32(resultsData["evacuationPct"]);
                    AddScoreBar("Hazard Recognition", hazardPct);
                    AddScoreBar("Extinguisher Use", extPct);
                    AddScoreBar("Time Taken", timePct);
                    AddScoreBar("Evacuation", evacPct);
                    AddScoreBar("Quiz Score", mcq);
                }
                else
                {
                    // Direct-testing fallback (page opened without a real drill behind it) —
                    // fall back to whatever's on record for this module, honestly zeroed if none.
                    var prog = AppDataService.Instance != null ? AppDataService.Instance.GetModuleProgress(moduleId) : null;
                    var cs = prog?.competencyScores;
                    AddScoreBar("Hazard Recognition", cs?.hazardRecognition ?? 0);
                    AddScoreBar("Extinguisher Use", cs?.extinguisherUse ?? 0);
                    AddScoreBar("Time Taken", cs?.timeManagement ?? 0);
                    AddScoreBar("Evacuation", cs?.evacuation ?? 0);
                    AddScoreBar("Quiz Score", cs?.quizScore ?? 0);
                }
            }
            if (certBtn != null)
            {
                var progForCert = AppDataService.Instance != null ? AppDataService.Instance.GetModuleProgress(moduleId) : null;
                bool hasCert = progForCert != null && !string.IsNullOrEmpty(progForCert.certificateId);
                certBtn.style.display = hasCert ? DisplayStyle.Flex : DisplayStyle.None;
            }
            // Store for navigation
            resultsData["moduleId"] = moduleId;
        }

        void AddScoreBar(string label, int value)
        {
            if (scoreBarTemplate == null || competencyBars == null) return;
            var bar = scoreBarTemplate.Instantiate();
            var inner = bar.Q("score-bar");
            var target = inner != null ? inner : bar;
            ScoreBarHelper.Configure(target, label, value);
            competencyBars.Add(bar);
        }

        void OnViewCertificate()
        {
            string mid = resultsData.ContainsKey("moduleId") ? resultsData["moduleId"] as string : "fire_safety";
            NavigationManager.Instance.NavigateTo("UI_Certificate", mid);
        }
        void OnRetry()
        {
            string mid = resultsData.ContainsKey("moduleId") ? resultsData["moduleId"] as string : "fire_safety";
            Debug.Log($"[Results] Retaking assessment for module '{mid}' -> Launching UI_Assessment...");
            NavigationManager.Instance.NavigateTo("UI_Assessment", mid);
        }
        void OnBackToModule()
        {
            string mid = resultsData.ContainsKey("moduleId") ? resultsData["moduleId"] as string : "fire_safety";
            var app = AppDataService.Instance;
            if (app != null)
            {
                var mod = app.GetModule(mid);
                if (mod != null && !string.IsNullOrEmpty(mod.parentId))
                {
                    Debug.Log($"[Results] Back to Module clicked for '{mid}' -> Redirecting to parent '{mod.parentId}' via UI_SubModuleList");
                    NavigationManager.Instance.NavigateTo("UI_SubModuleList", mod.parentId);
                    return;
                }
            }
            NavigationManager.Instance.NavigateTo("UI_ModuleDetail", mid);
        }
    }
}
