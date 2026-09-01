# 05 — Firebase & Backend Integration

## Architecture Decision: REST-Only Firebase

The project uses Firebase Firestore and Authentication via **raw REST API calls** through `UnityWebRequest`, intentionally avoiding the Firebase C# Unity SDK at runtime. This decision was made to prevent `DllNotFoundException` errors that occur on macOS in the Unity Editor when native Firebase SDK `.aar` libraries are resolved.

The native Firebase C++ SDK libraries **are** included in the Android build (via the `mainTemplate.gradle` dependencies resolved by EDM), but the C# layer communicates only via REST.

---

## Authentication

**File:** `Assets/Scripts/Firebase/FirebaseAuthManager.cs`

`FirebaseAuthManager : MonoBehaviour` — Singleton, spawned via `FirebaseBootstrap` at runtime startup.

### Authentication Modes

#### 1. Email/Password Login (REST)

Calls `identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={API_KEY}`:

```
POST https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=AIzaSyBxhWZvIqgIMP1niRSc_H2iRMkdDLNdybE
Body: { "email": "{workerId}@minesafety.jharkhand.in", "password": "{pin}", "returnSecureToken": true }
```

Worker ID is converted to email by appending `@minesafety.jharkhand.in`. PIN is used as password.

#### 2. Registration (REST)

Calls `identitytoolkit.googleapis.com/v1/accounts:signUp?key={API_KEY}`:
- Creates Firebase Auth user with the email/pin combination.
- On success, calls Firestore REST to create a worker profile document.

#### 3. Demo Login

`DemoLogin()` — bypasses Firebase Auth entirely; loads a hardcoded `WorkerData` object into `AppDataService` for demo/testing. Credentials: `JH10293` / `1234`.

#### 4. Auto-Login (Token Persistence)

Firebase ID token and UID are cached in `PlayerPrefs`:
- `PlayerPrefs.SetString("FirebaseIdToken", idToken)`
- `PlayerPrefs.SetString("FirebaseUid", uid)`

On `Awake`, if a cached token exists, `FirebaseAuthManager` validates it (or triggers a refresh) and fires `OnLoginSuccess`.

### Events

| Event | Signature |
|---|---|
| `OnLoginSuccess` | `Action<FirebaseUser>` |
| `OnLoginFailed` | `Action<string>` |
| `OnLogout` | `Action` |

### `IsLoggedIn` Property

Returns `true` if a valid Firebase UID is stored and has not been explicitly logged out.

---

## Firestore Service

**File:** `Assets/Scripts/Firebase/FirestoreService.cs`

`FirestoreService : MonoBehaviour` — Singleton, spawned via `FirebaseBootstrap`.

### Firestore REST Base URL

```
https://firestore.googleapis.com/v1/projects/minesafetyar/databases/(default)/documents
```

### Collections

| Collection | Document ID Pattern | Contents |
|---|---|---|
| `workers` | `{firebaseUid}` | WorkerData fields |
| `workers/{uid}/attempts` | `{resultId}` | TrainingResult fields |
| `workers/{uid}/progress` | `{moduleId}` | ModuleProgress fields |

### Key Methods

#### `GetDocument(collectionPath, documentId, onSuccess, onError)`

```
GET .../documents/{collectionPath}/{documentId}
Authorization: Bearer {idToken}
```

Parses Firestore JSON format (field-typed: `{"stringValue": "..."}`) into flat dictionaries.

#### `SetDocument(collectionPath, documentId, data, onSuccess, onError)`

```
PATCH .../documents/{collectionPath}/{documentId}?currentDocument.exists=false
```

Creates or updates a document.

#### `AddDocument(collectionPath, data, onSuccess, onError)`

```
POST .../documents/{collectionPath}
```

Auto-generates document ID.

#### `QueryCollection(collectionPath, filters, onSuccess, onError)`

```
POST .../documents:runQuery
Body: { structuredQuery: { from: [...], where: {...} } }
```

---

## App Data Service

**File:** `Assets/Scripts/Data/AppDataService.cs`

`AppDataService : MonoBehaviour` — Singleton, `DontDestroyOnLoad`.

This is the central data hub for the app. It:
- Manages in-memory state of the current worker.
- Loads worker data from Firestore (async) on login.
- Provides data to all UI page controllers.
- Caches worker progress as a `Dictionary<string, ModuleProgress>`.

### Data Flow on Login

```
FirebaseAuthManager.OnLoginSuccess
    → AppDataService.OnFirebaseLoginSuccess(FirebaseUser)
        → LoadWorkerFromFirestore(uid)
            → FirestoreService.GetDocument("workers", uid)
                → Parses WorkerData
                → Loads ModuleProgress sub-collection
                → Fires OnWorkerLoaded event
```

### Key Methods

| Method | Description |
|---|---|
| `GetModule(moduleId)` | Returns `ModuleData` from `ModuleDatabase` |
| `GetModuleProgress(moduleId)` | Returns per-worker `ModuleProgress` |
| `GetAllModulesWithProgress()` | Returns `List<ModuleData>` with progress overlaid from Firestore |
| `GetQuestions(moduleId)` | Returns `List<QuizQuestionData>` from `QuestionDatabase` |
| `GetCertificate(certId)` | Returns `CertificateData` from `CertificateDatabase` |
| `SaveAttempt(moduleId, score, passed)` | Creates `TrainingResult`, saves locally + to Firestore |
| `UpdateModuleCompetencyScores(moduleId, ...)` | Updates per-competency scores in Firestore progress |
| `GetModulesByStatusDynamic(status)` | Filters modules by current worker progress status |
| `Logout()` | Clears worker state + fires `OnWorkerLoggedOut` |
| `LoadCachedWorker()` | Restores worker from `PlayerPrefs` JSON on cold start |

### Offline Fallback

If Firestore is unreachable, the cached `PlayerPrefs` JSON worker data is used. Results are stored locally in `LocalScoreManager` with `synced = false`. When connectivity resumes, `AppDataService` re-syncs unsynced results.

---

## Firebase Bootstrap

**File:** `Assets/Scripts/Firebase/FirebaseBootstrap.cs`

Uses `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` to auto-spawn:
```csharp
var go = new GameObject("AppManagers (Firebase)");
Object.DontDestroyOnLoad(go);
go.AddComponent<MainThreadDispatcher>();
go.AddComponent<FirebaseAuthManager>();
go.AddComponent<FirestoreService>();
```

`EnsureExists()` — can be called manually for Editor play-from-scene safety.

---

## Main Thread Dispatcher

**File:** `Assets/Scripts/Helpers/MainThreadDispatcher.cs`

`MainThreadDispatcher : MonoBehaviour` — Singleton.

Since Firebase REST callbacks arrive on background threads (`UnityWebRequest` coroutines actually run on Unity's main thread, but other async operations may not), this dispatcher queues `Action` callbacks for execution on the Unity main thread.

Pattern:
```csharp
MainThreadDispatcher.Instance.Enqueue(() => { /* UI update */ });
```

---

## Firestore Provisioning Script

**File:** `create_firestore_db.py`

Python 3 admin script using `google-auth` and `requests` to:
- Check if the Firestore Native database already exists.
- Create it in `asia-south1` (Mumbai) if not.
- Poll the long-running operation until complete.

Used once during initial project setup; not part of the app runtime.

---

## Firebase Admin SDK Service Account

**File:** `Assets/minesafetyar-firebase-adminsdk-fbsvc-8714f6eb7f.json` *(not in repository — .gitignored)*

Used only by `create_firestore_db.py`. Not embedded in the app.

---

## NOT IMPLEMENTED

- **Firebase Realtime Database** — Firestore is used exclusively; no RTDB.
- **Cloud Functions** — no server-side logic; all processing is client-side.
- **Firebase Storage** — no file uploads (certificates are rendered client-side only).
- **Firebase Analytics events** — `firebase-analytics` library is in Gradle, but no custom analytics events are fired from C# code.
- **Push Notifications (FCM)** — no FCM integration.
- **Token refresh** — ID token expiry handling is not explicitly implemented; expired tokens may cause silent Firestore failures.
