# 10 — Data Models Reference

This document is a complete, consolidated reference for every data model class in the project, organized by namespace.

---

## Namespace: `MiningSafetyAR.Data`

### `WorkerData`
**File:** `Assets/Scripts/Data/WorkerData.cs`

| Field | Type | Description |
|---|---|---|
| `firebaseUid` | `string` | Firebase Auth UID |
| `id` | `string` | Worker ID (e.g. "JH10293") |
| `name` | `string` | Full name |
| `organization` | `string` | Mine/company name |
| `sector` | `string` | Mining sector type |
| `phone` | `string` | Phone number |
| `language` | `string` | Preferred language |
| `joinDate` | `string` | ISO date string |
| `overallProgress` | `int` | 0-100 aggregated progress |
| `certificatesEarned` | `int` | Count of earned certificates |
| `totalAttempts` | `int` | Total training attempts |
| `competencyScores` | `CompetencyScores` | Aggregate competency scores |

---

### `WorkerSaveData`
**File:** `Assets/Scripts/Data/AppDataService.cs` (lines 22–35)

Serializable class for `PlayerPrefs`/local JSON persistence of worker data.

| Field | Type | Notes |
|---|---|---|
| `firebaseUid` | `string` | |
| `id` | `string` | |
| `name` | `string` | |
| `organization` | `string` | |
| `sector` | `string` | |
| `phone` | `string` | |
| `language` | `string` | |
| `joinDate` | `string` | |
| `overallProgress` | `int` | |
| `certificatesEarned` | `int` | |
| `totalAttempts` | `int` | |

---

### `ModuleData`
**File:** `Assets/Scripts/Data/ModuleData.cs`

| Field | Type | Description |
|---|---|---|
| `id` | `string` | Unique identifier (e.g. "fire_safety") |
| `title` | `string` | Display title |
| `iconEmoji` | `string` | Emoji icon character |
| `domain` | `string` | Domain label (e.g. "Fire Safety") |
| `duration` | `string` | Estimated duration (e.g. "45 min") |
| `difficulty` | `string` | "Easy" / "Medium" / "Hard" |
| `status` | `ModuleStatus` | Current status |
| `progress` | `int` | 0-100 |
| `bestScore` | `int` | 0-100 |
| `attempts` | `int` | Attempt count |
| `lastAttempt` | `string` | ISO date string |
| `certificateId` | `string` | Certificate ID if earned |
| `color` | `string` | Hex color (e.g. "#FF6D00") |
| `description` | `string` | [TextArea] full description |
| `objectives` | `string[]` | Learning objectives list |
| `competencyScores` | `CompetencyScores` | Best competency scores |

---

### `ModuleProgress`
**File:** `Assets/Scripts/Data/ModuleData.cs`

Per-worker progress overlay (stored in Firestore).

| Field | Type | Default |
|---|---|---|
| `moduleId` | `string` | |
| `status` | `ModuleStatus` | `NotStarted` |
| `progress` | `int` | `0` |
| `bestScore` | `int` | `0` |
| `attempts` | `int` | `0` |
| `lastAttempt` | `string` | `""` |
| `certificateId` | `string` | `""` |
| `competencyScores` | `CompetencyScores` | |

---

### `CompetencyScores`
**File:** `Assets/Scripts/Data/ModuleData.cs`

| Field | Type | Represents |
|---|---|---|
| `hazardRecognition` | `int` | % score in hazard ID questions |
| `extinguisherUse` | `int` | % score in extinguisher use questions |
| `ppeSelection` | `int` | % score in PPE selection questions |
| `evacuation` | `int` | % score in evacuation questions |
| `emergencyResponse` | `int` | % score in emergency response questions |

---

### `ModuleStatus`
**File:** `Assets/Scripts/Data/ModuleData.cs`

```csharp
public enum ModuleStatus {
    NotStarted,
    InProgress,
    Completed,
    Locked
}
```

---

### `TrainingResult`
**File:** `Assets/Scripts/Data/LocalDataModels.cs`

| Field | Type | Description |
|---|---|---|
| `resultId` | `string` | `Guid.NewGuid().ToString()` |
| `workerId` | `string` | Worker's ID |
| `moduleName` | `string` | Module name string |
| `score` | `int` | Final score (0-100) |
| `maxScore` | `int` | Max achievable score |
| `percentage` | `float` | score/maxScore * 100 |
| `passed` | `bool` | percentage >= 70 |
| `mistakesCount` | `int` | Number of wrong actions |
| `completionTimeSeconds` | `float` | Time to complete |
| `timestamp` | `string` | UTC ISO 8601 |
| `synced` | `bool` | Whether written to Firestore |
| `latitude` | `double` | GPS latitude |
| `longitude` | `double` | GPS longitude |
| `locationAccuracyMeters` | `float` | GPS accuracy in meters |
| `locationName` | `string` | Reverse-geocoded place name |
| `hasLocation` | `bool` | Whether GPS was available |
| `capturedOffline` | `bool` | Whether device was offline |

---

### `QuizQuestionData`
**File:** `Assets/Scripts/Data/QuizQuestionData.cs`

| Field | Type | Description |
|---|---|---|
| `id` | `string` | e.g. "fire_q1" |
| `moduleId` | `string` | e.g. "fire_safety" |
| `textEN` | `string` | Question text (English) |
| `textHI` | `string` | Question text (Hindi) — currently empty |
| `textSAT` | `string` | Question text (Santali) — currently empty |
| `optionsEN` | `string[]` | Answer options (English) |
| `optionsHI` | `string[]` | Answer options (Hindi) — currently empty |
| `optionsSAT` | `string[]` | Answer options (Santali) — currently empty |
| `correctIndex` | `int` | 0-based correct answer index |
| `competency` | `string` | Competency key (e.g. "hazardRecognition") |

---

### `QuizQuestion` (Legacy)
**File:** `Assets/Scripts/Data/LocalDataModels.cs`

Older question model used by the legacy `AssessmentEngine`.

| Field | Type |
|---|---|
| `questionId` | `string` |
| `questionTextTextEN` | `string` |
| `questionTextTextHI` | `string` |
| `questionTextTextSAT` | `string` |
| `optionsEN` | `string[]` |
| `optionsHI` | `string[]` |
| `optionsSAT` | `string[]` |
| `correctOptionIndex` | `int` |

---

### `CertificateData`
**File:** `Assets/Scripts/Data/CertificateData.cs`

| Field | Type | Description |
|---|---|---|
| `id` | `string` | Certificate ID (e.g. "JH-FIRE-001928") |
| `workerName` | `string` | Worker full name |
| `workerId` | `string` | Worker ID |
| `moduleId` | `string` | Module identifier |
| `moduleTitle` | `string` | Module display name |
| `score` | `int` | Score percentage |
| `issuedDate` | `string` | "yyyy-MM-dd" |
| `expiryDate` | `string` | "yyyy-MM-dd" (1 year after issued) |
| `organization` | `string` | Organization name |
| `status` | `string` | "valid" or "expired" |

---

### `CertificatePayload`
**File:** `Assets/Scripts/Data/LocalDataModels.cs`

Used by `CertificateGenerator` for HMAC signing.

| Field | Type | Description |
|---|---|---|
| `certificateId` | `string` | |
| `workerId` | `string` | |
| `moduleName` | `string` | |
| `score` | `int` | |
| `issuedAt` | `string` | ISO 8601 UTC |
| `verificationUrl` | `string` | e.g. "https://minesafetyar.web.app/verify/{id}" |
| `signatureHash` | `string` | HMAC-SHA256 base64 |

---

### `LocationDataPayload`
**File:** `Assets/Scripts/Data/TrainingLocationCapture.cs`

| Field | Type | Description |
|---|---|---|
| `latitude` | `double` | GPS latitude |
| `longitude` | `double` | GPS longitude |
| `horizontalAccuracy` | `float` | Accuracy in meters |
| `locationName` | `string` | Reverse-geocoded or default |
| `hasLocation` | `bool` | Whether GPS succeeded |
| `capturedOffline` | `bool` | Whether captured in offline mode |

---

## Namespace: `MiningSafetyAR.Data` — Enums

### `ModuleType`
**File:** `Assets/Scripts/Data/LocalDataModels.cs`

```csharp
public enum ModuleType {
    FireAndExplosion,
    GasLeakAndConfinedSpace
}
```

### `Language`
**File:** `Assets/Scripts/Data/LocalDataModels.cs`

```csharp
public enum Language {
    English,   // 0
    Hindi,     // 1
    Santali    // 2
}
```

---

## ScriptableObject Databases

### `ModuleDatabase`
**Asset:** `Assets/Data/ModuleDatabase.asset` and `Assets/Resources/Data/ModuleDatabase.asset`

```csharp
public class ModuleDatabase : ScriptableObject {
    public List<ModuleData> modules;
    public ModuleData Get(string id) => modules.Find(m => m.id == id);
    public List<ModuleData> GetAll() => modules;
}
```

### `QuestionDatabase`
**Asset:** `Assets/Data/QuestionDatabase.asset`

```csharp
public class QuestionDatabase : ScriptableObject {
    public List<QuizQuestionData> questions;
}
```

### `CertificateDatabase`
**Asset:** `Assets/Data/CertificateDatabase.asset`

```csharp
public class CertificateDatabase : ScriptableObject {
    public List<CertificateData> certificates;
}
```

---

## Firestore Document Schema

### Collection: `workers`
Document ID: `{firebaseUid}`

Fields map directly to `WorkerData` fields plus a nested `progress` sub-collection.

### Sub-collection: `workers/{uid}/progress`
Document ID: `{moduleId}`

Fields map directly to `ModuleProgress` fields.

### Sub-collection: `workers/{uid}/attempts`
Document ID: `{resultId}`

Fields map directly to `TrainingResult` fields.

---

## Local Storage File

**Path:** `{Application.persistentDataPath}/mining_safety_results.json`

**Format:**
```json
{
  "results": [
    {
      "resultId": "guid-string",
      "workerId": "JH10293",
      "moduleName": "Fire & Explosion Response",
      "score": 85,
      "maxScore": 100,
      "percentage": 85.0,
      "passed": true,
      "mistakesCount": 0,
      "completionTimeSeconds": 142.5,
      "timestamp": "2026-08-26T09:34:12Z",
      "synced": true,
      "latitude": 23.6102,
      "longitude": 85.2799,
      "locationAccuracyMeters": 12.0,
      "locationName": "Jharia Coalfield Mining Zone",
      "hasLocation": true,
      "capturedOffline": false
    }
  ]
}
```
