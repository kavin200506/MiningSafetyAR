using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Helpers;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Modules;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.UI.Pages
{
    /// <summary>
    /// AR Simulation HUD overlay controller. Binds the full AR toolbar, 3-tier alert system,
    /// score history modal, tutorial flow, and elapsed timer. Runs the AR scene additively.
    /// </summary>
    public class ARSimulationPageController : PageController
    {
        private string moduleId;
        private int currentScore = 100;
        private float elapsedTime = 0f;
        private bool timerRunning = false;

        // ── Toolbar ──
        private Button btnExit;
        private Button btnMove;
        private Button btnMesh;
        private Button btnHint;
        private Label timerValue;
        private Button btnScoreDropdown;
        private Label scoreValue;

        // ── Tier 1: Instruction ──
        private Label instructionIcon;
        private Label instructionText;

        // ── Tier 2: Warning ──
        private VisualElement bannerWarning;
        private Label warningText;

        // ── Tier 3: Critical ──
        private VisualElement bannerCritical;
        private Label criticalTitle;
        private Label criticalText;
        private Button btnRetry;

        // ── Score History Modal ──
        private VisualElement modalScoreHistory;
        private VisualElement tableStepRows;
        private Label totalDuration;
        private Label totalScore;
        private Button btnCloseModal;

        // ── P.A.S.S. Action Buttons (Mobile) ──
        private VisualElement passButtonPanel;
        private Button btnPassGrab;
        private Button btnPassPin;
        private Button btnPassAim;
        private Button btnPassSqueeze;
        private VisualElement passStatus;
        private Label passStatusText;

        // ── Tutorial ──
        private VisualElement tutorialVignette;
        private VisualElement tutorialCallout;
        private Label tipLabel;
        private Label tipText;
        private Button btnCloseTip;
        private Button btnSkipTutorial;

        // ── Mission ──
        private VisualElement missionModal;
        private Label missionText;
        private Button btnStartMission;

        // ── Reticle Confirmation ──
        private VisualElement reticleConfirmCard;
        private Label reticleCheckmark;
        private Label reticleTitle;
        private Label reticleSubtitle;

        // ── State ──
        private bool repositionMode = false;
        private bool meshVisible = false;
        private bool modalOpen = false;
        private int currentTipIndex = 0;

        private readonly string[] tutorialTips = new string[]
        {
            "Need help to progress? Just tap this light bulb icon then follow the blue arrow.",
            "Point your camera at the floor to detect surfaces for placing the fire hazard.",
            "Walk 5-15 steps to discover the fire extinguisher on a wall or stand."
        };

        protected override void BindUI()
        {
            btnExit = root.Q<Button>("btn-exit");
            btnMove = root.Q<Button>("btn-move");
            btnMesh = root.Q<Button>("btn-mesh");
            btnHint = root.Q<Button>("btn-hint");
            timerValue = root.Q<Label>("timer-value");
            btnScoreDropdown = root.Q<Button>("btn-score-dropdown");
            scoreValue = root.Q<Label>("score-value");

            instructionIcon = root.Q<Label>("instruction-icon");
            instructionText = root.Q<Label>("instruction-text");

            bannerWarning = root.Q<VisualElement>("banner-warning");
            warningText = root.Q<Label>("warning-text");

            bannerCritical = root.Q<VisualElement>("banner-critical");
            criticalTitle = root.Q<Label>("critical-title");
            criticalText = root.Q<Label>("critical-text");
            btnRetry = root.Q<Button>("btn-retry");

            modalScoreHistory = root.Q<VisualElement>("modal-score-history");
            tableStepRows = root.Q<VisualElement>("table-step-rows");
            totalDuration = root.Q<Label>("total-duration");
            totalScore = root.Q<Label>("total-score");
            btnCloseModal = root.Q<Button>("btn-close-modal");

            tutorialVignette = root.Q<VisualElement>("tutorial-vignette");
            tutorialCallout = root.Q<VisualElement>("tutorial-callout");
            tipLabel = root.Q<Label>("tip-label");
            tipText = root.Q<Label>("tip-text");
            btnCloseTip = root.Q<Button>("btn-close-tip");
            btnSkipTutorial = root.Q<Button>("btn-skip-tutorial");

            missionModal = root.Q<VisualElement>("mission-modal");
            missionText = root.Q<Label>("mission-text");
            btnStartMission = root.Q<Button>("btn-start-mission");

            reticleConfirmCard = root.Q<VisualElement>("reticle-confirm-card");
            reticleCheckmark = root.Q<Label>("reticle-checkmark");
            reticleTitle = root.Q<Label>("reticle-title");
            reticleSubtitle = root.Q<Label>("reticle-subtitle");

            // P.A.S.S. buttons
            passButtonPanel = root.Q<VisualElement>("pass-button-panel");
            btnPassGrab = root.Q<Button>("btn-pass-grab");
            btnPassPin = root.Q<Button>("btn-pass-pin");
            btnPassAim = root.Q<Button>("btn-pass-aim");
            btnPassSqueeze = root.Q<Button>("btn-pass-squeeze");
            passStatus = root.Q<VisualElement>("pass-status");
            passStatusText = root.Q<Label>("pass-status-text");

            btnExit?.RegisterCallback<ClickEvent>(OnExitClicked);
            btnMove?.RegisterCallback<ClickEvent>(OnMoveClicked);
            btnMesh?.RegisterCallback<ClickEvent>(OnMeshClicked);
            btnHint?.RegisterCallback<ClickEvent>(OnHintClicked);
            btnScoreDropdown?.RegisterCallback<ClickEvent>(OnScoreDropdownClicked);
            btnRetry?.RegisterCallback<ClickEvent>(OnRetryClicked);
            btnCloseModal?.RegisterCallback<ClickEvent>(OnCloseModalClicked);
            btnCloseTip?.RegisterCallback<ClickEvent>(OnCloseTipClicked);
            btnSkipTutorial?.RegisterCallback<ClickEvent>(OnSkipTutorialClicked);
            btnStartMission?.RegisterCallback<ClickEvent>(OnStartMissionClicked);
            reticleConfirmCard?.RegisterCallback<ClickEvent>(OnReticleCardClicked);

            // P.A.S.S. button callbacks
            btnPassGrab?.RegisterCallback<ClickEvent>(OnPassGrabClicked);
            btnPassPin?.RegisterCallback<ClickEvent>(OnPassPinClicked);
            btnPassAim?.RegisterCallback<ClickEvent>(OnPassAimClicked);

            // SQUEEZE uses pointer down/up for hold-to-spray
            if (btnPassSqueeze != null)
            {
                btnPassSqueeze.RegisterCallback<PointerDownEvent>(OnPassSqueezePointerDown);
                btnPassSqueeze.RegisterCallback<PointerUpEvent>(OnPassSqueezePointerUp);
            }
        }

        private void OnReticleCardClicked(ClickEvent evt)
        {
            if (AR.ARPlacementManager.Instance != null)
            {
                AR.ARPlacementManager.Instance.ConfirmPlacementLock();
            }
            if (reticleConfirmCard != null)
            {
                reticleConfirmCard.style.display = DisplayStyle.None;
            }
        }

        public override void SetNavigationParameter(object param) => moduleId = param as string;

        public override void OnPageEnter()
        {
            currentScore = 100;
            elapsedTime = 0f;
            timerRunning = false;
            repositionMode = false;
            meshVisible = false;
            modalOpen = false;
            currentTipIndex = 0;

            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";

            if (scoreValue != null) scoreValue.text = currentScore.ToString();
            if (timerValue != null) timerValue.text = "00:00";
            HideAllBanners();
            HideModal();
            HideTutorial();
            HideMissionModal();
            HidePassButtons();

            SubscribeToEvents();

            ShowTutorial();
        }

        public override void OnPageExit()
        {
            StopAllCoroutines();
            timerRunning = false;
            UnsubscribeFromEvents();
        }

        // ═══════════════════════════════════════════════════════
        // Event Subscriptions
        // ═══════════════════════════════════════════════════════

        private void SubscribeToEvents()
        {
            if (FireSafetyModuleManager.Instance != null)
            {
                FireSafetyModuleManager.Instance.OnStepChanged += OnStepChanged;
                FireSafetyModuleManager.Instance.OnMistakeMade += OnMistakeMade;
                FireSafetyModuleManager.Instance.OnFailureEscalated += OnFailureEscalated;
                FireSafetyModuleManager.Instance.OnModuleCompletedWithMetrics += OnModuleCompleted;
            }

            if (AR.ARProximitySafetyValidator.Instance != null)
            {
                AR.ARProximitySafetyValidator.Instance.OnProximityBreached += OnProximityBreached;
                AR.ARProximitySafetyValidator.Instance.OnProximityResolved += OnProximityResolved;
            }

            if (AR.ARPlacementManager.Instance != null)
            {
                AR.ARPlacementManager.Instance.OnFireHazardConfirmed += OnFireHazardConfirmed;
            }

            if (AR.ARStepCounterTracker.Instance != null)
            {
                AR.ARStepCounterTracker.Instance.OnStepCountUpdated += OnStepCountUpdated;
                AR.ARStepCounterTracker.Instance.OnExtinguisherDiscovered += OnExtinguisherDiscovered;
            }

            // Subscribe to P.A.S.S. state changes
            if (AR.FireExtinguisherGrabController.Instance != null)
            {
                AR.FireExtinguisherGrabController.Instance.OnExtinguisherGrabbed += OnExtinguisherGrabbed;
                AR.FireExtinguisherGrabController.Instance.OnPinPulled += OnPinPulled;
                AR.FireExtinguisherGrabController.Instance.OnNozzleAimed += OnNozzleAimed;
                AR.FireExtinguisherGrabController.Instance.OnSprayStarted += OnSprayStarted;
                AR.FireExtinguisherGrabController.Instance.OnExtinguisherDepleted += OnExtinguisherDepleted;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (FireSafetyModuleManager.Instance != null)
            {
                FireSafetyModuleManager.Instance.OnStepChanged -= OnStepChanged;
                FireSafetyModuleManager.Instance.OnMistakeMade -= OnMistakeMade;
                FireSafetyModuleManager.Instance.OnFailureEscalated -= OnFailureEscalated;
                FireSafetyModuleManager.Instance.OnModuleCompletedWithMetrics -= OnModuleCompleted;
            }

            if (AR.ARProximitySafetyValidator.Instance != null)
            {
                AR.ARProximitySafetyValidator.Instance.OnProximityBreached -= OnProximityBreached;
                AR.ARProximitySafetyValidator.Instance.OnProximityResolved -= OnProximityResolved;
            }

            if (AR.ARPlacementManager.Instance != null)
            {
                AR.ARPlacementManager.Instance.OnFireHazardConfirmed -= OnFireHazardConfirmed;
            }

            if (AR.ARStepCounterTracker.Instance != null)
            {
                AR.ARStepCounterTracker.Instance.OnStepCountUpdated -= OnStepCountUpdated;
                AR.ARStepCounterTracker.Instance.OnExtinguisherDiscovered -= OnExtinguisherDiscovered;
            }

            // Unsubscribe from P.A.S.S. state changes
            if (AR.FireExtinguisherGrabController.Instance != null)
            {
                AR.FireExtinguisherGrabController.Instance.OnExtinguisherGrabbed -= OnExtinguisherGrabbed;
                AR.FireExtinguisherGrabController.Instance.OnPinPulled -= OnPinPulled;
                AR.FireExtinguisherGrabController.Instance.OnNozzleAimed -= OnNozzleAimed;
                AR.FireExtinguisherGrabController.Instance.OnSprayStarted -= OnSprayStarted;
                AR.FireExtinguisherGrabController.Instance.OnExtinguisherDepleted -= OnExtinguisherDepleted;
            }
        }

        // ═══════════════════════════════════════════════════════
        // Timer
        // ═══════════════════════════════════════════════════════

        private IEnumerator TimerCoroutine()
        {
            while (timerRunning)
            {
                elapsedTime += Time.deltaTime;
                UpdateTimerDisplay();
                yield return null;
            }
        }

        private bool timerCoroutineRunning = false;

        private void StartTimer()
        {
            if (timerCoroutineRunning) return;
            timerRunning = true;
            timerCoroutineRunning = true;
            StartCoroutine(TimerCoroutineWrapper());
        }

        private IEnumerator TimerCoroutineWrapper()
        {
            yield return TimerCoroutine();
            timerCoroutineRunning = false;
        }

        private void UpdateTimerDisplay()
        {
            if (timerValue == null) return;
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            timerValue.text = $"{minutes:00}:{seconds:00}";
        }

        // ═══════════════════════════════════════════════════════
        // Tier 1: Instruction
        // ═══════════════════════════════════════════════════════

        private void ShowTier1Info(string message)
        {
            if (instructionText != null)
            {
                instructionText.text = message;
            }
        }

        // ═══════════════════════════════════════════════════════
        // P.A.S.S. Buttons (Mobile)
        // ═══════════════════════════════════════════════════════

        private void ShowPassButtons()
        {
            if (passButtonPanel != null) passButtonPanel.style.display = DisplayStyle.Flex;
            UpdatePassButtonVisibility();
        }

        private void HidePassButtons()
        {
            if (passButtonPanel != null) passButtonPanel.style.display = DisplayStyle.None;
            if (passStatus != null) passStatus.style.display = DisplayStyle.None;
        }

        private void UpdatePassButtonVisibility()
        {
            var ext = AR.FireExtinguisherGrabController.Instance;
            if (ext == null) return;

            // Hide all first
            if (btnPassGrab != null) btnPassGrab.style.display = DisplayStyle.None;
            if (btnPassPin != null) btnPassPin.style.display = DisplayStyle.None;
            if (btnPassAim != null) btnPassAim.style.display = DisplayStyle.None;
            if (btnPassSqueeze != null) btnPassSqueeze.style.display = DisplayStyle.None;

            // Show appropriate button based on state
            if (!ext.IsGrabbed)
            {
                // Show Grab button
                if (btnPassGrab != null) btnPassGrab.style.display = DisplayStyle.Flex;
                UpdatePassStatus("Extinguisher available — tap GRAB to pick up");
            }
            else
            {
                switch (ext.CurrentPassState)
                {
                    case AR.FireExtinguisherGrabController.PassStepState.None:
                        if (btnPassPin != null) btnPassPin.style.display = DisplayStyle.Flex;
                        UpdatePassStatus("Tap PULL PIN to begin P.A.S.S.");
                        break;
                    case AR.FireExtinguisherGrabController.PassStepState.PinPulled:
                        if (btnPassAim != null) btnPassAim.style.display = DisplayStyle.Flex;
                        UpdatePassStatus("Pin pulled — tap AIM to aim nozzle");
                        break;
                    case AR.FireExtinguisherGrabController.PassStepState.NozzleAimed:
                    case AR.FireExtinguisherGrabController.PassStepState.HandleSqueezed:
                        if (btnPassSqueeze != null) btnPassSqueeze.style.display = DisplayStyle.Flex;
                        UpdatePassStatus("Aimed — tap SQUEEZE to spray foam");
                        break;
                    case AR.FireExtinguisherGrabController.PassStepState.SweepComplete:
                        UpdatePassStatus("Sweep complete — keep spraying!");
                        if (btnPassSqueeze != null) btnPassSqueeze.style.display = DisplayStyle.Flex;
                        break;
                }
            }
        }

        private void UpdatePassStatus(string message)
        {
            if (passStatus != null) passStatus.style.display = DisplayStyle.Flex;
            if (passStatusText != null) passStatusText.text = message;
        }

        // ═══════════════════════════════════════════════════════
        // P.A.S.S. Button Click Handlers
        // ═══════════════════════════════════════════════════════

        private void OnPassGrabClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-pass-grab", "Grabbed Extinguisher (Mobile)");
            if (AR.FireExtinguisherGrabController.Instance != null)
            {
                AR.FireExtinguisherGrabController.Instance.SimulateGrab();
            }
        }

        private void OnPassPinClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-pass-pin", "Pulled Pin (Mobile)");
            if (AR.FireExtinguisherGrabController.Instance != null)
            {
                AR.FireExtinguisherGrabController.Instance.PullPin();
            }
            UpdatePassButtonVisibility();
        }

        private void OnPassAimClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-pass-aim", "Aimed Nozzle (Mobile)");
            if (AR.FireExtinguisherGrabController.Instance != null)
            {
                AR.FireExtinguisherGrabController.Instance.AimNozzle();
            }
            UpdatePassButtonVisibility();
        }

        private void OnPassSqueezeClicked(ClickEvent evt)
        {
            // Legacy click handler - now handled by pointer down/up
            OnPassSqueezePointerDown(null);
        }

        private void OnPassSqueezePointerDown(PointerDownEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-pass-squeeze", "Squeezed Handle (Mobile) - START");
            if (AR.FireExtinguisherGrabController.Instance != null)
            {
                if (AR.FireExtinguisherGrabController.Instance.IsSqueezing) return; // debounce
                AR.FireExtinguisherGrabController.Instance.StartSqueezing();
            }
            // Visual feedback: darken the button while held
            if (btnPassSqueeze != null)
            {
                btnPassSqueeze.style.backgroundColor = new Color(0.8f, 0.2f, 0.15f, 0.95f);
            }
            // Don't call UpdatePassButtonVisibility here — it hides/shows buttons which disrupts pointer flow
        }

        private void OnPassSqueezePointerUp(PointerUpEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-pass-squeeze", "Squeezed Handle (Mobile) - STOP");
            if (AR.FireExtinguisherGrabController.Instance != null)
            {
                AR.FireExtinguisherGrabController.Instance.StopSqueezing();
            }
            // Restore button color
            if (btnPassSqueeze != null)
            {
                btnPassSqueeze.style.backgroundColor = new Color(244f / 255f, 67f / 255f, 54f / 255f, 0.9f);
            }
            // Don't call UpdatePassButtonVisibility here — it hides/shows buttons which disrupts pointer flow
        }

        // ═══════════════════════════════════════════════════════
        // P.A.S.S. Event Handlers
        // ═══════════════════════════════════════════════════════

        private void OnExtinguisherGrabbed(GameObject obj)
        {
            ShowPassButtons();
        }

        private void OnPinPulled()
        {
            UpdatePassButtonVisibility();
        }

        private void OnNozzleAimed()
        {
            UpdatePassButtonVisibility();
        }

        private void OnSprayStarted()
        {
            UpdatePassButtonVisibility();
        }

        private void OnExtinguisherDepleted()
        {
            HidePassButtons();
            ShowTier2Warning("Extinguisher depleted! Fire may reignite.");
        }

        // ═══════════════════════════════════════════════════════
        // Tier 2: Warning Banner
        // ═══════════════════════════════════════════════════════

        private void ShowTier2Warning(string message)
        {
            if (bannerWarning != null) bannerWarning.style.display = DisplayStyle.Flex;
            if (warningText != null) warningText.text = message;
            StartCoroutine(HideAfterDelay(bannerWarning, 4f));
        }

        // ═══════════════════════════════════════════════════════
        // Tier 3: Critical Alert
        // ═══════════════════════════════════════════════════════

        private void ShowTier3Critical(string title, string message)
        {
            if (bannerCritical != null) bannerCritical.style.display = DisplayStyle.Flex;
            if (criticalTitle != null) criticalTitle.text = title;
            if (criticalText != null) criticalText.text = message;
        }

        // ═══════════════════════════════════════════════════════
        // Hide All Banners
        // ═══════════════════════════════════════════════════════

        private void HideAllBanners()
        {
            if (bannerWarning != null) bannerWarning.style.display = DisplayStyle.None;
            if (bannerCritical != null) bannerCritical.style.display = DisplayStyle.None;
        }

        // ═══════════════════════════════════════════════════════
        // Score
        // ═══════════════════════════════════════════════════════

        private void UpdateScoreBadge(int newScore)
        {
            currentScore = Mathf.Max(0, newScore);
            if (scoreValue != null) scoreValue.text = currentScore.ToString();
        }

        // ═══════════════════════════════════════════════════════
        // Score History Modal
        // ═══════════════════════════════════════════════════════

        private void ShowScoreModal(List<StepMetric> metrics)
        {
            if (modalScoreHistory == null) return;
            modalOpen = true;
            modalScoreHistory.style.display = DisplayStyle.Flex;
            BuildScoreHistoryTable(metrics);
        }

        private void HideModal()
        {
            if (modalScoreHistory != null) modalScoreHistory.style.display = DisplayStyle.None;
            modalOpen = false;
        }

        private void BuildScoreHistoryTable(List<StepMetric> metrics)
        {
            if (tableStepRows == null) return;
            tableStepRows.Clear();

            foreach (var metric in metrics)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("table-row");

                Label stepName = new Label(metric.stepName);
                stepName.style.flexGrow = 1;
                stepName.style.fontSize = 13;
                stepName.style.color = Color.white;

                Label errors = new Label(metric.errorCount.ToString());
                errors.style.flexGrow = 1;
                errors.style.fontSize = 13;
                errors.style.color = metric.errorCount > 0 ? new Color(1f, 0.4f, 0.4f) : Color.white;

                int mins = Mathf.FloorToInt(metric.durationSeconds / 60f);
                int secs = Mathf.FloorToInt(metric.durationSeconds % 60f);
                Label duration = new Label($"{mins:00}:{secs:00}");
                duration.style.flexGrow = 1;
                duration.style.fontSize = 13;
                duration.style.color = Color.white;

                Label score = new Label($"{metric.score}/100");
                score.style.flexGrow = 1;
                score.style.fontSize = 13;
                score.style.color = metric.score >= 75 ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.6f, 0.2f);

                row.Add(stepName);
                row.Add(errors);
                row.Add(duration);
                row.Add(score);
                tableStepRows.Add(row);
            }

            float totalSecs = 0f;
            int totalPts = 0;
            foreach (var m in metrics) { totalSecs += m.durationSeconds; totalPts += m.score; }

            int tMins = Mathf.FloorToInt(totalSecs / 60f);
            int tSecs = Mathf.FloorToInt(totalSecs % 60f);
            if (totalDuration != null) totalDuration.text = $"TOTAL: {tMins:00}:{tSecs:00}";
            if (totalScore != null) totalScore.text = $"SCORE: {totalPts}/{metrics.Count * 100}";
        }

        // ═══════════════════════════════════════════════════════
        // Tutorial
        // ═══════════════════════════════════════════════════════

        private void ShowTutorial()
        {
            currentTipIndex = 0;
            if (tutorialVignette != null) tutorialVignette.style.display = DisplayStyle.Flex;
            ShowTip(currentTipIndex);
        }

        private void ShowTip(int index)
        {
            if (index >= tutorialTips.Length)
            {
                HideTutorial();
                ShowMissionBriefing();
                return;
            }
            if (tutorialCallout != null) tutorialCallout.style.display = DisplayStyle.Flex;
            if (tipLabel != null) tipLabel.text = $"TIP {index + 1}/{tutorialTips.Length}";
            if (tipText != null) tipText.text = tutorialTips[index];
        }

        private void HideTutorial()
        {
            if (tutorialVignette != null) tutorialVignette.style.display = DisplayStyle.None;
            if (tutorialCallout != null) tutorialCallout.style.display = DisplayStyle.None;
        }

        private void ShowMissionBriefing()
        {
            if (missionModal != null) missionModal.style.display = DisplayStyle.Flex;
        }

        private void HideMissionModal()
        {
            if (missionModal != null) missionModal.style.display = DisplayStyle.None;
        }

        // ═══════════════════════════════════════════════════════
        // Button Handlers
        // ═══════════════════════════════════════════════════════

        private void OnExitClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-exit", "Navigated Back");
            NavigationManager.Instance?.GoBack();
        }

        private void OnMoveClicked(ClickEvent evt)
        {
            repositionMode = !repositionMode;
            AR.ARSimulationLogger.LogButton("btn-move", $"Toggled Rescale Mode -> {repositionMode}");
            if (btnMove != null)
            {
                btnMove.style.backgroundColor = repositionMode
                    ? new Color(1f, 0.42f, 0f, 0.8f)
                    : new Color(1f, 1f, 1f, 0.2f);
            }
            AR.ARPlacementManager.Instance.RescaleMode = repositionMode;
            ShowTier1Info(repositionMode ? "Rescale mode: Tap an object to resize it." : "Rescale mode off.");
        }

        private void OnMeshClicked(ClickEvent evt)
        {
            meshVisible = !meshVisible;
            AR.ARSimulationLogger.LogButton("btn-mesh", $"Toggled Plane Grid Mesh -> {meshVisible}");
            if (btnMesh != null)
            {
                btnMesh.style.backgroundColor = meshVisible
                    ? new Color(1f, 0.42f, 0f, 0.8f)
                    : new Color(1f, 1f, 1f, 0.2f);
            }
            AR.ARPlacementManager.Instance?.SetPlanesVisible(meshVisible);
            ShowTier1Info(meshVisible ? "Plane mesh visible." : "Plane mesh hidden.");
        }

        private void OnHintClicked(ClickEvent evt)
        {
            if (FireSafetyModuleManager.Instance != null)
            {
                int step = FireSafetyModuleManager.Instance.CurrentStepIndex;
                string hint = FireSafetyModuleManager.Instance.GetStepInstruction(step);
                AR.ARSimulationLogger.LogButton("btn-hint", $"Requested Context Hint for Step {step}: '{hint}'");
                ShowTier1Info($"💡 {hint}");
            }
        }

        private void OnScoreDropdownClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-score-dropdown", $"Toggled Score History Modal -> {!modalOpen}");
            if (modalOpen)
            {
                HideModal();
                return;
            }

            if (FireSafetyModuleManager.Instance != null)
            {
                List<StepMetric> metrics = FireSafetyModuleManager.Instance.GetStepMetrics();
                ShowScoreModal(metrics);
            }
        }

        private void OnRetryClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-retry", "Triggered Module Retry");
            HideAllBanners();
            currentScore = 100;
            elapsedTime = 0f;
            if (scoreValue != null) scoreValue.text = currentScore.ToString();
            if (timerValue != null) timerValue.text = "00:00";
            StartTimer();

            if (FireSafetyModuleManager.Instance != null)
            {
                FireSafetyModuleManager.Instance.RetryModule();
            }
        }

        private void OnCloseModalClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-close-modal", "Closed Score History Modal");
            HideModal();
        }

        private void OnCloseTipClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-close-tip", $"Advanced Tutorial Tip -> Index {currentTipIndex + 1}");
            currentTipIndex++;
            ShowTip(currentTipIndex);
        }

        private void OnSkipTutorialClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-skip-tutorial", "Skipped Tutorial to Mission Briefing");
            HideTutorial();
            ShowMissionBriefing();
        }

        private void OnStartMissionClicked(ClickEvent evt)
        {
            AR.ARSimulationLogger.LogButton("btn-start-mission", "Started Mission Drill");
            HideMissionModal();
            StartTimer();

            if (FireSafetyModuleManager.Instance != null)
            {
                FireSafetyModuleManager.Instance.StartModule();
            }
        }

        // ═══════════════════════════════════════════════════════
        // Event Handlers
        // ═══════════════════════════════════════════════════════

        private void OnStepChanged(int step, string instruction)
        {
            ShowTier1Info(instruction);
            if (FireSafetyModuleManager.Instance != null)
            {
                UpdateScoreBadge(FireSafetyModuleManager.Instance.GetTotalScore());
            }
        }

        private void OnMistakeMade(string feedback)
        {
            ShowTier2Warning(feedback);
            if (FireSafetyModuleManager.Instance != null)
            {
                UpdateScoreBadge(FireSafetyModuleManager.Instance.GetTotalScore());
            }
        }

        private void OnProximityBreached(float distance)
        {
            ShowTier2Warning($"Step back! Don't stand within 3.5 ft. of the fire. ({distance:F1}m)");
        }

        private void OnProximityResolved()
        {
            if (bannerWarning != null) bannerWarning.style.display = DisplayStyle.None;
        }

        private void OnFireHazardConfirmed()
        {
            ShowReticleConfirm();
        }

        private void OnFailureEscalated(string reason)
        {
            timerRunning = false;
            ShowTier3Critical("DRILL FAILED", reason);
        }

        private void OnModuleCompleted(List<StepMetric> metrics)
        {
            timerRunning = false;
            ShowTier1Info("Module complete! Tap the score badge to view your results.");
            ShowScoreModal(metrics);
        }

        // ═══════════════════════════════════════════════════════
        // Step Counter & Extinguisher Discovery
        // ═══════════════════════════════════════════════════════

        private void OnStepCountUpdated(int currentSteps, int targetSteps)
        {
            int remaining = Mathf.Max(0, targetSteps - currentSteps);
            if (remaining > 0)
            {
                ShowTier1Info($"Walk {remaining} more steps to find the fire extinguisher!");
            }
            else
            {
                ShowTier1Info("Steps complete! Point camera at a wall and TAP to place extinguisher.");
            }
        }

        private void OnExtinguisherDiscovered(Vector3 position)
        {
            ShowTier1Info("Extinguisher found! Tap GRAB to pick it up!");
            ShowPassButtons();
        }

        // ═══════════════════════════════════════════════════════
        // Reticle Confirmation
        // ═══════════════════════════════════════════════════════

        private void ShowReticleConfirm()
        {
            if (reticleConfirmCard != null)
            {
                reticleConfirmCard.style.display = DisplayStyle.Flex;
                StartCoroutine(HideAfterDelay(reticleConfirmCard, 3f));
            }
        }

        // ═══════════════════════════════════════════════════════
        // Utility
        // ═══════════════════════════════════════════════════════

        private IEnumerator HideAfterDelay(VisualElement element, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (element != null) element.style.display = DisplayStyle.None;
        }

        [ContextMenu("Test Show Tier 2 Warning Banner")]
        public void TestShowTier2WarningBanner()
        {
            ShowTier2Warning("Step back! Don't stand within 3.5 ft. of the fire.");
        }

        [ContextMenu("Test Show Tier 3 Critical Alert")]
        public void TestShowTier3CriticalAlert()
        {
            ShowTier3Critical("⚡ ALERT!", "The extinguisher is empty and the fire is spreading! Now call the Fire Department.");
        }

        [ContextMenu("Test Show Score History Modal Table")]
        public void TestShowScoreHistoryModalTable()
        {
            List<StepMetric> mockMetrics = new List<StepMetric>
            {
                new StepMetric { stepName = "Sound Alarm", errorCount = 1, durationSeconds = 8f, score = 75 },
                new StepMetric { stepName = "Select Extinguisher", errorCount = 0, durationSeconds = 4f, score = 100 },
                new StepMetric { stepName = "Pull Pin", errorCount = 0, durationSeconds = 5f, score = 100 },
                new StepMetric { stepName = "Aim & Test Spray", errorCount = 0, durationSeconds = 6f, score = 100 },
                new StepMetric { stepName = "Squeeze & Sweep", errorCount = 0, durationSeconds = 25f, score = 100 }
            };
            ShowScoreModal(mockMetrics);
        }
    }
}
