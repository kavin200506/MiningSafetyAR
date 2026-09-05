using System.Collections;
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
            StartCoroutine(AutoSyncRoutine());

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
            // If already loaded in memory or cached locally, serve immediately for offline speed
            if (CurrentWorker != null && (CurrentWorker.firebaseUid == firebaseUid || CurrentWorker.id != "NEW"))
            {
                Debug.Log($"[AppDataService] Serving in-memory/cached worker for {firebaseUid}: {CurrentWorker.name}");
                LoadProgressFromCache(firebaseUid);
                LoadAttemptsLocally(CurrentWorker.id);
                RecomputeWorkerStatsFromMap();
                OnWorkerLoaded?.Invoke(CurrentWorker);
            }
            else
            {
                string userCache = PlayerPrefs.GetString("CachedWorker_" + firebaseUid, "");
                if (string.IsNullOrEmpty(userCache))
                {
                    string mappedId = PlayerPrefs.GetString("UIDToWorkerId_" + firebaseUid, PlayerPrefs.GetString("LastWorkerId", ""));
                    if (!string.IsNullOrEmpty(mappedId))
                        userCache = PlayerPrefs.GetString("CachedWorker_" + mappedId, "");
                }
                if (string.IsNullOrEmpty(userCache)) userCache = PlayerPrefs.GetString("CachedWorker", "");

                if (!string.IsNullOrEmpty(userCache))
                {
                    var cached = JsonUtility.FromJson<WorkerData>(userCache);
                    if (cached != null)
                    {
                        cached.firebaseUid = firebaseUid;
                        CurrentWorker = cached;
                        LoadProgressFromCache(firebaseUid);
                        LoadAttemptsLocally(CurrentWorker.id);
                        RecomputeWorkerStatsFromMap();
                        OnWorkerLoaded?.Invoke(CurrentWorker);
                    }
                }
            }

            Firebase.FirestoreService.Instance.GetWorker(firebaseUid, (ok, json) =>
            {
                if (!ok || string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning($"[AppDataService] Firestore load failed for {firebaseUid} (offline or unavailable), using local cache");
                    
                    if (CurrentWorker == null || CurrentWorker.id == "NEW" || CurrentWorker.id == "WORKER")
                    {
                        CurrentWorker = CreateNewWorker(firebaseUid);
                        LoadAttemptsLocally(CurrentWorker.id);
                        CacheWorkerLocally(CurrentWorker);
                    }

                    LoadProgressFromCache(firebaseUid);
                    if (progressMap.Count == 0)
                    {
                        string mappedId = PlayerPrefs.GetString("UIDToWorkerId_" + firebaseUid, PlayerPrefs.GetString("LastWorkerId", ""));
                        if (!string.IsNullOrEmpty(mappedId)) LoadProgressFromCache(mappedId);
                    }
                    if (progressMap.Count == 0)
                    {
                        InitProgressMapForNewWorker();
                    }

                    RecomputeWorkerStatsFromMap();
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
                // Load certificates from Firestore subcollection & local
                LoadCertificatesFromFirestore(firebaseUid);
                CacheWorkerLocally(CurrentWorker);

                // Auto-sync any offline pending data to cloud
                SyncPendingDataToCloud();
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
                            emergencyResponse = Firebase.FirestoreService.GetintValue(csFields, "emergencyResponse"),
                            timeManagement = Firebase.FirestoreService.GetintValue(csFields, "timeManagement"),
                            quizScore = Firebase.FirestoreService.GetintValue(csFields, "quizScore")
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
                            prog.competencyScores.timeManagement = Mathf.Max(prog.competencyScores.timeManagement, localProg.competencyScores.timeManagement);
                            prog.competencyScores.quizScore = Mathf.Max(prog.competencyScores.quizScore, localProg.competencyScores.quizScore);
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
            if (string.IsNullOrEmpty(firebaseUid) || prog == null) return;
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
                    { "emergencyResponse", prog.competencyScores.emergencyResponse },
                    { "timeManagement", prog.competencyScores.timeManagement },
                    { "quizScore", prog.competencyScores.quizScore }
                };
            }
            string flatJson = MiniJSON.Json.Serialize(data);
            bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;
            if (isOnline && Firebase.FirestoreService.Instance != null)
            {
                Firebase.FirestoreService.Instance.SaveModuleProgress(firebaseUid, moduleId, flatJson, (ok, resp) =>
                {
                    if (ok) RemovePendingModule(moduleId);
                    else QueuePendingModule(moduleId);
                });
            }
            else
            {
                QueuePendingModule(moduleId);
            }
        }

        void InitProgressMapForNewWorker()
        {
            progressMap.Clear();
            var all = GetAllModules();
            foreach (var m in all)
            {
                progressMap[m.id] = new ModuleProgress
                {
                    moduleId = m.id,
                    status = m.id == "heights_safety" || m.id.StartsWith("heights_safety_") ? ModuleStatus.Locked : ModuleStatus.NotStarted,
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

            var all = GetAllModules();
            var subModules = all.FindAll(m => !string.IsNullOrEmpty(m.parentId));
            int totalSubProgress = 0;
            int totalSubCount = Mathf.Max(1, subModules.Count); // 25 sub-modules

            foreach (var sub in subModules)
            {
                if (progressMap.TryGetValue(sub.id, out var prog))
                {
                    totalSubProgress += prog.progress;
                }
            }

            // Overall progress is percentage of curriculum completed across all 25 sub-modules
            CurrentWorker.overallProgress = Mathf.Clamp(totalSubProgress / totalSubCount, 0, 100);

            var uniqueCertIds = new HashSet<string>();
            if (dynamicCertificates != null)
            {
                foreach (var c in dynamicCertificates)
                {
                    if (!string.IsNullOrEmpty(c.id)) uniqueCertIds.Add(c.id);
                }
            }
            foreach (var kv in progressMap)
            {
                if (kv.Value.status == ModuleStatus.Completed && !string.IsNullOrEmpty(kv.Value.certificateId))
                    uniqueCertIds.Add(kv.Value.certificateId);
            }

            CurrentWorker.certificatesEarned = uniqueCertIds.Count;
            CurrentWorker.totalAttempts = allAttempts != null ? allAttempts.Count : 0;
        }

        // ================================================================
        // MODULE DEFINITIONS
        // ================================================================

        public ModuleData GetModule(string id)
        {
            var all = GetAllModules();
            return all != null ? all.Find(m => m.id == id) : null;
        }

        public List<ModuleData> GetAllModules()
        {
            // Force load from Resources in case the wrong one is assigned in the Inspector
            if (moduleDatabase == null) 
            {
                moduleDatabase = Resources.Load<ModuleDatabase>("Data/ModuleDatabase");
            }
            var list = moduleDatabase != null ? moduleDatabase.GetAll() : new List<ModuleData>();
            EnsureSubModulesLoaded(list);
            return list;
        }

        private void EnsureSubModulesLoaded(List<ModuleData> list)
        {
            if (list == null) return;
            if (list.Exists(m => !string.IsNullOrEmpty(m.parentId))) return;

            var categories = new Dictionary<string, string[]>
            {
                { "fire_safety", new[] { "Fire Extinguisher Protocol", "High-Voltage Panel Arc Flash", "Suspended Coal Dust Ignition", "Hydraulic Fluid Spill Fire", "Methane Gas Pocket Ignition" } },
                { "gas_safety", new[] { "Hydrogen Sulfide (H2S) Sump Leak", "Methane Pocket Strike During Drilling", "Blackdamp Accumulation in Abandoned Shaft", "Unprepared Confined Space Entry Rescue", "Diesel Exhaust Inhalation" } },
                { "electrical_safety", new[] { "Unshored Trench Wall Cave-In", "Room-and-Pillar Roof Collapse", "Overloaded Scaffolding Collapse", "Material Silo Structural Rupture", "Open-Pit Highwall Landslide" } },
                { "machinery_safety", new[] { "Dump Truck Blind Spot Crushing", "Excavator Rollover on Uneven Terrain", "Conveyor Belt Entanglement", "Haul Truck Brake Failure on Incline", "Suspended Crane Load Drop" } },
                { "heights_safety", new[] { "Fall from Unprotected Highwall Edge", "Slip on Oil-Coated Walkway", "Broken Rung Ladder Fall", "Plunge through Unmarked Floor Opening", "Trip over Unsecured Power Cables" } }
            };

            var emojis = new Dictionary<string, string> {
                { "fire_safety", "🔥" }, { "gas_safety", "☠️" }, { "electrical_safety", "🏗️" }, { "machinery_safety", "⚙️" }, { "heights_safety", "⚠️" }
            };

            var domains = new Dictionary<string, string> {
                { "fire_safety", "Fire Safety" }, { "gas_safety", "Gas/Hazmat" }, { "electrical_safety", "Structural" }, { "machinery_safety", "Machinery" }, { "heights_safety", "Slips & Falls" }
            };

            var colors = new Dictionary<string, string> {
                { "fire_safety", "#FFCDD2" }, { "gas_safety", "#E1BEE7" }, { "electrical_safety", "#FFE0B2" }, { "machinery_safety", "#B3E5FC" }, { "heights_safety", "#C8E6C9" }
            };

            foreach (var kvp in categories)
            {
                string parentId = kvp.Key;
                string[] subNames = kvp.Value;
                for (int i = 0; i < subNames.Length; i++)
                {
                    string subId = $"{parentId}_sub{i + 1}";
                    if (!list.Exists(m => m.id == subId))
                    {
                        list.Add(new ModuleData
                        {
                            id = subId,
                            parentId = parentId,
                            title = subNames[i],
                            iconEmoji = emojis.ContainsKey(parentId) ? emojis[parentId] : "⚡",
                            domain = domains.ContainsKey(parentId) ? domains[parentId] : "Safety",
                            duration = "15 min",
                            difficulty = "Medium",
                            status = ModuleStatus.NotStarted,
                            color = colors.ContainsKey(parentId) ? colors[parentId] : "#ECEFF1",
                            description = $"Learn how to handle {subNames[i].ToLower()} safely and efficiently in an interactive AR environment.",
                            objectives = new[] { "Identify hazards", "Apply correct protocols", "Evacuate safely" }
                        });
                    }
                }
            }
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
                    id = m.id, parentId = m.parentId, title = m.title, iconEmoji = m.iconEmoji, domain = m.domain, duration = m.duration, difficulty = m.difficulty,
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
                result.Add(copy);
            }

            // Group by Main Modules
            var mainModules = result.FindAll(m => string.IsNullOrEmpty(m.parentId));
            foreach (var main in mainModules)
            {
                var subs = result.FindAll(m => m.parentId == main.id);
                if (subs.Count > 0)
                {
                    // Compute parent module progress dynamically based on sub-modules
                    int subTotal = 0;
                    foreach (var s in subs) subTotal += s.progress;
                    main.progress = subTotal / subs.Count;

                    bool allSubsCompleted = subs.TrueForAll(s => s.status == ModuleStatus.Completed);
                    if (allSubsCompleted)
                    {
                        main.status = ModuleStatus.Completed;
                        main.progress = 100;
                    }
                    else if (subs.Exists(s => s.status == ModuleStatus.Completed || s.status == ModuleStatus.InProgress || s.progress > 0))
                    {
                        main.status = ModuleStatus.InProgress;
                        main.certificateId = "";
                        var mainProgress = GetModuleProgress(main.id);
                        if (mainProgress != null)
                        {
                            mainProgress.status = ModuleStatus.InProgress;
                            mainProgress.progress = main.progress;
                            mainProgress.certificateId = "";
                        }
                    }
                    else
                    {
                        if (main.status != ModuleStatus.Locked)
                            main.status = ModuleStatus.NotStarted;
                        main.progress = 0;
                    }

                    // Enforce Sequential Locking on sub-modules
                    for (int i = 1; i < subs.Count; i++)
                    {
                        var prevSub = subs[i - 1];
                        var currSub = subs[i];
                        if (prevSub.status != ModuleStatus.Completed)
                        {
                            currSub.status = ModuleStatus.Locked;
                        }
                        else if (currSub.status == ModuleStatus.Locked)
                        {
                            currSub.status = ModuleStatus.NotStarted;
                        }
                    }
                }
            }

            // Enforce Sequential Locking on Main Modules
            for (int i = 1; i < mainModules.Count; i++)
            {
                var prevMain = mainModules[i - 1];
                var currMain = mainModules[i];
                if (prevMain.status != ModuleStatus.Completed)
                {
                    currMain.status = ModuleStatus.Locked;
                    
                    // Also lock all its sub-modules
                    var currSubs = result.FindAll(m => m.parentId == currMain.id);
                    foreach (var sub in currSubs)
                    {
                        sub.status = ModuleStatus.Locked;
                    }
                }
                else if (currMain.status == ModuleStatus.Locked)
                {
                    currMain.status = ModuleStatus.NotStarted;
                    
                    // Unlock the first sub-module if any
                    var currSubs = result.FindAll(m => m.parentId == currMain.id);
                    if (currSubs.Count > 0 && currSubs[0].status == ModuleStatus.Locked)
                    {
                        currSubs[0].status = ModuleStatus.NotStarted;
                    }
                }
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

        /// <summary>
        /// Update competency scores for a module from real AR drill performance (not the quiz).
        /// Each percentage is computed by FireSafetyModuleManager from actual gameplay signals —
        /// see documents/technical_scoring_explained.md §3.9. Merges with existing scores using
        /// best-score logic, same convention as UpdateModuleCompetencyScores above.
        /// </summary>
        public void UpdateModuleCompetencyScoresFromDrill(string moduleId, int hazardRecognitionPct, int extinguisherUsePct, int timeManagementPct, int evacuationPct, int quizScorePct)
        {
            if (string.IsNullOrEmpty(moduleId)) return;
            if (CurrentWorker == null)
            {
                Debug.LogWarning($"[AppDataService] UpdateModuleCompetencyScoresFromDrill({moduleId}) — no CurrentWorker, skipped (Playing the scene directly with no signed-in worker).");
                return;
            }
            var prog = GetModuleProgress(moduleId);
            if (prog == null) return;
            if (prog.competencyScores == null) prog.competencyScores = new CompetencyScores();

            prog.competencyScores.hazardRecognition = Mathf.Max(prog.competencyScores.hazardRecognition, hazardRecognitionPct);
            prog.competencyScores.extinguisherUse = Mathf.Max(prog.competencyScores.extinguisherUse, extinguisherUsePct);
            prog.competencyScores.timeManagement = Mathf.Max(prog.competencyScores.timeManagement, timeManagementPct);
            prog.competencyScores.evacuation = Mathf.Max(prog.competencyScores.evacuation, evacuationPct);
            prog.competencyScores.quizScore = Mathf.Max(prog.competencyScores.quizScore, quizScorePct);

            SaveModuleProgressToFirestore(CurrentWorker.firebaseUid, moduleId, prog);
            PlayerPrefs.SetString("ProgressMap_" + CurrentWorker.firebaseUid, ProgressMapToJson());
            PlayerPrefs.Save();
        }

        public List<ModuleData> GetModulesByStatusDynamic(ModuleStatus status) => GetAllModulesWithProgress().FindAll(m => m.status == status && string.IsNullOrEmpty(m.parentId));
        public List<ModuleData> GetModulesByStatus(ModuleStatus status) => GetModulesByStatusDynamic(status);

        public List<ModuleData> GetSubModules(string parentId)
        {
            return GetAllModules().FindAll(m => m.parentId == parentId);
        }

        public List<ModuleData> GetSubModulesWithProgress(string parentId)
        {
            var all = GetAllModulesWithProgress();
            var filtered = all.FindAll(m => m.parentId == parentId);
            UnityEngine.Debug.Log($"[Nav] GetSubModulesWithProgress({parentId}) - Total modules: {all.Count}, Filtered: {filtered.Count}");
            foreach (var m in all) {
                if (m.id.Contains("sub")) UnityEngine.Debug.Log($"[Nav] Found sub-module: {m.id} with parentId: '{m.parentId}'");
            }
            return filtered;
        }

        // ================================================================
        // QUESTIONS
        // ================================================================

        public List<QuizQuestionData> GetQuestions(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return new List<QuizQuestionData>();
            if (questionDatabase == null) questionDatabase = Resources.Load<QuestionDatabase>("Data/QuestionDatabase");
            if (questionDatabase != null)
            {
                if (moduleId.StartsWith("fire_safety_sub"))
                {
                    return questionDatabase.GetForModule("fire_safety");
                }
                return questionDatabase.GetForModule(moduleId);
            }
            return new List<QuizQuestionData>();
        }

        List<CertificateData> dynamicCertificates = new List<CertificateData>();

        // ================================================================
        // CERTIFICATES
        // ================================================================

        public CertificateData GetCertificate(string certIdOrModuleId)
        {
            if (string.IsNullOrEmpty(certIdOrModuleId)) return null;

            // 1. Try matching by certificate ID
            var found = dynamicCertificates.FirstOrDefault(c => string.Equals(c.id, certIdOrModuleId, System.StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;

            // 2. Try matching by moduleId in dynamicCertificates
            found = dynamicCertificates.FirstOrDefault(c => string.Equals(c.moduleId, certIdOrModuleId, System.StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;

            // 3. Try checking module progress for certificateId
            if (progressMap.TryGetValue(certIdOrModuleId, out var prog) && !string.IsNullOrEmpty(prog.certificateId))
            {
                found = dynamicCertificates.FirstOrDefault(c => string.Equals(c.id, prog.certificateId, System.StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }

            // 4. Try matching from static database
            if (certificateDatabase == null) certificateDatabase = Resources.Load<CertificateDatabase>("Data/CertificateDatabase");
            if (certificateDatabase != null)
            {
                var staticCert = certificateDatabase.GetById(certIdOrModuleId);
                if (staticCert != null) return staticCert;

                var workerCerts = CurrentWorker != null ? certificateDatabase.GetByWorker(CurrentWorker.id) : certificateDatabase.GetAll();
                if (workerCerts != null)
                {
                    var matchMod = workerCerts.FirstOrDefault(c => string.Equals(c.moduleId, certIdOrModuleId, System.StringComparison.OrdinalIgnoreCase));
                    if (matchMod != null) return matchMod;
                }
            }

            return null;
        }

        public List<CertificateData> GetWorkerCertificates()
        {
            var result = new List<CertificateData>();
            if (CurrentWorker != null)
            {
                result.AddRange(dynamicCertificates.Where(c => string.IsNullOrEmpty(c.workerId) || c.workerId == CurrentWorker.id));
                if (certificateDatabase == null) certificateDatabase = Resources.Load<CertificateDatabase>("Data/CertificateDatabase");
                if (certificateDatabase != null)
                {
                    foreach (var c in certificateDatabase.GetByWorker(CurrentWorker.id))
                    {
                        if (!result.Any(r => r.id == c.id)) result.Add(c);
                    }
                }
            }
            else
            {
                result.AddRange(dynamicCertificates);
            }
            return result;
        }

        public void SaveCertificate(CertificateData cert)
        {
            if (cert == null || string.IsNullOrEmpty(cert.id)) return;
            dynamicCertificates.RemoveAll(c => c.id == cert.id);
            dynamicCertificates.Add(cert);
            SaveCertificatesLocally();

            bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;
            if (isOnline && CurrentWorker != null && !string.IsNullOrEmpty(CurrentWorker.firebaseUid) && Firebase.FirestoreService.Instance != null)
            {
                string certJson = JsonUtility.ToJson(cert);
                Firebase.FirestoreService.Instance.SaveCertificateToFirestore(CurrentWorker.firebaseUid, cert.id, certJson, (ok, resp) =>
                {
                    if (ok)
                    {
                        Debug.Log($"[AppDataService] Certificate Cloud Sync SUCCESS: {cert.id}");
                        RemovePendingCertificate(cert.id);
                    }
                    else
                    {
                        Debug.LogWarning($"[AppDataService] Certificate Cloud Sync FAIL (queued for retry): {cert.id}");
                        QueuePendingCertificate(cert.id);
                    }
                });
            }
            else
            {
                // OFFLINE: Queue certificate for sync when online
                QueuePendingCertificate(cert.id);
                Debug.Log($"[AppDataService] Certificate saved locally (OFFLINE). Queued for sync: {cert.id}");
            }
        }

        void SaveCertificatesLocally()
        {
            if (CurrentWorker == null) return;
            var wrapper = new CertificateListWrapper { certificates = dynamicCertificates };
            PlayerPrefs.SetString("Certificates_" + CurrentWorker.id, JsonUtility.ToJson(wrapper));
            PlayerPrefs.Save();
        }

        public void LoadCertificatesLocally(string workerId)
        {
            dynamicCertificates.Clear();
            if (string.IsNullOrEmpty(workerId)) return;
            string json = PlayerPrefs.GetString("Certificates_" + workerId, "");
            if (!string.IsNullOrEmpty(json))
            {
                var wrapper = JsonUtility.FromJson<CertificateListWrapper>(json);
                if (wrapper != null && wrapper.certificates != null)
                {
                    dynamicCertificates = wrapper.certificates;
                }
            }
        }

        public void LoadCertificatesFromFirestore(string firebaseUid)
        {
            if (string.IsNullOrEmpty(firebaseUid)) return;
            if (CurrentWorker != null) LoadCertificatesLocally(CurrentWorker.id);

            Firebase.FirestoreService.Instance.GetAllCertificates(firebaseUid, (ok, docs) =>
            {
                if (!ok || docs == null || docs.Count == 0) return;

                bool changed = false;
                foreach (var doc in docs)
                {
                    var fields = doc.ContainsKey("fields") ? doc["fields"] as Dictionary<string, object> : doc;
                    if (fields == null) continue;

                    string certId = Firebase.FirestoreService.GetstringValue(fields, "id");
                    if (string.IsNullOrEmpty(certId))
                    {
                        string docName = doc.ContainsKey("name") ? doc["name"] as string : "";
                        certId = docName.Contains("/") ? docName.Split('/')[^1] : "";
                    }
                    if (string.IsNullOrEmpty(certId)) continue;

                    if (!dynamicCertificates.Any(c => c.id == certId))
                    {
                        var cert = new CertificateData
                        {
                            id = certId,
                            workerName = Firebase.FirestoreService.GetstringValue(fields, "workerName"),
                            workerId = Firebase.FirestoreService.GetstringValue(fields, "workerId"),
                            moduleId = Firebase.FirestoreService.GetstringValue(fields, "moduleId"),
                            moduleTitle = Firebase.FirestoreService.GetstringValue(fields, "moduleTitle"),
                            score = Firebase.FirestoreService.GetintValue(fields, "score"),
                            issuedDate = Firebase.FirestoreService.GetstringValue(fields, "issuedDate"),
                            expiryDate = Firebase.FirestoreService.GetstringValue(fields, "expiryDate"),
                            organization = Firebase.FirestoreService.GetstringValue(fields, "organization"),
                            status = Firebase.FirestoreService.GetstringValue(fields, "status"),
                            signatureHash = Firebase.FirestoreService.GetstringValue(fields, "signatureHash"),
                            verificationUrl = Firebase.FirestoreService.GetstringValue(fields, "verificationUrl")
                        };
                        dynamicCertificates.Add(cert);
                        changed = true;
                    }
                }

                if (changed)
                {
                    SaveCertificatesLocally();
                    Debug.Log($"[AppDataService] Merged {dynamicCertificates.Count} certificates from Firestore to local cache.");
                }
            });
        }

        // ================================================================
        // OFFLINE SYNC QUEUE MANAGEMENT
        // ================================================================

        public void QueuePendingCertificate(string certId)
        {
            if (string.IsNullOrEmpty(certId) || CurrentWorker == null) return;
            var queue = LoadPendingQueue(CurrentWorker.id);
            if (!queue.pendingCertificateIds.Contains(certId))
            {
                queue.pendingCertificateIds.Add(certId);
                SavePendingQueue(CurrentWorker.id, queue);
                Debug.Log($"[AppDataService] Queued certificate for cloud sync: {certId}");
            }
        }

        public void RemovePendingCertificate(string certId)
        {
            if (string.IsNullOrEmpty(certId) || CurrentWorker == null) return;
            var queue = LoadPendingQueue(CurrentWorker.id);
            if (queue.pendingCertificateIds.Remove(certId))
            {
                SavePendingQueue(CurrentWorker.id, queue);
            }
        }

        public void QueuePendingAttempt(string resultId)
        {
            if (string.IsNullOrEmpty(resultId) || CurrentWorker == null) return;
            var queue = LoadPendingQueue(CurrentWorker.id);
            if (!queue.pendingAttemptIds.Contains(resultId))
            {
                queue.pendingAttemptIds.Add(resultId);
                SavePendingQueue(CurrentWorker.id, queue);
            }
        }

        public void RemovePendingAttempt(string resultId)
        {
            if (string.IsNullOrEmpty(resultId) || CurrentWorker == null) return;
            var queue = LoadPendingQueue(CurrentWorker.id);
            if (queue.pendingAttemptIds.Remove(resultId))
            {
                SavePendingQueue(CurrentWorker.id, queue);
            }
        }

        public void QueuePendingModule(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId) || CurrentWorker == null) return;
            var queue = LoadPendingQueue(CurrentWorker.id);
            if (!queue.pendingModuleIds.Contains(moduleId))
            {
                queue.pendingModuleIds.Add(moduleId);
                SavePendingQueue(CurrentWorker.id, queue);
            }
        }

        public void RemovePendingModule(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId) || CurrentWorker == null) return;
            var queue = LoadPendingQueue(CurrentWorker.id);
            if (queue.pendingModuleIds.Remove(moduleId))
            {
                SavePendingQueue(CurrentWorker.id, queue);
            }
        }

        PendingSyncQueue LoadPendingQueue(string workerId)
        {
            if (string.IsNullOrEmpty(workerId)) return new PendingSyncQueue();
            string json = PlayerPrefs.GetString("PendingSyncQueue_" + workerId, "");
            if (string.IsNullOrEmpty(json)) return new PendingSyncQueue();
            try
            {
                var q = JsonUtility.FromJson<PendingSyncQueue>(json);
                return q ?? new PendingSyncQueue();
            }
            catch
            {
                return new PendingSyncQueue();
            }
        }

        void SavePendingQueue(string workerId, PendingSyncQueue queue)
        {
            if (string.IsNullOrEmpty(workerId) || queue == null) return;
            string json = JsonUtility.ToJson(queue);
            PlayerPrefs.SetString("PendingSyncQueue_" + workerId, json);
            PlayerPrefs.Save();
        }

        // ================================================================
        // CLOUD SYNC FLUSH WORKER
        // ================================================================

        private bool isSyncingPending = false;

        public void TriggerCloudSync() => SyncPendingDataToCloud();

        public void SyncPendingDataToCloud()
        {
            if (isSyncingPending) return;
            if (Application.internetReachability == NetworkReachability.NotReachable) return;
            if (CurrentWorker == null || string.IsNullOrEmpty(CurrentWorker.firebaseUid) || Firebase.FirestoreService.Instance == null) return;

            StartCoroutine(SyncPendingCoroutine());
        }

        IEnumerator SyncPendingCoroutine()
        {
            isSyncingPending = true;
            string uid = CurrentWorker.firebaseUid;
            string workerId = CurrentWorker.id;
            var queue = LoadPendingQueue(workerId);

            // 1. Sync pending certificates
            if (queue.pendingCertificateIds != null && queue.pendingCertificateIds.Count > 0)
            {
                var certIds = queue.pendingCertificateIds.ToList();
                foreach (var certId in certIds)
                {
                    var cert = dynamicCertificates.Find(c => c.id == certId);
                    if (cert != null)
                    {
                        string json = JsonUtility.ToJson(cert);
                        bool done = false;
                        bool success = false;
                        Firebase.FirestoreService.Instance.SaveCertificateToFirestore(uid, cert.id, json, (ok, resp) =>
                        {
                            success = ok;
                            done = true;
                        });
                        yield return new WaitUntil(() => done);
                        if (success)
                        {
                            RemovePendingCertificate(certId);
                            Debug.Log($"[AppDataService] Offline certificate synced successfully to Firestore: {certId}");
                        }
                    }
                    else
                    {
                        RemovePendingCertificate(certId);
                    }
                    yield return new WaitForSeconds(0.1f);
                }
            }

            // 2. Sync pending quiz attempts
            var unsyncedAttempts = allAttempts.Where(a => !a.synced).ToList();
            foreach (var attempt in unsyncedAttempts)
            {
                string json = JsonUtility.ToJson(attempt);
                bool done = false;
                bool success = false;
                Firebase.FirestoreService.Instance.SaveTrainingResult(uid, attempt.resultId, json, (ok, resp) =>
                {
                    success = ok;
                    done = true;
                });
                yield return new WaitUntil(() => done);
                if (success)
                {
                    attempt.synced = true;
                    SaveAttemptsLocally();
                    RemovePendingAttempt(attempt.resultId);
                    Debug.Log($"[AppDataService] Offline attempt synced successfully to Firestore: {attempt.resultId}");
                }
                yield return new WaitForSeconds(0.1f);
            }

            // 3. Sync pending module progress
            if (queue.pendingModuleIds != null && queue.pendingModuleIds.Count > 0)
            {
                var moduleIds = queue.pendingModuleIds.ToList();
                foreach (var modId in moduleIds)
                {
                    if (progressMap.TryGetValue(modId, out var prog))
                    {
                        bool done = false;
                        bool success = false;
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
                        if (prog.competencyScores != null)
                        {
                            data["competencyScores"] = new Dictionary<string, object>
                            {
                                { "hazardRecognition", prog.competencyScores.hazardRecognition },
                                { "extinguisherUse", prog.competencyScores.extinguisherUse },
                                { "ppeSelection", prog.competencyScores.ppeSelection },
                                { "evacuation", prog.competencyScores.evacuation },
                                { "emergencyResponse", prog.competencyScores.emergencyResponse },
                                { "timeManagement", prog.competencyScores.timeManagement },
                                { "quizScore", prog.competencyScores.quizScore }
                            };
                        }
                        string flatJson = MiniJSON.Json.Serialize(data);
                        Firebase.FirestoreService.Instance.SaveModuleProgress(uid, modId, flatJson, (ok, resp) =>
                        {
                            success = ok;
                            done = true;
                        });
                        yield return new WaitUntil(() => done);
                        if (success)
                        {
                            RemovePendingModule(modId);
                        }
                    }
                    else
                    {
                        RemovePendingModule(modId);
                    }
                    yield return new WaitForSeconds(0.1f);
                }
            }

            isSyncingPending = false;
        }

        IEnumerator AutoSyncRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(20f);
                if (Application.internetReachability != NetworkReachability.NotReachable &&
                    CurrentWorker != null && !string.IsNullOrEmpty(CurrentWorker.firebaseUid))
                {
                    var queue = LoadPendingQueue(CurrentWorker.id);
                    bool hasUnsyncedAttempts = allAttempts != null && allAttempts.Any(a => !a.synced);
                    if ((queue.pendingCertificateIds != null && queue.pendingCertificateIds.Count > 0) ||
                        (queue.pendingModuleIds != null && queue.pendingModuleIds.Count > 0) ||
                        hasUnsyncedAttempts)
                    {
                        SyncPendingDataToCloud();
                    }
                }
            }
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
            => SaveAttempt(moduleId, score, passed, mistakesCount: 0, completionTimeSeconds: 0f, stepMetrics: null);

        /// <summary>
        /// Full-data overload — carries real mistake count / completion time / per-step metrics
        /// through to the saved TrainingResult, instead of the 3-arg overload's zeros.
        /// See documents/technical_scoring_explained.md §5.1.
        /// </summary>
        public void SaveAttempt(string moduleId, int score, bool passed, int mistakesCount, float completionTimeSeconds, List<StepMetric> stepMetrics)
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
                mistakesCount = mistakesCount,
                completionTimeSeconds = completionTimeSeconds,
                stepMetrics = stepMetrics ?? new List<StepMetric>(),
                timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                synced = false
            };

            allAttempts.Add(result);
            SaveAttemptsLocally();

            // Save to Firestore under worker's subcollection — if online, save immediately. If offline, queue for sync.
            bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;
            if (isOnline && CurrentWorker != null && !string.IsNullOrEmpty(CurrentWorker.firebaseUid) && Firebase.FirestoreService.Instance != null)
            {
                string json = JsonUtility.ToJson(result);
                Firebase.FirestoreService.Instance.SaveTrainingResult(CurrentWorker.firebaseUid, result.resultId, json,
                    (ok, resp) =>
                    {
                        if (ok)
                        {
                            result.synced = true;
                            SaveAttemptsLocally();
                            RemovePendingAttempt(result.resultId);
                            Debug.Log($"[AppDataService] Attempt saved to cloud: {moduleId} {score}%");
                        }
                        else
                        {
                            QueuePendingAttempt(result.resultId);
                            Debug.LogWarning($"[AppDataService] Attempt cloud save failed (queued): {result.resultId}");
                        }
                    });
            }
            else
            {
                QueuePendingAttempt(result.resultId);
                Debug.Log($"[AppDataService] Attempt saved locally (OFFLINE). Queued for sync: {result.resultId}");
            }

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
                
                // Award certificate when 1st sub-module of Fire Extinguisher (fire_safety_sub1 / fire_safety) or a sub-module is completed
                bool isFireSub1 = moduleId == "fire_safety_sub1" || moduleId == "fire_safety";
                bool isFinalSub5 = moduleId.EndsWith("_sub5");

                if (passed && string.IsNullOrEmpty(prog.certificateId) && (isFireSub1 || isFinalSub5))
                {
                    var modDef = GetModule(moduleId);
                    string parentId = modDef != null && !string.IsNullOrEmpty(modDef.parentId) ? modDef.parentId : moduleId;
                    string codePrefix = parentId.ToUpper().Replace("_", "");
                    if (codePrefix.Length > 4) codePrefix = codePrefix.Substring(0, 4);

                    string certId = $"JH-{codePrefix}-{Random.Range(100000, 999999)}";
                    prog.certificateId = certId;

                    // If it's a sub-module, also mirror the certificate to the parent module
                    if (modDef != null && !string.IsNullOrEmpty(modDef.parentId))
                    {
                        var parentProg = GetModuleProgress(modDef.parentId);
                        if (parentProg != null && string.IsNullOrEmpty(parentProg.certificateId))
                        {
                            parentProg.certificateId = certId;
                        }
                    }

                    string modTitle = modDef != null ? modDef.title : "Fire Extinguisher Protocol";
                    if (isFireSub1)
                        modTitle = "Fire & Explosion Response - Fire Extinguisher Protocol";

                    string workerName = CurrentWorker != null ? CurrentWorker.name : "Mining Worker";
                    string workerId = CurrentWorker != null ? CurrentWorker.id : "WORKER";
                    string org = CurrentWorker != null ? CurrentWorker.organization : "DGMS Certified Mining Org";
                    string nowStr = System.DateTime.UtcNow.ToString("yyyy-MM-dd");
                    string expStr = System.DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd");
                    string veriUrl = $"https://cert-veri.web.app/verify?cert={certId}";

                    // Compute HMAC-SHA256 signature for verification portal compatibility
                    string rawPayload = $"{certId}|{workerId}|{moduleId}|{score}|{nowStr}";
                    string sigHash = "";
                    try
                    {
                        using (var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("DGMS_MINING_SAFETY_SECRET_KEY_2026")))
                        {
                            byte[] hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawPayload));
                            sigHash = System.BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                        }
                    }
                    catch
                    {
                        sigHash = System.Guid.NewGuid().ToString("N");
                    }

                    var certData = new CertificateData
                    {
                        id = certId,
                        workerName = workerName,
                        workerId = workerId,
                        moduleId = moduleId,
                        moduleTitle = modTitle,
                        score = score,
                        issuedDate = nowStr,
                        expiryDate = expStr,
                        organization = org,
                        status = "VALID",
                        signatureHash = sigHash,
                        verificationUrl = veriUrl
                    };

                    SaveCertificate(certData);
                    Debug.Log($"[AppDataService] Auto-awarded Certificate for {moduleId}: {certId} ({modTitle})");
                }

                // Save this module's progress to its own Firestore document — skipped when
                // there's no signed-in worker (e.g. Playing directly rather than through Login).
                if (CurrentWorker != null)
                {
                    SaveModuleProgressToFirestore(CurrentWorker.firebaseUid, moduleId, prog);
                }
            }

            RecomputeWorkerStatsFromMap();
            SaveWorkerProfile();
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
            if (!string.IsNullOrEmpty(CurrentWorker.id))
            {
                PlayerPrefs.SetString("ProgressMap_" + CurrentWorker.id, ProgressMapToJson());
            }
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
            if (worker == null) return;
            string json = JsonUtility.ToJson(worker);
            PlayerPrefs.SetString("CachedWorker", json);
            if (!string.IsNullOrEmpty(worker.firebaseUid))
                PlayerPrefs.SetString("CachedWorker_" + worker.firebaseUid, json);
            if (!string.IsNullOrEmpty(worker.id))
                PlayerPrefs.SetString("CachedWorker_" + worker.id, json);
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
                LoadCertificatesLocally(worker.id);
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
            string workerId = "WORKER";
            string workerName = "Worker";
            string organization = "Mining Safety Corp";
            string sector = "Underground Operations";

            if (firebaseUid == "demo_worker_01")
            {
                workerId = "DEMO-001";
                workerName = "Demo Worker";
                organization = "Dhanbad Coalfields Ltd";
                sector = "Underground Coal Mining";
            }
            else if (!string.IsNullOrEmpty(firebaseUid) && firebaseUid.StartsWith("offline_"))
            {
                workerId = firebaseUid.Substring("offline_".Length);
                workerName = PlayerPrefs.GetString("WorkerDisplayName_" + workerId, workerId);
                organization = PlayerPrefs.GetString("WorkerOrg_" + workerId, "Mining Safety Corp");
                sector = PlayerPrefs.GetString("WorkerSector_" + workerId, "Underground Operations");
            }
            else
            {
                string storedWorkerId = PlayerPrefs.GetString("UIDToWorkerId_" + firebaseUid, PlayerPrefs.GetString("LastWorkerId", "WORKER"));
                workerId = storedWorkerId;
                workerName = PlayerPrefs.GetString("WorkerDisplayName_" + storedWorkerId, storedWorkerId);
                organization = PlayerPrefs.GetString("WorkerOrg_" + storedWorkerId, "Mining Safety Corp");
                sector = PlayerPrefs.GetString("WorkerSector_" + storedWorkerId, "Underground Operations");
            }

            return new WorkerData
            {
                firebaseUid = firebaseUid,
                id = workerId,
                name = workerName,
                organization = organization,
                sector = sector,
                phone = "",
                language = "English",
                joinDate = System.DateTime.UtcNow.ToString("yyyy-MM-dd"),
                overallProgress = 0,
                certificatesEarned = 0,
                totalAttempts = allAttempts != null ? allAttempts.Count : 0,
                competencyScores = new CompetencyScores()
            };
        }
    }
}
