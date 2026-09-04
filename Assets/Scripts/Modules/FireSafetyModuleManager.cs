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
        [SerializeField] private AudioClip step5AudioEN;
        [SerializeField] private AudioClip step5AudioHI;
        [SerializeField] private AudioClip step5AudioSAT;

        [Header("Scoring")]
        [SerializeField] private int pointsPerStep = 100;
        [SerializeField] private int penaltyPerError = 25;

        private List<StepMetric> stepMetrics = new List<StepMetric>();
        private float[] stepStartTimes;
        private int[] stepErrorCounts;
        private bool failureEscalated = false;

        public new event Action<int, string> OnStepChanged;
        public new event Action<string> OnMistakeMade;
        public event Action<string> OnFailureEscalated;
        public event Action<List<StepMetric>> OnModuleCompletedWithMetrics;

        private static readonly string[] StepNames =
        {
            "Sound Alarm",
            "Select Extinguisher",
            "Pull Pin",
            "Aim & Test Spray",
            "Squeeze & Sweep"
        };

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
            totalSteps = 5;
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            if (FireExtinguisherGrabController.Instance != null)
            {
                FireExtinguisherGrabController.Instance.OnPinPulled += HandlePinPulled;
                FireExtinguisherGrabController.Instance.OnSprayStarted += HandleSprayStarted;
                FireExtinguisherGrabController.Instance.OnSweepDetected += HandleSweepDetected;
                FireExtinguisherGrabController.Instance.OnExtinguisherDepleted += HandleExtinguisherDepleted;
            }

            if (GroundFireController.Instance != null)
            {
                GroundFireController.Instance.OnFireExtinguished += HandleFireExtinguished;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (FireExtinguisherGrabController.Instance != null)
            {
                FireExtinguisherGrabController.Instance.OnPinPulled -= HandlePinPulled;
                FireExtinguisherGrabController.Instance.OnSprayStarted -= HandleSprayStarted;
                FireExtinguisherGrabController.Instance.OnSweepDetected -= HandleSweepDetected;
                FireExtinguisherGrabController.Instance.OnExtinguisherDepleted -= HandleExtinguisherDepleted;
            }

            if (GroundFireController.Instance != null)
            {
                GroundFireController.Instance.OnFireExtinguished -= HandleFireExtinguished;
            }
        }

        public override void StartModule()
        {
            stepMetrics.Clear();
            stepStartTimes = new float[totalSteps];
            stepErrorCounts = new int[totalSteps];
            failureEscalated = false;

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

            switch (stepIndex)
            {
                case 0:
                    PlayStepAudio(step1AudioEN, step1AudioHI, step1AudioSAT);
                    break;
                case 1:
                    PlayStepAudio(step2AudioEN, step2AudioHI, step2AudioSAT);
                    break;
                case 2:
                    PlayStepAudio(step3AudioEN, step3AudioHI, step3AudioSAT);
                    break;
                case 3:
                    PlayStepAudio(step4AudioEN, step4AudioHI, step4AudioSAT);
                    break;
                case 4:
                    PlayStepAudio(step5AudioEN, step5AudioHI, step5AudioSAT);
                    break;
            }
        }

        public override void CompleteCurrentStep()
        {
            if (!isModuleActive || failureEscalated) return;

            RecordStepMetric(currentStepIndex);

            Debug.Log($"[FireSafetyModuleManager] Step {currentStepIndex} ({StepNames[currentStepIndex]}) completed.");

            base.CompleteCurrentStep();
        }

        public override void RegisterMistake(string feedbackMessage)
        {
            if (!isModuleActive || failureEscalated) return;

            if (currentStepIndex < stepErrorCounts.Length)
            {
                stepErrorCounts[currentStepIndex]++;
            }

            base.RegisterMistake(feedbackMessage);
            OnMistakeMade?.Invoke(feedbackMessage);
        }

        public void TriggerFailureEscalation(string reason)
        {
            if (!isModuleActive || failureEscalated) return;

            failureEscalated = true;
            isModuleActive = false;

            Debug.LogWarning($"[FireSafetyModuleManager] FAILURE ESCALATION: {reason}");
            OnFailureEscalated?.Invoke(reason);
        }

        public void RetryModule()
        {
            failureEscalated = false;
            StartModule();
        }

        protected override void FinishModule()
        {
            isModuleActive = false;
            float timeTaken = Time.time - startTime;

            int maxScore = GetMaxPossibleScore();
            int finalScore = GetTotalScore();
            float percentage = maxScore > 0 ? ((float)finalScore / maxScore * 100f) : 0f;
            bool passed = percentage >= 70f;

            TrainingResult result = new TrainingResult
            {
                workerId = PlayerPrefs.GetString("WorkerID", "WORKER_001"),
                moduleName = moduleName,
                score = finalScore,
                maxScore = maxScore,
                percentage = percentage,
                passed = passed,
                mistakesCount = mistakesCount,
                completionTimeSeconds = timeTaken,
                stepMetrics = new List<StepMetric>(stepMetrics)
            };

            if (LocalScoreManager.Instance != null)
            {
                LocalScoreManager.Instance.SaveResult(result);
            }
            if (Data.AppDataService.Instance != null)
            {
                Data.AppDataService.Instance.SaveAttempt(moduleName, (int)percentage, passed);
            }

            Debug.Log($"[FireSafetyModuleManager] Module {moduleName} finished. Steps: {stepMetrics.Count}, Total Score: {finalScore}/{maxScore} ({percentage:F1}%). Passed: {passed}");

            OnModuleCompletedWithMetrics?.Invoke(new List<StepMetric>(stepMetrics));
        }

        private void RecordStepMetric(int stepIndex)
        {
            if (stepIndex >= totalSteps) return;
            if (stepIndex < stepStartTimes.Length && stepStartTimes[stepIndex] > 0f)
            {
                float duration = Time.time - stepStartTimes[stepIndex];
                int errors = stepIndex < stepErrorCounts.Length ? stepErrorCounts[stepIndex] : 0;
                int stepScore = Mathf.Clamp(pointsPerStep - (errors * penaltyPerError), 0, pointsPerStep);

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
                case 0: return "Step 1: Sound the emergency alarm on the safety station.";
                case 1: return "Step 2: Select the correct CO2/Dry Powder fire extinguisher.";
                case 2: return "Step 3: Pull the safety pin on the extinguisher handle.";
                case 3: return "Step 4: Aim the nozzle at the base of the fire and test spray.";
                case 4: return "Step 5: Squeeze the handle and sweep side-to-side across the base.";
                default: return "Module Complete!";
            }
        }

        /// <summary>
        /// Called by scene wiring when the alarm is activated (Step 0).
        /// </summary>
        public void NotifyAlarmActivated()
        {
            if (currentStepIndex == 0 && isModuleActive)
            {
                CompleteCurrentStep();
            }
        }

        /// <summary>
        /// Called by scene wiring when an extinguisher is selected (Step 1).
        /// </summary>
        public void NotifyExtinguisherSelected(bool isValid)
        {
            if (currentStepIndex != 1 || !isModuleActive) return;

            if (isValid)
            {
                CompleteCurrentStep();
            }
            else
            {
                RegisterMistake("Selected wrong extinguisher type for this fire hazard!");
            }
        }

        private void HandlePinPulled()
        {
            if (isModuleActive && currentStepIndex <= 2)
            {
                while (currentStepIndex <= 2 && isModuleActive && currentStepIndex < totalSteps - 1)
                {
                    CompleteCurrentStep();
                }
            }
        }

        private void HandleSprayStarted()
        {
            if (isModuleActive && currentStepIndex <= 3)
            {
                while (currentStepIndex <= 3 && isModuleActive && currentStepIndex < totalSteps - 1)
                {
                    CompleteCurrentStep();
                }
            }
        }

        private void HandleSweepDetected()
        {
            if (isModuleActive && currentStepIndex <= 4)
            {
                if (currentStepIndex < 4)
                {
                    while (currentStepIndex < 4 && isModuleActive)
                    {
                        CompleteCurrentStep();
                    }
                }
            }
        }

        private void HandleFireExtinguished()
        {
            Debug.Log("[FireSafetyModuleManager] Fire extinguished — drill complete.");
            if (isModuleActive)
            {
                while (isModuleActive && currentStepIndex < totalSteps)
                {
                    CompleteCurrentStep();
                }
            }
        }

        private void HandleExtinguisherDepleted()
        {
            TriggerFailureEscalation("Extinguisher foam depleted before fire was extinguished!");
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
