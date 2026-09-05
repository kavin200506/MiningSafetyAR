using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI.Pages
{
    public class ModuleDetailPageController : PageController
    {
        [SerializeField] VisualTreeAsset scoreBarTemplate;

        string moduleId;
        ModuleData currentModule;

        Label moduleTitle, heroBadge;
        VisualElement heroEmoji;
        Label description, duration, difficulty, domain;
        VisualElement bestScoreCard, competencySection;
        VisualElement objectivesList, scoreBars;
        VisualElement tableBody;
        Button actionBtn, certBtn, backBtn;

        protected override void BindUI()
        {
            moduleTitle = root.Q<Label>("module-title");
            heroEmoji = root.Q<VisualElement>("hero-emoji");
            heroBadge = root.Q<Label>("hero-badge");
            description = root.Q<Label>("description");
            duration = root.Q<Label>("duration");
            difficulty = root.Q<Label>("difficulty");
            domain = root.Q<Label>("domain");
            bestScoreCard = root.Q("best-score-card");
            competencySection = root.Q("competency-section");
            objectivesList = root.Q("objectives-list");
            scoreBars = root.Q("score-bars");
            tableBody = root.Q("table-body");
            actionBtn = root.Q<Button>("action-btn");
            certBtn = root.Q<Button>("cert-btn");
            backBtn = root.Q<Button>("back-btn");

            if (scoreBarTemplate == null)
            {
                scoreBarTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/Components/ScoreBar");
#if UNITY_EDITOR
                if (scoreBarTemplate == null)
                    scoreBarTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Templates/Components/ScoreBar.uxml");
#endif
            }

            if (backBtn != null) backBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.GoBack());
            if (actionBtn != null) actionBtn.RegisterCallback<ClickEvent>(e => OnActionClicked());
            if (certBtn != null) certBtn.RegisterCallback<ClickEvent>(e => OnViewCertificate());

            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
        }

        public override void SetNavigationParameter(object param) => moduleId = param as string;

        public override void OnPageEnter()
        {
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            var app = AppDataService.Instance;
            if (app != null)
            {
                // Use dynamic per-worker progress overlay
                var allWithProgress = app.GetAllModulesWithProgress();
                currentModule = allWithProgress.Find(m => m.id == moduleId);
                if (currentModule == null) currentModule = app.GetModule(moduleId);
            }
            if (currentModule != null) Refresh();
            else Debug.LogWarning($"[ModuleDetail] Module not found: {moduleId}");
        }

        void Refresh()
        {
            var app = AppDataService.Instance;
            var mod = currentModule;
            if (moduleTitle != null) moduleTitle.text = mod.title;
            if (heroEmoji != null)
            {
                IconLoader.ApplyModuleIcon(heroEmoji, mod.id);
            }
            if (heroBadge != null)
            {
                heroBadge.text = mod.status.ToString();
                heroBadge.RemoveFromClassList("badge--completed");
                heroBadge.RemoveFromClassList("badge--in_progress");
                heroBadge.RemoveFromClassList("badge--not_started");
                heroBadge.RemoveFromClassList("badge--locked");
                heroBadge.AddToClassList($"badge--{mod.status.ToString().ToLower()}");
            }
            if (description != null) description.text = mod.description;
            if (duration != null) duration.text = mod.duration;
            if (difficulty != null) difficulty.text = mod.difficulty;
            if (domain != null) domain.text = mod.domain;

            if (bestScoreCard != null)
            {
                bestScoreCard.style.display = DisplayStyle.None;
            }

            if (objectivesList != null)
            {
                objectivesList.Clear();
                if (mod.objectives != null)
                {
                    for (int i = 0; i < mod.objectives.Length; i++)
                    {
                        var row = new Label($"{i + 1}. {mod.objectives[i]}");
                        row.style.fontSize = 12;
                        row.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
                        row.style.marginBottom = 6;
                        objectivesList.Add(row);
                    }
                }
            }

            if (competencySection != null && scoreBars != null)
            {
                if (mod.bestScore > 0 && mod.competencyScores != null)
                {
                    competencySection.style.display = DisplayStyle.Flex;
                    scoreBars.Clear();
                    AddScoreBar("Hazard Recognition", mod.competencyScores.hazardRecognition);
                    AddScoreBar("Extinguisher Use", mod.competencyScores.extinguisherUse);
                    AddScoreBar("Time Taken", mod.competencyScores.timeManagement);
                    AddScoreBar("Evacuation", mod.competencyScores.evacuation);
                    AddScoreBar("Quiz Score", mod.competencyScores.quizScore);
                }
                else if (competencySection != null) competencySection.style.display = DisplayStyle.None;
            }

            if (tableBody != null)
            {
                tableBody.Clear();
                var attempts = app != null ? app.GetAttemptsForModule(mod.id) : new List<TrainingResult>();
                if (attempts != null && attempts.Count > 0)
                {
                    int index = 1;
                    foreach (var att in attempts)
                    {
                        var row = new VisualElement();
                        row.AddToClassList("table-row");
                        row.Add(CreateCell(index.ToString()));
                        row.Add(CreateCell(att.timestamp));
                        bool isBest = att.score == mod.bestScore && att.score > 0;
                        string scoreClass = isBest ? "table-cell--accent" : null;
                        string scoreText = isBest ? $"{att.score}% ★ Best" : $"{att.score}%";
                        row.Add(CreateCell(scoreText, scoreClass));
                        row.Add(CreateCell(att.passed ? "Pass" : "Fail", att.passed ? "table-cell--success" : "table-cell--danger"));
                        tableBody.Add(row);
                        index++;
                    }
                }
                else if (mod.bestScore > 0 && !string.IsNullOrEmpty(mod.lastAttempt))
                {
                    var row = new VisualElement();
                    row.AddToClassList("table-row");
                    row.Add(CreateCell("1"));
                    row.Add(CreateCell(mod.lastAttempt));
                    row.Add(CreateCell($"{mod.bestScore}% ★ Best", "table-cell--accent"));
                    row.Add(CreateCell(mod.bestScore >= 60 ? "Pass" : "Fail", mod.bestScore >= 60 ? "table-cell--success" : "table-cell--danger"));
                    tableBody.Add(row);
                }
                else
                {
                    var empty = new Label("No attempts yet");
                    empty.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                    empty.style.fontSize = 11;
                    tableBody.Add(empty);
                }
            }

            ConfigureActionButton();
        }

        void ConfigureActionButton()
        {
            if (actionBtn == null) return;
            var lockedMsg = root.Q("locked-message");
            switch (currentModule.status)
            {
                case ModuleStatus.Locked:
                    actionBtn.style.display = DisplayStyle.None;
                    if (certBtn != null) certBtn.style.display = DisplayStyle.None;
                    if (lockedMsg != null) lockedMsg.style.display = DisplayStyle.Flex;
                    break;
                case ModuleStatus.Completed:
                    actionBtn.text = "RETAKE TRAINING";
                    actionBtn.style.display = DisplayStyle.Flex;
                    if (certBtn != null) certBtn.style.display = string.IsNullOrEmpty(currentModule.certificateId) ? DisplayStyle.None : DisplayStyle.Flex;
                    if (lockedMsg != null) lockedMsg.style.display = DisplayStyle.None;
                    break;
                case ModuleStatus.InProgress:
                    actionBtn.text = "CONTINUE TRAINING";
                    actionBtn.style.display = DisplayStyle.Flex;
                    if (certBtn != null) certBtn.style.display = DisplayStyle.None;
                    if (lockedMsg != null) lockedMsg.style.display = DisplayStyle.None;
                    break;
                default:
                    actionBtn.text = "START TRAINING";
                    actionBtn.style.display = DisplayStyle.Flex;
                    if (certBtn != null) certBtn.style.display = DisplayStyle.None;
                    if (lockedMsg != null) lockedMsg.style.display = DisplayStyle.None;
                    break;
            }
        }

        void OnActionClicked()
        {
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            Debug.Log($"[ModuleDetail] Launching AR Fire Safety Scene ('ar_fire_safety') for module '{moduleId}'...");
            NavigationManager.Instance.NavigateTo("ar_fire_safety", moduleId);
        }

        void OnViewCertificate() => NavigationManager.Instance.NavigateTo("UI_Certificate", currentModule.certificateId);

        Label CreateCell(string text, string cls = null)
        {
            var cell = new Label(text);
            cell.AddToClassList("table-cell");
            if (!string.IsNullOrEmpty(cls)) cell.AddToClassList(cls);
            return cell;
        }

        void AddScoreBar(string label, int value)
        {
            if (scoreBarTemplate == null || scoreBars == null) return;
            var bar = scoreBarTemplate.Instantiate();
            // bar root is the score-bar VisualElement
            var inner = bar.Q("score-bar");
            var target = inner != null ? inner : bar;
            ScoreBarHelper.Configure(target, label, value);
            scoreBars.Add(bar);
        }
    }
}
