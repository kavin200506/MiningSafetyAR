# 11 — Page Implementation: Results & Certificates

## Scene: UI_Main

---

## 1. ResultsPage

**File:** `Assets/Scripts/UI/Pages/ResultsPageController.cs`
**Template:** `Assets/UI/Templates/Pages/ResultsPage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <ScrollView class="scroll-area">
        <!-- Result Header -->
        <VisualElement class="results__header" name="results-header">
            <Label class="results__icon" name="result-icon" />
            <Label class="results__title" name="result-title" />
            <Label class="results__subtitle"
                   name="result-subtitle" />
        </VisualElement>

        <!-- Score Card -->
        <VisualElement class="card" name="score-card">
            <VisualElement class="score-row">
                <Label class="score-row__label"
                       text="Simulation Score" />
                <Label class="score-row__value"
                       name="sim-score" text="80/100" />
            </VisualElement>
            <VisualElement class="divider" />
            <VisualElement class="score-row">
                <Label class="score-row__label"
                       text="MCQ Score" />
                <Label class="score-row__value"
                       name="mcq-score" text="80/100" />
            </VisualElement>
            <VisualElement class="divider" />
            <VisualElement class="score-row">
                <Label class="score-row__label"
                       text="Questions Correct" />
                <Label class="score-row__value"
                       name="correct-count" text="4/5" />
            </VisualElement>
            <VisualElement class="divider" />
            <VisualElement class="score-row score-row--final">
                <Label class="score-row__label"
                       text="Final Score" />
                <Label class="score-row__value score-row__value--large"
                       name="final-score" text="80" />
            </VisualElement>
            <Label class="results__badge" name="result-badge" />
        </VisualElement>

        <!-- Competency Breakdown -->
        <VisualElement class="section" style="margin-top: 20px;">
            <Label class="section-title"
                   text="Competency Breakdown" />
            <VisualElement class="score-bars"
                           name="competency-bars" />
        </VisualElement>

        <!-- Attempt Info -->
        <VisualElement class="card" name="attempt-info"
                       style="margin-top: 16px;">
            <VisualElement class="info-row">
                <Label class="info-row__label" text="Module" />
                <Label class="info-row__value"
                       name="module-name" text="" />
            </VisualElement>
            <VisualElement class="info-row">
                <Label class="info-row__label"
                       text="Attempt #" />
                <Label class="info-row__value"
                       name="attempt-num" text="" />
            </VisualElement>
            <VisualElement class="info-row">
                <Label class="info-row__label"
                       text="Personal Best" />
                <Label class="info-row__value"
                       name="personal-best" text="" />
            </VisualElement>
        </VisualElement>

        <!-- Action Buttons -->
        <VisualElement class="action-section"
                       style="margin-top: 24px;">
            <Button class="btn-primary" name="cert-btn"
                    text="VIEW CERTIFICATE"
                    style="display: none;" />
            <Button class="btn-primary" name="retry-btn"
                    text="TRY AGAIN" />
            <Button class="btn-secondary" name="module-btn"
                    text="BACK TO MODULE"
                    style="margin-top: 8px;" />
            <Button class="btn-secondary" name="dashboard-btn"
                    text="BACK TO DASHBOARD"
                    style="margin-top: 8px;" />
        </VisualElement>

        <VisualElement style="height: 80px;" />
    </ScrollView>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class ResultsPageController : PageController
    {
        private Dictionary<string, object> resultsData;

        private Label resultIcon, resultTitle, resultSubtitle;
        private Label simScore, mcqScore, correctCount, finalScore;
        private Label resultBadge, moduleName, attemptNum;
        private Label personalBest;
        private VisualElement competencyBars;
        private Button certBtn, retryBtn, moduleBtn, dashboardBtn;

        protected override void BindUI()
        {
            resultIcon = root.Q<Label>("result-icon");
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

            certBtn.RegisterCallback<ClickEvent>(evt =>
                OnViewCertificate());
            retryBtn.RegisterCallback<ClickEvent>(evt =>
                OnRetry());
            moduleBtn.RegisterCallback<ClickEvent>(evt =>
                OnBackToModule());
            dashboardBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.NavigateToRoot(
                    "dashboard"));
        }

        public override void SetNavigationParameter(object param)
        {
            resultsData = param as Dictionary<string, object>;
        }

        public override void OnPageEnter()
        {
            if (resultsData == null) return;
            Refresh();
        }

        private void Refresh()
        {
            int sim = (int)resultsData["simulationScore"];
            int mcq = (int)resultsData["mcqScore"];
            int correct = (int)resultsData["correct"];
            int total = (int)resultsData["total"];
            int final = (int)resultsData["finalScore"];
            bool passed = (bool)resultsData["passed"];
            string moduleId = resultsData["moduleId"] as string;

            // Header
            resultIcon.text = passed ? "🏆" : "❌";
            resultTitle.text = passed
                ? "Congratulations!" : "Keep Trying!";
            resultSubtitle.text = passed
                ? "You've passed the assessment!"
                : "You need 60% to pass. Review and try again.";

            // Scores
            simScore.text = $"{sim}/100";
            mcqScore.text = $"{mcq}/100";
            correctCount.text = $"{correct}/{total}";
            finalScore.text = final.ToString();

            // Badge
            resultBadge.text = passed ? "PASSED" : "FAILED";
            resultBadge.AddToClassList(
                passed ? "badge--pass" : "badge--fail");

            // Module info
            var mod = AppDataService.Instance.GetModule(moduleId);
            moduleName.text = mod?.title ?? moduleId;

            var worker = AppDataService.Instance.CurrentWorker;
            int attempts = worker.attempts
                .FindAll(a => a.module == moduleId).Count;
            attemptNum.text = attempts.ToString();

            int best = worker.attempts
                .Where(a => a.module == moduleId)
                .DefaultIfEmpty()
                .Max(a => a?.score ?? 0);
            personalBest.text = $"{best}%";

            // Show certificate button if passed
            if (passed)
            {
                certBtn.style.display = DisplayStyle.Flex;
            }
        }

        private void OnViewCertificate()
        {
            string moduleId = resultsData["moduleId"] as string;
            NavigationManager.Instance.NavigateTo(
                "certificate", moduleId);
        }

        private void OnRetry()
        {
            string moduleId = resultsData["moduleId"] as string;
            NavigationManager.Instance.NavigateTo(
                "learning_content", moduleId);
        }

        private void OnBackToModule()
        {
            string moduleId = resultsData["moduleId"] as string;
            NavigationManager.Instance.NavigateTo(
                "module_detail", moduleId);
        }
    }
}
```

---

## 2. CertificatePage

**File:** `Assets/Scripts/UI/Pages/CertificatePageController.cs`
**Template:** `Assets/UI/Templates/Pages/CertificatePage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <VisualElement class="header">
        <Button class="header__back-btn" name="back-btn">
            <Label text="←" style="font-size: 20px;" />
        </Button>
        <Label class="header__title" text="Certificate" />
        <VisualElement class="header__right-action" />
    </VisualElement>

    <ScrollView class="scroll-area">
        <!-- Certificate Card -->
        <VisualElement class="card cert-card" name="cert-card">
            <!-- Orange Header -->
            <VisualElement class="cert-card__header">
                <Label text="🛡️" style="font-size: 28px;" />
                <Label class="cert-card__title"
                       text="SAFETY CERTIFICATE" />
                <Label class="cert-card__govt"
                       text="Government of Jharkhand" />
            </VisualElement>

            <!-- Certificate Body -->
            <VisualElement class="cert-card__body">
                <Label class="cert-card__certifies"
                       text="This certifies that" />
                <Label class="cert-card__worker-name"
                       name="worker-name" text="" />
                <Label class="cert-card__has-completed"
                       text="has successfully completed" />
                <Label class="cert-card__module-title"
                       name="module-title" text="" />

                <VisualElement class="cert-card__score">
                    <Label name="score" text="85%" />
                    <Label class="badge badge--pass"
                           text="PASSED" />
                </VisualElement>

                <!-- QR Placeholder -->
                <VisualElement class="cert-card__qr">
                    <Label class="cert-card__qr-text"
                           text="QR Code" />
                    <Label class="cert-card__qr-id"
                           name="cert-id" text="" />
                </VisualElement>
                <Label class="cert-card__scan-hint"
                       text="Scan to verify certificate" />

                <!-- Metadata -->
                <VisualElement class="cert-card__meta">
                    <VisualElement class="cert-meta__row">
                        <Label class="cert-meta__label"
                               text="Certificate ID" />
                        <Label class="cert-meta__value"
                               name="cert-id-meta" text="" />
                    </VisualElement>
                    <VisualElement class="cert-meta__row">
                        <Label class="cert-meta__label"
                               text="Issued" />
                        <Label class="cert-meta__value"
                               name="issued-date" text="" />
                    </VisualElement>
                    <VisualElement class="cert-meta__row">
                        <Label class="cert-meta__label"
                               text="Expires" />
                        <Label class="cert-meta__value"
                               name="expiry-date" text="" />
                    </VisualElement>
                    <VisualElement class="cert-meta__row">
                        <Label class="cert-meta__label"
                               text="Organization" />
                        <Label class="cert-meta__value"
                               name="organization" text="" />
                    </VisualElement>
                </VisualElement>
            </VisualElement>
        </VisualElement>

        <!-- Action Buttons -->
        <VisualElement class="action-section"
                       style="margin-top: 16px;">
            <Button class="btn-primary" name="download-btn"
                    text="Download Certificate" />
            <Button class="btn-secondary" name="share-btn"
                    text="Share Certificate"
                    style="margin-top: 8px;" />
            <Button class="btn-secondary" name="verify-btn"
                    text="Verify Certificate"
                    style="margin-top: 8px;" />
        </VisualElement>

        <VisualElement style="height: 80px;" />
    </ScrollView>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class CertificatePageController : PageController
    {
        private string moduleId;

        private Label workerName, moduleTitle, score;
        private Label certId, certIdMeta;
        private Label issuedDate, expiryDate, organization;
        private Button backBtn, downloadBtn, shareBtn, verifyBtn;

        protected override void BindUI()
        {
            workerName = root.Q<Label>("worker-name");
            moduleTitle = root.Q<Label>("module-title");
            score = root.Q<Label>("score");
            certId = root.Q<Label>("cert-id");
            certIdMeta = root.Q<Label>("cert-id-meta");
            issuedDate = root.Q<Label>("issued-date");
            expiryDate = root.Q<Label>("expiry-date");
            organization = root.Q<Label>("organization");
            backBtn = root.Q<Button>("back-btn");
            downloadBtn = root.Q<Button>("download-btn");
            shareBtn = root.Q<Button>("share-btn");
            verifyBtn = root.Q<Button>("verify-btn");

            backBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.GoBack());
            downloadBtn.RegisterCallback<ClickEvent>(evt =>
                Debug.Log("Download certificate - not implemented"));
            shareBtn.RegisterCallback<ClickEvent>(evt =>
                Debug.Log("Share certificate - not implemented"));
            verifyBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.NavigateTo(
                    "qr_verify"));
        }

        public override void SetNavigationParameter(object param)
        {
            moduleId = param as string;
        }

        public override void OnPageEnter()
        {
            Refresh();
        }

        private void Refresh()
        {
            var worker = AppDataService.Instance.CurrentWorker;
            var mod = AppDataService.Instance.GetModule(moduleId);

            if (worker == null || mod == null) return;

            workerName.text = worker.name;
            moduleTitle.text = mod.title ?? moduleId;
            score.text = $"{mod.bestScore}%";

            // Generate certificate ID
            string certIdStr =
                $"JH-{moduleId.ToUpper().Substring(0,
                    Mathf.Min(4, moduleId.Length))}" +
                $"-{UnityEngine.Random.Range(100000, 999999)}";

            certId.text = certIdStr;
            certIdMeta.text = certIdStr;
            issuedDate.text = mod.lastAttempt ?? "2026-08-26";
            expiryDate.text = "2027-08-26";
            organization.text = worker.organization;
        }
    }
}
```

---

## 3. QRVerifyPage

**File:** `Assets/Scripts/UI/Pages/QRVerifyPageController.cs`
**Template:** `Assets/UI/Templates/Pages/QRVerifyPage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <VisualElement class="header">
        <Button class="header__back-btn" name="back-btn">
            <Label text="←" style="font-size: 20px;" />
        </Button>
        <Label class="header__title"
               text="Verify Certificate" />
        <VisualElement class="header__right-action" />
    </VisualElement>

    <ScrollView class="scroll-area">
        <!-- Search Section -->
        <VisualElement class="card" name="search-card">
            <Label text="🔍" style="font-size: 28px;
                                   text-align: center;" />
            <Label class="verify__title"
                   text="Certificate Verification"
                   style="text-align: center;
                           font-size: 16px;
                           font-weight: 700;
                           margin-top: 12px;" />
            <Label class="verify__desc"
                   text="Enter your certificate ID to verify"
                   style="text-align: center;
                           color: var(--text-muted);
                           font-size: 13px;
                           margin-top: 4px;" />

            <TextField class="input-field" name="cert-input"
                       placeholder="e.g. JH-FIRE-001928"
                       style="margin-top: 16px;" />
            <Button class="btn-primary" name="verify-btn"
                    text="VERIFY" style="margin-top: 12px;" />

            <!-- Quick Fill Buttons -->
            <VisualElement class="quick-fill" name="quick-fill">
                <Button class="quick-fill-btn" name="qf-1"
                        text="JH-FIRE-001928" />
                <Button class="quick-fill-btn" name="qf-2"
                        text="JH-GAS-002156" />
                <Button class="quick-fill-btn" name="qf-3"
                        text="INVALID-000" />
            </VisualElement>
        </VisualElement>

        <!-- Loading State -->
        <VisualElement class="card" name="loading-card"
                       style="display: none; margin-top: 16px;
                              align-items: center;">
            <VisualElement class="spinner" />
            <Label text="Checking certificate..."
                   style="margin-top: 12px;
                           color: var(--text-muted);" />
        </VisualElement>

        <!-- Result Card -->
        <VisualElement class="card" name="result-card"
                       style="display: none; margin-top: 16px;">
            <Label class="verify-result__badge"
                   name="verify-badge" text="" />
            <VisualElement class="verify-result__details"
                           name="verify-details" />
            <Button class="btn-primary" name="view-cert-btn"
                    text="VIEW CERTIFICATE"
                    style="display: none; margin-top: 16px;" />
        </VisualElement>
    </ScrollView>
</VisualElement>
```

### C# Controller
```csharp
namespace MiningSafetyAR.UI.Pages
{
    public class QRVerifyPageController : PageController
    {
        private TextField certInput;
        private Button verifyBtn, backBtn, viewCertBtn;
        private Button qf1, qf2, qf3;
        private VisualElement loadingCard, resultCard;
        private VisualElement verifyDetails;
        private Label verifyBadge;

        protected override void BindUI()
        {
            certInput = root.Q<TextField>("cert-input");
            verifyBtn = root.Q<Button>("verify-btn");
            backBtn = root.Q<Button>("back-btn");
            viewCertBtn = root.Q<Button>("view-cert-btn");
            qf1 = root.Q<Button>("qf-1");
            qf2 = root.Q<Button>("qf-2");
            qf3 = root.Q<Button>("qf-3");
            loadingCard = root.Q("loading-card");
            resultCard = root.Q("result-card");
            verifyDetails = root.Q("verify-details");
            verifyBadge = root.Q<Label>("verify-badge");

            verifyBtn.RegisterCallback<ClickEvent>(evt =>
                OnVerify());
            backBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.GoBack());
            qf1.RegisterCallback<ClickEvent>(evt =>
                certInput.value = "JH-FIRE-001928");
            qf2.RegisterCallback<ClickEvent>(evt =>
                certInput.value = "JH-GAS-002156");
            qf3.RegisterCallback<ClickEvent>(evt =>
                certInput.value = "INVALID-000");
            viewCertBtn.RegisterCallback<ClickEvent>(evt =>
                OnViewCertificate());
        }

        private async void OnVerify()
        {
            string certId = certInput.value;
            if (string.IsNullOrEmpty(certId)) return;

            // Show loading
            loadingCard.style.display = DisplayStyle.Flex;
            resultCard.style.display = DisplayStyle.None;

            // Simulate delay
            await Task.Delay(1000);

            // Search
            var cert = AppDataService.Instance
                .GetCertificate(certId);

            loadingCard.style.display = DisplayStyle.None;
            resultCard.style.display = DisplayStyle.Flex;

            if (cert != null)
            {
                // Valid
                verifyBadge.text = "CERTIFICATE VALID";
                verifyBadge.AddToClassList("badge--pass");

                verifyDetails.Clear();
                AddDetail("Worker", cert.workerName);
                AddDetail("Worker ID", cert.workerId);
                AddDetail("Module", cert.moduleTitle);
                AddDetail("Score", $"{cert.score}%");
                AddDetail("Issued", cert.issuedDate);
                AddDetail("Expires", cert.expiryDate);
                AddDetail("Status", cert.status);

                viewCertBtn.style.display = DisplayStyle.Flex;
            }
            else
            {
                // Invalid
                verifyBadge.text = "INVALID CERTIFICATE";
                verifyBadge.AddToClassList("badge--fail");

                verifyDetails.Clear();
                var msg = new Label(
                    $"No certificate found with ID: {certId}");
                msg.style.color = new StyleColor(
                    HexToColor("#B0B0B0"));
                msg.style.textAlign = TextAnchor.Center;
                verifyDetails.Add(msg);

                viewCertBtn.style.display = DisplayStyle.None;
            }
        }

        private void AddDetail(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = Row.Row;
            row.style.marginBottom = 8;

            var l = new Label(label);
            l.style.flex = 1;
            l.style.color = new StyleColor(
                HexToColor("#707070"));
            l.style.fontSize = 13;

            var v = new Label(value);
            v.style.color = new StyleColor(
                HexToColor("#FFFFFF"));
            v.style.fontSize = 13;
            v.style.fontWeight = FontWeight.Bold;

            row.Add(l);
            row.Add(v);
            verifyDetails.Add(row);
        }

        private void OnViewCertificate()
        {
            // Navigate to certificate with module ID
            NavigationManager.Instance.NavigateTo(
                "certificate", "fire_safety");
        }
    }
}
```
