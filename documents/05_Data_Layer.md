# 05 — Data Layer (Firebase)

## Architecture

```
┌─────────────────────────────────────────┐
│              AppDataService              │
│         (Singleton, DontDestroyOnLoad)   │
├─────────────────────────────────────────┤
│                                          │
│  ┌──────────────┐  ┌──────────────────┐ │
│  │FirebaseAuth  │  │ FirestoreService │ │
│  │Manager       │  │                  │ │
│  │- Login       │  │- GetWorker       │ │
│  │- Register    │  │- SaveWorker      │ │
│  │- Logout      │  │- GetScores       │ │
│  │- CurrentUser │  │- SaveScore       │ │
│  └──────────────┘  │- GetCertificates │ │
│                     │- SaveCertificate │ │
│                     └──────────────────┘ │
│                                          │
│  ┌──────────────────────────────────┐   │
│  │        Local Cache (PlayerPrefs)  │   │
│  │  - Worker profile (JSON string)   │   │
│  │  - Selected language              │   │
│  │  - Last login timestamp           │   │
│  └──────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

## Data Models

### ModuleData.cs
```csharp
namespace MiningSafetyAR.Data
{
    public enum ModuleStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Locked
    }

    [Serializable]
    public class ModuleData
    {
        public string id;
        public string title;
        public string iconEmoji;
        public string domain;
        public string duration;
        public string difficulty;
        public ModuleStatus status;
        public int progress;
        public int bestScore;
        public int attempts;
        public string lastAttempt;
        public string certificateId;
        public string color;
        public string description;
        public string[] objectives;
        public CompetencyScores competencyScores;
    }

    [Serializable]
    public class CompetencyScores
    {
        public int hazardRecognition;
        public int extinguisherUse;
        public int ppeSelection;
        public int evacuation;
        public int emergencyResponse;
    }
}
```

### WorkerData.cs
```csharp
namespace MiningSafetyAR.Data
{
    [Serializable]
    public class WorkerData
    {
        public string firebaseUid;      // Firebase Auth UID
        public string id;               // Worker ID (JH10293)
        public string name;
        public string organization;
        public string sector;
        public string phone;
        public string language;
        public string joinDate;
        public int overallProgress;
        public int certificatesEarned;
        public int totalAttempts;
        public CompetencyScores competencyScores;
    }
}
```

### QuizQuestionData.cs
```csharp
namespace MiningSafetyAR.Data
{
    [Serializable]
    public class QuizQuestionData
    {
        public string id;
        public string moduleId;
        public string textEN;
        public string textHI;
        public string textSAT;
        public string[] optionsEN;
        public string[] optionsHI;
        public string[] optionsSAT;
        public int correctIndex;
        public string competency;
    }
}
```

### CertificateData.cs
```csharp
namespace MiningSafetyAR.Data
{
    [Serializable]
    public class CertificateData
    {
        public string id;
        public string workerName;
        public string workerId;
        public string moduleId;
        public string moduleTitle;
        public int score;
        public string issuedDate;
        public string expiryDate;
        public string organization;
        public string status;
    }
}

### TrainingResult.cs (for Firestore)
```csharp
namespace MiningSafetyAR.Data
{
    [Serializable]
    public class TrainingResult
    {
        public string resultId;
        public string workerId;
        public string moduleId;
        public int score;
        public int maxScore;
        public float percentage;
        public bool passed;
        public int mistakesCount;
        public float completionTimeSeconds;
        public string timestamp;
        public bool synced;
    }
}
```

## Firestore Collections

```
firestore/
├── workers/
│   └── {firebaseUid}/
│       ├── id: "JH10293"
│       ├── name: "Ramesh Kumar"
│       ├── organization: "Jharkhand Steel Works"
│       ├── sector: "Steel Manufacturing"
│       ├── phone: "9876543210"
│       ├── language: "English"
│       ├── joinDate: "2026-01-15"
│       ├── overallProgress: 68
│       ├── certificatesEarned: 2
│       ├── totalAttempts: 7
│       └── competencyScores: { ... }
│
├── trainingResults/
│   └── {resultId}/
│       ├── workerId: "JH10293"
│       ├── moduleId: "fire_safety"
│       ├── score: 85
│       ├── maxScore: 100
│       ├── percentage: 85.0
│       ├── passed: true
│       ├── mistakesCount: 3
│       ├── completionTimeSeconds: 245.5
│       └── timestamp: "2026-08-26T10:30:00Z"
│
├── certificates/
│   └── {certId}/
│       ├── id: "JH-FIRE-001928"
│       ├── workerId: "JH10293"
│       ├── moduleId: "fire_safety"
│       ├── score: 85
│       ├── issuedDate: "2026-08-26"
│       └── expiryDate: "2027-08-26"
│
└── modules/           (static data, optional)
    └── {moduleId}/
        ├── title: "Fire & Explosion Response"
        └── questions: [...]
```

## FirebaseAuthManager.cs

```csharp
using Firebase;
using Firebase.Auth;
using UnityEngine;

namespace MiningSafetyAR.Firebase
{
    public class FirebaseAuthManager : MonoBehaviour
    {
        public static FirebaseAuthManager Instance { get; private set; }

        private FirebaseAuth auth;
        private FirebaseUser currentUser;

        public bool IsLoggedIn => currentUser != null;
        public string CurrentUserId => currentUser?.UserId;
        public string CurrentUserEmail => currentUser?.Email;

        public event System.Action<FirebaseUser> OnLoginSuccess;
        public event System.Action<string> OnLoginFailed;
        public event System.Action OnLogout;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }

        private void InitializeFirebase()
        {
            FirebaseApp.CheckAndFixDependenciesAsync()
                .ContinueWith(task =>
                {
                    if (task.Result == DependencyStatus.Available)
                    {
                        auth = FirebaseAuth.DefaultInstance;
                        Debug.Log("[Firebase] Auth initialized");

                        // Check if already logged in
                        if (auth.CurrentUser != null)
                        {
                            currentUser = auth.CurrentUser;
                            OnLoginSuccess?.Invoke(currentUser);
                        }
                    }
                    else
                    {
                        Debug.LogError($"[Firebase] Dependencies: {task.Result}");
                    }
                });
        }

        /// <summary>
        /// Register new user with email/password.
        /// Email format: {workerId}@miningsafety.app
        /// </summary>
        public void Register(string workerId, string pin,
                             string name, string onSuccessScene)
        {
            string email = $"{workerId}@miningsafety.app";
            string password = $"ARC{pin}2026!"; // PIN-based password

            auth.CreateUserWithEmailAndPasswordAsync(email, password)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        string error = task.Exception?.InnerExceptions
                            .FirstOrDefault()?.Message ?? "Registration failed";
                        MainThreadDispatcher.Enqueue(() =>
                            OnLoginFailed?.Invoke(error));
                        return;
                    }

                    var result = task.Result;
                    currentUser = result.User;

                    // Update display name
                    var profile = new UserProfile { DisplayName = name };
                    currentUser.UpdateUserProfileAsync(profile);

                    MainThreadDispatcher.Enqueue(() =>
                    {
                        Debug.Log($"[Firebase] Registered: {currentUser.UserId}");
                        OnLoginSuccess?.Invoke(currentUser);
                    });
                });
        }

        /// <summary>
        /// Login with worker ID and PIN.
        /// </summary>
        public void Login(string workerId, string pin)
        {
            string email = $"{workerId}@miningsafety.app";
            string password = $"ARC{pin}2026!";

            auth.SignInWithEmailAndPasswordAsync(email, password)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        string error = task.Exception?.InnerExceptions
                            .FirstOrDefault()?.Message ?? "Login failed";
                        MainThreadDispatcher.Enqueue(() =>
                            OnLoginFailed?.Invoke(error));
                        return;
                    }

                    currentUser = task.Result.User;
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        Debug.Log($"[Firebase] Logged in: {currentUser.UserId}");
                        OnLoginSuccess?.Invoke(currentUser);
                    });
                });
        }

        /// <summary>
        /// Quick demo login (creates account if needed).
        /// </summary>
        public void DemoLogin()
        {
            string email = "demo@miningsafety.app";
            string password = "ARCDemo2026!";

            // Try login first
            auth.SignInWithEmailAndPasswordAsync(email, password)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        // Account doesn't exist, create it
                        auth.CreateUserWithEmailAndPasswordAsync(email, password)
                            .ContinueWith(createTask =>
                            {
                                if (createTask.IsFaulted)
                                {
                                    MainThreadDispatcher.Enqueue(() =>
                                        OnLoginFailed?.Invoke("Demo login failed"));
                                    return;
                                }

                                currentUser = createTask.Result.User;
                                var profile = new UserProfile { DisplayName = "Demo Worker" };
                                currentUser.UpdateUserProfileAsync(profile);

                                MainThreadDispatcher.Enqueue(() =>
                                    OnLoginSuccess?.Invoke(currentUser));
                            });
                        return;
                    }

                    currentUser = task.Result.User;
                    MainThreadDispatcher.Enqueue(() =>
                        OnLoginSuccess?.Invoke(currentUser));
                });
        }

        public void Logout()
        {
            auth.SignOut();
            currentUser = null;
            OnLogout?.Invoke();
            Debug.Log("[Firebase] Logged out");
        }
    }
}
```

## FirestoreService.cs

```csharp
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

namespace MiningSafetyAR.Firebase
{
    public class FirestoreService : MonoBehaviour
    {
        public static FirestoreService Instance { get; private set; }

        private const string BASE_URL =
            "https://firestore.googleapis.com/v1/projects/minesafetyar/databases/(default)/documents";

        private string AuthToken => FirebaseAuthManager.Instance?.CurrentUserId;

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

        // --- Worker Operations ---

        public void SaveWorker(WorkerData worker,
                               System.Action onSuccess = null)
        {
            string json = JsonUtility.ToJson(worker);
            string url = $"{BASE_URL}/workers/{worker.firebaseUid}";
            StartCoroutine(PutRequest(url, json, onSuccess));
        }

        public void GetWorker(string firebaseUid,
                               System.Action<WorkerData> callback)
        {
            string url = $"{BASE_URL}/workers/{firebaseUid}";
            StartCoroutine(GetRequest(url, (json) =>
            {
                var worker = JsonUtility.FromJson<WorkerData>(json);
                callback?.Invoke(worker);
            }));
        }

        // --- Training Results ---

        public void SaveTrainingResult(TrainingResult result,
                                        System.Action onSuccess = null)
        {
            string json = JsonUtility.ToJson(result);
            string url = $"{BASE_URL}/trainingResults/{result.resultId}";
            StartCoroutine(PutRequest(url, json, onSuccess));
        }

        public void GetWorkerResults(string workerId,
                                      System.Action<TrainingResult[]> callback)
        {
            string url = $"{BASE_URL}/trainingResults"
                + $"?filter=workerId%3D%3D{workerId}";
            StartCoroutine(GetRequest(url, (json) =>
            {
                // Parse Firestore array response
                var results = ParseTrainingResults(json);
                callback?.Invoke(results);
            }));
        }

        // --- Certificates ---

        public void SaveCertificate(CertificateData cert,
                                     System.Action onSuccess = null)
        {
            string json = JsonUtility.ToJson(cert);
            string url = $"{BASE_URL}/certificates/{cert.id}";
            StartCoroutine(PutRequest(url, json, onSuccess));
        }

        public void GetCertificate(string certId,
                                    System.Action<CertificateData> callback)
        {
            string url = $"{BASE_URL}/certificates/{certId}";
            StartCoroutine(GetRequest(url, (json) =>
            {
                var cert = JsonUtility.FromJson<CertificateData>(json);
                callback?.Invoke(cert);
            }));
        }

        // --- HTTP Helpers ---

        private IEnumerator PutRequest(string url, string json,
                                        System.Action onSuccess)
        {
            using var request = new UnityWebRequest(url, "PUT");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Firestore] PUT OK: {url}");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Firestore] PUT failed: {request.error}");
            }
        }

        private IEnumerator GetRequest(string url,
                                        System.Action<string> callback)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[Firestore] GET failed: {request.error}");
                callback?.Invoke(null);
            }
        }

        private TrainingResult[] ParseTrainingResults(string json)
        {
            // Simplified parsing - adjust based on actual Firestore response format
            try
            {
                var wrapper = JsonUtility.FromJson<TrainingResultListWrapper>(json);
                return wrapper?.results ?? new TrainingResult[0];
            }
            catch
            {
                return new TrainingResult[0];
            }
        }
    }

    [System.Serializable]
    public class TrainingResultListWrapper
    {
        public TrainingResult[] results;
    }
}
```

## AppDataService.cs (Updated for Firebase)

```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MiningSafetyAR.Data
{
    public class AppDataService : MonoBehaviour
    {
        public static AppDataService Instance { get; private set; }

        [SerializeField] private ModuleDatabase moduleDatabase;
        [SerializeField] private QuestionDatabase questionDatabase;
        [SerializeField] private CertificateDatabase certificateDatabase;

        public WorkerData CurrentWorker { get; private set; }

        public event System.Action<WorkerData> OnWorkerLoaded;
        public event System.Action OnWorkerLoggedOut;

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

        private void OnEnable()
        {
            Firebase.FirebaseAuthManager.Instance.OnLoginSuccess
                += OnFirebaseLoginSuccess;
            Firebase.FirebaseAuthManager.Instance.OnLogout
                += OnFirebaseLogout;
        }

        private void OnDisable()
        {
            if (Firebase.FirebaseAuthManager.Instance != null)
            {
                Firebase.FirebaseAuthManager.Instance.OnLoginSuccess
                    -= OnFirebaseLoginSuccess;
                Firebase.FirebaseAuthManager.Instance.OnLogout
                    -= OnFirebaseLogout;
            }
        }

        private async void OnFirebaseLoginSuccess(
            Firebase.FirebaseUser user)
        {
            // Load worker from Firestore
            await LoadWorkerFromFirestore(user.UserId);
        }

        private void OnFirebaseLogout()
        {
            CurrentWorker = null;
            OnWorkerLoggedOut?.Invoke();
        }

        private System.Threading.Tasks.Task LoadWorkerFromFirestore(
            string firebaseUid)
        {
            Firebase.FirestoreService.Instance.GetWorker(firebaseUid,
                (worker) =>
                {
                    if (worker == null)
                    {
                        // New user — create profile
                        CurrentWorker = CreateNewWorker(firebaseUid);
                        Firebase.FirestoreService.Instance
                            .SaveWorker(CurrentWorker);
                    }
                    else
                    {
                        CurrentWorker = worker;
                    }

                    // Cache locally
                    CacheWorkerLocally(CurrentWorker);
                    OnWorkerLoaded?.Invoke(CurrentWorker);
                });

            return System.Threading.Tasks.Task.CompletedTask;
        }

        // --- Module Access ---
        public ModuleData GetModule(string id)
            => moduleDatabase.GetById(id);

        public List<ModuleData> GetAllModules()
            => moduleDatabase.GetAll();

        public List<ModuleData> GetModulesByStatus(ModuleStatus status)
            => moduleDatabase.GetByStatus(status);

        // --- Question Access ---
        public List<QuizQuestionData> GetQuestions(string moduleId)
            => questionDatabase.GetForModule(moduleId);

        // --- Certificate Access ---
        public CertificateData GetCertificate(string certId)
            => certificateDatabase.GetById(certId);

        public List<CertificateData> GetWorkerCertificates()
        {
            if (CurrentWorker == null) return new List<CertificateData>();
            return certificateDatabase.GetByWorker(CurrentWorker.id);
        }

        // --- Training Results ---
        public void SaveAttempt(string moduleId, int score, bool passed)
        {
            var result = new TrainingResult
            {
                resultId = System.Guid.NewGuid().ToString(),
                workerId = CurrentWorker.id,
                moduleId = moduleId,
                score = score,
                maxScore = 100,
                percentage = score,
                passed = passed,
                timestamp = System.DateTime.UtcNow.ToString("o"),
                synced = false
            };

            // Save to Firestore
            Firebase.FirestoreService.Instance.SaveTrainingResult(result);

            // Update local cache
            UpdateLocalProgress(moduleId, score, passed);
        }

        private void UpdateLocalProgress(string moduleId,
                                          int score, bool passed)
        {
            CurrentWorker.totalAttempts++;

            // Update module in database
            var mod = GetModule(moduleId);
            if (mod != null)
            {
                mod.attempts++;
                mod.bestScore = Mathf.Max(mod.bestScore, score);
                mod.progress = passed
                    ? 100
                    : Mathf.Max(mod.progress, score);
                mod.status = passed
                    ? ModuleStatus.Completed
                    : ModuleStatus.InProgress;
                mod.lastAttempt =
                    System.DateTime.UtcNow.ToString("yyyy-MM-dd");
            }

            // Update overall progress
            var allModules = GetAllModules();
            int totalProgress = 0;
            foreach (var m in allModules)
                totalProgress += m.progress;
            CurrentWorker.overallProgress =
                totalProgress / Mathf.Max(1, allModules.Count);

            // Save updated worker to Firestore
            Firebase.FirestoreService.Instance
                .SaveWorker(CurrentWorker);
            CacheWorkerLocally(CurrentWorker);
        }

        // --- Auth Shortcuts ---
        public void Login(string workerId, string pin)
            => Firebase.FirebaseAuthManager.Instance
                .Login(workerId, pin);

        public void DemoLogin()
            => Firebase.FirebaseAuthManager.Instance.DemoLogin();

        public void Register(string name, string workerId,
                             string organization, string sector,
                             string phone, string pin)
            => Firebase.FirebaseAuthManager.Instance
                .Register(workerId, pin, name,
                    "UI_Dashboard");

        public void Logout()
            => Firebase.FirebaseAuthManager.Instance.Logout();

        // --- Local Cache ---
        private void CacheWorkerLocally(WorkerData worker)
        {
            string json = JsonUtility.ToJson(worker);
            PlayerPrefs.SetString("CachedWorker", json);
            PlayerPrefs.Save();
        }

        private WorkerData LoadCachedWorker()
        {
            string json = PlayerPrefs.GetString("CachedWorker", "");
            if (!string.IsNullOrEmpty(json))
                return JsonUtility.FromJson<WorkerData>(json);
            return null;
        }

        private WorkerData CreateNewWorker(string firebaseUid)
        {
            return new WorkerData
            {
                firebaseUid = firebaseUid,
                id = "NEW",
                name = "New Worker",
                organization = "",
                sector = "",
                phone = "",
                language = "English",
                joinDate = System.DateTime.UtcNow.ToString("yyyy-MM-dd"),
                overallProgress = 0,
                certificatesEarned = 0,
                totalAttempts = 0,
                competencyScores = new CompetencyScores()
            };
        }
    }
}
```

## ScriptableObject Databases

### ModuleDatabase.cs
```csharp
namespace MiningSafetyAR.Data
{
    [CreateAssetMenu(fileName = "ModuleDatabase",
                     menuName = "MiningSafetyAR/Module Database")]
    public class ModuleDatabase : ScriptableObject
    {
        public List<ModuleData> modules = new List<ModuleData>();

        public ModuleData GetById(string id)
            => modules.Find(m => m.id == id);

        public List<ModuleData> GetByStatus(ModuleStatus status)
            => modules.FindAll(m => m.status == status);

        public List<ModuleData> GetAll()
            => new List<ModuleData>(modules);
    }
}
```

### QuestionDatabase.cs
```csharp
namespace MiningSafetyAR.Data
{
    [CreateAssetMenu(fileName = "QuestionDatabase",
                     menuName = "MiningSafetyAR/Question Database")]
    public class QuestionDatabase : ScriptableObject
    {
        public List<QuizQuestionData> questions
            = new List<QuizQuestionData>();

        public List<QuizQuestionData> GetForModule(string moduleId)
            => questions.FindAll(q => q.moduleId == moduleId);
    }
}
```

### CertificateDatabase.cs
```csharp
namespace MiningSafetyAR.Data
{
    [CreateAssetMenu(fileName = "CertificateDatabase",
                     menuName = "MiningSafetyAR/Certificate Database")]
    public class CertificateDatabase : ScriptableObject
    {
        public List<CertificateData> certificates
            = new List<CertificateData>();

        public CertificateData GetById(string id)
            => certificates.Find(c => c.id == id);

        public List<CertificateData> GetByWorker(string workerId)
            => certificates.FindAll(c => c.workerId == workerId);
    }
}
```
