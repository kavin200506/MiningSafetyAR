using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.Sync
{
    public class CloudSyncManager : MonoBehaviour
    {
        public static CloudSyncManager Instance { get; private set; }

        [Header("Cloud Endpoint Settings")]
        [SerializeField] private string firestoreEndpointUrl = "https://firestore.googleapis.com/v1/projects/miningsafetyar/databases/(default)/documents/trainingResults";
        [SerializeField] private float autoSyncIntervalSeconds = 30f;

        private bool isSyncing = false;

        public event Action<int> OnSyncCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(AutoSyncLoop());
        }

        public void TriggerSync()
        {
            if (!isSyncing)
            {
                StartCoroutine(SyncPendingResultsCoroutine());
            }
        }

        private IEnumerator AutoSyncLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(autoSyncIntervalSeconds);
                if (Application.internetReachability != NetworkReachability.NotReachable)
                {
                    TriggerSync();
                }
            }
        }

        private IEnumerator SyncPendingResultsCoroutine()
        {
            if (LocalScoreManager.Instance == null) yield break;

            List<TrainingResult> unsynced = LocalScoreManager.Instance.GetUnsyncedResults();
            if (unsynced == null || unsynced.Count == 0) yield break;

            isSyncing = true;
            Debug.Log($"[CloudSyncManager] Attempting to sync {unsynced.Count} unsynced results...");

            int syncedCount = 0;

            foreach (var result in unsynced)
            {
                string jsonBody = JsonUtility.ToJson(result);

                using (UnityWebRequest request = new UnityWebRequest(firestoreEndpointUrl, "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success || request.responseCode == 200 || request.responseCode == 201)
                    {
                        LocalScoreManager.Instance.MarkResultAsSynced(result.resultId);
                        syncedCount++;
                        Debug.Log($"[CloudSyncManager] Successfully synced result ID: {result.resultId}");
                    }
                    else
                    {
                        Debug.LogWarning($"[CloudSyncManager] Sync failed for result ID {result.resultId}: {request.error}");
                    }
                }
            }

            isSyncing = false;
            OnSyncCompleted?.Invoke(syncedCount);
        }
    }
}
