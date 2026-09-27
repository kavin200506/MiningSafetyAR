using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MiningSafetyAR.Data;
using MiningSafetyAR.AR;

namespace MiningSafetyAR.Modules
{
    public class GasLeakModuleManager : BaseModuleManager
    {
        public static GasLeakModuleManager Instance { get; private set; }

        [Header("Step Audio")]
        [SerializeField] private AudioClip step1AudioEN;
        [SerializeField] private AudioClip step1AudioHI;
        [SerializeField] private AudioClip step1AudioSAT;

        [SerializeField] private AudioClip step2AudioEN;
        [SerializeField] private AudioClip step2AudioHI;
        [SerializeField] private AudioClip step2AudioSAT;

        [SerializeField] private AudioClip step3AudioEN;
        [SerializeField] private AudioClip step3AudioHI;
        [SerializeField] private AudioClip step3AudioSAT;

        [SerializeField] private AudioClip step4AudioEN;
        [SerializeField] private AudioClip step4AudioHI;
        [SerializeField] private AudioClip step4AudioSAT;

        [Header("Scoring")]
        [SerializeField] private int pointsPerStep = 100;

        [Header("Gas Detector Response")]
        [Tooltip("Maximum time (seconds) after alarm to check detector for full credit.")]
        [SerializeField] private float firstCheckTimeBudget = 15f;
        [Tooltip("Bonus for checking detector quickly after alarm.")]
        [SerializeField] private int firstCheckBonus = 15;
        [Tooltip("Penalty if detector never checked.")]
        [SerializeField] private int firstCheckPenalty = 15;

        [Header("Second Gas Check (Re-assessment)")]
        [Tooltip("Bonus for re-checking gas levels before exit.")]
        [SerializeField] private int secondCheckBonus = 10;

        [Header("PPE Selection (Mask Donning)")]
        [Tooltip("Bonus for donning SCSR mask.")]
        [SerializeField] private int maskDonBonus = 15;

        [Header("Evacuation")]
        [Tooltip("Target total drill time for Time competency.")]
        [SerializeField] private float parTimeSeconds = 90f;
        [Tooltip("Points lost per second over par time.")]
        [SerializeField] private float timeScorePointsLostPerSecondOver = 1.5f;

        [Header("Competency Weights")]
        [Tooltip("Penalty per mistake on Hazard Recognition.")]
        [SerializeField] private int hazardRecognitionPenaltyPerMistake = 20;

        public enum MistakeSeverity
        {
            Standard = ScoringConstants.GenericMistakePenalty,        // 25
            Critical = ScoringConstants.ProximityBreachPenalty        // 50
        }

        private const int FirstCheckStepIndex = 0;      // Respond to alarm, check detector
        private const int SecondCheckStepIndex = 1;     // Re-check gas levels en route
        private const int MaskDonStepIndex = 2;         // Don SCSR mask
        private const int EvacuationStepIndex = 3;      // Reach emergency exit

        private static readonly string[] StepNames =
        {
            "Initial Detector Check", "Gas Re-assessment", "Don SCSR Mask", "Emergency Evacuation"
        };

        private List<StepMetric> stepMetrics = new List<StepMetric>();
        private float[] stepStartTimes;
        private int[] stepErrorCounts;
        private int[] stepPenaltyPoints;
        private int?[] stepScoreOverride;

        private bool alarmTriggered = false;
        private float firstCheckTime = -1f;
        private bool firstCheckInTime = false;
        private bool secondCheckDone = false;
        private bool maskDonned = false;
        private float exitTime = -1f;

        private DrillResultPayload lastDrillResult;
        public DrillResultPayload LastDrillResult => lastDrillResult;

        public new event Action<int, string> OnStepChanged;
        public new event Action<string> OnMistakeMade;
        public event Action<List<StepMetric>> OnModuleCompletedWithMetrics;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            moduleType = ModuleType.GasLeakAndConfinedSpace;
            moduleName = "Gas Leak Emergency Response";
            totalSteps = 4;

            Debug.Log("[GAS_SCORING] [GasLeakModuleManager] Awake — Instance set, totalSteps=4.");

            // Same fix FireSafetyModuleManager already has (see its own Awake()) — Unity doesn't
            // guarantee Awake() order between GameObjects, so ARSimulationPageController's own
            // SubscribeToEvents() attempt may have already run and found Instance still null here.
            MiningSafetyAR.UI.Pages.ARSimulationPageController.Instance?.NotifyGasLeakModuleManagerReady();
        }

        private void OnEnable()
        {
            Debug.Log("[GAS_SCORING] [GasLeakModuleManager] OnEnable");
        }

        private void Start()
        {
            // Auto-start rather than waiting for ARSimulationPageController's "Start Mission"
            // button tap (OnStartMissionClicked) to call StartModule() — that manual step was
            // being missed in practice, leaving isModuleActive permanently false and silently
            // no-opping every Notify*() call including the exit's FinishModule(), regardless of
            // what the worker actually did. The intended flow has no separate "tap to begin"
            // gesture: the drill begins the moment the worker spawns in the mine, right as the
            // 7s alarm countdown starts. Calling StartModule() again later (if the button IS
            // tapped) is harmless — same as FireSafetyModuleManager.RetryModule(), it just resets
            // and restarts the drill.
            Debug.Log("[GAS_SCORING] [GasLeakModuleManager] Start() — auto-starting drill.");
            StartModule();
        }

        private void OnDisable()
        {
        }

        public override void StartModule()
        {
            Debug.Log("[GAS_SCORING] [GasLeakModuleManager] StartModule() called — drill starting.");
            stepMetrics.Clear();
            stepStartTimes = new float[totalSteps];
            stepErrorCounts = new int[totalSteps];
            stepPenaltyPoints = new int[totalSteps];
            stepScoreOverride = new int?[totalSteps];

            alarmTriggered = false;
            firstCheckTime = -1f;
            firstCheckInTime = false;
            secondCheckDone = false;
            maskDonned = false;
            exitTime = -1f;
            lastDrillResult = null;

            base.StartModule();

            stepStartTimes[0] = Time.time;
            OnStepChanged?.Invoke(0, GetStepInstruction(0));
        }

        protected override void OnStepStart(int stepIndex)
        {
            if (stepIndex < totalSteps)
            {
                stepStartTimes[stepIndex] = Time.time;
            }

            Debug.Log($"[GAS_SCORING] [GasLeakModuleManager] OnStepStart({stepIndex}) — \"{StepNames[stepIndex]}\"");

            switch (stepIndex)
            {
                case FirstCheckStepIndex:
                    PlayStepAudio(step1AudioEN, step1AudioHI, step1AudioSAT);
                    alarmTriggered = true;
                    break;
                case SecondCheckStepIndex:
                    PlayStepAudio(step2AudioEN, step2AudioHI, step2AudioSAT);
                    break;
                case MaskDonStepIndex:
                    PlayStepAudio(step3AudioEN, step3AudioHI, step3AudioSAT);
                    break;
                case EvacuationStepIndex:
                    PlayStepAudio(step4AudioEN, step4AudioHI, step4AudioSAT);
                    break;
            }
        }

        public override void CompleteCurrentStep()
        {
            if (!isModuleActive) return;
            RecordStepMetric(currentStepIndex);
            base.CompleteCurrentStep();
        }

        /// <summary>Called by GasDetectorAlarmTrigger when alarm starts.</summary>
        public void NotifyAlarmTriggered()
        {
            alarmTriggered = true;
            Debug.Log("[GAS_SCORING] [GasLeakModuleManager] Alarm triggered — first check timer started.");
        }

        /// <summary>Called when worker checks detector (first time after alarm).</summary>
        public void NotifyFirstDetectorCheck()
        {
            if (!alarmTriggered) return;

            firstCheckTime = Time.time;
            float responseTime = firstCheckTime - startTime;

            if (responseTime <= firstCheckTimeBudget)
            {
                firstCheckInTime = true;
                Debug.Log($"[GAS_SCORING] First detector check in time: {responseTime:F1}s (budget: {firstCheckTimeBudget}s)");
            }
            else
            {
                Debug.Log($"[GAS_SCORING] First detector check late: {responseTime:F1}s (budget: {firstCheckTimeBudget}s)");
            }

            // Auto-complete first step when detector is checked
            if (currentStepIndex == FirstCheckStepIndex && isModuleActive)
            {
                CompleteCurrentStep();
            }
        }

        /// <summary>
        /// Called when worker re-checks detector en route (second check). Sets the scoring flag
        /// unconditionally (not gated on currentStepIndex) — in a real mine, doing this "out of
        /// order" (e.g. after skipping ahead) should still count toward the score. Only the UI
        /// step-banner advance is conditional, since that's purely cosmetic.
        /// </summary>
        public void NotifySecondDetectorCheck()
        {
            if (!isModuleActive || secondCheckDone) return;

            secondCheckDone = true;
            Debug.Log("[GAS_SCORING] Second detector check completed.");

            if (currentStepIndex == SecondCheckStepIndex) CompleteCurrentStep();
        }

        /// <summary>
        /// Called when worker dons SCSR mask (via BeltItemGrabController). Same reasoning as
        /// NotifySecondDetectorCheck — the scoring flag is unconditional.
        /// </summary>
        public void NotifyMaskDonned()
        {
            if (!isModuleActive || maskDonned) return;

            maskDonned = true;
            Debug.Log("[GAS_SCORING] SCSR mask donned.");

            if (currentStepIndex == MaskDonStepIndex) CompleteCurrentStep();
        }

        /// <summary>
        /// Called by MineExitTrigger when worker reaches exit. Reaching the exit ALWAYS ends the
        /// drill — exactly like a real mine, nothing physically stops a worker from walking out
        /// regardless of which safety checks they did or skipped. Skipping a check only costs
        /// points via the competency formulas below (ComputeHazardRecognitionScore /
        /// ComputePpeSelectionScore), it never blocks this. Any step the worker never reached gets
        /// force-recorded here (RecordStepMetric treats a never-started step as a 0, see below) so
        /// stepMetrics always has exactly totalSteps entries in order, which every Compute*Score()
        /// formula assumes.
        /// </summary>
        public void NotifyExitReached()
        {
            if (!isModuleActive) return;

            exitTime = Time.time;

            // Reaching the exit IS the evacuation step completing — make sure RecordStepMetric
            // always treats it as "reached" even if the worker skipped straight here without any
            // earlier step ever formally starting (OnStepStart(EvacuationStepIndex) may never have
            // run naturally in that case, which would otherwise score it 0 despite them having just
            // evacuated).
            if (stepStartTimes[EvacuationStepIndex] <= 0f) stepStartTimes[EvacuationStepIndex] = Time.time;

            while (currentStepIndex < totalSteps)
            {
                RecordStepMetric(currentStepIndex);
                currentStepIndex++;
            }

            FinishModule();
        }

        public override void RegisterMistake(string feedbackMessage) => RegisterMistake(feedbackMessage, MistakeSeverity.Standard);

        public void RegisterMistake(string feedbackMessage, MistakeSeverity severity)
        {
            Debug.Log($"[GAS_SCORING] RegisterMistake(\"{feedbackMessage}\", {severity}) — step={currentStepIndex}");
            if (!isModuleActive) return;

            if (currentStepIndex < stepErrorCounts.Length)
            {
                stepErrorCounts[currentStepIndex]++;
                stepPenaltyPoints[currentStepIndex] += (int)severity;
            }

            base.RegisterMistake(feedbackMessage);
            OnMistakeMade?.Invoke(feedbackMessage);
        }

        protected override void FinishModule()
        {
            isModuleActive = false;
            float timeTaken = Time.time - startTime;

            int drillScore = GetTotalScore();
            int drillMaxScore = GetMaxPossibleScore();
            float drillPercentage = drillMaxScore > 0 ? (float)drillScore / drillMaxScore * 100f : 0f;

            // Compute competency scores
            int hazardRecognitionPct = ComputeHazardRecognitionScore();
            int ppeSelectionPct = ComputePpeSelectionScore();  // Mask donning
            int timeManagementPct = ComputeTimeScore(timeTaken);
            int evacuationPct = ComputeEvacuationScore();

            // The real, unified save (local JSON cache + offline-queue-aware Firestore push) happens
            // once, after the quiz, in AdaptiveQuizPageController via AppDataService.SaveAttempt +
            // UpdateModuleCompetencyScoresFromDrill — same pattern FireSafetyModuleManager uses.
            // A direct write here used to fire early with quizScorePct hardcoded to 0 and against
            // the parent "gas_safety" id instead of the real progressModuleId (e.g. "gas_safety_sub1"),
            // writing a stray/duplicate progress doc instead of the one actually read later.

            lastDrillResult = new DrillResultPayload
            {
                drillScorePercentage = drillPercentage,
                mistakesCount = mistakesCount,
                completionTimeSeconds = timeTaken,
                stepMetrics = new List<StepMetric>(stepMetrics),
                hazardRecognitionPct = hazardRecognitionPct,
                extinguisherUsePct = ppeSelectionPct,
                timeManagementPct = timeManagementPct,
                evacuationPct = evacuationPct
            };

            Debug.Log($"[GAS_SCORING] FinishModule() — Drill Score: {drillScore}/{drillMaxScore} ({drillPercentage:F1}%)");
            Debug.Log($"[GAS_SCORING] Competencies — Hazard: {hazardRecognitionPct}, PPE: {ppeSelectionPct}, Time: {timeManagementPct}, Evacuation: {evacuationPct}");

            OnModuleCompletedWithMetrics?.Invoke(new List<StepMetric>(stepMetrics));
        }

        private void RecordStepMetric(int stepIndex)
        {
            if (stepIndex >= totalSteps) return;

            // A step the worker exited before ever reaching (OnStepStart never ran for it, so it
            // has no start time) always scores 0 here — it never blocks the exit (NotifyExitReached
            // force-records every remaining step so stepMetrics always has totalSteps entries), it
            // just doesn't get credit. A step that WAS reached uses the normal formula below, same
            // as before.
            bool wasStarted = stepIndex < stepStartTimes.Length && stepStartTimes[stepIndex] > 0f;
            float duration = wasStarted ? Time.time - stepStartTimes[stepIndex] : 0f;
            int errors = stepIndex < stepErrorCounts.Length ? stepErrorCounts[stepIndex] : 0;
            int penalty = stepIndex < stepPenaltyPoints.Length ? stepPenaltyPoints[stepIndex] : 0;

            int stepScore;
            if (stepIndex < stepScoreOverride.Length && stepScoreOverride[stepIndex].HasValue)
                stepScore = stepScoreOverride[stepIndex].Value;
            else if (!wasStarted)
                stepScore = 0;
            else
                stepScore = Mathf.Clamp(pointsPerStep - penalty, 0, pointsPerStep);

            StepMetric metric = new StepMetric
            {
                stepName = StepNames[stepIndex],
                errorCount = errors,
                durationSeconds = duration,
                score = stepScore
            };
            stepMetrics.Add(metric);
        }

        public List<StepMetric> GetStepMetrics() => new List<StepMetric>(stepMetrics);
        public int GetTotalScore() => stepMetrics.Sum(m => m.score);
        public float GetTotalDuration() => stepMetrics.Sum(m => m.durationSeconds);
        public int GetMaxPossibleScore() => totalSteps * pointsPerStep;

        public override string GetStepInstruction(int stepIndex)
        {
            switch (stepIndex)
            {
                case FirstCheckStepIndex: return "Step 1: Gas detector alarm! Check your multi-gas detector immediately.";
                case SecondCheckStepIndex: return "Step 2: Move to fresh air zone and re-check gas levels.";
                case MaskDonStepIndex: return "Step 3: Don your SCSR mask from the belt.";
                case EvacuationStepIndex: return "Step 4: Emergency evacuation — reach the exit!";
                default: return "Module Complete!";
            }
        }

        private int ComputeHazardRecognitionScore()
        {
            int score = 100;

            if (firstCheckInTime) score += firstCheckBonus;
            else if (alarmTriggered) score -= firstCheckPenalty;

            if (secondCheckDone) score += secondCheckBonus;

            return Mathf.Clamp(score, 0, 100);
        }

        private int ComputePpeSelectionScore()
        {
            int score = 50; // Base for attempting

            if (maskDonned) score += maskDonBonus;
            else score = 0; // No mask = 0 PPE score

            return Mathf.Clamp(score, 0, 100);
        }

        private int ComputeEvacuationScore()
        {
            // Based on whether exit was reached (step completed)
            return stepMetrics.Count > EvacuationStepIndex && stepMetrics[EvacuationStepIndex].score > 0 ? 100 : 0;
        }

        private int ComputeTimeScore(float actualSeconds)
        {
            float overage = Mathf.Max(0f, actualSeconds - parTimeSeconds);
            return Mathf.Clamp(100 - Mathf.RoundToInt(overage * timeScorePointsLostPerSecondOver), 0, 100);
        }

        private void PlayStepAudio(AudioClip en, AudioClip hi, AudioClip sat)
        {
            if (Localization.LanguageManager.Instance != null)
            {
                Localization.LanguageManager.Instance.PlayVoiceover(en, hi, sat);
            }
        }
    }
}