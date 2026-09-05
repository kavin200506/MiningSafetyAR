using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MiningSafetyAR.Data
{
    /// <summary>
    /// A single JSON file (persistentDataPath/offline_store.json) holding everything the app needs
    /// to work offline: the persisted login session (so a worker stays logged in across app restarts
    /// and even without connectivity, until they explicitly log out) and a queue of Firestore writes
    /// that couldn't reach the server yet, to be replayed by SyncManager once connectivity returns.
    /// Chosen over PlayerPrefs/SQLite: the data here is a handful of small, nested C# objects with no
    /// relational queries — JsonUtility already round-trips them, and a plain file is trivial to
    /// inspect/diff/back up, with no per-platform native plugin needed. (Added 2026-09-05.)
    /// </summary>
    public static class OfflineStore
    {
        [Serializable]
        public class OfflineSession
        {
            public string firebaseUid;
            public string workerId;
            public string email;
            public string refreshToken;
            public string savedAtUtc;
        }

        [Serializable]
        public class PendingChange
        {
            public string id;
            public string kind;       // "worker" | "progress" | "result" | "certificate"
            public string firebaseUid;
            public string subId;      // moduleId / resultId / certId — empty for "worker"
            public string flatJson;   // the exact plain-field JSON that would have gone to Firestore
            public string timestampUtc;
        }

        [Serializable]
        private class StringEntry
        {
            public string key;
            public string value;
        }

        [Serializable]
        private class StoreFile
        {
            public OfflineSession session = new OfflineSession();
            public List<PendingChange> pendingChanges = new List<PendingChange>();
            // Every cached read (worker profile, progress map, attempts, certificates) that used to
            // live in PlayerPrefs as its own JSON-string blob now lives here instead, under the same
            // key names — one JSON file as the single offline store, consolidated 2026-09-06.
            public List<StringEntry> strings = new List<StringEntry>();
        }

        private static StoreFile _data;
        private static string FilePath => Path.Combine(Application.persistentDataPath, "offline_store.json");

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            try
            {
                if (File.Exists(FilePath))
                {
                    _data = JsonUtility.FromJson<StoreFile>(File.ReadAllText(FilePath)) ?? new StoreFile();
                    if (_data.session == null) _data.session = new OfflineSession();
                    if (_data.pendingChanges == null) _data.pendingChanges = new List<PendingChange>();
                    if (_data.strings == null) _data.strings = new List<StringEntry>();
                }
                else
                {
                    _data = new StoreFile();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OfflineStore] Failed to load {FilePath}, starting fresh: {ex.Message}");
                _data = new StoreFile();
            }
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_data));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OfflineStore] Failed to save {FilePath}: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // SESSION (persisted login — survives app restart and offline launches)
        // ------------------------------------------------------------------

        /// <summary>Null only on the very first-ever launch. Check refreshToken for validity, not this.</summary>
        public static OfflineSession GetSession()
        {
            EnsureLoaded();
            return string.IsNullOrEmpty(_data.session.refreshToken) ? null : _data.session;
        }

        public static void SaveSession(string firebaseUid, string workerId, string email, string refreshToken)
        {
            EnsureLoaded();
            _data.session = new OfflineSession
            {
                firebaseUid = firebaseUid,
                workerId = workerId,
                email = email,
                refreshToken = refreshToken,
                savedAtUtc = DateTime.UtcNow.ToString("o")
            };
            Save();
            Debug.Log($"[OfflineStore] Session saved for {firebaseUid} — will stay logged in until manual logout.");
        }

        /// <summary>Called only on an explicit user logout, or when the server rejects the refresh token as revoked.</summary>
        public static void ClearSession()
        {
            EnsureLoaded();
            _data.session = new OfflineSession();
            Save();
            Debug.Log("[OfflineStore] Session cleared.");
        }

        // ------------------------------------------------------------------
        // PENDING CHANGES (offline write queue, replayed by SyncManager)
        // ------------------------------------------------------------------

        public static void EnqueuePendingChange(string kind, string firebaseUid, string subId, string flatJson)
        {
            EnsureLoaded();
            _data.pendingChanges.Add(new PendingChange
            {
                id = Guid.NewGuid().ToString("N"),
                kind = kind,
                firebaseUid = firebaseUid,
                subId = subId ?? "",
                flatJson = flatJson,
                timestampUtc = DateTime.UtcNow.ToString("o")
            });
            Save();
        }

        public static List<PendingChange> GetPendingChanges()
        {
            EnsureLoaded();
            return new List<PendingChange>(_data.pendingChanges);
        }

        public static void RemovePendingChange(string id)
        {
            EnsureLoaded();
            _data.pendingChanges.RemoveAll(p => p.id == id);
            Save();
        }

        public static int PendingCount
        {
            get { EnsureLoaded(); return _data.pendingChanges.Count; }
        }

        // ------------------------------------------------------------------
        // GENERIC STRING STORE (cached worker/progress/attempts/certificates JSON blobs — the
        // consolidated replacement for what used to be scattered PlayerPrefs.GetString/SetString
        // calls with the exact same key names, e.g. "CachedWorker", "ProgressMap_"+uid,
        // "Attempts_"+workerId, "Certificates_"+workerId)
        // ------------------------------------------------------------------

        public static string GetString(string key, string defaultValue = "")
        {
            EnsureLoaded();
            var entry = _data.strings.Find(e => e.key == key);
            if (entry != null) return entry.value;

            // Lazy one-time migration from the pre-2026-09-06 PlayerPrefs-based cache: copy forward
            // on first read so existing installs don't appear to lose worker/progress/certificate
            // data just because the storage backend moved to this JSON file. After this, the value
            // lives here and PlayerPrefs is never consulted again for this key.
            if (PlayerPrefs.HasKey(key))
            {
                string legacy = PlayerPrefs.GetString(key, defaultValue);
                SetString(key, legacy);
                return legacy;
            }

            return defaultValue;
        }

        public static void SetString(string key, string value)
        {
            EnsureLoaded();
            var entry = _data.strings.Find(e => e.key == key);
            if (entry != null) entry.value = value;
            else _data.strings.Add(new StringEntry { key = key, value = value });
            Save();
        }

        public static void DeleteKey(string key)
        {
            EnsureLoaded();
            _data.strings.RemoveAll(e => e.key == key);
            Save();
        }
    }
}
