using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI.Pages
{
    public class ProgressPageController : PageController
    {
        [SerializeField] VisualTreeAsset scoreBarTemplate;

        Label overallProgress, completedCount, totalAttempts, certCount;
        VisualElement overviewTrack;
        VisualElement competencyBars, moduleRows, historyBody;

        protected override void BindUI()
        {
            overallProgress = root.Q<Label>("overall-progress");
            completedCount = root.Q<Label>("completed-count");
            totalAttempts = root.Q<Label>("total-attempts");
            certCount = root.Q<Label>("cert-count");
            overviewTrack = root.Q("overview-track");
            competencyBars = root.Q("competency-bars");
            moduleRows = root.Q("module-rows");
            historyBody = root.Q("history-body");

            if (scoreBarTemplate == null)
            {
                scoreBarTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/Components/ScoreBar");
#if UNITY_EDITOR
                if (scoreBarTemplate == null)
                    scoreBarTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Templates/Components/ScoreBar.uxml");
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

            var avatarBtn = root.Q<Button>("avatar-btn");
            if (avatarBtn != null) avatarBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
        }

        public override void OnPageEnter() => Refresh();

        void Refresh()
        {
            var app = AppDataService.Instance;
            var worker = app != null ? app.CurrentWorker : null;
            if (worker == null)
            {
                if (overallProgress != null) overallProgress.text = "0%";
                return;
            }

            if (overallProgress != null) overallProgress.text = $"{worker.overallProgress}%";
            if (overviewTrack != null) ProgressBarHelper.SetProgress(overviewTrack, worker.overallProgress);
            if (completedCount != null) completedCount.text = app.GetModulesByStatus(ModuleStatus.Completed).Count.ToString();
            if (totalAttempts != null) totalAttempts.text = worker.totalAttempts.ToString();
            if (certCount != null) certCount.text = worker.certificatesEarned.ToString();

            if (competencyBars != null)
            {
                competencyBars.Clear();
                // Compute aggregate competency scores from all module progress
                var allModules = app.GetAllModulesWithProgress();
                int hazardRec = 0, extUse = 0, ppeSel = 0, evac = 0, emergResp = 0;
                int hazardCount = 0, extCount = 0, ppeCount = 0, evacCount = 0, emergCount = 0;
                foreach (var m in allModules)
                {
                    var p = app.GetModuleProgress(m.id);
                    if (p?.competencyScores == null) continue;
                    if (p.competencyScores.hazardRecognition > 0) { hazardRec += p.competencyScores.hazardRecognition; hazardCount++; }
                    if (p.competencyScores.extinguisherUse > 0) { extUse += p.competencyScores.extinguisherUse; extCount++; }
                    if (p.competencyScores.ppeSelection > 0) { ppeSel += p.competencyScores.ppeSelection; ppeCount++; }
                    if (p.competencyScores.evacuation > 0) { evac += p.competencyScores.evacuation; evacCount++; }
                    if (p.competencyScores.emergencyResponse > 0) { emergResp += p.competencyScores.emergencyResponse; emergCount++; }
                }
                AddScoreBar("Hazard Recognition", hazardCount > 0 ? hazardRec / hazardCount : 0);
                AddScoreBar("Extinguisher Use", extCount > 0 ? extUse / extCount : 0);
                AddScoreBar("Time Taken", ppeCount > 0 ? ppeSel / ppeCount : 0);
                AddScoreBar("Evacuation", evacCount > 0 ? evac / evacCount : 0);
            }

            if (moduleRows != null)
            {
                moduleRows.Clear();
                    var modules = app.GetAllModulesWithProgress();
                foreach (var mod in modules)
                {
                    var row = new VisualElement();
                    row.AddToClassList("card-interactive");
                    row.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
                    row.style.paddingTop = 12; row.style.paddingBottom = 12; row.style.paddingLeft = 12; row.style.paddingRight = 12;
                    row.style.marginBottom = 8;
                    row.style.alignItems = new StyleEnum<Align>(Align.Center);
                    var icon = new VisualElement();
                    icon.style.width = 22; icon.style.height = 22; icon.style.marginRight = 12;
                    IconLoader.ApplyModuleIcon(icon, mod.id);
                    var info = new VisualElement(); info.style.flexGrow = 1;
                    var title = new Label(mod.title); title.style.fontSize = 13; title.style.color = new StyleColor(new Color(0.1f,0.1f,0.1f));
                    var meta = new Label($"{mod.progress}%"); meta.style.fontSize = 11; meta.style.color = new StyleColor(new Color(0.44f,0.44f,0.44f));
                    info.Add(title); info.Add(meta);
                    var badge = new Label(mod.status.ToString()); badge.AddToClassList($"badge--{mod.status.ToString().ToLower()}"); badge.AddToClassList("badge");
                    row.Add(icon); row.Add(info); row.Add(badge);
                    // Click to detail
                    var captured = mod;
                    row.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateTo("UI_ModuleDetail", captured.id));
                    moduleRows.Add(row);
                }
            }

            if (historyBody != null)
            {
                historyBody.Clear();
                var allAttempts = app != null ? app.GetAllAttempts() : new List<TrainingResult>();
                int idx = 1;
                if (allAttempts != null && allAttempts.Count > 0)
                {
                    foreach (var att in allAttempts)
                    {
                        var mod = app.GetModule(att.moduleName);
                        string title = mod != null ? mod.title : att.moduleName;
                        var row = new VisualElement(); row.AddToClassList("table-row");
                        row.Add(CreateCell(idx.ToString()));
                        row.Add(CreateCell(title, "table-cell"));
                        row.Add(CreateCell(att.timestamp));
                        row.Add(CreateCell($"{att.score}%", "table-cell--accent"));
                        row.Add(CreateCell(att.passed ? "Pass" : "Fail", att.passed ? "table-cell--success" : "table-cell--danger"));
                        historyBody.Add(row);
                        idx++;
                    }
                }
                else
                {
                    var empty = new Label("No attempts yet");
                    empty.style.color = new StyleColor(new Color(0.6f,0.6f,0.6f)); empty.style.fontSize = 11;
                    historyBody.Add(empty);
                }
            }
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

        Label CreateCell(string text, string cls = null)
        {
            var cell = new Label(text); cell.AddToClassList("table-cell");
            if (!string.IsNullOrEmpty(cls)) cell.AddToClassList(cls);
            return cell;
        }
    }
}
