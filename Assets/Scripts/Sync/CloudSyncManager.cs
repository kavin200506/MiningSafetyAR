using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.Sync
{
    /// <summary>
    /// Replays the offline write queue (OfflineStore.PendingChanges) against Firestore once
    /// connectivity returns. This used to be an empty dead shell ("legacy... kept to avoid breaking
    /// references") — this is the real implementation of the sync-on-reconnect behavior that was
    /// only ever planned, never built (see OfflineStore.cs for the queue itself, and
    /// AppDataService.PushOrQueue for where writes get queued). Kept under the same class
    /// name/file/GUID rather than a new file, since several scenes already reference this component
    /// (ar_fire_safety.unity, AR Plane Detection Placement.unity, SampleScene.unity) — replacing the
    /// class body in place avoids leaving those GameObjects with a "missing script" (2026-09-05).
    /// Polls connectivity on an interval rather than reacting to an OS network-change event, since
    /// Unity has no cross-platform push notification for that.
    /// </summary>
    public class CloudSyncManager : MonoBehaviour
    {
        public static CloudSyncManager Instance { get; private set; }

        [SerializeField] float checkIntervalSeconds = 15f;

        bool isFlushing;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            InvokeRepeating(nameof(CheckAndFlush), 2f, checkIntervalSeconds);
        }

        void CheckAndFlush()
        {
            if (isFlushing) return;
            if (Application.internetReachability == NetworkReachability.NotReachable) return;

            // Device has a connection again, but the auth session may still be flagged offline
            // (set once, the last time a silent refresh failed) — new writes would otherwise keep
            // queuing forever until an app restart. Re-attempt the silent refresh here so it
            // upgrades back to a live session as soon as connectivity genuinely returns.
            var auth = Firebase.FirebaseAuthManager.Instance;
            if (auth != null && auth.IsOfflineSession)
            {
                auth.TryRestoreSession(_ => { });
            }

            if (OfflineStore.PendingCount == 0) return;
            FlushPendingChanges();
        }

        public void FlushPendingChanges()
        {
            if (isFlushing) return;
            if (Firebase.FirestoreService.Instance == null) return;
            var pending = OfflineStore.GetPendingChanges();
            if (pending.Count == 0) return;

            isFlushing = true;
            Debug.Log($"[CloudSyncManager] Flushing {pending.Count} pending offline change(s)...");
            StartCoroutine(FlushSequential(pending, 0));
        }

        IEnumerator FlushSequential(List<OfflineStore.PendingChange> list, int index)
        {
            if (index >= list.Count)
            {
                isFlushing = false;
                Debug.Log("[CloudSyncManager] Flush complete.");
                yield break;
            }

            var change = list[index];
            bool done = false;
            bool ok = false;
            string resp = null;

            Action<bool, string> onResult = (success, response) =>
            {
                ok = success;
                resp = response;
                done = true;
            };

            switch (change.kind)
            {
                case "worker":
                    Firebase.FirestoreService.Instance.SaveWorker(change.firebaseUid, change.flatJson, onResult);
                    break;
                case "progress":
                    Firebase.FirestoreService.Instance.SaveModuleProgress(change.firebaseUid, change.subId, change.flatJson, onResult);
                    break;
                case "result":
                    Firebase.FirestoreService.Instance.SaveTrainingResult(change.firebaseUid, change.subId, change.flatJson, onResult);
                    break;
                case "certificate":
                    Firebase.FirestoreService.Instance.SaveCertificateToFirestore(change.firebaseUid, change.subId, change.flatJson, onResult);
                    break;
                default:
                    Debug.LogWarning($"[CloudSyncManager] Unknown pending change kind '{change.kind}', dropping.");
                    OfflineStore.RemovePendingChange(change.id);
                    done = true;
                    break;
            }

            float deadline = Time.time + 10f;
            while (!done && Time.time < deadline) yield return null;

            if (done && ok)
            {
                OfflineStore.RemovePendingChange(change.id);
            }
            else if (done && !ok)
            {
                Debug.LogWarning($"[CloudSyncManager] Failed to sync pending {change.kind} change ({change.firebaseUid}/{change.subId}): {resp} — will retry next cycle.");
            }
            else
            {
                Debug.LogWarning($"[CloudSyncManager] Timed out syncing pending {change.kind} change ({change.firebaseUid}/{change.subId}) — will retry next cycle.");
            }

            yield return FlushSequential(list, index + 1);
        }
    }
}
