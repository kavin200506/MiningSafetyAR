using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Firebase.Auth;
using MiningSafetyAR.Firebase;

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

    [System.Serializable]
    public class WorkerSaveData
    {
        public string firebaseUid;
        public string id;
        public string name;
        public string organization;
        public string sector;
        public string phone;
        public string language;
        public string joinDate;
        public int overallProgress;
        public int certificatesEarned;
        public int totalAttempts;
    }

    public class AppDataService : MonoBehaviour
    {
        public static AppDataService Instance { get; private set; }

        [SerializeField] ModuleDatabase moduleDatabase;
        [SerializeField] QuestionDatabase questionDatabase;
        [SerializeField] CertificateDatabase certificateDatabase;

        public WorkerData CurrentWorker { get; private set; }
        Dictionary<string, ModuleProgress> progressMap = new Dictionary<string, ModuleProgress>();
        List<TrainingResult> allAttempts = new List<TrainingResult>();

        public event System.Action<WorkerData> OnWorkerLoaded;
        public event System.Action OnWorkerLoggedOut;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (moduleDatabase == null) moduleDatabase = Resources.Load<ModuleDatabase>("Data/ModuleDatabase");
            if (questionDatabase == null) questionDatabase = Resources.Load<QuestionDatabase>("Data/QuestionDatabase");
            if (certificateDatabase == null) certificateDatabase = Resources.Load<CertificateDatabase>("Data/CertificateDatabase");

            LoadCachedWorker();

            Debug.Log($"[AppDataService] Databases: Modules={moduleDatabase?.GetAll()?.Count ?? 0}, Questions={questionDatabase?.questions?.Count ?? 0}, Certs={certificateDatabase?.certificates?.Count ?? 0}");
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

        void OnFirebaseLoginSuccess(FirebaseUser user)
        {
            string uid = user != null ? user.UserId : Firebase.FirebaseAuthManager.Instance.CurrentUserId;
            Debug.Log($"[AppDataService] Login: {uid}");
            if (!string.IsNullOrEmpty(uid)) LoadWorkerFromFirestore(uid);
        }

        void OnFirebaseLogout()
        {
            string uid = CurrentWorker?.firebaseUid;
            CurrentWorker = null;
            progressMap.Clear();
            allAttempts.Clear();
            PlayerPrefs.DeleteKey("CachedWorker");
            // DO NOT delete ProgressMap so it persists across sessions!
            // if (!string.IsNullOrEmpty(uid)) PlayerPrefs.DeleteKey("ProgressMap_" + uid);
            OnWorkerLoggedOut?.Invoke();
        }

        // ================================================================
        // WORKER PROFILE (workers/{uid})
        // ================================================================

        void LoadWorkerFromFirestore(string firebaseUid)
        {
            Firebase.FirestoreService.Instance.GetWorker(firebaseUid, (ok, json) =>
            {
                if (!ok || string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning($"[AppDataService] Firestore load failed for {firebaseUid}, using cache");
                    
                    // Try to recover from the persistent user cache
                    string userCache = PlayerPrefs.GetString("CachedWorker_" + firebaseUid, "");
                    if (!string.IsNullOrEmpty(userCache))
                    {
                        Debug.Log($"[AppDataService] Recovered worker from local cache for {firebaseUid}");
                        CurrentWorker = JsonUtility.FromJson<WorkerData>(userCache);
                        LoadProgressFromSubcollection(firebaseUid);
                        LoadAttemptsFromFirestore(firebaseUid);
                        return;
                    }

                    if (CurrentWorker != null && CurrentWorker.firebaseUid == firebaseUid && CurrentWorker.id != "NEW")
                    {
                        Debug.Log($"[AppDataService] Keeping cached worker: {CurrentWorker.name}");
                        RecomputeWorkerStatsFromMap();
                        OnWorkerLoaded?.Invoke(CurrentWorker);
                        return;
                    }
                    CurrentWorker = CreateNewWorker(firebaseUid);
                    InitProgressMapForNewWorker();
                    LoadAttemptsLocally(CurrentWorker.id);
                    CacheWorkerLocally(CurrentWorker);
                    OnWorkerLoaded?.Invoke(CurrentWorker);
                    return;
                }

                var fields = Firebase.FirestoreService.ParseFirestoreFields(json);
                WorkerData worker;
                if (fields != null)
                {
                    worker = new WorkerData();
                    worker.firebaseUid = firebaseUid;
                    worker.id = Firebase.FirestoreService.GetstringValue(fields, "id");
                    worker.name = Firebase.FirestoreService.GetstringValue(fields, "name");
                    worker.organization = Firebase.FirestoreService.GetstringValue(fields, "organization");
                    worker.sector = Firebase.FirestoreService.GetstringValue(fields, "sector");
                    worker.phone = Firebase.FirestoreService.GetstringValue(fields, "phone");
                    worker.language = Firebase.FirestoreService.GetstringValue(fields, "language");
                    worker.joinDate = Firebase.FirestoreService.GetstringValue(fields, "joinDate");
                    worker.overallProgress = Firebase.FirestoreService.GetintValue(fields, "overallProgress");
                    worker.certificatesEarned = Firebase.FirestoreService.GetintValue(fields, "certificatesEarned");
                    worker.totalAttempts = Firebase.FirestoreService.GetintValue(fields, "totalAttempts");
                    if (worker.competencyScores == null) worker.competencyScores = new CompetencyScores();
                }
                else
                {
                    worker = JsonUtility.FromJson<WorkerData>(json);
                }

                if (worker == null || string.IsNullOrEmpty(worker.id) || worker.id == "NEW")
                    worker = CreateNewWorker(firebaseUid);
                worker.firebaseUid = firebaseUid;
                CurrentWorker = worker;

                // Load progress from subcollection
                LoadProgressFromSubcollection(firebaseUid);
                // Load attempts from Firestore subcollection
                LoadAttemptsFromFirestore(firebaseUid);
                CacheWorkerLocally(CurrentWorker);
            });
        }

        // ================================================================
        // MODULE PROGRESS (workers/{uid}/progress/{moduleId})
        // ================================================================

        void LoadProgressFromSubcollection(string firebaseUid)
        {
            // First try to load from local cache immediately
            LoadProgressFromCache(firebaseUid);

            // Then fetch from Firestore subcollection
            Firebase.FirestoreService.Instance.GetAllModuleProgress(firebaseUid, (ok, docs) =>
            {
                if (!ok || docs == null || docs.Count == 0)
                {
                    Debug.Log($"[AppDataService] No progress docs from Firestore, using cache ({progressMap.Count} entries)");
                    if (progressMap.Count == 0) InitProgressMapForNewWorker();
                    FinalizeProgressLoad(firebaseUid);
                    return;
                }

                // Instead of clearing the map, we merge! This protects against partial parsing failures from SimpleDeserialize.
                foreach (var doc in docs)
                {
                    // doc is the full document: {"name":".../progress/fire_safety","fields":{...}}
                    var fields = doc.ContainsKey("fields") ? doc["fields"] as Dictionary<string, object> : doc;
                    if (fields == null) continue;

                    string moduleId = Firebase.FirestoreService.GetstringValue(fields, "moduleId");
                    if (string.IsNullOrEmpty(moduleId))
                    {
                        // Extract from document name: .../progress/{moduleId}
                        string docName = doc.ContainsKey("name") ? doc["name"] as string : "";
                        moduleId = docName.Contains("/") ? docName.Split('/')[^1] : "";
                    }
                    if (string.IsNullOrEmpty(moduleId)) continue;

                    var prog = new ModuleProgress
                    {
                        moduleId = moduleId,
                        status = (ModuleStatus)Firebase.FirestoreService.GetintValue(fields, "status"),
                        progress = Firebase.FirestoreService.GetintValue(fields, "progress"),
                        bestScore = Firebase.FirestoreService.GetintValue(fields, "bestScore"),
                        attempts = Firebase.FirestoreService.GetintValue(fields, "attempts"),
                        lastAttempt = Firebase.FirestoreService.GetstringValue(fields, "lastAttempt"),
                        certificateId = Firebase.FirestoreService.GetstringValue(fields, "certificateId")
                    };
                    // Load competency scores from nested map
                    var csFields = Firebase.FirestoreService.GetmapValue(fields, "competencyScores");
                    if (csFields != null)
                    {
                        prog.competencyScores = new CompetencyScores
                        {
                            hazardRecognition = Firebase.FirestoreService.GetintValue(csFields, "hazardRecognition"),
                            extinguisherUse = Firebase.FirestoreService.GetintValue(csFields, "extinguisherUse"),
                            ppeSelection = Firebase.FirestoreService.GetintValue(csFields, "ppeSelection"),
                            evacuation = Firebase.FirestoreService.GetintValue(csFields, "evacuation"),
                            emergencyResponse = Firebase.FirestoreService.GetintValue(csFields, "emergencyResponse")
                        };
                    }

                    // MERGE with local cache (retain highest values)
                    if (progressMap.TryGetValue(moduleId, out var localProg))
                    {
                        prog.progress = Mathf.Max(prog.progress, localProg.progress);
                        prog.bestScore = Mathf.Max(prog.bestScore, localProg.bestScore);
                        prog.attempts = Mathf.Max(prog.attempts, localProg.attempts);
                        if (localProg.status > prog.status) prog.status = localProg.status;
                        if (string.IsNullOrEmpty(prog.certificateId)) prog.certificateId = localProg.certificateId;
                        if (string.IsNullOrEmpty(prog.lastAttempt)) prog.lastAttempt = localProg.lastAttempt;

                        if (localProg.competencyScores != null)
                        {
                            if (prog.competencyScores == null) prog.competencyScores = new CompetencyScores();
                            prog.competencyScores.hazardRecognition = Mathf.Max(prog.competencyScores.hazardRecognition, localProg.competencyScores.hazardRecognition);
                            prog.competencyScores.extinguisherUse = Mathf.Max(prog.competencyScores.extinguisherUse, localProg.competencyScores.extinguisherUse);
                            prog.competencyScores.ppeSelection = Mathf.Max(prog.competencyScores.ppeSelection, localProg.competencyScores.ppeSelection);
                            prog.competencyScores.evacuation = Mathf.Max(prog.competencyScores.evacuation, localProg.competencyScores.evacuation);
                            prog.competencyScores.emergencyResponse = Mathf.Max(prog.competencyScores.emergencyResponse, localProg.competencyScores.emergencyResponse);
                        }
                    }

                    progressMap[moduleId] = prog;
                }

                Debug.Log($"[AppDataService] Merged {progressMap.Count} progress docs from Firestore/Cache");
                FinalizeProgressLoad(firebaseUid);
            });
        }

        void FinalizeProgressLoad(string firebaseUid)
        {
            RecomputeWorkerStatsFromMap();
            CacheWorkerLocally(CurrentWorker);
            PlayerPrefs.SetString("ProgressMap_" + firebaseUid, ProgressMapToJson());
            PlayerPrefs.Save();
            OnWorkerLoaded?.Invoke(CurrentWorker);
            Debug.Log($"[AppDataService] Worker ready: {CurrentWorker.name} ({CurrentWorker.id}) overall={CurrentWorker.overallProgress}% certs={CurrentWorker.certificatesEarned}");
        }

        void SaveModuleProgressToFirestore(string firebaseUid, string moduleId, ModuleProgress prog)
        {
            var data = new Dictionary<string, object>
            {
                { "moduleId", prog.moduleId },
                { "status", (int)prog.status },
                { "progress", prog.progress },
                { "bestScore", prog.bestScore },
                { "attempts", prog.attempts },
                { "lastAttempt", prog.lastAttempt ?? "" },
                { "certificateId", prog.certificateId ?? "" }
            };
            // Save competency scores as nested map
            if (prog.competencyScores != null)
            {
                data["competencyScores"] = new Dictionary<string, object>
                {
                    { "hazardRecognition", prog.competencyScores.hazardRecognition },
                    { "extinguisherUse", prog.competencyScores.extinguisherUse },
                    { "ppeSelection", prog.competencyScores.ppeSelection },
                    { "evacuation", prog.competencyScores.evacuation },
                    { "emergencyResponse", prog.competencyScores.emergencyResponse }
                };
            }
            string flatJson = MiniJSON.Json.Serialize(data);
            Firebase.FirestoreService.Instance.SaveModuleProgress(firebaseUid, moduleId, flatJson);
        }

        void InitProgressMapForNewWorker()
        {
            progressMap.Clear();
            var all = moduleDatabase != null ? moduleDatabase.GetAll() : new List<ModuleData>();
            foreach (var m in all)
            {
                progressMap[m.id] = new ModuleProgress
                {
                    moduleId = m.id,
                    status = m.id == "heights_safety" ? ModuleStatus.Locked : ModuleStatus.NotStarted,
                    progress = 0, bestScore = 0, attempts = 0,
                    lastAttempt = "", certificateId = ""
                };
            }
        }

        string ProgressMapToJson()
        {
            var wrapper = new ProgressMapWrapper { list = new List<ModuleProgress>(progressMap.Values) };
            return JsonUtility.ToJson(wrapper);
        }

        void LoadProgressFromCache(string firebaseUid)
        {
            try
            {
                string cached = PlayerPrefs.GetString("ProgressMap_" + firebaseUid, "");
                if (string.IsNullOrEmpty(cached)) return;
                var wrapper = JsonUtility.FromJson<ProgressMapWrapper>(cached);
                if (wrapper?.list == null) return;
                progressMap.Clear();
                foreach (var p in wrapper.list) progressMap[p.moduleId] = p;
                Debug.Log($"[AppDataService] Loaded {progressMap.Count} progress entries from cache");
            }
            catch (System.Exception e) { Debug.LogWarning($"[AppDataService] Cache load failed: {e.Message}"); }
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
            int certs = 0;
            foreach (var kv in progressMap)
                if (kv.Value.status == ModuleStatus.Completed && !string.IsNullOrEmpty(kv.Value.certificateId)) certs++;
            CurrentWorker.certificatesEarned = certs;
        }

        // ================================================================
        // MODULE DEFINITIONS
        // ================================================================

        public ModuleData GetModule(string id)
        {
            if (moduleDatabase == null) moduleDatabase = Resources.Load<ModuleDatabase>("Data/ModuleDatabase");
            return moduleDatabase != null ? moduleDatabase.GetById(id) : null;
        }

        public List<ModuleData> GetAllModules()
        {
            if (moduleDatabase == null) moduleDatabase = Resources.Load<ModuleDatabase>("Data/ModuleDatabase");
            return moduleDatabase != null ? moduleDatabase.GetAll() : new List<ModuleData>();
        }

        // ================================================================
        // DYNAMIC PROGRESS (per-worker)
        // ================================================================

        public ModuleProgress GetModuleProgress(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return null;
            if (progressMap.TryGetValue(moduleId, out var p)) return p;
            var def = GetModule(moduleId);
            if (def == null) return null;
            var np = new ModuleProgress { moduleId = moduleId, status = moduleId == "heights_safety" ? ModuleStatus.Locked : ModuleStatus.NotStarted };
            progressMap[moduleId] = np;
            return np;
        }

        public List<ModuleData> GetAllModulesWithProgress()
        {
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
                    color = m.color, description = m.description, objectives = m.objectives,
                    // Use dynamic competency scores from progress, fall back to static defaults
                    competencyScores = (p != null && p.competencyScores != null) ? p.competencyScores : m.competencyScores
                };
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

        /// <summary>
        /// Update competency scores for a module after a quiz attempt.
        /// For each competency, calculates: (correct in category / total in category) * 100.
        /// Merges with existing scores using best-score logic.
        /// </summary>
        public void UpdateModuleCompetencyScores(string moduleId, Dictionary<string, int> correctByCompetency, Dictionary<string, int> totalByCompetency)
        {
            if (string.IsNullOrEmpty(moduleId)) return;
            var prog = GetModuleProgress(moduleId);
            if (prog == null) return;

            if (prog.competencyScores == null) prog.competencyScores = new CompetencyScores();

            foreach (var kv in totalByCompetency)
            {
                string comp = kv.Key;
                int total = kv.Value;
                if (total <= 0) continue;
                int correct = 0;
                if (correctByCompetency.ContainsKey(comp)) correct = correctByCompetency[comp];
                int pct = (int)((float)correct / total * 100f);

                // Update the matching competency field (best score logic)
                switch (comp)
                {
                    case "hazardRecognition":
                        prog.competencyScores.hazardRecognition = Mathf.Max(prog.competencyScores.hazardRecognition, pct);
                        break;
                    case "extinguisherUse":
                        prog.competencyScores.extinguisherUse = Mathf.Max(prog.competencyScores.extinguisherUse, pct);
                        break;
                    case "ppeSelection":
                        prog.competencyScores.ppeSelection = Mathf.Max(prog.competencyScores.ppeSelection, pct);
                        break;
                    case "evacuation":
                        prog.competencyScores.evacuation = Mathf.Max(prog.competencyScores.evacuation, pct);
                        break;
                    case "emergencyResponse":
                        prog.competencyScores.emergencyResponse = Mathf.Max(prog.competencyScores.emergencyResponse, pct);
                        break;
                }
            }

            // Save to Firestore
            SaveModuleProgressToFirestore(CurrentWorker.firebaseUid, moduleId, prog);
            // Update local cache
            PlayerPrefs.SetString("ProgressMap_" + CurrentWorker.firebaseUid, ProgressMapToJson());
            PlayerPrefs.Save();
        }

        public List<ModuleData> GetModulesByStatusDynamic(ModuleStatus status) => GetAllModulesWithProgress().FindAll(m => m.status == status);
        public List<ModuleData> GetModulesByStatus(ModuleStatus status) => GetModulesByStatusDynamic(status);

        // ================================================================
        // QUESTIONS
        // ================================================================

        public List<QuizQuestionData> GetQuestions(string moduleId)
        {
            if (questionDatabase == null) questionDatabase = Resources.Load<QuestionDatabase>("Data/QuestionDatabase");
            return questionDatabase != null ? questionDatabase.GetForModule(moduleId) : new List<QuizQuestionData>();
        }

        // ================================================================
        // CERTIFICATES
        // ================================================================

        public CertificateData GetCertificate(string certId)
        {
            if (certificateDatabase == null) certificateDatabase = Resources.Load<CertificateDatabase>("Data/CertificateDatabase");
            return certificateDatabase != null ? certificateDatabase.GetById(certId) : null;
        }

        public List<CertificateData> GetWorkerCertificates()
        {
            if (certificateDatabase == null) certificateDatabase = Resources.Load<CertificateDatabase>("Data/CertificateDatabase");
            if (CurrentWorker == null || certificateDatabase == null) return new List<CertificateData>();
            return certificateDatabase.GetByWorker(CurrentWorker.id);
        }

        // ================================================================
        // TRAINING RESULTS & ATTEMPTS
        // ================================================================

        public List<TrainingResult> GetAttemptsForModule(string moduleId)
        {
            if (allAttempts == null) return new List<TrainingResult>();
            return allAttempts.Where(a => string.Equals(a.moduleName, moduleId, System.StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public List<TrainingResult> GetAllAttempts() => allAttempts ?? new List<TrainingResult>();

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

            // Save to Firestore under worker's subcollection
            string json = JsonUtility.ToJson(result);
            Firebase.FirestoreService.Instance.SaveTrainingResult(CurrentWorker.firebaseUid, result.resultId, json,
                (ok, resp) => Debug.Log($"[AppDataService] Attempt {(ok ? "saved" : "FAIL")} {moduleId} {score}%"));

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
                    prog.certificateId = $"JH-{moduleId.ToUpper().Substring(0, System.Math.Min(4, moduleId.Length))}-{Random.Range(100000,999999)}";
                }
                if (passed) CheckUnlockHeights();

                // Save this module's progress to its own Firestore document
                SaveModuleProgressToFirestore(CurrentWorker.firebaseUid, moduleId, prog);
            }

            RecomputeWorkerStatsFromMap();
            SaveWorkerProfile();
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

        // ================================================================
        // SAVE WORKER PROFILE ONLY (no progressJson blob)
        // ================================================================

        void SaveWorkerProfile()
        {
            if (CurrentWorker == null) return;

            var saveData = new WorkerSaveData
            {
                firebaseUid = CurrentWorker.firebaseUid,
                id = CurrentWorker.id,
                name = CurrentWorker.name,
                organization = CurrentWorker.organization,
                sector = CurrentWorker.sector,
                phone = CurrentWorker.phone,
                language = CurrentWorker.language,
                joinDate = CurrentWorker.joinDate,
                overallProgress = CurrentWorker.overallProgress,
                certificatesEarned = CurrentWorker.certificatesEarned,
                totalAttempts = CurrentWorker.totalAttempts
            };
            string workerJson = JsonUtility.ToJson(saveData);

            Firebase.FirestoreService.Instance.SaveWorker(CurrentWorker.firebaseUid, workerJson);
            CacheWorkerLocally(CurrentWorker);

            // Cache progress map locally
            PlayerPrefs.SetString("ProgressMap_" + CurrentWorker.firebaseUid, ProgressMapToJson());
            PlayerPrefs.Save();
        }

        // ================================================================
        // AUTH SHORTCUTS
        // ================================================================

        public void Login(string workerId, string pin) => Firebase.FirebaseAuthManager.Instance?.Login(workerId, pin);
        public void DemoLogin() => Firebase.FirebaseAuthManager.Instance?.DemoLogin();
        public void Register(string name, string workerId, string org, string sector, string phone, string pin) => Firebase.FirebaseAuthManager.Instance?.Register(workerId, pin, name);
        public void Logout() => Firebase.FirebaseAuthManager.Instance?.Logout();

        // ================================================================
        // LOCAL CACHE
        // ================================================================

        void CacheWorkerLocally(WorkerData worker)
        {
            string json = JsonUtility.ToJson(worker);
            PlayerPrefs.SetString("CachedWorker", json);
            PlayerPrefs.SetString("CachedWorker_" + worker.firebaseUid, json);
            PlayerPrefs.Save();
        }

        void LoadCachedWorker()
        {
            try
            {
                string json = PlayerPrefs.GetString("CachedWorker", "");
                if (string.IsNullOrEmpty(json)) return;
                var worker = JsonUtility.FromJson<WorkerData>(json);
                if (worker == null || string.IsNullOrEmpty(worker.id) || worker.id == "NEW") return;
                CurrentWorker = worker;
                LoadProgressFromCache(worker.firebaseUid);
                LoadAttemptsLocally(worker.id);
                Debug.Log($"[AppDataService] Cached worker: {worker.name} ({worker.id}) overall={worker.overallProgress}%");
            }
            catch (System.Exception e) { Debug.LogWarning($"[AppDataService] LoadCachedWorker: {e.Message}"); }
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
            if (string.IsNullOrEmpty(json)) return;
            var wrapper = JsonUtility.FromJson<AttemptListWrapper>(json);
            if (wrapper?.list != null) allAttempts = wrapper.list;
        }

        void LoadAttemptsFromFirestore(string firebaseUid)
        {
            // Load from local cache first
            LoadAttemptsLocally(CurrentWorker.id);

            // Then fetch from Firestore subcollection
            Firebase.FirestoreService.Instance.GetAllTrainingResults(firebaseUid, (ok, docs) =>
            {
                if (!ok || docs == null || docs.Count == 0)
                {
                    Debug.Log($"[AppDataService] No attempt docs from Firestore ({allAttempts.Count} from cache)");
                    return;
                }

                allAttempts.Clear();
                foreach (var doc in docs)
                {
                    var fields = doc.ContainsKey("fields") ? doc["fields"] as Dictionary<string, object> : doc;
                    if (fields == null) continue;

                    var result = new TrainingResult
                    {
                        resultId = Firebase.FirestoreService.GetstringValue(fields, "resultId"),
                        workerId = Firebase.FirestoreService.GetstringValue(fields, "workerId"),
                        moduleName = Firebase.FirestoreService.GetstringValue(fields, "moduleName"),
                        score = Firebase.FirestoreService.GetintValue(fields, "score"),
                        maxScore = Firebase.FirestoreService.GetintValue(fields, "maxScore"),
                        percentage = Firebase.FirestoreService.GetintValue(fields, "percentage"),
                        passed = Firebase.FirestoreService.GetboolValue(fields, "passed"),
                        mistakesCount = Firebase.FirestoreService.GetintValue(fields, "mistakesCount"),
                        completionTimeSeconds = (float)Firebase.FirestoreService.GetintValue(fields, "completionTimeSeconds"),
                        timestamp = Firebase.FirestoreService.GetstringValue(fields, "timestamp"),
                        synced = true
                    };
                    allAttempts.Add(result);
                }

                // Sort by timestamp descending (newest first)
                allAttempts.Sort((a, b) => string.Compare(b.timestamp, a.timestamp, System.StringComparison.Ordinal));

                // Update local cache
                SaveAttemptsLocally();
                Debug.Log($"[AppDataService] Loaded {allAttempts.Count} attempts from Firestore");
            });
        }

        WorkerData CreateNewWorker(string firebaseUid)
        {
            return new WorkerData
            {
                firebaseUid = firebaseUid, id = "NEW", name = "New Worker",
                organization = "", sector = "", phone = "", language = "English",
                joinDate = System.DateTime.UtcNow.ToString("yyyy-MM-dd"),
                overallProgress = 0, certificatesEarned = 0, totalAttempts = 0,
                competencyScores = new CompetencyScores()
            };
        }
    }
}
