using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.Localization;
using MiningSafetyAR.Modules;

namespace MiningSafetyAR.UI.Pages
{
    /// <summary>
    /// The adaptive post-training MCQ quiz — takes over from ARSimulationPageController.NavigateToQuiz()
    /// once a submodule's AR drill finishes. Questions come from QuizSelectionService.GetAdaptiveQuiz()
    /// (rule-based, tag-driven selection — no LLM); the only LLM call is the personalized feedback
    /// paragraph fetched AFTER scoring, via QuizSelectionService.FetchFeedbackText().
    ///
    /// Deliberately separate from AssessmentPageController (the original static-question flow), which
    /// is left completely untouched. This page replicates the two pieces of AssessmentPageController.
    /// ShowResults() that actually persist progress (AppDataService.SaveAttempt +
    /// UpdateModuleCompetencyScoresFromDrill) — without those, finishing a drill through this new path
    /// would silently stop counting toward certification/progress.
    /// </summary>
    public class AdaptiveQuizPageController : PageController
    {
        // ---- navigation payload (same shape ARSimulationPageController.NavigateToQuiz() already
        // builds for AssessmentPageController — kept identical so that call site only needs its
        // destination scene name changed, not its payload) ----
        // moduleId is the shared parent CATEGORY (e.g. "fire_safety") used for QuizSelectionService
        // (mistakes + question bank) — NOT the specific cosmetic sub-label the worker clicked through
        // (AppDataService.EnsureSubModulesLoaded() synthesizes 5 of those per category, e.g.
        // "fire_safety_sub1".."sub5", all currently routing to the same one real AR drill).
        // progressModuleId is that specific id, kept separately so certificate/progress tracking
        // (AppDataService.SaveAttempt / UpdateModuleCompetencyScoresFromDrill) keeps working exactly
        // as it already did before this page existed. Defaults to moduleId if the caller doesn't
        // supply one, so a plain string nav-param (no submodule distinction) still works.
        string moduleId = "fire_safety";
        string progressModuleId = "fire_safety";
        string submoduleId = "main";
        int simulationScore;
        int drillMistakesCount;
        float drillTimeSeconds;
        int hazardRecognitionPct, extinguisherUsePct, timeManagementPct, evacuationPct;

        List<QuestionBankItem> questions;
        List<QuizSelectionService.QuizAnswer> answers;
        int currentIndex;
        int selectedOptionIndex = -1;
        int[] shuffledIndices;

        Label pageTitle, questionCounter, questionLabel, questionText;
        VisualElement quizFill, loadingView, quizView, resultsView, optionsList, quizNav, feedbackBanner;
        Label loadingText, resultsIcon, resultsScore, resultsSubtext, feedbackText;
        Button nextBtn, submitBtn, continueBtn;

        protected override void BindUI()
        {
            pageTitle = root.Q<Label>("page-title");
            questionCounter = root.Q<Label>("question-counter");
            quizFill = root.Q("quiz-fill");
            loadingView = root.Q("loading-view");
            loadingText = root.Q<Label>("loading-text");
            quizView = root.Q("quiz-view");
            questionLabel = root.Q<Label>("question-label");
            questionText = root.Q<Label>("question-text");
            optionsList = root.Q("options-list");
            resultsView = root.Q("results-view");
            resultsIcon = root.Q<Label>("results-icon");
            resultsScore = root.Q<Label>("results-score");
            resultsSubtext = root.Q<Label>("results-subtext");
            feedbackBanner = root.Q("feedback-banner");
            feedbackText = root.Q<Label>("feedback-text");
            continueBtn = root.Q<Button>("continue-btn");
            quizNav = root.Q("quiz-nav");
            nextBtn = root.Q<Button>("next-btn");
            submitBtn = root.Q<Button>("submit-btn");

            if (nextBtn != null) nextBtn.RegisterCallback<ClickEvent>(e => OnNextClicked());
            if (submitBtn != null) submitBtn.RegisterCallback<ClickEvent>(e => OnSubmitClicked());
            if (continueBtn != null) continueBtn.RegisterCallback<ClickEvent>(e => OnContinueClicked());

            if (pageTitle != null)
            {
                var lang = LanguageManager.Instance;
                pageTitle.text = lang != null
                    ? lang.GetLocalizedText("Quiz", "प्रश्नोत्तरी", "ᱡᱟᱶᱟᱨ", "வினாடி வினா")
                    : "Quiz";
            }
        }

        public override void SetNavigationParameter(object param)
        {
            if (param is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("moduleId", out var mid)) moduleId = mid as string;
                progressModuleId = dict.TryGetValue("progressModuleId", out var pmid) && pmid is string pms && !string.IsNullOrEmpty(pms)
                    ? pms : moduleId;
                if (dict.TryGetValue("submoduleId", out var sid) && sid is string s && !string.IsNullOrEmpty(s)) submoduleId = s;
                if (dict.TryGetValue("simulationScore", out var sim)) simulationScore = System.Convert.ToInt32(sim);
                if (dict.TryGetValue("drillMistakesCount", out var dmc)) drillMistakesCount = System.Convert.ToInt32(dmc);
                if (dict.TryGetValue("drillTimeSeconds", out var dts)) drillTimeSeconds = System.Convert.ToSingle(dts);
                if (dict.TryGetValue("hazardRecognitionPct", out var hrp)) hazardRecognitionPct = System.Convert.ToInt32(hrp);
                if (dict.TryGetValue("extinguisherUsePct", out var eup)) extinguisherUsePct = System.Convert.ToInt32(eup);
                if (dict.TryGetValue("timeManagementPct", out var tmp)) timeManagementPct = System.Convert.ToInt32(tmp);
                if (dict.TryGetValue("evacuationPct", out var evp)) evacuationPct = System.Convert.ToInt32(evp);
            }
            else if (param is string s2 && !string.IsNullOrEmpty(s2))
            {
                moduleId = s2;
                progressModuleId = s2;
            }
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            if (string.IsNullOrEmpty(progressModuleId)) progressModuleId = moduleId;
            if (string.IsNullOrEmpty(submoduleId)) submoduleId = "main";
        }

        public override void OnPageEnter()
        {
            ShowLoading();

            string uid = AppDataService.Instance != null && AppDataService.Instance.CurrentWorker != null
                ? AppDataService.Instance.CurrentWorker.firebaseUid : null;

            if (string.IsNullOrEmpty(uid) || QuizSelectionService.Instance == null)
            {
                Debug.LogWarning("[WARN] AdaptiveQuizPageController No worker/service available — skipping quiz.");
                NavigateAway();
                return;
            }

            QuizSelectionService.Instance.GetAdaptiveQuiz(uid, moduleId, submoduleId, 5, OnQuizLoaded);
        }

        void OnQuizLoaded(List<QuestionBankItem> loaded)
        {
            questions = loaded ?? new List<QuestionBankItem>();
            answers = new List<QuizSelectionService.QuizAnswer>();
            currentIndex = 0;

            if (questions.Count == 0)
            {
                Debug.LogWarning($"[WARN] AdaptiveQuizPageController No question bank content for {moduleId}/{submoduleId} — question bank may not be seeded yet. Skipping quiz.");
                if (loadingText != null)
                {
                    loadingText.text = LocalizedOrDefault(
                        "No quiz questions are available for this module yet.",
                        "इस मॉड्यूल के लिए अभी कोई प्रश्न उपलब्ध नहीं हैं।",
                        "ᱱᱚᱶᱟ ᱢᱚᱰᱭᱩᱞ ᱞᱟᱹᱜᱤᱛ ᱱᱤᱛᱚᱜ ᱚᱠᱛᱚ ᱥᱟᱶᱟᱞ ᱵᱟᱭ ᱢᱮᱱᱟᱜ ᱠᱟᱱᱟ।",
                        "இந்த தொகுதிக்கு இன்னும் கேள்விகள் இல்லை.");
                }
                Invoke(nameof(NavigateAway), 2.0f);
                return;
            }

            RenderQuestion();
        }

        void RenderQuestion()
        {
            ShowQuizView();
            var q = questions[currentIndex];
            selectedOptionIndex = -1;

            if (questionCounter != null) questionCounter.text = $"{currentIndex + 1}/{questions.Count}";
            if (quizFill != null) quizFill.style.width = Length.Percent((float)currentIndex / questions.Count * 100f);
            if (questionLabel != null)
            {
                questionLabel.text = LocalizedOrDefault("Question", "प्रश्न", "ᱥᱟᱶᱟᱞ", "கேள்வி");
            }

            // Fully qualified: MiningSafetyAR.UI.Language (a real, dead-legacy namespace under
            // Assets/Scripts/Ui/Language/LanguageController.cs) shadows the MiningSafetyAR.Data.Language
            // enum for any unqualified "Language" reference from within MiningSafetyAR.UI.Pages —
            // C# resolves sibling/enclosing namespaces before `using`-imported types.
            var lang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : MiningSafetyAR.Data.Language.English;
            if (questionText != null) questionText.text = q.GetStem(lang);

            var options = q.GetOptions(lang);
            shuffledIndices = new int[options.Length];
            for (int i = 0; i < options.Length; i++) shuffledIndices[i] = i;
            var rng = new System.Random();
            for (int i = shuffledIndices.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffledIndices[i], shuffledIndices[j]) = (shuffledIndices[j], shuffledIndices[i]);
            }

            if (optionsList != null)
            {
                optionsList.Clear();
                string[] letters = { "A", "B", "C", "D", "E" };
                for (int displayIdx = 0; displayIdx < shuffledIndices.Length; displayIdx++)
                {
                    int originalIdx = shuffledIndices[displayIdx];
                    var btn = BuildOptionButton(letters[displayIdx % letters.Length], options[originalIdx]);
                    int capturedOriginalIdx = originalIdx;
                    btn.RegisterCallback<ClickEvent>(e => OnOptionSelected(capturedOriginalIdx, btn));
                    optionsList.Add(btn);
                }
            }

            bool isLast = currentIndex == questions.Count - 1;
            if (nextBtn != null) { nextBtn.style.display = isLast ? DisplayStyle.None : DisplayStyle.Flex; nextBtn.SetEnabled(false); }
            if (submitBtn != null) { submitBtn.style.display = isLast ? DisplayStyle.Flex : DisplayStyle.None; submitBtn.SetEnabled(false); }
        }

        Button BuildOptionButton(string letter, string text)
        {
            var btn = new Button();
            btn.AddToClassList("option-button");
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.backgroundColor = new StyleColor(new Color(0.96f, 0.96f, 0.96f));
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(new Color(0.88f, 0.88f, 0.88f));
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 10;
            btn.style.paddingTop = btn.style.paddingBottom = btn.style.paddingLeft = btn.style.paddingRight = 12;
            btn.style.marginBottom = 8;

            var badge = new Label(letter);
            badge.AddToClassList("option-button__badge");
            badge.style.width = 28; badge.style.height = 28;
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 14;
            badge.style.backgroundColor = new StyleColor(Color.white);
            badge.style.borderTopWidth = badge.style.borderBottomWidth = badge.style.borderLeftWidth = badge.style.borderRightWidth = 1;
            badge.style.borderTopColor = new StyleColor(new Color(0.88f, 0.88f, 0.88f));
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.fontSize = 12;

            var txt = new Label(text);
            txt.AddToClassList("option-button__text");
            txt.style.flexGrow = 1;
            txt.style.marginLeft = 12;
            txt.style.color = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
            txt.style.fontSize = 13;
            txt.style.whiteSpace = WhiteSpace.Normal;

            btn.Add(badge);
            btn.Add(txt);
            return btn;
        }

        void OnOptionSelected(int originalIndex, Button clickedBtn)
        {
            selectedOptionIndex = originalIndex;
            foreach (var child in optionsList.Children())
            {
                if (child is Button b)
                {
                    bool isSelected = b == clickedBtn;
                    b.style.backgroundColor = new StyleColor(isSelected ? new Color(1f, 0.92f, 0.85f) : new Color(0.96f, 0.96f, 0.96f));
                    var borderColor = new StyleColor(isSelected ? new Color(1f, 0.42f, 0f) : new Color(0.88f, 0.88f, 0.88f));
                    b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = borderColor;
                }
            }
            if (nextBtn != null) nextBtn.SetEnabled(true);
            if (submitBtn != null) submitBtn.SetEnabled(true);
        }

        void OnNextClicked()
        {
            if (selectedOptionIndex < 0) return;
            RecordCurrentAnswer();
            currentIndex++;
            RenderQuestion();
        }

        void OnSubmitClicked()
        {
            if (selectedOptionIndex < 0) return;
            RecordCurrentAnswer();
            SubmitQuiz();
        }

        void RecordCurrentAnswer()
        {
            answers.Add(new QuizSelectionService.QuizAnswer
            {
                questionId = questions[currentIndex].id,
                selectedIndex = selectedOptionIndex,
            });
        }

        void SubmitQuiz()
        {
            ShowLoading();
            if (loadingText != null)
            {
                loadingText.text = LocalizedOrDefault("Scoring your quiz...", "आपकी प्रश्नोत्तरी का मूल्यांकन हो रहा है...",
                    "ᱟᱢᱟᱜ ᱥᱟᱶᱟᱞ ᱧᱮᱞᱚᱜ ᱠᱟᱱᱟ...", "உங்கள் வினாடி வினா மதிப்பிடப்படுகிறது...");
            }

            string uid = AppDataService.Instance.CurrentWorker.firebaseUid;
            QuizSelectionService.Instance.SubmitQuizResult(uid, moduleId, submoduleId, questions, answers, OnQuizSubmitted);
        }

        void OnQuizSubmitted(QuizSelectionService.QuizSubmissionResult result)
        {
            // Blend with the drill score using the same weights AssessmentPageController's static
            // flow uses, and persist through the same two AppDataService calls it makes — so
            // finishing a drill through this new adaptive path still counts toward
            // progress/certification exactly like the old flow did.
            int mcqScore = Mathf.RoundToInt(result.scorePercent);
            int finalScore = Mathf.RoundToInt(simulationScore * ScoringConstants.DrillWeight + mcqScore * ScoringConstants.QuizWeight);
            bool passed = finalScore >= ScoringConstants.PassThresholdPercentage;

            if (AppDataService.Instance != null)
            {
                AppDataService.Instance.SaveAttempt(progressModuleId, finalScore, passed, drillMistakesCount, drillTimeSeconds, stepMetrics: null);
                AppDataService.Instance.UpdateModuleCompetencyScoresFromDrill(progressModuleId, hazardRecognitionPct, extinguisherUsePct, timeManagementPct, evacuationPct, mcqScore);
            }

            ShowResults(finalScore, passed, result);

            // Score is already visible at this point — fetch feedback as a separate, visibly
            // slower step rather than blocking the score display on the network call.
            QuizSelectionService.Instance.FetchFeedbackText(result, moduleId, submoduleId, feedback =>
            {
                if (feedbackText != null) feedbackText.text = feedback.text;
            });
        }

        void ShowResults(int finalScore, bool passed, QuizSelectionService.QuizSubmissionResult result)
        {
            SetView(loading: false, quiz: false, results: true);

            if (resultsIcon != null) resultsIcon.text = passed ? "🎉" : "📘";
            if (resultsScore != null) resultsScore.text = $"{finalScore}%";
            if (resultsSubtext != null)
            {
                string status = passed
                    ? LocalizedOrDefault("Passed!", "उत्तीर्ण!", "ᱯᱟᱥ ᱦᱩᱭ ᱮᱱᱟ!", "தேர்ச்சி!")
                    : LocalizedOrDefault("Keep practicing.", "अभ्यास जारी रखें।", "ᱨᱮᱭᱟᱜ ᱪᱮᱥᱴᱟ ᱛᱮᱭᱟᱨ ᱫᱚᱦᱚ ᱢᱮ।", "தொடர்ந்து பயிற்சி செய்யுங்கள்.");
                resultsSubtext.text = $"{result.correctCount}/{result.total} " +
                    LocalizedOrDefault("correct", "सही", "ᱴᱷᱤᱠ", "சரியானவை") + $" — {status}";
            }
            if (feedbackText != null)
            {
                feedbackText.text = LocalizedOrDefault("Generating personalized feedback...", "व्यक्तिगत सुझाव तैयार हो रहा है...",
                    "ᱦᱚᱲ ᱨᱮᱭᱟᱜ ᱴᱤᱯᱥ ᱛᱮᱭᱟᱨ ᱦᱩᱭᱩᱜ ᱠᱟᱱᱟ...", "தனிப்பயன் கருத்து உருவாக்கப்படுகிறது...");
            }
        }

        void OnContinueClicked() => NavigateAway();

        void NavigateAway()
        {
            // Back to the submodule/module list — matches where a worker would normally land after
            // finishing a submodule (SubModuleListPageController shows children of a parent module;
            // ModuleDetailPageController shows a single module, which is what fire_safety's real
            // drill flow actually returns to since it has no distinct submodule list of its own).
            NavigationManager.Instance.NavigateTo("UI_ModuleDetail", progressModuleId);
        }

        void ShowLoading() => SetView(loading: true, quiz: false, results: false);
        void ShowQuizView() => SetView(loading: false, quiz: true, results: false);

        void SetView(bool loading, bool quiz, bool results)
        {
            if (loadingView != null) loadingView.style.display = loading ? DisplayStyle.Flex : DisplayStyle.None;
            if (quizView != null) quizView.style.display = quiz ? DisplayStyle.Flex : DisplayStyle.None;
            if (resultsView != null) resultsView.style.display = results ? DisplayStyle.Flex : DisplayStyle.None;
            if (quizNav != null) quizNav.style.display = quiz ? DisplayStyle.Flex : DisplayStyle.None;
        }

        string LocalizedOrDefault(string en, string hi, string sat, string ta)
        {
            var lang = LanguageManager.Instance;
            return lang != null ? lang.GetLocalizedText(en, hi, sat, ta) : en;
        }
    }
}
