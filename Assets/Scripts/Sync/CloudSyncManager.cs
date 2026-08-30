using UnityEngine;

namespace MiningSafetyAR.Sync
{
    /// <summary>
    /// Legacy sync manager — no longer needed since AppDataService.SaveAttempt
    /// writes directly to Firestore. Kept as empty shell to avoid breaking references.
    /// </summary>
    public class CloudSyncManager : MonoBehaviour
    {
        public static CloudSyncManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
