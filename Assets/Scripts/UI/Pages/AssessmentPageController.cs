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
    public class AssessmentPageController : PageController
    {
        string moduleId;
        List<QuizQuestionData> questions;
        int currentQ = 0;
        int correctCount = 0;
        int simulationScore = 80;

        // Per-competency tracking
        Dictionary<string, int> correctByCompetency = new Dictionary<string, int>();
        Dictionary<string, int> totalByCompetency = new Dictionary<string, int>();

        Label questionCounter, questionText;
        Label feedbackIcon, feedbackText;
        VisualElement optionsList, feedbackBanner;
        VisualElement quizFill;
        Button nextBtn, resultsBtn;

        bool answered = false;

        protected override void BindUI()
        {
            questionCounter = root.Q<Label>("question-counter");
            questionText = root.Q<Label>("question-text");
            feedbackIcon = root.Q<Label>("feedback-icon");
            feedbackText = root.Q<Label>("feedback-text");
            optionsList = root.Q("options-list");
            feedbackBanner = root.Q("feedback-banner");
            quizFill = root.Q("quiz-fill");
            nextBtn = root.Q<Button>("next-btn");
            resultsBtn = root.Q<Button>("results-btn");

            if (nextBtn != null) nextBtn.RegisterCallback<ClickEvent>(e => NextQuestion());
            if (resultsBtn != null) resultsBtn.RegisterCallback<ClickEvent>(e => ShowResults());
        }

        public override void SetNavigationParameter(object param)
        {
            if (param is string s) moduleId = s;
            else if (param is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("moduleId", out var mid)) moduleId = mid as string;
                if (dict.TryGetValue("simulationScore", out var sim)) simulationScore = System.Convert.ToInt32(sim);
            }
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
        }

        public override void OnPageEnter()
        {
            var app = AppDataService.Instance;
            questions = app != null ? app.GetQuestions(moduleId) : new List<QuizQuestionData>();
            if (questions.Count == 0)
            {
                Debug.LogWarning($"[Assessment] No questions for {moduleId}, using fallback");
                questions = new List<QuizQuestionData> { new QuizQuestionData{ id="fallback", moduleId=moduleId, textEN="Sample question?", optionsEN=new[]{"A","B","C","D"}, correctIndex=0 } };
            }
            currentQ = 0;
            correctCount = 0;
            answered = false;
            correctByCompetency.Clear();
            totalByCompetency.Clear();
            RefreshQuestion();
        }

        void RefreshQuestion()
        {
            if (questions == null || currentQ >= questions.Count) { ShowResults(); return; }
            var q = questions[currentQ];
            answered = false;
            if (questionCounter != null) questionCounter.text = $"{currentQ + 1}/{questions.Count}";
            if (questionText != null) questionText.text = q.textEN;
            float progress = (float)currentQ / questions.Count * 100f;
            var track = quizFill?.parent as VisualElement;
            if (track != null) ProgressBarHelper.SetProgress(track, progress);
            else if (quizFill != null) quizFill.style.width = Length.Percent(progress);

            if (optionsList != null)
            {
                optionsList.Clear();
                string[] letters = { "A", "B", "C", "D" };
                
                // Shuffle logic
                var indices = new List<int>();
                for (int i = 0; i < q.optionsEN.Length; i++) indices.Add(i);
                
                var rng = new System.Random();
                int n = indices.Count;
                while (n > 1) {
                    n--;
                    int k = rng.Next(n + 1);
                    int val = indices[k];
                    indices[k] = indices[n];
                    indices[n] = val;
                }
                
                int shuffledCorrectIndex = indices.IndexOf(q.correctIndex);

                for (int i = 0; i < indices.Count; i++)
                {
                    int originalIdx = indices[i];
                    int displayIdx = i; // Visual position (A, B, C, D)
                    
                    var btn = new Button();
                    btn.AddToClassList("option-button");
                    btn.style.flexDirection = FlexDirection.Row;
                    btn.style.alignItems = Align.Center;
                    btn.style.backgroundColor = new StyleColor(new Color(0.96f,0.96f,0.96f));
                    btn.style.borderTopColor = new StyleColor(new Color(0.88f,0.88f,0.88f));
                    btn.style.borderBottomColor = new StyleColor(new Color(0.88f,0.88f,0.88f));
                    btn.style.borderLeftColor = new StyleColor(new Color(0.88f,0.88f,0.88f));
                    btn.style.borderRightColor = new StyleColor(new Color(0.88f,0.88f,0.88f));
                    btn.style.borderTopWidth = 1; btn.style.borderBottomWidth = 1; btn.style.borderLeftWidth = 1; btn.style.borderRightWidth = 1;
                    btn.style.borderTopLeftRadius = 10; btn.style.borderTopRightRadius = 10; btn.style.borderBottomLeftRadius = 10; btn.style.borderBottomRightRadius = 10;
                    btn.style.paddingTop = 12; btn.style.paddingBottom = 12; btn.style.paddingLeft = 12; btn.style.paddingRight = 12;
                    btn.style.marginBottom = 8;

                    var badge = new Label(letters[displayIdx]);
                    badge.AddToClassList("option-button__badge");
                    badge.style.width = 28; badge.style.height = 28;
                    badge.style.borderTopLeftRadius = 14; badge.style.borderTopRightRadius = 14; badge.style.borderBottomLeftRadius = 14; badge.style.borderBottomRightRadius = 14;
                    badge.style.backgroundColor = new StyleColor(Color.white);
                    badge.style.borderTopWidth = 1; badge.style.borderBottomWidth = 1; badge.style.borderLeftWidth = 1; badge.style.borderRightWidth = 1;
                    badge.style.borderTopColor = new StyleColor(new Color(0.88f,0.88f,0.88f));
                    badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                    badge.style.fontSize = 12;

                    var txt = new Label(q.optionsEN[originalIdx]);
                    txt.AddToClassList("option-button__text");
                    txt.style.flexGrow = 1;
                    txt.style.marginLeft = 12;
                    txt.style.color = new StyleColor(new Color(0.1f,0.1f,0.1f));
                    txt.style.fontSize = 13;

                    btn.Add(badge);
                    btn.Add(txt);
                    btn.RegisterCallback<ClickEvent>(e => OnOptionSelected(displayIdx, shuffledCorrectIndex, btn));
                    optionsList.Add(btn);
                }
            }
            if (feedbackBanner != null) feedbackBanner.style.display = DisplayStyle.None;
            if (nextBtn != null) nextBtn.style.display = DisplayStyle.Flex;
            if (resultsBtn != null) resultsBtn.style.display = DisplayStyle.None;
        }

        void OnOptionSelected(int selected, int correct, Button btn)
        {
            if (answered) return;
            answered = true;
            bool isCorrect = selected == correct;
            if (isCorrect) correctCount++;

            // Track per-competency scores
            string comp = questions[currentQ].competency;
            if (!string.IsNullOrEmpty(comp))
            {
                if (!totalByCompetency.ContainsKey(comp)) totalByCompetency[comp] = 0;
                if (!correctByCompetency.ContainsKey(comp)) correctByCompetency[comp] = 0;
                totalByCompetency[comp]++;
                if (isCorrect) correctByCompetency[comp]++;
            }

            var options = optionsList.Children().ToList();
            for (int i = 0; i < options.Count; i++)
            {
                var b = options[i] as Button;
                if (b == null) continue;
                if (i == correct) {
                    b.style.backgroundColor = new StyleColor(new Color(0.86f,0.95f,0.86f));
                    b.style.borderTopColor = new StyleColor(new Color(0.30f,0.68f,0.31f)); b.style.borderBottomColor = new StyleColor(new Color(0.30f,0.68f,0.31f)); b.style.borderLeftColor = new StyleColor(new Color(0.30f,0.68f,0.31f)); b.style.borderRightColor = new StyleColor(new Color(0.30f,0.68f,0.31f));
                }
                else if (i == selected && !isCorrect) {
                    b.style.backgroundColor = new StyleColor(new Color(0.98f,0.85f,0.85f));
                    b.style.borderTopColor = new StyleColor(new Color(0.96f,0.26f,0.21f)); b.style.borderBottomColor = new StyleColor(new Color(0.96f,0.26f,0.21f)); b.style.borderLeftColor = new StyleColor(new Color(0.96f,0.26f,0.21f)); b.style.borderRightColor = new StyleColor(new Color(0.96f,0.26f,0.21f));
                }
            }

            if (feedbackIcon != null) feedbackIcon.text = isCorrect ? "✓" : "✗";
            if (feedbackText != null) feedbackText.text = isCorrect ? "Correct!" : "Incorrect";
            if (feedbackBanner != null)
            {
                feedbackBanner.style.display = DisplayStyle.Flex;
                feedbackBanner.style.backgroundColor = new StyleColor(isCorrect ? new Color(0.86f,0.95f,0.86f) : new Color(0.98f,0.85f,0.85f));
                if (feedbackIcon != null) feedbackIcon.style.color = new StyleColor(isCorrect ? new Color(0.18f,0.49f,0.20f) : new Color(0.77f,0.15f,0.15f));
                if (feedbackText != null) feedbackText.style.color = new StyleColor(isCorrect ? new Color(0.18f,0.49f,0.20f) : new Color(0.77f,0.15f,0.15f));
            }

            bool isLast = currentQ == questions.Count - 1;
            if (nextBtn != null) nextBtn.style.display = isLast ? DisplayStyle.None : DisplayStyle.Flex;
            if (resultsBtn != null) resultsBtn.style.display = isLast ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void NextQuestion()
        {
            if (!answered) return;
            currentQ++;
            RefreshQuestion();
        }

        void ShowResults()
        {
            int total = questions.Count;
            int mcqScore = total > 0 ? (int)((float)correctCount / total * 100f) : 0;
            int finalScore = (int)(simulationScore * 0.6f + mcqScore * 0.4f);
            bool passed = finalScore >= 60;
            if (AppDataService.Instance != null)
            {
                AppDataService.Instance.SaveAttempt(moduleId, finalScore, passed);
                AppDataService.Instance.UpdateModuleCompetencyScores(moduleId, correctByCompetency, totalByCompetency);
            }
            var resultsData = new Dictionary<string, object> { { "moduleId", moduleId }, { "mcqScore", mcqScore }, { "correct", correctCount }, { "total", total }, { "simulationScore", simulationScore }, { "finalScore", finalScore }, { "passed", passed } };
            // Try UI_Results, fallback to placeholder dashboard if not yet built
            var nav = NavigationManager.Instance;
            if (nav != null)
            {
                // Check if UI_Results exists in BuildSettings
                bool hasResults = false;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
                {
                    string p = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                    if (p.Contains("UI_Results")) { hasResults = true; break; }
                }
                if (hasResults) nav.NavigateTo("UI_Results", resultsData);
                else
                {
                    Debug.Log($"[Assessment] Results: {finalScore}% {(passed ? "PASS" : "FAIL")} -> Dashboard (Results not yet built)");
                    nav.NavigateTo("UI_Dashboard");
                }
            }
        }
    }
}
