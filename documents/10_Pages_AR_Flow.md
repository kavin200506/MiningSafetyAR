# 10 — Page Implementation: AR Flow

## Scene: UI_ARFlow

---

## 1. LearningContentPage

**File:** `Assets/Scripts/UI/Pages/LearningContentPageController.cs`
**Template:** `Assets/UI/Templates/Pages/LearningContentPage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <!-- Custom Header -->
    <VisualElement class="header">
        <Button class="header__back-btn" name="back-btn">
            <Label text="←" style="font-size: 20px;" />
        </Button>
        <Label class="header__title" text="Learn" />
        <Label class="slide-counter" name="slide-counter"
               text="1/3" />
    </VisualElement>

    <!-- Progress Track -->
    <VisualElement class="learning__progress-track"
                   name="learning-track">
        <VisualElement class="progress-fill" name="learning-fill" />
    </VisualElement>

    <!-- Slide Content -->
    <ScrollView class="scroll-area">
        <VisualElement class="slide-card" name="slide-card">
            <Label class="slide-emoji" name="slide-emoji" />
            <Label class="slide-title" name="slide-title" />
            <VisualElement class="slide-points" name="slide-points" />
            <VisualElement class="slide-tip" name="slide-tip">
                <Label text="💡" style="font-size: 16px;" />
                <Label class="slide-tip__text" name="tip-text" />
            </VisualElement>
        </VisualElement>
    </ScrollView>

    <!-- Navigation Bar -->
    <VisualElement class="learning__nav">
        <Button class="btn-secondary" name="prev-btn"
                text="Previous" style="flex: 1;" />
        <Button class="btn-primary" name="next-btn"
                text="Next" style="flex: 1;" />
        <Button class="btn-primary" name="ready-btn"
                text="I'm Ready"
                style="flex: 1; display: none;
                       background-color: var(--success);" />
    </VisualElement>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class LearningContentPageController : PageController
    {
        private string moduleId;
        private int currentSlide = 0;
        private LearningSlide[] slides;

        private Label slideCounter, slideEmoji, slideTitle;
        private Label tipText;
        private VisualElement slidePoints, learningFill;
        private Button prevBtn, nextBtn, readyBtn, backBtn;

        // Slide data structure
        [Serializable]
        private class LearningSlide
        {
            public string emoji;
            public string title;
            public string[] points;
            public string tip;
        }

        protected override void BindUI()
        {
            slideCounter = root.Q<Label>("slide-counter");
            slideEmoji = root.Q<Label>("slide-emoji");
            slideTitle = root.Q<Label>("slide-title");
            tipText = root.Q<Label>("tip-text");
            slidePoints = root.Q("slide-points");
            learningFill = root.Q("learning-fill");
            prevBtn = root.Q<Button>("prev-btn");
            nextBtn = root.Q<Button>("next-btn");
            readyBtn = root.Q<Button>("ready-btn");
            backBtn = root.Q<Button>("back-btn");

            prevBtn.RegisterCallback<ClickEvent>(evt =>
                PrevSlide());
            nextBtn.RegisterCallback<ClickEvent>(evt =>
                NextSlide());
            readyBtn.RegisterCallback<ClickEvent>(evt =>
                OnReady());
            backBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.GoBack());
        }

        public override void SetNavigationParameter(object param)
        {
            moduleId = param as string;
            LoadSlides();
        }

        private void LoadSlides()
        {
            // Load slides based on module
            slides = moduleId switch
            {
                "fire_safety" => new LearningSlide[]
                {
                    new LearningSlide {
                        emoji = "🔥",
                        title = "Fire Safety Basics",
                        points = new[] {
                            "Understand fire classes (A, B, C, D, K)",
                            "Know the fire triangle: Heat, Fuel, Oxygen",
                            "Identify evacuation routes in your workplace",
                            "Recognize fire hazards in mining environments"
                        },
                        tip = "Always know at least 2 exit routes " +
                              "from any location in the mine."
                    },
                    new LearningSlide {
                        emoji = "🧯",
                        title = "P.A.S.S. Technique",
                        points = new[] {
                            "P - Pull the pin",
                            "A - Aim at the base of the fire",
                            "S - Squeeze the handle",
                            "S - Sweep side to side"
                        },
                        tip = "Stand 6-8 feet away from the fire " +
                              "when using an extinguisher."
                    },
                    new LearningSlide {
                        emoji = "🚪",
                        title = "Evacuation Procedures",
                        points = new[] {
                            "Sound the alarm immediately",
                            "Close doors behind you",
                            "Stay low in smoky conditions",
                            "Proceed to the designated assembly point"
                        },
                        tip = "Never use elevators during a " +
                              "fire evacuation."
                    }
                },
                "gas_safety" => new LearningSlide[]
                {
                    new LearningSlide {
                        emoji = "☣️",
                        title = "Gas Leak Hazards",
                        points = new[] {
                            "Methane (CH₄) is explosive at 5-15%",
                            "Carbon Monoxide (CO) is odorless and deadly",
                            "Hydrogen Sulfide (H₂S) smells like rotten eggs",
                            "Always use a multi-gas detector"
                        },
                        tip = "If you smell gas, evacuate " +
                              "immediately and alert others."
                    },
                    new LearningSlide {
                        emoji = "😷",
                        title = "PPE for Gas Hazards",
                        points = new[] {
                            "SCBA (Self-Contained Breathing Apparatus)",
                            "Gas-tight chemical suit",
                            "Personal gas monitor",
                            "Two-way radio for communication"
                        },
                        tip = "Always check your SCBA pressure " +
                              "before entering a confined space."
                    },
                    new LearningSlide {
                        emoji = "🤝",
                        title = "Confined Space Protocol",
                        points = new[] {
                            "Get a confined space entry permit",
                            "Test atmosphere before entry",
                            "Have a standby buddy outside",
                            "Maintain constant radio contact"
                        },
                        tip = "Never enter a confined space alone. " +
                              "The standby buddy saves lives."
                    }
                },
                _ => new LearningSlide[]
                {
                    new LearningSlide {
                        emoji = "📚",
                        title = "Training Content",
                        points = new[] {
                            "Review safety protocols",
                            "Understand emergency procedures",
                            "Practice proper equipment usage"
                        },
                        tip = "Complete all slides to proceed " +
                              "to the AR simulation."
                    }
                }
            };
        }

        public override void OnPageEnter()
        {
            currentSlide = 0;
            RefreshSlide();
        }

        private void RefreshSlide()
        {
            if (slides == null || slides.Length == 0) return;

            var slide = slides[currentSlide];

            // Update content
            slideCounter.text =
                $"{currentSlide + 1}/{slides.Length}";
            slideEmoji.text = slide.emoji;
            slideTitle.text = slide.title;
            tipText.text = slide.tip;

            // Points
            slidePoints.Clear();
            foreach (var point in slide.points)
            {
                var pointLabel = new Label($"• {point}");
                pointLabel.style.fontSize = 13;
                pointLabel.style.color = new StyleColor(
                    HexToColor("#B0B0B0"));
                pointLabel.style.marginBottom = 8;
                slidePoints.Add(pointLabel);
            }

            // Progress
            float progress =
                (float)(currentSlide + 1) / slides.Length * 100f;
            ProgressBarHelper.SetProgress(
                learningFill, progress);

            // Navigation buttons
            prevBtn.style.display = currentSlide > 0
                ? DisplayStyle.Flex : DisplayStyle.None;

            bool isLast = currentSlide == slides.Length - 1;
            nextBtn.style.display = isLast
                ? DisplayStyle.None : DisplayStyle.Flex;
            readyBtn.style.display = isLast
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void PrevSlide()
        {
            if (currentSlide > 0)
            {
                currentSlide--;
                RefreshSlide();
            }
        }

        private void NextSlide()
        {
            if (currentSlide < slides.Length - 1)
            {
                currentSlide++;
                RefreshSlide();
            }
        }

        private void OnReady()
        {
            NavigationManager.Instance.NavigateTo(
                "ar_simulation", moduleId);
        }
    }
}
```

---

## 2. ARSimulationPage

**File:** `Assets/Scripts/UI/Pages/ARSimulationPageController.cs`
**Template:** `Assets/UI/Templates/Pages/ARSimulationPage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <!-- Score + Timer Pills -->
    <VisualElement class="ar__hud">
        <VisualElement class="ar__score-pill">
            <Label text="⭐" />
            <Label class="ar__score-value" name="score-value"
                   text="50" />
        </VisualElement>
        <VisualElement class="ar__timer-pill" name="timer-pill">
            <Label text="⏱" />
            <Label class="ar__timer-value timer-green"
                   name="timer-value" text="120s" />
        </VisualElement>
    </VisualElement>

    <!-- AR Scene Area -->
    <VisualElement class="ar__scene" name="ar-scene">
        <!-- Scan Line -->
        <VisualElement class="ar__scan-line" name="scan-line" />

        <!-- Phase Content (swapped per phase) -->
        <VisualElement class="ar__phase-content"
                       name="phase-content">
            <Label class="ar__phase-emoji" name="phase-emoji" />
            <Label class="ar__phase-instruction"
                   name="phase-instruction" />
            <VisualElement class="ar__phase-visual"
                           name="phase-visual" />
        </VisualElement>
    </VisualElement>

    <!-- Phase Dots -->
    <VisualElement class="ar__phase-dots" name="phase-dots">
        <!-- 8 dots populated by C# -->
    </VisualElement>

    <!-- Instruction Bar -->
    <VisualElement class="ar__instruction-bar"
                   name="instruction-bar">
        <Label class="ar__instruction-text"
               name="instruction-text"
               text="Scanning environment..." />
    </VisualElement>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class ARSimulationPageController : PageController
    {
        private string moduleId;
        private int currentPhase = 0;
        private int score = 50;
        private float timeLeft = 120f;
        private bool timerRunning = false;

        private Label scoreValue, timerValue, instructionText;
        private Label phaseEmoji, phaseInstruction;
        private VisualElement phaseDots, scanLine, phaseVisual;
        private VisualElement arScene, timerPill;

        // Phase data
        private PhaseData[] phases;

        [Serializable]
        private class PhaseData
        {
            public string emoji;
            public string instruction;
            public int scoreBonus;
            public bool showScanLine;
            public bool startTimer;
        }

        protected override void BindUI()
        {
            scoreValue = root.Q<Label>("score-value");
            timerValue = root.Q<Label>("timer-value");
            instructionText = root.Q<Label>("instruction-text");
            phaseEmoji = root.Q<Label>("phase-emoji");
            phaseInstruction = root.Q<Label>("phase-instruction");
            phaseDots = root.Q("phase-dots");
            scanLine = root.Q("scan-line");
            phaseVisual = root.Q("phase-visual");
            arScene = root.Q("ar-scene");
            timerPill = root.Q("timer-pill");
        }

        public override void SetNavigationParameter(object param)
        {
            moduleId = param as string;
        }

        public override void OnPageEnter()
        {
            currentPhase = 0;
            score = 50;
            timeLeft = 120f;
            timerRunning = false;

            LoadPhases();
            CreatePhaseDots();
            RefreshPhase();
        }

        private void LoadPhases()
        {
            phases = new PhaseData[]
            {
                new PhaseData {
                    emoji = "📱",
                    instruction = "Scanning environment...",
                    showScanLine = true
                },
                new PhaseData {
                    emoji = "👆",
                    instruction = "Move phone slowly across " +
                                  "a flat surface...",
                    showScanLine = true
                },
                new PhaseData {
                    emoji = "✅",
                    instruction = "Surface detected! " +
                                  "Tap to place scenario"
                },
                new PhaseData {
                    emoji = "🔥",
                    instruction = "Fire detected! " +
                                  "Locate the extinguisher!",
                    scoreBonus = 0
                },
                new PhaseData {
                    emoji = "🧯",
                    instruction = "Extinguisher located! " +
                                  "Move to it and tap to pick up",
                    scoreBonus = 10,
                    startTimer = true
                },
                new PhaseData {
                    emoji = "🔓",
                    instruction = "Tap to OPEN CAP",
                    scoreBonus = 10
                },
                new PhaseData {
                    emoji = "💨",
                    instruction = "HOLD to SPRAY (6 seconds)...",
                    scoreBonus = 10
                },
                new PhaseData {
                    emoji = "✅",
                    instruction = "Fire extinguished! Area Clear!",
                    scoreBonus = 10
                }
            };
        }

        private void CreatePhaseDots()
        {
            phaseDots.Clear();
            for (int i = 0; i < phases.Length; i++)
            {
                var dot = new VisualElement();
                dot.name = $"phase-dot-{i}";
                dot.AddToClassList("phase-dot");
                dot.style.width = 8;
                dot.style.height = 8;
                dot.style.borderRadius = 4;
                dot.style.marginHorizontal = 4;
                dot.style.backgroundColor = new StyleColor(
                    HexToColor("#333333"));
                phaseDots.Add(dot);
            }
        }

        private void RefreshPhase()
        {
            if (currentPhase >= phases.Length)
            {
                OnSimulationComplete();
                return;
            }

            var phase = phases[currentPhase];

            // Update content
            phaseEmoji.text = phase.emoji;
            phaseInstruction.text = phase.instruction;
            instructionText.text = phase.instruction;

            // Score
            score += phase.scoreBonus;
            scoreValue.text = score.ToString();

            // Scan line
            scanLine.style.display = phase.showScanLine
                ? DisplayStyle.Flex : DisplayStyle.None;

            // Timer
            if (phase.startTimer && !timerRunning)
            {
                timerRunning = true;
                StartCoroutine(TimerCoroutine());
            }

            // Phase dots
            for (int i = 0; i < phases.Length; i++)
            {
                var dot = phaseDots.Q($"phase-dot-{i}");
                if (dot == null) continue;

                dot.style.backgroundColor = new StyleColor(
                    i < currentPhase
                        ? HexToColor("#4CAF50")   // completed
                        : i == currentPhase
                            ? HexToColor("#FF6D00") // active
                            : HexToColor("#333333") // inactive
                );
            }

            // Auto-advance (simulate)
            StartCoroutine(AutoAdvance(phase.showScanLine ? 1.5f : 2f));
        }

        private IEnumerator AutoAdvance(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (currentPhase < phases.Length - 1)
            {
                currentPhase++;
                RefreshPhase();
            }
        }

        private IEnumerator TimerCoroutine()
        {
            while (timeLeft > 0 && timerRunning)
            {
                timeLeft -= Time.deltaTime;
                timerValue.text = $"{Mathf.CeilToInt(timeLeft)}s";

                // Color
                timerValue.RemoveFromClassList("timer-green");
                timerValue.RemoveFromClassList("timer-yellow");
                timerValue.RemoveFromClassList("timer-red");

                if (timeLeft > 60)
                    timerValue.AddToClassList("timer-green");
                else if (timeLeft > 30)
                    timerValue.AddToClassList("timer-yellow");
                else
                    timerValue.AddToClassList("timer-red");

                yield return null;
            }
        }

        private void OnSimulationComplete()
        {
            StopAllCoroutines();

            // Navigate to assessment
            NavigationManager.Instance.NavigateTo(
                "assessment", moduleId);
        }
    }
}
```

---

## 3. AssessmentPage

**File:** `Assets/Scripts/UI/Pages/AssessmentPageController.cs`
**Template:** `Assets/UI/Templates/Pages/AssessmentPage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <!-- Header -->
    <VisualElement class="header">
        <Button class="header__back-btn" name="back-btn">
            <Label text="←" style="font-size: 20px;" />
        </Button>
        <Label class="header__title" text="Assessment" />
        <Label class="question-counter" name="question-counter"
               text="1/5" />
    </VisualElement>

    <!-- Progress Track -->
    <VisualElement class="quiz__progress-track"
                   name="quiz-track">
        <VisualElement class="progress-fill" name="quiz-fill" />
    </VisualElement>

    <!-- Question Card -->
    <ScrollView class="scroll-area">
        <VisualElement class="card quiz-card" name="quiz-card">
            <Label class="quiz-card__label"
                   text="Question 1" />
            <Label class="quiz-card__question"
                   name="question-text" text="" />
        </VisualElement>

        <!-- Options -->
        <VisualElement class="options-list" name="options-list">
            <!-- 4 option buttons populated by C# -->
        </VisualElement>

        <!-- Feedback Banner -->
        <VisualElement class="feedback-banner"
                       name="feedback-banner"
                       style="display: none;">
            <Label class="feedback-banner__icon"
                   name="feedback-icon" text="" />
            <Label class="feedback-banner__text"
                   name="feedback-text" text="" />
        </VisualElement>
    </ScrollView>

    <!-- Next Button -->
    <VisualElement class="quiz__nav">
        <Button class="btn-primary" name="next-btn"
                text="Next Question" style="flex: 1;" />
        <Button class="btn-primary" name="results-btn"
                text="View Results"
                style="flex: 1; display: none;" />
    </VisualElement>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class AssessmentPageController : PageController
    {
        private string moduleId;
        private List<QuizQuestionData> questions;
        private int currentQ = 0;
        private int correctCount = 0;
        private int simulationScore = 80;

        private Label questionCounter, questionText;
        private Label feedbackIcon, feedbackText;
        private VisualElement optionsList, feedbackBanner;
        private VisualElement quizFill;
        private Button nextBtn, resultsBtn, backBtn;

        private bool answered = false;
        private int selectedOption = -1;

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
            backBtn = root.Q<Button>("back-btn");

            nextBtn.RegisterCallback<ClickEvent>(evt =>
                NextQuestion());
            resultsBtn.RegisterCallback<ClickEvent>(evt =>
                ShowResults());
            backBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.GoBack());
        }

        public override void SetNavigationParameter(object param)
        {
            moduleId = param as string;
        }

        public override void OnPageEnter()
        {
            questions = AppDataService.Instance
                .GetQuestions(moduleId);
            currentQ = 0;
            correctCount = 0;
            answered = false;
            RefreshQuestion();
        }

        private void RefreshQuestion()
        {
            if (questions == null || currentQ >= questions.Count)
            {
                ShowResults();
                return;
            }

            var q = questions[currentQ];
            answered = false;
            selectedOption = -1;

            // Header
            questionCounter.text =
                $"{currentQ + 1}/{questions.Count}";

            // Question
            questionText.text = q.textEN;

            // Progress
            float progress =
                (float)currentQ / questions.Count * 100f;
            ProgressBarHelper.SetProgress(quizFill, progress);

            // Options
            optionsList.Clear();
            string[] letters = { "A", "B", "C", "D" };
            for (int i = 0; i < q.optionsEN.Length; i++)
            {
                int index = i;
                var option = new Button();
                option.AddToClassList("option-button");

                var badge = new Label(letters[i]);
                badge.AddToClassList("option-button__badge");

                var text = new Label(q.optionsEN[i]);
                text.AddToClassList("option-button__text");

                option.Add(badge);
                option.Add(text);

                option.RegisterCallback<ClickEvent>(evt =>
                    OnOptionSelected(index, q.correctIndex));

                optionsList.Add(option);
            }

            // Hide feedback
            feedbackBanner.style.display = DisplayStyle.None;

            // Show next button
            nextBtn.style.display = DisplayStyle.Flex;
            resultsBtn.style.display = DisplayStyle.None;
        }

        private void OnOptionSelected(int selected, int correct)
        {
            if (answered) return;
            answered = true;
            selectedOption = selected;

            bool isCorrect = selected == correct;
            if (isCorrect) correctCount++;

            // Update option colors
            var options = optionsList.Children().ToList();
            for (int i = 0; i < options.Count; i++)
            {
                if (i == correct)
                    options[i].AddToClassList("option--correct");
                else if (i == selected && !isCorrect)
                    options[i].AddToClassList("option--incorrect");
            }

            // Show feedback
            feedbackIcon.text = isCorrect ? "✓" : "✗";
            feedbackText.text = isCorrect ? "Correct!" : "Incorrect";
            feedbackBanner.style.display = DisplayStyle.Flex;

            // Show next or results
            bool isLast = currentQ == questions.Count - 1;
            nextBtn.style.display = isLast
                ? DisplayStyle.None : DisplayStyle.Flex;
            resultsBtn.style.display = isLast
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void NextQuestion()
        {
            if (!answered) return;
            currentQ++;
            RefreshQuestion();
        }

        private void ShowResults()
        {
            int total = questions.Count;
            int mcqScore = (int)((float)correctCount / total * 100f);
            int finalScore = (int)(simulationScore * 0.6f
                                 + mcqScore * 0.4f);
            bool passed = finalScore >= 60;

            // Save attempt
            AppDataService.Instance.SaveAttempt(
                moduleId, finalScore, passed);

            // Navigate to results
            var resultsData = new Dictionary<string, object>
            {
                { "moduleId", moduleId },
                { "mcqScore", mcqScore },
                { "correct", correctCount },
                { "total", total },
                { "simulationScore", simulationScore },
                { "finalScore", finalScore },
                { "passed", passed }
            };

            NavigationManager.Instance.NavigateTo(
                "results", resultsData);
        }
    }
}
```
