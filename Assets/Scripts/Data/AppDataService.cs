using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Firebase.Auth;

namespace MiningSafetyAR.Data
{
    [System.Serializable]
    class ProgressMapWrapper
    {
        public List<ModuleProgress> list = new List<ModuleProgress>();
    }

    [System.Serializable]
    class AttemptListWrapper
    {
        public List<TrainingResult> list = new List<TrainingResult>();
    }

    public class AppDataService : MonoBehaviour
    {
        public static AppDataService Instance { get; private set; }

        [SerializeField] ModuleDatabase moduleDatabase;
        [SerializeField] QuestionDatabase questionDatabase;
        [SerializeField] CertificateDatabase certificateDatabase;

        public WorkerData CurrentWorker { get; private set; }
        // Per-worker dynamic progress, keyed by moduleId
        Dictionary<string, ModuleProgress> progressMap = new Dictionary<string, ModuleProgress>();
        List<TrainingResult> allAttempts = new List<TrainingResult>();

        public event System.Action<WorkerData> OnWorkerLoaded;
        public event System.Action OnWorkerLoggedOut;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            if (Firebase.FirebaseAuthManager.Instance != null)
            {
                Firebase.FirebaseAuthManager.Instance.OnLoginSuccess += OnFirebaseLoginSuccess;
                Firebase.FirebaseAuthManager.Instance.OnLogout += OnFirebaseLogout;
            }
            else Invoke(nameof(TrySubscribe), 0.5f);
        }

        void TrySubscribe()
        {
            if (Firebase.FirebaseAuthManager.Instance == null) return;
            Firebase.FirebaseAuthManager.Instance.OnLoginSuccess += OnFirebaseLoginSuccess;
            Firebase.FirebaseAuthManager.Instance.OnLogout += OnFirebaseLogout;
        }

        void OnDisable()
        {
            if (Firebase.FirebaseAuthManager.Instance != null)
            {
                Firebase.FirebaseAuthManager.Instance.OnLoginSuccess -= OnFirebaseLoginSuccess;
                Firebase.FirebaseAuthManager.Instance.OnLogout -= OnFirebaseLogout;
            }
        }

        void OnFirebaseLoginSuccess(FirebaseUser user) => LoadWorkerFromFirestore(user.UserId);
        void OnFirebaseLogout() { CurrentWorker = null; progressMap.Clear(); allAttempts.Clear(); OnWorkerLoggedOut?.Invoke(); }

        void LoadWorkerFromFirestore(string firebaseUid)
        {
            Firebase.FirestoreService.Instance.GetWorkerJson(firebaseUid, (ok, json) =>
            {
                if (!ok || string.IsNullOrEmpty(json))
                {
                    CurrentWorker = CreateNewWorker(firebaseUid);
                    InitProgressMapForNewWorker();
                    LoadAttemptsLocally(CurrentWorker.id);
                    CacheWorkerLocally(CurrentWorker);
                    OnWorkerLoaded?.Invoke(CurrentWorker);
                    return;
                }
                var worker = ParseWorkerFromFirestoreJson(json);
                if (worker == null) worker = JsonUtility.FromJson<WorkerData>(json);
                if (worker == null || string.IsNullOrEmpty(worker.id))
                    worker = CreateNewWorker(firebaseUid);
                worker.firebaseUid = firebaseUid;
                CurrentWorker = worker;
                LoadProgressMapFromJson(json);
                // If no progress map in doc, init
                if (progressMap.Count == 0) InitProgressMapForNewWorker();
                // Recompute overallProgress from map (truth from Firestore per-module)
                RecomputeWorkerStatsFromMap();
                LoadAttemptsLocally(CurrentWorker.id);
                CacheWorkerLocally(CurrentWorker);
                OnWorkerLoaded?.Invoke(CurrentWorker);
                Debug.Log($"[AppDataService] Worker loaded: {CurrentWorker.name} ({CurrentWorker.id}) overall={CurrentWorker.overallProgress}% certs={CurrentWorker.certificatesEarned} attemptsLoaded={allAttempts.Count}");
            });
        }

        void InitProgressMapForNewWorker()
        {
            progressMap.Clear();
            var all = moduleDatabase != null ? moduleDatabase.GetAll() : new List<ModuleData>();
            foreach (var m in all)
            {
                progressMap[m.id] = new ModuleProgress { moduleId = m.id, status = m.id == "heights_safety" ? ModuleStatus.Locked : ModuleStatus.NotStarted, progress = 0, bestScore = 0, attempts = 0, lastAttempt = "", certificateId = "" };
            }
        }

        void LoadProgressMapFromJson(string firestoreJson)
        {
            progressMap.Clear();
            try
            {
                var dict = MiningSafetyAR.Firebase.MiniJSON.Json.Deserialize(firestoreJson) as Dictionary<string, object>;
                if (dict != null && dict.TryGetValue("fields", out var fieldsObj) && fieldsObj is Dictionary<string, object> fields)
                {
                    if (fields.TryGetValue("progressJson", out var pj) && pj is Dictionary<string, object> pjDict && pjDict.TryGetValue("stringValue", out var sv))
                    {
                        string json = sv as string;
                        if (!string.IsNullOrEmpty(json))
                        {
                            var wrapper = JsonUtility.FromJson<ProgressMapWrapper>(json);
                            if (wrapper != null && wrapper.list != null)
                                foreach (var p in wrapper.list) progressMap[p.moduleId] = p;
                        }
                    }
                    // Also try to load legacy per-module progress stored as separate fields? Ignore
                }
                // Fallback: try raw JSON if it was stored as raw (not Firestore fields)
                if (progressMap.Count == 0 && firestoreJson.Contains("\"progressMap\""))
                {
                    var raw = JsonUtility.FromJson<ProgressMapWrapper>(firestoreJson);
                    if (raw != null && raw.list != null) foreach (var p in raw.list) progressMap[p.moduleId] = p;
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"[AppDataService] LoadProgressMap failed: {e.Message}"); }
        }

        string ProgressMapToJson()
        {
            var wrapper = new ProgressMapWrapper { list = new List<ModuleProgress>(progressMap.Values) };
            return JsonUtility.ToJson(wrapper);
        }

        void RecomputeWorkerStatsFromMap()
        {
            if (CurrentWorker == null) return;
            int total = 0;
            int completed = 0;
            foreach (var kv in progressMap)
            {
                total += kv.Value.progress;
                if (kv.Value.status == ModuleStatus.Completed) completed++;
            }
            int count = Mathf.Max(1, progressMap.Count);
            CurrentWorker.overallProgress = total / count;
            // certificatesEarned based on completed with certificate
            int certs = 0;
            foreach (var kv in progressMap) if (kv.Value.status == ModuleStatus.Completed && !string.IsNullOrEmpty(kv.Value.certificateId)) certs++;
            // Also include certificates from CertificateDatabase for this worker? For now use map
            CurrentWorker.certificatesEarned = certs;
        }

        WorkerData ParseWorkerFromFirestoreJson(string firestoreJson)
        {
            try
            {
                var dict = MiningSafetyAR.Firebase.MiniJSON.Json.Deserialize(firestoreJson) as Dictionary<string, object>;
                if (dict != null && dict.ContainsKey("fields"))
                {
                    var fields = dict["fields"] as Dictionary<string, object>;
                    if (fields != null)
                    {
                        var w = new WorkerData();
                        w.firebaseUid = GetStringField(fields, "firebaseUid");
                        w.id = GetStringField(fields, "id");
                        w.name = GetStringField(fields, "name");
                        w.organization = GetStringField(fields, "organization");
                        w.sector = GetStringField(fields, "sector");
                        w.phone = GetStringField(fields, "phone");
                        w.language = GetStringField(fields, "language");
                        w.joinDate = GetStringField(fields, "joinDate");
                        w.overallProgress = GetIntField(fields, "overallProgress");
                        w.certificatesEarned = GetIntField(fields, "certificatesEarned");
                        w.totalAttempts = GetIntField(fields, "totalAttempts");
                        if (w.competencyScores == null) w.competencyScores = new CompetencyScores();
                        return w;
                    }
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"[AppDataService] ParseWorker failed: {e.Message}"); }
            return null;
        }

        string GetStringField(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var v)) return "";
            if (v is Dictionary<string, object> d && d.TryGetValue("stringValue", out var sv)) return sv as string ?? "";
            if (v is Dictionary<string, object> d2 && d2.TryGetValue("integerValue", out var iv)) return iv.ToString();
            return v?.ToString() ?? "";
        }
        int GetIntField(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var v)) return 0;
            if (v is Dictionary<string, object> d)
            {
                if (d.TryGetValue("integerValue", out var iv) && int.TryParse(iv.ToString(), out int i)) return i;
                if (d.TryGetValue("stringValue", out var sv) && int.TryParse(sv.ToString(), out int i2)) return i2;
                if (d.TryGetValue("doubleValue", out var dv) && int.TryParse(dv.ToString(), out int i3)) return i3;
            }
            return 0;
        }

        // --- Module Access (definition) ---
        public ModuleData GetModule(string id) => moduleDatabase != null ? moduleDatabase.GetById(id) : null;
        public List<ModuleData> GetAllModules() => moduleDatabase != null ? moduleDatabase.GetAll() : new List<ModuleData>();

        // --- Dynamic per-worker progress ---
        public ModuleProgress GetModuleProgress(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return null;
            if (progressMap.TryGetValue(moduleId, out var p)) return p;
            // Not in map (e.g., new module added after worker created) -> create default
            var def = GetModule(moduleId);
            if (def == null) return null;
            var np = new ModuleProgress { moduleId = moduleId, status = moduleId == "heights_safety" ? ModuleStatus.Locked : ModuleStatus.NotStarted };
            progressMap[moduleId] = np;
            return np;
        }

        public List<ModuleData> GetAllModulesWithProgress()
        {
            // Return copies with dynamic progress overlaid (do not mutate shared ModuleData)
            var all = GetAllModules();
            var result = new List<ModuleData>();
            foreach (var m in all)
            {
                var p = GetModuleProgress(m.id);
                var copy = new ModuleData
                {
                    id = m.id, title = m.title, iconEmoji = m.iconEmoji, domain = m.domain, duration = m.duration, difficulty = m.difficulty,
                    status = p != null ? p.status : m.status,
                    progress = p != null ? p.progress : 0,
                    bestScore = p != null ? p.bestScore : 0,
                    attempts = p != null ? p.attempts : 0,
                    lastAttempt = p != null ? p.lastAttempt : "",
                    certificateId = p != null ? p.certificateId : "",
                    color = m.color, description = m.description, objectives = m.objectives, competencyScores = m.competencyScores
                };
                // Unlock logic: heights_safety locked until others completed
                if (m.id == "heights_safety" && copy.status == ModuleStatus.Locked)
                {
                    bool allPrevCompleted = true;
                    foreach (var prev in all) if (prev.id != "heights_safety")
                    {
                        var pp = GetModuleProgress(prev.id);
                        if (pp == null || pp.status != ModuleStatus.Completed) { allPrevCompleted = false; break; }
                    }
                    if (allPrevCompleted) copy.status = ModuleStatus.NotStarted;
                }
                result.Add(copy);
            }
            return result;
        }

        public List<ModuleData> GetModulesByStatusDynamic(ModuleStatus status)
        {
            return GetAllModulesWithProgress().FindAll(m => m.status == status);
        }

        // Legacy wrappers for old code (now dynamic)
        public List<ModuleData> GetModulesByStatus(ModuleStatus status) => GetModulesByStatusDynamic(status);

        // --- Question Access ---
        public List<QuizQuestionData> GetQuestions(string moduleId) => questionDatabase != null ? questionDatabase.GetForModule(moduleId) : new List<QuizQuestionData>();

        // --- Certificate Access ---
        public CertificateData GetCertificate(string certId) => certificateDatabase != null ? certificateDatabase.GetById(certId) : null;
        public List<CertificateData> GetWorkerCertificates()
        {
            if (CurrentWorker == null || certificateDatabase == null) return new List<CertificateData>();
            return certificateDatabase.GetByWorker(CurrentWorker.id);
        }

        // --- Training Results & Attempt History ---
        public List<TrainingResult> GetAttemptsForModule(string moduleId)
        {
            if (allAttempts == null) return new List<TrainingResult>();
            return allAttempts.Where(a => string.Equals(a.moduleName, moduleId, System.StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public List<TrainingResult> GetAllAttempts()
        {
            return allAttempts ?? new List<TrainingResult>();
        }

        public void SaveAttempt(string moduleId, int score, bool passed)
        {
            var result = new TrainingResult
            {
                resultId = System.Guid.NewGuid().ToString(),
                workerId = CurrentWorker != null ? CurrentWorker.id : "unknown",
                moduleName = moduleId,
                score = score,
                maxScore = 100,
                percentage = score,
                passed = passed,
                mistakesCount = 0,
                completionTimeSeconds = 0,
                timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                synced = false
            };

            allAttempts.Add(result);
            SaveAttemptsLocally();

            string json = JsonUtility.ToJson(result);
            Firebase.FirestoreService.Instance.SaveRaw($"trainingResults/{result.resultId}", json, (ok, resp) => Debug.Log($"[AppDataService] SaveAttempt {(ok ? "OK" : "FAIL")} {moduleId} {score}%"));

            UpdateLocalProgress(moduleId, score, passed);
        }

        void UpdateLocalProgress(string moduleId, int score, bool passed)
        {
            if (CurrentWorker != null) CurrentWorker.totalAttempts++;

            var prog = GetModuleProgress(moduleId);
            if (prog != null)
            {
                prog.attempts++;
                prog.bestScore = Mathf.Max(prog.bestScore, score);
                prog.progress = passed ? 100 : Mathf.Max(prog.progress, score);
                prog.status = passed ? ModuleStatus.Completed : ModuleStatus.InProgress;
                prog.lastAttempt = System.DateTime.UtcNow.ToString("yyyy-MM-dd");
                if (passed && string.IsNullOrEmpty(prog.certificateId))
                {
                    // Generate cert id
                    prog.certificateId = $"JH-{moduleId.ToUpper().Substring(0, System.Math.Min(4, moduleId.Length))}-{Random.Range(100000,999999)}";
                }
                // Unlock next if needed (heights)
                if (passed) CheckUnlockHeights();
            }

            RecomputeWorkerStatsFromMap();
            SaveWorkerAndProgress();
        }

        void CheckUnlockHeights()
        {
            var heights = GetModuleProgress("heights_safety");
            if (heights == null || heights.status != ModuleStatus.Locked) return;
            var all = GetAllModules();
            bool allPrevCompleted = true;
            foreach (var m in all) if (m.id != "heights_safety")
            {
                var p = GetModuleProgress(m.id);
                if (p == null || p.status != ModuleStatus.Completed) { allPrevCompleted = false; break; }
            }
            if (allPrevCompleted) heights.status = ModuleStatus.NotStarted;
        }

        void SaveWorkerAndProgress()
        {
            if (CurrentWorker == null) return;
            // Save worker doc with progressJson
            string progressJson = ProgressMapToJson();
            // Build worker json and inject progressJson as stringValue via manual JSON
            string workerJson = JsonUtility.ToJson(CurrentWorker);
            // Inject progressJson into JSON string before final }
            // workerJson is {"firebaseUid":"...","id":"...",...} -> add ,"progressJson":"{...escaped...}"
            string escaped = progressJson.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string withProgress = workerJson.TrimEnd('}');
            if (!withProgress.EndsWith("{")) withProgress += ",";
            withProgress += $"\"progressJson\":\"{escaped}\"}}";
            Firebase.FirestoreService.Instance.SaveWorkerJson(CurrentWorker.firebaseUid, withProgress);
            CacheWorkerLocally(CurrentWorker);
            // Also cache progressMap locally
            PlayerPrefs.SetString("ProgressMap_" + CurrentWorker.firebaseUid, progressJson);
            PlayerPrefs.Save();
        }

        // --- Auth shortcuts ---
        public void Login(string workerId, string pin) => Firebase.FirebaseAuthManager.Instance?.Login(workerId, pin);
        public void DemoLogin() => Firebase.FirebaseAuthManager.Instance?.DemoLogin();
        public void Register(string name, string workerId, string org, string sector, string phone, string pin) => Firebase.FirebaseAuthManager.Instance?.Register(workerId, pin, name);
        public void Logout() => Firebase.FirebaseAuthManager.Instance?.Logout();

        void CacheWorkerLocally(WorkerData worker)
        {
            string json = JsonUtility.ToJson(worker);
            PlayerPrefs.SetString("CachedWorker", json);
            PlayerPrefs.Save();
        }

        public void SaveAttemptsLocally()
        {
            if (CurrentWorker == null) return;
            var wrapper = new AttemptListWrapper { list = allAttempts };
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString("Attempts_" + CurrentWorker.id, json);
            PlayerPrefs.Save();
        }

        public void LoadAttemptsLocally(string workerId)
        {
            allAttempts.Clear();
            if (string.IsNullOrEmpty(workerId)) return;
            string json = PlayerPrefs.GetString("Attempts_" + workerId, "");
            if (!string.IsNullOrEmpty(json))
            {
                var wrapper = JsonUtility.FromJson<AttemptListWrapper>(json);
                if (wrapper != null && wrapper.list != null)
                {
                    allAttempts = wrapper.list;
                }
            }
        }

        WorkerData CreateNewWorker(string firebaseUid)
        {
            var w = new WorkerData { firebaseUid = firebaseUid, id = "NEW", name = "New Worker", organization = "", sector = "", phone = "", language = "English", joinDate = System.DateTime.UtcNow.ToString("yyyy-MM-dd"), overallProgress = 0, certificatesEarned = 0, totalAttempts = 0, competencyScores = new CompetencyScores() };
            return w;
        }
    }
}
