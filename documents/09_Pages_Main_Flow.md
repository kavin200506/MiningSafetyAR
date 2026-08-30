# 09 — Page Implementation: Main Flow

## Scene: UI_Main

---

## 1. DashboardPage

**File:** `Assets/Scripts/UI/Pages/DashboardPageController.cs`
**Template:** `Assets/UI/Templates/Pages/DashboardPage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <ScrollView class="scroll-area">
        <!-- Top Bar (Greeting) -->
        <VisualElement class="dashboard__greeting" name="greeting-bar">
            <VisualElement class="greeting__left">
                <Label class="greeting__text" name="greeting-text"
                       text="Hello, Ramesh" />
                <Label class="greeting__sub" name="greeting-sub"
                       text="Stay safe, stay certified" />
            </VisualElement>
            <VisualElement class="greeting__avatar" name="avatar">
                <Label text="👷" style="font-size: 20px;" />
            </VisualElement>
        </VisualElement>

        <!-- Overall Progress Card -->
        <VisualElement class="card" name="progress-card"
                       style="margin-bottom: 20px;">
            <VisualElement class="progress-card__header">
                <Label class="progress-card__title"
                       text="Overall Progress" />
                <Label class="progress-card__value"
                       name="progress-value" text="68%" />
            </VisualElement>
            <VisualElement class="progress-track" name="overall-track"
                           style="margin: 12px 0;">
                <VisualElement class="progress-fill" />
            </VisualElement>
            <VisualElement class="progress-card__stats">
                <VisualElement class="stat">
                    <Label class="stat__value" name="completed-count"
                           text="2" />
                    <Label class="stat__label" text="Completed" />
                </VisualElement>
                <VisualElement class="stat">
                    <Label class="stat__value" name="cert-count"
                           text="2" />
                    <Label class="stat__label" text="Certificates" />
                </VisualElement>
                <VisualElement class="stat">
                    <Label class="stat__value" name="attempts-count"
                           text="7" />
                    <Label class="stat__label" text="Attempts" />
                </VisualElement>
            </VisualElement>
        </VisualElement>

        <!-- Training Modules Section -->
        <VisualElement class="section-header">
            <Label class="section-title" text="Training Modules" />
            <Button class="btn-text" name="see-all-btn"
                    text="See All" />
        </VisualElement>
        <VisualElement class="module-list" name="module-list" />

        <!-- Certificate Banner -->
        <VisualElement class="card-accent" name="cert-banner"
                       style="margin-top: 16px;">
            <VisualElement class="cert-banner__content">
                <Label text="🏆" style="font-size: 28px;" />
                <VisualElement>
                    <Label class="cert-banner__title"
                           text="Your Certificates" />
                    <Label class="cert-banner__sub"
                           name="cert-banner-sub"
                           text="2 certificates earned" />
                </VisualElement>
            </VisualElement>
            <Button class="btn-primary" name="view-certs-btn"
                    text="View All" style="margin-top: 12px;" />
        </VisualElement>

        <!-- Bottom Spacer -->
        <VisualElement style="height: 80px;" />
    </ScrollView>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class DashboardPageController : PageController
    {
        [SerializeField] private VisualTreeAsset moduleCardTemplate;

        private Label greetingText, progressValue;
        private Label completedCount, certCount, attemptsCount;
        private Label certBannerSub;
        private VisualElement moduleList;
        private VisualElement overallTrack;
        private Button seeAllBtn, viewCertsBtn;

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

            seeAllBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.NavigateToTab(
                    "training_catalogue"));
            viewCertsBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.NavigateTo("progress"));
        }

        public override void OnPageEnter()
        {
            Refresh();
        }

        private void Refresh()
        {
            var worker = AppDataService.Instance.CurrentWorker;
            if (worker == null) return;

            // Greeting
            string firstName = worker.name.Split(' ')[0];
            greetingText.text = $"Hello, {firstName}";

            // Progress
            progressValue.text = $"{worker.overallProgress}%";
            ProgressBarHelper.SetProgress(
                overallTrack, worker.overallProgress);

            // Stats
            int completed = AppDataService.Instance
                .GetModulesByStatus(ModuleStatus.Completed).Count;
            completedCount.text = completed.ToString();
            certCount.text = worker.certificatesEarned.ToString();
            attemptsCount.text = worker.totalAttempts.ToString();

            // Certificate banner
            certBannerSub.text =
                $"{worker.certificatesEarned} certificates earned";

            // Module cards (first 3)
            moduleList.Clear();
            var modules = AppDataService.Instance.GetAllModules();
            int count = Mathf.Min(3, modules.Count);
            for (int i = 0; i < count; i++)
            {
                var card = ModuleCardFactory.Create(
                    moduleCardTemplate,
                    modules[i],
                    OnModuleClicked);
                moduleList.Add(card);
            }
        }

        private void OnModuleClicked(ModuleData mod)
        {
            NavigationManager.Instance.NavigateTo(
                "module_detail", mod.id);
        }
    }
}
```

---

## 2. TrainingCataloguePage

**File:** `Assets/Scripts/UI/Pages/TrainingCataloguePageController.cs`

### UXML Structure
```xml
<VisualElement class="page-container">
    <VisualElement class="header">
        <VisualElement class="header__back-btn" />
        <Label class="header__title" text="Training Modules" />
        <VisualElement class="header__right-action" />
    </VisualElement>

    <!-- Filter Pills -->
    <VisualElement class="filter-row" name="filter-row">
        <Button class="filter-pill filter-pill--active"
                name="filter-all" text="All" />
        <Button class="filter-pill" name="filter-completed"
                text="Completed" />
        <Button class="filter-pill" name="filter-in-progress"
                text="In Progress" />
        <Button class="filter-pill" name="filter-not-started"
                text="Not Started" />
    </VisualElement>

    <!-- Module Count -->
    <Label class="module-count" name="module-count"
           text="5 modules" style="padding: 0 16px; margin: 8px 0;" />

    <!-- Module List -->
    <ScrollView class="scroll-area">
        <VisualElement class="module-list" name="module-list" />
    </ScrollView>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class TrainingCataloguePageController : PageController
    {
        [SerializeField] private VisualTreeAsset moduleCardTemplate;

        private Button[] filterButtons;
        private VisualElement moduleList;
        private Label moduleCount;
        private string activeFilter = "all";

        protected override void BindUI()
        {
            moduleList = root.Q("module-list");
            moduleCount = root.Q<Label>("module-count");

            filterButtons = new Button[]
            {
                root.Q<Button>("filter-all"),
                root.Q<Button>("filter-completed"),
                root.Q<Button>("filter-in-progress"),
                root.Q<Button>("filter-not-started")
            };

            string[] filters = {
                "all", "completed", "in_progress", "not_started" };
            for (int i = 0; i < filterButtons.Length; i++)
            {
                int index = i;
                filterButtons[i].RegisterCallback<ClickEvent>(evt =>
                    SetFilter(filters[index]));
            }
        }

        public override void OnPageEnter()
        {
            Refresh();
        }

        private void SetFilter(string filter)
        {
            activeFilter = filter;

            // Update pill styles
            string[] filterNames = {
                "all", "completed", "in-progress", "not-started" };
            for (int i = 0; i < filterButtons.Length; i++)
            {
                filterButtons[i].RemoveFromClassList(
                    "filter-pill--active");
                if (filterNames[i] == filter)
                    filterButtons[i].AddToClassList(
                        "filter-pill--active");
            }

            Refresh();
        }

        private void Refresh()
        {
            var modules = AppDataService.Instance.GetAllModules();

            // Apply filter
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

            // Update count
            moduleCount.text = $"{modules.Count} modules";

            // Populate cards
            moduleList.Clear();
            if (modules.Count == 0)
            {
                // Empty state
                var empty = new Label("📭 No modules found");
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.color =
                    new StyleColor(HexToColor("#707070"));
                empty.style.marginTop = 40;
                moduleList.Add(empty);
                return;
            }

            foreach (var mod in modules)
            {
                var card = ModuleCardFactory.Create(
                    moduleCardTemplate, mod, OnModuleClicked);
                moduleList.Add(card);
            }
        }

        private void OnModuleClicked(ModuleData mod)
        {
            NavigationManager.Instance.NavigateTo(
                "module_detail", mod.id);
        }
    }
}
```

---

## 3. ModuleDetailPage

**File:** `Assets/Scripts/UI/Pages/ModuleDetailPageController.cs`

### UXML Structure
```xml
<VisualElement class="page-container">
    <VisualElement class="header">
        <Button class="header__back-btn" name="back-btn">
            <Label text="←" style="font-size: 20px;" />
        </Button>
        <Label class="header__title" name="module-title" text="" />
        <VisualElement class="header__right-action" />
    </VisualElement>

    <ScrollView class="scroll-area">
        <!-- Hero Section -->
        <VisualElement class="card module-hero" name="hero-section">
            <Label class="module-hero__emoji" name="hero-emoji" />
            <Label class="module-hero__badge" name="hero-badge" />
        </VisualElement>

        <!-- Description -->
        <Label class="module-detail__desc" name="description"
               style="margin: 16px 0;" />

        <!-- Meta Row -->
        <VisualElement class="meta-row" name="meta-row">
            <VisualElement class="pill">
                <Label text="⏱" />
                <Label name="duration" text="45 min" />
            </VisualElement>
            <VisualElement class="pill">
                <Label text="⭐" />
                <Label name="difficulty" text="Medium" />
            </VisualElement>
            <VisualElement class="pill">
                <Label text="🎯" />
                <Label name="domain" text="Fire Safety" />
            </VisualElement>
        </VisualElement>

        <!-- Best Score (if available) -->
        <VisualElement class="card" name="best-score-card"
                       style="margin-top: 16px; display: none;">
            <Label class="card-title" text="Best Score" />
            <VisualElement class="progress-track" name="best-track">
                <VisualElement class="progress-fill" />
            </VisualElement>
            <Label name="best-score-text" text="" />
            <Label name="attempt-count" text="" />
            <Label name="last-attempt" text="" />
        </VisualElement>

        <!-- Learning Objectives -->
        <VisualElement class="section" style="margin-top: 16px;">
            <Label class="section-title"
                   text="Learning Objectives" />
            <VisualElement class="objectives-list"
                           name="objectives-list" />
        </VisualElement>

        <!-- Competency Scores -->
        <VisualElement class="section" name="competency-section"
                       style="margin-top: 16px; display: none;">
            <Label class="section-title"
                   text="Competency Scores" />
            <VisualElement class="score-bars"
                           name="score-bars" />
        </VisualElement>

        <!-- Attempt History -->
        <VisualElement class="section" style="margin-top: 16px;">
            <Label class="section-title"
                   text="Attempt History" />
            <VisualElement class="table" name="attempt-table">
                <VisualElement class="table-header">
                    <Label class="table-header__cell"
                           style="width: 30px;" text="#" />
                    <Label class="table-header__cell"
                           style="flex: 1;" text="Date" />
                    <Label class="table-header__cell"
                           style="width: 60px; text-align: right;"
                           text="Score" />
                    <Label class="table-header__cell"
                           style="width: 60px; text-align: right;"
                           text="Status" />
                </VisualElement>
                <VisualElement class="table-body"
                               name="table-body" />
            </VisualElement>
        </VisualElement>

        <!-- Action Buttons -->
        <VisualElement class="action-section" name="action-section"
                       style="margin-top: 24px;">
            <Button class="btn-primary" name="action-btn"
                    text="START TRAINING" />
            <Button class="btn-secondary" name="cert-btn"
                    text="VIEW CERTIFICATE"
                    style="display: none; margin-top: 8px;" />
            <VisualElement class="locked-message"
                           name="locked-message"
                           style="display: none;">
                <Label text="🔒" style="font-size: 24px;" />
                <Label text="Complete previous modules to unlock"
                       style="color: var(--text-muted); font-size: 13px;
                               text-align: center;" />
            </VisualElement>
        </VisualElement>

        <VisualElement style="height: 80px;" />
    </ScrollView>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class ModuleDetailPageController : PageController
    {
        [SerializeField] private VisualTreeAsset scoreBarTemplate;

        private string moduleId;
        private ModuleData currentModule;

        // Element references
        private Label moduleTitle, heroEmoji, heroBadge;
        private Label description, duration, difficulty, domain;
        private VisualElement bestScoreCard, competencySection;
        private VisualElement objectivesList, scoreBars;
        private VisualElement tableBody, actionSection;
        private VisualElement lockedMessage;
        private Button actionBtn, certBtn, backBtn;

        protected override void BindUI()
        {
            moduleTitle = root.Q<Label>("module-title");
            heroEmoji = root.Q<Label>("hero-emoji");
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
            actionSection = root.Q("action-section");
            lockedMessage = root.Q("locked-message");
            actionBtn = root.Q<Button>("action-btn");
            certBtn = root.Q<Button>("cert-btn");
            backBtn = root.Q<Button>("back-btn");

            backBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.GoBack());
            actionBtn.RegisterCallback<ClickEvent>(evt =>
                OnActionClicked());
            certBtn.RegisterCallback<ClickEvent>(evt =>
                OnViewCertificate());
        }

        public override void SetNavigationParameter(object param)
        {
            moduleId = param as string;
        }

        public override void OnPageEnter()
        {
            currentModule = AppDataService.Instance.GetModule(moduleId);
            if (currentModule != null) Refresh();
        }

        private void Refresh()
        {
            var mod = currentModule;

            // Header
            moduleTitle.text = mod.title;
            heroEmoji.text = mod.iconEmoji;
            heroBadge.text = mod.status.ToString();
            heroBadge.AddToClassList(
                $"badge--{mod.status.ToString().ToLower()}");

            // Info
            description.text = mod.description;
            duration.text = mod.duration;
            difficulty.text = mod.difficulty;
            domain.text = mod.domain;

            // Best Score
            if (mod.bestScore > 0)
            {
                bestScoreCard.style.display = DisplayStyle.Flex;
                ProgressBarHelper.SetProgress(
                    bestScoreCard.Q("best-track"), mod.bestScore);
                root.Q<Label>("best-score-text").text =
                    $"{mod.bestScore}%";
                root.Q<Label>("attempt-count").text =
                    $"{mod.attempts} attempts";
                root.Q<Label>("last-attempt").text =
                    $"Last: {mod.lastAttempt}";
            }

            // Objectives
            objectivesList.Clear();
            if (mod.objectives != null)
            {
                for (int i = 0; i < mod.objectives.Length; i++)
                {
                    var row = new Label(
                        $"{i + 1}. {mod.objectives[i]}");
                    row.style.fontSize = 13;
                    row.style.color = new StyleColor(
                        HexToColor("#B0B0B0"));
                    row.style.marginBottom = 6;
                    objectivesList.Add(row);
                }
            }

            // Competency Scores
            if (mod.bestScore > 0)
            {
                competencySection.style.display = DisplayStyle.Flex;
                scoreBars.Clear();
                var cs = mod.competencyScores;
                AddScoreBar("Hazard Recognition",
                    cs.hazardRecognition);
                AddScoreBar("Extinguisher Use",
                    cs.extinguisherUse);
                AddScoreBar("Evacuation", cs.evacuation);
                AddScoreBar("Emergency Response",
                    cs.emergencyResponse);
            }

            // Attempt History
            tableBody.Clear();
            var attempts = AppDataService.Instance.CurrentWorker
                .attempts.FindAll(a => a.module == moduleId);
            for (int i = 0; i < attempts.Count; i++)
            {
                var a = attempts[i];
                var row = new VisualElement();
                row.AddToClassList("table-row");

                row.Add(CreateCell((i + 1).ToString()));
                row.Add(CreateCell(a.date));
                row.Add(CreateCell($"{a.score}%",
                    "table-cell--accent"));
                row.Add(CreateCell(a.passed ? "Pass" : "Fail",
                    a.passed ? "table-cell--success"
                             : "table-cell--danger"));

                tableBody.Add(row);
            }

            // Action buttons
            ConfigureActionButton();
        }

        private void ConfigureActionButton()
        {
            switch (currentModule.status)
            {
                case ModuleStatus.Locked:
                    actionBtn.style.display = DisplayStyle.None;
                    certBtn.style.display = DisplayStyle.None;
                    lockedMessage.style.display = DisplayStyle.Flex;
                    break;
                case ModuleStatus.Completed:
                    actionBtn.text = "RETAKE TRAINING";
                    actionBtn.style.display = DisplayStyle.Flex;
                    if (!string.IsNullOrEmpty(
                        currentModule.certificateId))
                    {
                        certBtn.style.display = DisplayStyle.Flex;
                    }
                    break;
                case ModuleStatus.InProgress:
                    actionBtn.text = "CONTINUE TRAINING";
                    actionBtn.style.display = DisplayStyle.Flex;
                    break;
                default:
                    actionBtn.text = "START TRAINING";
                    actionBtn.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        private void OnActionClicked()
        {
            NavigationManager.Instance.NavigateTo(
                "learning_content", moduleId);
        }

        private void OnViewCertificate()
        {
            NavigationManager.Instance.NavigateTo(
                "certificate", currentModule.certificateId);
        }

        private Label CreateCell(string text, string className = null)
        {
            var cell = new Label(text);
            cell.AddToClassList("table-cell");
            if (className != null)
                cell.AddToClassList(className);
            return cell;
        }

        private void AddScoreBar(string label, int value)
        {
            // Instantiate score bar from template and configure
            if (scoreBarTemplate != null)
            {
                var bar = scoreBarTemplate.Instantiate();
                ScoreBarHelper.Configure(bar, label, value);
                scoreBars.Add(bar);
            }
        }
    }
}
```

---

## 4. ProgressPage

**File:** `Assets/Scripts/UI/Pages/ProgressPageController.cs`

### UXML Structure
```xml
<VisualElement class="page-container">
    <VisualElement class="header">
        <VisualElement class="header__back-btn" />
        <Label class="header__title" text="Progress" />
        <VisualElement class="header__right-action" />
    </VisualElement>

    <ScrollView class="scroll-area">
        <!-- Overview Card (Orange gradient bg) -->
        <VisualElement class="card-accent"
                       name="overview-card">
            <Label class="overview__value"
                   name="overall-progress" text="68%" />
            <VisualElement class="progress-track"
                           name="overview-track">
                <VisualElement class="progress-fill" />
            </VisualElement>
            <VisualElement class="overview__stats">
                <VisualElement class="stat">
                    <Label class="stat__value"
                           name="completed-count" text="2" />
                    <Label class="stat__label"
                           text="Completed" />
                </VisualElement>
                <VisualElement class="stat">
                    <Label class="stat__value"
                           name="total-attempts" text="7" />
                    <Label class="stat__label"
                           text="Total Attempts" />
                </VisualElement>
                <VisualElement class="stat">
                    <Label class="stat__value"
                           name="cert-count" text="2" />
                    <Label class="stat__label"
                           text="Certificates" />
                </VisualElement>
            </VisualElement>
        </VisualElement>

        <!-- Competency Scores -->
        <VisualElement class="section" style="margin-top: 20px;">
            <Label class="section-title"
                   text="Competency Scores" />
            <VisualElement class="score-bars"
                           name="competency-bars" />
        </VisualElement>

        <!-- Module Progress -->
        <VisualElement class="section" style="margin-top: 20px;">
            <Label class="section-title"
                   text="Module Progress" />
            <VisualElement class="module-rows"
                           name="module-rows" />
        </VisualElement>

        <!-- Attempt History -->
        <VisualElement class="section" style="margin-top: 20px;">
            <Label class="section-title"
                   text="Attempt History" />
            <VisualElement class="table" name="history-table">
                <VisualElement class="table-header">
                    <Label class="table-header__cell"
                           style="width: 30px;" text="#" />
                    <Label class="table-header__cell"
                           style="flex: 1;" text="Module" />
                    <Label class="table-header__cell"
                           style="width: 80px;" text="Date" />
                    <Label class="table-header__cell"
                           style="width: 50px; text-align: right;"
                           text="Score" />
                    <Label class="table-header__cell"
                           style="width: 50px; text-align: right;"
                           text="Status" />
                </VisualElement>
                <VisualElement class="table-body"
                               name="history-body" />
            </VisualElement>
        </VisualElement>

        <VisualElement style="height: 80px;" />
    </ScrollView>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class ProgressPageController : PageController
    {
        private Label overallProgress, completedCount;
        private Label totalAttempts, certCount;
        private VisualElement overviewTrack;
        private VisualElement competencyBars, moduleRows;
        private VisualElement historyBody;

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
        }

        public override void OnPageEnter()
        {
            Refresh();
        }

        private void Refresh()
        {
            var worker = AppDataService.Instance.CurrentWorker;
            if (worker == null) return;

            // Overview
            overallProgress.text = $"{worker.overallProgress}%";
            ProgressBarHelper.SetProgress(
                overviewTrack, worker.overallProgress);
            completedCount.text = AppDataService.Instance
                .GetModulesByStatus(ModuleStatus.Completed)
                .Count.ToString();
            totalAttempts.text = worker.totalAttempts.ToString();
            certCount.text = worker.certificatesEarned.ToString();

            // Competency Scores
            competencyBars.Clear();
            var cs = worker.competencyScores;
            AddScoreBar("Hazard Recognition", cs.hazardRecognition);
            AddScoreBar("PPE Selection", cs.ppeSelection);
            AddScoreBar("Evacuation", cs.evacuation);
            AddScoreBar("Emergency Response", cs.emergencyResponse);

            // Module Rows
            moduleRows.Clear();
            var modules = AppDataService.Instance.GetAllModules();
            foreach (var mod in modules)
            {
                var row = new VisualElement();
                row.AddToClassList("card-interactive");
                row.style.flexDirection = Row.Row;
                row.style.padding = 12;
                row.style.marginBottom = 8;
                row.style.alignItems = Align.Center;

                // Icon
                var icon = new Label(mod.iconEmoji);
                icon.style.fontSize = 22;
                icon.style.marginRight = 12;

                // Info
                var info = new VisualElement();
                info.style.flex = 1;
                var title = new Label(mod.title);
                title.style.fontSize = 13;
                title.style.fontWeight = FontWeight.Bold;
                var meta = new Label(
                    $"{mod.progress}% · Best: {mod.bestScore}%");
                meta.style.fontSize = 11;
                meta.style.color = new StyleColor(
                    HexToColor("#707070"));
                info.Add(title);
                info.Add(meta);

                // Status
                var badge = new Label(mod.status.ToString());
                badge.AddToClassList(
                    $"badge--{mod.status.ToString().ToLower()}");

                row.Add(icon);
                row.Add(info);
                row.Add(badge);
                moduleRows.Add(row);
            }

            // Attempt History
            historyBody.Clear();
            var attempts = worker.attempts;
            for (int i = 0; i < attempts.Count; i++)
            {
                var a = attempts[i];
                var row = new VisualElement();
                row.AddToClassList("table-row");

                row.Add(CreateCell((i + 1).ToString()));
                row.Add(CreateCell(a.module));
                row.Add(CreateCell(a.date));
                row.Add(CreateCell($"{a.score}%",
                    "table-cell--accent"));
                row.Add(CreateCell(a.passed ? "Pass" : "Fail",
                    a.passed ? "table-cell--success"
                             : "table-cell--danger"));

                historyBody.Add(row);
            }
        }

        private void AddScoreBar(string label, int value)
        {
            var bar = new VisualElement();
            bar.style.marginBottom = 12;
            ScoreBarHelper.Configure(bar, label, value);
            competencyBars.Add(bar);
        }

        private Label CreateCell(string text, string cls = null)
        {
            var cell = new Label(text);
            cell.AddToClassList("table-cell");
            if (cls != null) cell.AddToClassList(cls);
            return cell;
        }
    }
}
```

---

## 5. SettingsPage

**File:** `Assets/Scripts/UI/Pages/SettingsPageController.cs`

### UXML Structure
```xml
<VisualElement class="page-container">
    <VisualElement class="header">
        <VisualElement class="header__back-btn" />
        <Label class="header__title" text="Settings" />
        <VisualElement class="header__right-action" />
    </VisualElement>

    <ScrollView class="scroll-area">
        <!-- Profile Card -->
        <VisualElement class="card" name="profile-card">
            <VisualElement class="profile__avatar">
                <Label text="👷" style="font-size: 24px;" />
            </VisualElement>
            <Label class="profile__name" name="profile-name" />
            <Label class="profile__id" name="profile-id" />
            <Label class="profile__org" name="profile-org" />
        </VisualElement>

        <!-- Language -->
        <VisualElement class="section" style="margin-top: 20px;">
            <Label class="section-title" text="Language" />
            <VisualElement class="language-selector"
                           name="lang-selector">
                <Button class="lang-btn lang-btn--active"
                        name="lang-en" text="English" />
                <Button class="lang-btn"
                        name="lang-hi" text="Hindi" />
                <Button class="lang-btn"
                        name="lang-sat" text="Santali" />
            </VisualElement>
        </VisualElement>

        <!-- Preferences -->
        <VisualElement class="section" style="margin-top: 20px;">
            <Label class="section-title" text="Preferences" />

            <VisualElement class="settings-row">
                <Label class="settings-row__label"
                       text="Sound Effects" />
                <VisualElement class="toggle-switch"
                               name="toggle-sound">
                    <VisualElement class="toggle-switch__knob" />
                </VisualElement>
            </VisualElement>

            <VisualElement class="settings-row">
                <Label class="settings-row__label"
                       text="Voice Guidance" />
                <VisualElement class="toggle-switch"
                               name="toggle-voice">
                    <VisualElement class="toggle-switch__knob" />
                </VisualElement>
            </VisualElement>
        </VisualElement>

        <!-- Data & Storage -->
        <VisualElement class="section" style="margin-top: 20px;">
            <Label class="section-title"
                   text="Data &amp; Storage" />
            <VisualElement class="settings-row">
                <Label class="settings-row__label"
                       text="Offline Content" />
                <Label class="settings-row__value"
                       text="3 of 5" />
            </VisualElement>
            <VisualElement class="settings-row">
                <Label class="settings-row__label"
                       text="Storage Used" />
                <Label class="settings-row__value"
                       text="248 MB" />
            </VisualElement>
            <VisualElement class="settings-row">
                <Label class="settings-row__label"
                       text="Sync Status" />
                <Label class="settings-row__value"
                       text="Synced"
                       style="color: var(--success);" />
            </VisualElement>
        </VisualElement>

        <!-- About -->
        <VisualElement class="section" style="margin-top: 20px;">
            <Label class="section-title" text="About" />
            <VisualElement class="settings-row">
                <Label class="settings-row__label"
                       text="App Version" />
                <Label class="settings-row__value"
                       text="1.0.0" />
            </VisualElement>
            <VisualElement class="settings-row">
                <Label class="settings-row__label"
                       text="PS Number" />
                <Label class="settings-row__value"
                       text="SIH26041" />
            </VisualElement>
            <VisualElement class="settings-row">
                <Label class="settings-row__label"
                       text="Organization" />
                <Label class="settings-row__value"
                       text="Govt. of Jharkhand" />
            </VisualElement>
        </VisualElement>

        <!-- Logout -->
        <Button class="btn-danger" name="logout-btn"
                text="LOG OUT" style="margin-top: 24px;" />

        <Label style="text-align: center; color: var(--text-muted);
                       font-size: 10px; margin-top: 16px;"
               text="ARC MineSafetyAr - Demo UI Reference" />

        <VisualElement style="height: 80px;" />
    </ScrollView>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class SettingsPageController : PageController
    {
        private Label profileName, profileId, profileOrg;
        private ToggleSwitchController soundToggle;
        private ToggleSwitchController voiceToggle;
        private Button logoutBtn;
        private Button langEn, langHi, langSat;

        protected override void BindUI()
        {
            profileName = root.Q<Label>("profile-name");
            profileId = root.Q<Label>("profile-id");
            profileOrg = root.Q<Label>("profile-org");
            logoutBtn = root.Q<Button>("logout-btn");
            langEn = root.Q<Button>("lang-en");
            langHi = root.Q<Button>("lang-hi");
            langSat = root.Q<Button>("lang-sat");

            soundToggle = new ToggleSwitchController(
                root.Q("toggle-sound"));
            voiceToggle = new ToggleSwitchController(
                root.Q("toggle-voice"));

            logoutBtn.RegisterCallback<ClickEvent>(evt =>
                OnLogout());
            langEn.RegisterCallback<ClickEvent>(evt =>
                SetLanguage("English", langEn));
            langHi.RegisterCallback<ClickEvent>(evt =>
                SetLanguage("Hindi", langHi));
            langSat.RegisterCallback<ClickEvent>(evt =>
                SetLanguage("Santali", langSat));
        }

        public override void OnPageEnter()
        {
            var worker = AppDataService.Instance.CurrentWorker;
            if (worker != null)
            {
                profileName.text = worker.name;
                profileId.text = worker.id;
                profileOrg.text = worker.organization;
            }

            // Restore language selection
            string currentLang =
                PlayerPrefs.GetString("SelectedLanguage", "English");
            SetLanguageUI(currentLang);
        }

        private void OnLogout()
        {
            AppDataService.Instance.Logout();
            NavigationManager.Instance.NavigateToRoot("login");
        }

        private void SetLanguage(string lang, Button activeBtn)
        {
            PlayerPrefs.SetString("SelectedLanguage", lang);
            SetLanguageUI(lang);
        }

        private void SetLanguageUI(string lang)
        {
            langEn.RemoveFromClassList("lang-btn--active");
            langHi.RemoveFromClassList("lang-btn--active");
            langSat.RemoveFromClassList("lang-btn--active");

            switch (lang)
            {
                case "Hindi": langHi.AddToClassList("lang-btn--active"); break;
                case "Santali": langSat.AddToClassList("lang-btn--active"); break;
                default: langEn.AddToClassList("lang-btn--active"); break;
            }
        }
    }
}
```
