using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MiningSafetyAR.Data;
using MiningSafetyAR.AR;

namespace MiningSafetyAR.Modules
{
    public class FireSafetyModuleManager : BaseModuleManager
    {
        public static FireSafetyModuleManager Instance { get; private set; }

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
        [SerializeField] private int pointsPerStep = 100; // real mistakes now cost points via MistakeSeverity (see RegisterMistake overload below), not a flat per-error constant

        [Header("Squeeze & Sweep Quality (see documents/technical_scoring_explained.md §3.5)")]
        [Tooltip("Minimum Squeeze & Sweep score even at zero measured sweep technique — distinguishes 'put it out standing still' from 'never put it out' rather than flooring both at zero.")]
        [SerializeField] private int sweepQualityFloor = 30;

        [Header("Evacuation (see documents/technical_scoring_explained.md §3.7)")]
        [Tooltip("Distance from the fire hazard's position the safe assembly point is placed at.")]
        [SerializeField] private float evacuationSafeDistance = 5.0f;
        [Tooltip("How close to the safe point counts as 'arrived'.")]
        [SerializeField] private float evacuationArrivalRadius = 1.0f;
        [Tooltip("Must remain within evacuationArrivalRadius this long before the step completes (prevents completing on a fast pass-through).")]
        [SerializeField] private float evacuationSustainDuration = 1.5f;
        [Tooltip("Par time before lateness starts costing points.")]
        [SerializeField] private float evacuationTimeBudgetSeconds = 15f;
        [Tooltip("Points lost per second over the evacuation time budget.")]
        [SerializeField] private float evacuationLatePenaltyPerSecond = 4f;
        [Tooltip("Minimum Evacuation score even if very late.")]
        [SerializeField] private int evacuationScoreFloor = 40;

        [Header("Competency Formulas (see documents/technical_scoring_explained.md §3.9)")]
        [Tooltip("Points off Hazard Recognition per proximity breach across the whole drill.")]
        [SerializeField] private int hazardRecognitionPenaltyPerBreach = 20;
        [Tooltip("Target total drill time for the Time competency.")]
        [SerializeField] private float parTimeSeconds = 40f;
        [Tooltip("Points lost per second over the par time.")]
        [SerializeField] private float timeScorePointsLostPerSecondOver = 2f;

        [Header("Alarm Response (optional, feeds Hazard Recognition — decided 2026-09-05)")]
        [Tooltip("Hazard Recognition bonus if the alarm was sounded BEFORE the extinguisher was grabbed (best case).")]
        [SerializeField] private int alarmBonusActivatedBeforeGrab = 15;
        [Tooltip("Smaller Hazard Recognition bonus if the alarm was sounded, but only AFTER the extinguisher was already grabbed.")]
        [SerializeField] private int alarmBonusActivatedAfterGrab = 5;
        [Tooltip("Hazard Recognition penalty if the alarm was never sounded at all during the drill.")]
        [SerializeField] private int alarmPenaltyNeverActivated = 15;

        public enum MistakeSeverity
        {
            Standard = ScoringConstants.GenericMistakePenalty,        // 25
            ProximityBreach = ScoringConstants.ProximityBreachPenalty // 50
        }

        private const int PullPinStepIndex = 0;
        private const int AimSprayStepIndex = 1;
        private const int SqueezeSweepStepIndex = 2;
        private const int EvacuationStepIndex = 3;

        private static readonly string[] StepNames =
        {
            "Pull Pin", "Aim & Test Spray", "Squeeze & Sweep", "Evacuate to Safe Distance"
        };

        private List<StepMetric> stepMetrics = new List<StepMetric>();
        private float[] stepStartTimes;
        private int[] stepErrorCounts;      // count, for the "Errors" column display
        private int[] stepPenaltyPoints;    // accumulated point loss, for the score formula
        private int?[] stepScoreOverride;   // set by a step's own completion logic when it isn't the generic formula
        private int proximityBreachCount;   // whole-drill count, feeds Hazard Recognition
        private bool alarmActivated;            // whether the alarm was ever sounded at all this drill
        private bool alarmActivatedBeforeGrab;  // whether it was sounded before the extinguisher was grabbed specifically
        private bool failureEscalated = false;

        // Evacuation runtime state
        private Vector3 evacuationTargetPosition;
        private Transform evacuationTargetAnchor;
        private float evacuationStepStartTime;
        private float evacuationSustainedSince = -1f;
        private bool isEvacuationActive = false;

        private DrillResultPayload lastDrillResult;
        public DrillResultPayload LastDrillResult => lastDrillResult;

        public new event Action<int, string> OnStepChanged;
        public new event Action<string> OnMistakeMade;
        public event Action<string> OnFailureEscalated;
        public event Action<List<StepMetric>> OnModuleCompletedWithMetrics;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            moduleType = ModuleType.FireAndExplosion;
            moduleName = "Fire & Explosion Response";
            totalSteps = 4;

            Debug.Log("[SCORING_DIAG] [FireSafetyModuleManager] Awake — Instance set, totalSteps=4.");

            // ARSimulationPageController's own OnEnable() runs before this Awake() in some load
            // orders, so its first subscription attempt can find this Instance still null and
            // silently give up (confirmed via [SCORING_DIAG] logs — 0 subscribers at FinishModule()
            // time). Push a ready notification the moment this instance actually exists, mirroring
            // the same fix already used for GroundFireController/FireExtinguisherGrabController.
            MiningSafetyAR.UI.Pages.ARSimulationPageController.Instance?.NotifyFireSafetyModuleManagerReady();
        }

        private void OnEnable()
        {
            Debug.Log("[SCORING_DIAG] [FireSafetyModuleManager] OnEnable — attempting event subscription now.");
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        /// <summary>
        /// FireExtinguisherGrabController and GroundFireController are spawned at runtime, well
        /// after this manager's own OnEnable() already ran — so their first subscription attempt
        /// there usually finds both Instances still null and silently gives up. Each controller
        /// calls this the moment it actually comes alive (from its own Awake()), so the real
        /// subscription happens at the right time instead of never. SubscribeToEvents() always
        /// unsubscribes first, so calling this repeatedly (both controllers spawn independently,
        /// and RetryModule() may run this whole flow again) never produces duplicate handlers.
        /// </summary>
        public void NotifyFireControllerReady()
        {
            Debug.Log("[SCORING_DIAG] [FireSafetyModuleManager] NotifyFireControllerReady() — re-subscribing.");
            SubscribeToEvents();
        }

        public void NotifyGrabControllerReady()
        {
            Debug.Log("[SCORING_DIAG] [FireSafetyModuleManager] NotifyGrabControllerReady() — re-subscribing.");
            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            // Always unsubscribe first so repeated calls (from NotifyFireControllerReady /
            // NotifyGrabControllerReady, or a fresh OnEnable) never stack duplicate handlers.
            UnsubscribeFromEvents();

            bool grabFound = FireExtinguisherGrabController.Instance != null;
            bool fireFound = GroundFireController.Instance != null;
            Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] SubscribeToEvents — FireExtinguisherGrabController.Instance {(grabFound ? "FOUND" : "NOT FOUND (null)")}, GroundFireController.Instance {(fireFound ? "FOUND" : "NOT FOUND (null)")}.");

            if (FireExtinguisherGrabController.Instance != null)
            {
                FireExtinguisherGrabController.Instance.OnPinPulled += HandlePinPulled;
                FireExtinguisherGrabController.Instance.OnSprayStarted += HandleSprayStarted;
                FireExtinguisherGrabController.Instance.OnExtinguisherDepleted += HandleExtinguisherDepleted;
                Debug.Log("[SCORING_DIAG] [FireSafetyModuleManager] Subscribed to OnPinPulled / OnSprayStarted / OnExtinguisherDepleted.");
            }

            if (GroundFireController.Instance != null)
            {
                GroundFireController.Instance.OnFireExtinguished += HandleFireExtinguished;
                Debug.Log("[SCORING_DIAG] [FireSafetyModuleManager] Subscribed to OnFireExtinguished.");
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (FireExtinguisherGrabController.Instance != null)
            {
                FireExtinguisherGrabController.Instance.OnPinPulled -= HandlePinPulled;
                FireExtinguisherGrabController.Instance.OnSprayStarted -= HandleSprayStarted;
                FireExtinguisherGrabController.Instance.OnExtinguisherDepleted -= HandleExtinguisherDepleted;
            }

            if (GroundFireController.Instance != null)
            {
                GroundFireController.Instance.OnFireExtinguished -= HandleFireExtinguished;
            }
        }

        public override void StartModule()
        {
            Debug.Log("[SCORING_DIAG] [FireSafetyModuleManager] StartModule() called — drill is starting.");
            stepMetrics.Clear();
            stepStartTimes = new float[totalSteps];
            stepErrorCounts = new int[totalSteps];
            stepPenaltyPoints = new int[totalSteps];
            stepScoreOverride = new int?[totalSteps];
            proximityBreachCount = 0;
            alarmActivated = false;
            alarmActivatedBeforeGrab = false;
            failureEscalated = false;
            isEvacuationActive = false;
            evacuationSustainedSince = -1f;
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

            Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] OnStepStart({stepIndex}) — \"{(stepIndex < StepNames.Length ? StepNames[stepIndex] : "Module Complete")}\"");

            switch (stepIndex)
            {
                case PullPinStepIndex:
                    PlayStepAudio(step1AudioEN, step1AudioHI, step1AudioSAT);
                    break;
                case AimSprayStepIndex:
                    PlayStepAudio(step2AudioEN, step2AudioHI, step2AudioSAT);
                    break;
                case SqueezeSweepStepIndex:
                    PlayStepAudio(step3AudioEN, step3AudioHI, step3AudioSAT);
                    break;
                case EvacuationStepIndex:
                    PlayStepAudio(step4AudioEN, step4AudioHI, step4AudioSAT);
                    BeginEvacuation();
                    break;
            }
        }

        public override void CompleteCurrentStep()
        {
            if (!isModuleActive || failureEscalated) return;

            RecordStepMetric(currentStepIndex);

            base.CompleteCurrentStep();
        }

        /// <summary>
        /// Called when the 3D emergency fire alarm button is activated — optional, and can happen
        /// at any point in the drill. Records not just THAT it happened but WHEN, relative to the
        /// extinguisher grab, since that timing is what Hazard Recognition scores on (see
        /// ComputeHazardRecognitionScore).
        /// </summary>
        public void NotifyAlarmActivated()
        {
            alarmActivated = true;
            bool grabbedAlready = FireExtinguisherGrabController.Instance != null && FireExtinguisherGrabController.Instance.IsGrabbed;

            if (!grabbedAlready)
            {
                alarmActivatedBeforeGrab = true;
                Debug.Log("[FireSafetyModuleManager] 🚨 Alarm activated BEFORE extinguisher grab — full Hazard Recognition credit earned.");
            }
            else
            {
                Debug.Log("[FireSafetyModuleManager] 🚨 Alarm activated AFTER extinguisher grab — partial Hazard Recognition credit earned.");
            }
        }

        /// <summary>Standard-severity mistake (−25). Kept for BaseModuleManager compatibility.</summary>
        public override void RegisterMistake(string feedbackMessage) => RegisterMistake(feedbackMessage, MistakeSeverity.Standard);

        /// <summary>
        /// Severity-weighted mistake. A proximity breach costs more than a routine step mistake —
        /// see documents/technical_scoring_explained.md §3.3.
        /// </summary>
        public void RegisterMistake(string feedbackMessage, MistakeSeverity severity)
        {
            Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] RegisterMistake(\"{feedbackMessage}\", {severity}) — isModuleActive={isModuleActive}, currentStepIndex={currentStepIndex}.");
            if (!isModuleActive || failureEscalated) return;

            if (currentStepIndex < stepErrorCounts.Length)
            {
                stepErrorCounts[currentStepIndex]++;
                stepPenaltyPoints[currentStepIndex] += (int)severity;
            }
            if (severity == MistakeSeverity.ProximityBreach) proximityBreachCount++;

            base.RegisterMistake(feedbackMessage);
            OnMistakeMade?.Invoke(feedbackMessage);
        }

        public void TriggerFailureEscalation(string reason)
        {
            if (!isModuleActive || failureEscalated) return;

            failureEscalated = true;
            isModuleActive = false;
            isEvacuationActive = false;

            Debug.LogWarning($"[FireSafetyModuleManager] FAILURE ESCALATION: {reason}");
            OnFailureEscalated?.Invoke(reason);
        }

        public void RetryModule()
        {
            failureEscalated = false;
            StartModule();
        }

        private void Update()
        {
            if (!isEvacuationActive) return;
            UpdateEvacuationCheck();
        }

        protected override void FinishModule()
        {
            isModuleActive = false;
            float timeTaken = Time.time - startTime;

            int drillScore = GetTotalScore();
            int drillMaxScore = GetMaxPossibleScore();
            float drillPercentage = drillMaxScore > 0 ? (float)drillScore / drillMaxScore * 100f : 0f;

            int hazardRecognitionPct = ComputeHazardRecognitionScore();
            int extinguisherUsePct = ComputeExtinguisherUseScore();
            int timeManagementPct = ComputeTimeScore(timeTaken);
            int evacuationPct = stepMetrics.Count > EvacuationStepIndex ? stepMetrics[EvacuationStepIndex].score : 0;

            // The real, unified save (local JSON cache + offline-queue-aware Firestore push) happens
            // once, after the quiz, in AssessmentPageController.ShowResults() via
            // AppDataService.SaveAttempt (see documents/technical_scoring_explained.md §3.8 and
            // §4.3).

            lastDrillResult = new DrillResultPayload
            {
                drillScorePercentage = drillPercentage,
                mistakesCount = mistakesCount,
                completionTimeSeconds = timeTaken,
                stepMetrics = new List<StepMetric>(stepMetrics),
                hazardRecognitionPct = hazardRecognitionPct,
                extinguisherUsePct = extinguisherUsePct,
                timeManagementPct = timeManagementPct,
                evacuationPct = evacuationPct
            };

            Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] FinishModule() — Drill finished. Steps: {stepMetrics.Count}, Drill Score: {drillScore}/{drillMaxScore} ({drillPercentage:F1}%).");

            int subscriberCount = OnModuleCompletedWithMetrics?.GetInvocationList().Length ?? 0;
            Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] FinishModule() — OnModuleCompletedWithMetrics has {subscriberCount} subscriber(s) — this.InstanceID={GetInstanceID()}.");
            OnModuleCompletedWithMetrics?.Invoke(new List<StepMetric>(stepMetrics));
        }

        private void RecordStepMetric(int stepIndex)
        {
            if (stepIndex >= totalSteps) return;
            if (stepIndex < stepStartTimes.Length && stepStartTimes[stepIndex] > 0f)
            {
                float duration = Time.time - stepStartTimes[stepIndex];
                int errors = stepIndex < stepErrorCounts.Length ? stepErrorCounts[stepIndex] : 0;
                int penalty = stepIndex < stepPenaltyPoints.Length ? stepPenaltyPoints[stepIndex] : 0;

                int stepScore = (stepIndex < stepScoreOverride.Length && stepScoreOverride[stepIndex].HasValue)
                    ? stepScoreOverride[stepIndex].Value
                    : Mathf.Clamp(pointsPerStep - penalty, 0, pointsPerStep);

                StepMetric metric = new StepMetric
                {
                    stepName = StepNames[stepIndex],
                    errorCount = errors,
                    durationSeconds = duration,
                    score = stepScore
                };
                stepMetrics.Add(metric);
            }
        }

        public List<StepMetric> GetStepMetrics() => new List<StepMetric>(stepMetrics);
        public int GetTotalScore() => stepMetrics.Sum(m => m.score);
        public float GetTotalDuration() => stepMetrics.Sum(m => m.durationSeconds);
        public int GetMaxPossibleScore() => totalSteps * pointsPerStep;

        public override string GetStepInstruction(int stepIndex)
        {
            switch (stepIndex)
            {
                case PullPinStepIndex: return "Pull the safety pin on the extinguisher handle.";
                case AimSprayStepIndex: return "Aim the nozzle at the base of the fire and test spray.";
                case SqueezeSweepStepIndex: return "Squeeze the handle and sweep side-to-side across the base.";
                case EvacuationStepIndex: return "Fire's out! Move to a safe distance to complete evacuation.";
                default: return "Module Complete!";
            }
        }

        private void HandlePinPulled()
        {
            Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] HandlePinPulled() received — isModuleActive={isModuleActive}, currentStepIndex={currentStepIndex}.");
            if (isModuleActive && currentStepIndex == PullPinStepIndex)
            {
                CompleteCurrentStep();
            }
        }

        private void HandleSprayStarted()
        {
            Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] HandleSprayStarted() received — isModuleActive={isModuleActive}, currentStepIndex={currentStepIndex}.");
            if (!isModuleActive) return;

            // Narrow safety net: catch up Pull Pin if the spray-start event somehow landed before
            // the pin-pull event was processed. Does not touch any other step.
            if (currentStepIndex == PullPinStepIndex) CompleteCurrentStep();
            if (isModuleActive && currentStepIndex == AimSprayStepIndex) CompleteCurrentStep();
        }

        private void HandleFireExtinguished()
        {
            Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] HandleFireExtinguished() received — isModuleActive={isModuleActive}, currentStepIndex={currentStepIndex}. Starting evacuation.");

            // The alarm only ever turns off automatically, right when the fire is fully out —
            // never by tapping it again. Do this unconditionally (even if the module already
            // isn't active) so the alert doesn't linger visually.
            AlarmButtonInteractable.Instance?.ForceAlarmOff();

            if (!isModuleActive) return;

            // Safety net: complete anything not already marked done (shouldn't normally be needed).
            while (isModuleActive && currentStepIndex < SqueezeSweepStepIndex)
            {
                CompleteCurrentStep();
            }
            if (!isModuleActive || currentStepIndex != SqueezeSweepStepIndex) return;

            float avgIntensity = FireExtinguisherGrabController.Instance != null
                ? FireExtinguisherGrabController.Instance.AverageSweepIntensity : 0f;
            int qualityScore = Mathf.RoundToInt(pointsPerStep * avgIntensity);
            int penalty = stepPenaltyPoints[SqueezeSweepStepIndex];
            stepScoreOverride[SqueezeSweepStepIndex] = Mathf.Clamp(qualityScore - penalty, sweepQualityFloor, pointsPerStep);

            CompleteCurrentStep(); // records Squeeze & Sweep, advances into Evacuation via OnStepStart
        }

        private void HandleExtinguisherDepleted()
        {
            TriggerFailureEscalation("Extinguisher foam depleted before fire was extinguished!");
        }

        private void BeginEvacuation()
        {
            Vector3 firePos = ARPlacementManager.Instance != null && ARPlacementManager.Instance.FireHazardTransform != null
                ? ARPlacementManager.Instance.FireHazardTransform.position
                : transform.position;

            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            Vector3 retreatDir = cam != null ? (cam.transform.position - firePos) : Vector3.forward;
            retreatDir.y = 0f;
            if (retreatDir.sqrMagnitude < 0.01f)
            {
                // Degenerate case: player standing essentially on top of the fire's position.
                retreatDir = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
            }
            retreatDir.Normalize();

            evacuationTargetPosition = firePos + retreatDir * evacuationSafeDistance;

            // A plain, non-rendering anchor at the computed point — not a new prefab or model, just
            // a Transform for ARWaypointNavigationPointer.SetTarget() to point at.
            var anchorObj = new GameObject("EvacuationTargetAnchor");
            anchorObj.transform.position = evacuationTargetPosition;
            evacuationTargetAnchor = anchorObj.transform;

            if (ARWaypointNavigationPointer.Instance != null)
            {
                ARWaypointNavigationPointer.Instance.SetTarget(evacuationTargetAnchor);
                ARWaypointNavigationPointer.Instance.Activate();
            }

            // Visible "stand here" ground marker, so it's obvious how far away the safe point
            // actually is instead of relying on the arrow alone. Reuses the existing
            // placement-indicator reticle (already in the project, used for tap-to-place
            // targeting) — but CLONED, not the same instance: ARPlacementManager's own
            // UpdatePlacementIndicator() force-hides its instance every single frame
            // unconditionally, so reusing it directly would just get hidden again next frame.
            // Parented under the anchor so it's cleaned up automatically when the anchor is
            // destroyed in CompleteEvacuation(). See documents/technical_scoring_explained.md
            // investigation notes.
            GameObject indicatorSource = ARPlacementManager.Instance != null ? ARPlacementManager.Instance.PlacementIndicator : null;
            if (indicatorSource != null)
            {
                GameObject indicatorClone = Instantiate(indicatorSource, evacuationTargetPosition, Quaternion.identity, evacuationTargetAnchor);
                indicatorClone.name = "EvacuationTargetMarker";
                indicatorClone.SetActive(true);
            }

            evacuationStepStartTime = Time.time;
            evacuationSustainedSince = -1f;
            isEvacuationActive = true;

            Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] BeginEvacuation() — target={evacuationTargetPosition}, pointerFound={(ARWaypointNavigationPointer.Instance != null)}, markerCreated={(indicatorSource != null)}.");
        }

        private float nextEvacuationLogTime = 0f;

        private void UpdateEvacuationCheck()
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null) return;

            Vector3 camPos = cam.transform.position;
            float dx = camPos.x - evacuationTargetPosition.x;
            float dz = camPos.z - evacuationTargetPosition.z;
            float distanceToTarget = Mathf.Sqrt(dx * dx + dz * dz);

            // Throttled (once/sec) so standing still doesn't spam the console, but tells you
            // exactly how far you still need to walk while you're testing this.
            if (Time.time >= nextEvacuationLogTime)
            {
                nextEvacuationLogTime = Time.time + 1f;
                Debug.Log($"[SCORING_DIAG] [FireSafetyModuleManager] Evacuation check — camPos={camPos}, target={evacuationTargetPosition}, distanceToTarget={distanceToTarget:F2}m (need <= {evacuationArrivalRadius:F2}m, sustain {evacuationSustainDuration:F1}s).");
            }

            if (distanceToTarget <= evacuationArrivalRadius)
            {
                if (evacuationSustainedSince < 0f) evacuationSustainedSince = Time.time;
                if (Time.time - evacuationSustainedSince >= evacuationSustainDuration)
                {
                    CompleteEvacuation();
                }
            }
            else
            {
                evacuationSustainedSince = -1f;
            }
        }

        private void CompleteEvacuation()
        {
            Debug.Log("[SCORING_DIAG] [FireSafetyModuleManager] CompleteEvacuation() — evacuation arrival sustained, finishing drill.");
            isEvacuationActive = false;

            if (ARWaypointNavigationPointer.Instance != null) ARWaypointNavigationPointer.Instance.Deactivate();
            if (evacuationTargetAnchor != null) Destroy(evacuationTargetAnchor.gameObject);

            float elapsed = Time.time - evacuationStepStartTime;
            float lateSeconds = Mathf.Max(0f, elapsed - evacuationTimeBudgetSeconds);
            int latenessDeduction = Mathf.RoundToInt(lateSeconds * evacuationLatePenaltyPerSecond);
            int penalty = stepPenaltyPoints[EvacuationStepIndex];

            stepScoreOverride[EvacuationStepIndex] = Mathf.Clamp(pointsPerStep - penalty - latenessDeduction, evacuationScoreFloor, pointsPerStep);

            CompleteCurrentStep(); // last step — triggers FinishModule()
        }

        private int ComputeHazardRecognitionScore()
        {
            int alarmImpact;
            if (alarmActivatedBeforeGrab) alarmImpact = alarmBonusActivatedBeforeGrab;
            else if (alarmActivated) alarmImpact = alarmBonusActivatedAfterGrab;
            else alarmImpact = -alarmPenaltyNeverActivated;

            return Mathf.Clamp(100 - proximityBreachCount * hazardRecognitionPenaltyPerBreach + alarmImpact, 0, 100);
        }

        private int ComputeExtinguisherUseScore()
        {
            if (stepMetrics.Count < SqueezeSweepStepIndex + 1) return 0;
            int sum = stepMetrics[PullPinStepIndex].score + stepMetrics[AimSprayStepIndex].score + stepMetrics[SqueezeSweepStepIndex].score;
            return Mathf.RoundToInt(sum / 3f);
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
