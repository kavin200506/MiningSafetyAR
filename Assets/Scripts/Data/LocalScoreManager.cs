using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace MiningSafetyAR.Data
{
    /// <summary>
    /// RETIRED (2026-09-05): this was an early, never-wired-up offline-queue prototype (writes to
    /// its own file, nothing ever read it back). Real offline support now lives in OfflineStore.cs
    /// (session + pending-write queue) + AppDataService.PushOrQueue + CloudSyncManager (the actual
    /// sync-on-reconnect flush). No code calls this class anymore. Kept in place, unused, rather than
    /// deleted, because several existing scenes (ar_fire_safety.unity, AR Plane Detection
    /// Placement.unity, SampleScene.unity) reference this component by GUID — removing the file would
    /// leave those GameObjects with a "missing script" that only the Unity Editor can clean up.
    /// </summary>
    public class LocalScoreManager : MonoBehaviour
    {
        public static LocalScoreManager Instance { get; private set; }

        private string saveFilePath;
        private TrainingResultListWrapper cachedResults = new TrainingResultListWrapper();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            saveFilePath = Path.Combine(Application.persistentDataPath, "mining_safety_results.json");
            LoadResults();
        }

        public void SaveResult(TrainingResult result)
        {
            if (result == null) return;
            cachedResults.results.Add(result);
            PersistToDisk();
            Debug.Log($"[LocalScoreManager] Saved local result: {result.moduleName} - Score: {result.score}");
        }

        public List<TrainingResult> GetAllResults()
        {
            return new List<TrainingResult>(cachedResults.results);
        }

        public List<TrainingResult> GetUnsyncedResults()
        {
            return cachedResults.results.FindAll(r => !r.synced);
        }

        public void MarkResultAsSynced(string resultId)
        {
            var target = cachedResults.results.Find(r => r.resultId == resultId);
            if (target != null)
            {
                target.synced = true;
                PersistToDisk();
                Debug.Log($"[LocalScoreManager] Marked result {resultId} as synced.");
            }
        }

        private void LoadResults()
        {
            try
            {
                if (File.Exists(saveFilePath))
                {
                    string json = File.ReadAllText(saveFilePath);
                    cachedResults = JsonUtility.FromJson<TrainingResultListWrapper>(json) ?? new TrainingResultListWrapper();
                    Debug.Log($"[LocalScoreManager] Loaded {cachedResults.results.Count} results from disk.");
                }
                else
                {
                    cachedResults = new TrainingResultListWrapper();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalScoreManager] Failed to load results from file: {ex.Message}");
                cachedResults = new TrainingResultListWrapper();
            }
        }

        private void PersistToDisk()
        {
            try
            {
                string json = JsonUtility.ToJson(cachedResults, true);
                File.WriteAllText(saveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalScoreManager] Failed to save results to disk: {ex.Message}");
            }
        }
    }
}
