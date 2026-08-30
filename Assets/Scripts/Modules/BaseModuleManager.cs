using System;
using UnityEngine;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.Modules
{
    public abstract class BaseModuleManager : MonoBehaviour
    {
        [Header("Module Configuration")]
        [SerializeField] protected ModuleType moduleType;
        [SerializeField] protected string moduleName = "Base Safety Module";
        [SerializeField] protected int totalSteps = 4;
        [SerializeField] protected int baseScorePerStep = 25;

        protected int currentStepIndex = 0;
        protected int mistakesCount = 0;
        protected float startTime;
        protected bool isModuleActive = false;

        public event Action<int, string> OnStepChanged;
        public event Action<TrainingResult> OnModuleCompleted;
        public event Action<string> OnMistakeMade;

        public int CurrentStepIndex => currentStepIndex;
        public bool IsModuleActive => isModuleActive;

        public virtual void StartModule()
        {
            currentStepIndex = 0;
            mistakesCount = 0;
            startTime = Time.time;
            isModuleActive = true;

            Debug.Log($"[BaseModuleManager] Started module: {moduleName}");
            OnStepStart(currentStepIndex);
        }

        protected abstract void OnStepStart(int stepIndex);

        public virtual void CompleteCurrentStep()
        {
            if (!isModuleActive) return;

            Debug.Log($"[BaseModuleManager] Completed step {currentStepIndex} of {moduleName}");
            currentStepIndex++;

            if (currentStepIndex >= totalSteps)
            {
                FinishModule();
            }
            else
            {
                OnStepStart(currentStepIndex);
                OnStepChanged?.Invoke(currentStepIndex, GetStepInstruction(currentStepIndex));
            }
        }

        public virtual void RegisterMistake(string feedbackMessage)
        {
            if (!isModuleActive) return;

            mistakesCount++;
            Debug.LogWarning($"[BaseModuleManager] Mistake in {moduleName}: {feedbackMessage} (Total: {mistakesCount})");
            OnMistakeMade?.Invoke(feedbackMessage);
        }

        protected virtual void FinishModule()
        {
            isModuleActive = false;
            float timeTaken = Time.time - startTime;

            int maxScore = totalSteps * baseScorePerStep;
            int penalty = mistakesCount * 5;
            int finalScore = Mathf.Clamp(maxScore - penalty, 0, maxScore);
            float percentage = (float)finalScore / maxScore * 100f;
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
                completionTimeSeconds = timeTaken
            };

            if (LocalScoreManager.Instance != null)
            {
                LocalScoreManager.Instance.SaveResult(result);
            }
            // Also save to Firestore via AppDataService
            if (Data.AppDataService.Instance != null)
            {
                Data.AppDataService.Instance.SaveAttempt(moduleName, (int)percentage, passed);
            }

            Debug.Log($"[BaseModuleManager] Module {moduleName} finished. Score: {finalScore}/{maxScore} ({percentage}%). Passed: {passed}");
            OnModuleCompleted?.Invoke(result);
        }

        public abstract string GetStepInstruction(int stepIndex);
    }
}
