# 09 — Known Gaps, Bugs & TODOs

This document records all features that are listed in the project roadmap or implied by the architecture but are **NOT YET IMPLEMENTED**, along with known bugs and technical debt found by direct code inspection.

---

## CRITICAL / Security

### 🔴 Firebase API Key hardcoded in source

**File:** `Assets/google-services.json` (line 18)  
**Impact:** High — the Firebase API key `AIzaSyBxhWZvIqgIMP1niRSc_H2iRMkdDLNdybI` is committed to the repository.  
**Fix:** Move to environment variables or Firebase App Check; restrict key in Firebase Console.

### 🔴 HMAC Secret Key hardcoded in source

**File:** `Assets/Scripts/Certification/CertificateGenerator.cs`  
**Value:** `"MiningARCertSecret2026"`  
**Impact:** Anyone who decompiles the APK can forge certificate signatures.  
**Fix:** Derive key server-side via Cloud Functions; never embed in client.

### 🔴 Firebase ID Token expiry not handled

**File:** `Assets/Scripts/Firebase/FirebaseAuthManager.cs`  
**Impact:** Firebase ID tokens expire after 1 hour. If the user stays logged in for >1 hour without an explicit refresh, Firestore REST calls will return 401 errors silently.  
**Fix:** Implement token refresh via `securetoken.googleapis.com/v1/token`.

---

## Missing Modules (Major Feature Gap)

### 🟠 Machinery Safety AR Module — NOT IMPLEMENTED

- `ModuleData` entry: `machinery_safety` (seeded in `ModuleDatabase.asset`)
- `GasLeakModuleManager.cs` analog: **NO `MachineryModuleManager.cs` exists**
- AR simulation scene for this module: **does not exist**
- 4 MCQ questions are seeded in `QuestionDatabase.asset`

### 🟠 Electrical Safety AR Module — NOT IMPLEMENTED

- `ModuleData` entry: `electrical_safety` (seeded)
- Module manager: **does not exist**
- AR simulation scene: **does not exist**
- 4 MCQ questions are seeded

### 🟠 Working at Heights Module — NOT IMPLEMENTED

- `ModuleData` entry: `heights_safety` (seeded, status: Locked)
- Module manager: **does not exist**
- AR simulation scene: **does not exist**
- 4 MCQ questions are seeded

---

## Certificate System Gaps

### 🟡 Certificate PDF/PNG Export — STUB

**File:** `Assets/Scripts/UI/Pages/CertificatePageController.cs` (line 34)  
```csharp
if (downloadBtn != null) downloadBtn.RegisterCallback<ClickEvent>(
    e => Debug.Log("[Certificate] Download - not implemented (would save PNG)"));
```
No screen capture, no PDF rendering, no file save.

### 🟡 Native Share Sheet — STUB

**File:** `Assets/Scripts/UI/Pages/CertificatePageController.cs` (line 35)  
```csharp
if (shareBtn != null) shareBtn.RegisterCallback<ClickEvent>(
    e => Debug.Log("[Certificate] Share - not implemented"));
```
No Android `Intent.ACTION_SEND` integration.

### 🟡 Camera-based QR Scanning — NOT IMPLEMENTED

The `UI_QRVerify` scene uses manual text input. A QR scanning camera view has not been implemented.

### 🟡 QR Code Image Generation — NOT IMPLEMENTED

The app does not render a QR code image for the certificate ID. The verification URL `https://minesafetyar.web.app/verify/{certId}` exists in `CertificateGenerator.cs` but no web app is hosted there.

### 🟡 Certificate Expiry Enforcement — NOT IMPLEMENTED

`CertificateData.status` is hardcoded to `"valid"` at seeding time. There is no runtime check against `expiryDate`.

---

## Localization Gaps

### 🟡 Hindi/Santali UI Text — NOT IMPLEMENTED

All page labels in UXML files are hardcoded in English. `LanguageManager.OnLanguageChanged` fires on language switch but no page controller subscribes to re-render text.

### 🟡 Hindi/Santali MCQ Questions — NOT IMPLEMENTED

`QuizQuestionData.textHI`, `textSAT`, `optionsHI`, `optionsSAT` fields are empty strings in seeded data. The `AssessmentPageController` reads `q.textEN` / `q.optionsEN` only.

### 🟡 Voiceover Audio Assets — NOT ASSIGNED

`FireSafetyModuleManager` and `GasLeakModuleManager` have 12 AudioClip SerializedField slots (4 steps × 3 languages). **All are null/unassigned** in the current scene prefabs. `PlayVoiceover` silently no-ops.

### 🟡 Language Preference Key Inconsistency

`LanguageManager` persists language as `int` via key `"SelectedLanguage"`.  
`SettingsPageController` reads/writes language as `string` via key `"SelectedLanguage"`.  
These two read from the same key but write different types — potential conflict on Settings page.

---

## AR Implementation Gaps

### 🟡 ARKit Configuration — NOT COMPLETED

ARKit 6.3.5 is in `Packages/manifest.json` but:
- No iOS player settings configured.
- No `Info.plist` camera usage description.
- No iOS build target in project.

### 🟡 AR Occlusion — NOT WIRED TO MODULES

`AR Occlusion.unity` scene exists but is not integrated into the training flow. No `AROcclusionManager` is used in `AR Plane Detection Placement.unity`.

### 🟡 AR Cloud Anchors — NOT IMPLEMENTED

`ARAnchorManager` is not used anywhere in C# code.

---

## Firebase / Backend Gaps

### 🟡 Token Refresh Logic — NOT IMPLEMENTED

See Security section above.

### 🟡 Firebase Analytics Custom Events — NOT IMPLEMENTED

`firebase-analytics:23.2.0` is in Gradle dependencies but no `FirebaseAnalytics.LogEvent()` calls exist in any C# script.

### 🟡 Cloud Functions — NOT IMPLEMENTED

All business logic runs client-side. No Firebase Cloud Functions project exists.

### 🟡 Firestore Security Rules — UNKNOWN

No `firestore.rules` file is present in the repository. Default rules (permissive or restrictive) are assumed from Firebase Console setup.

### 🟡 CloudSyncManager — DEPRECATED

**File:** `Assets/Scripts/Sync/CloudSyncManager.cs`  
This is an **empty shell**:
```csharp
/// Legacy sync manager — no longer needed since AppDataService.SaveAttempt
/// writes directly to Firestore. Kept as empty shell to avoid breaking references.
```
The file and class can be deleted safely.

---

## UI / UX Gaps

### 🟢 UI_Results Scene — CONDITIONAL NAVIGATION

`AssessmentPageController` checks if `UI_Results` is in `SceneManager.sceneCountInBuildSettings` before navigating. If absent, it falls back to `UI_Dashboard`. This is a development safety net that should be removed once `UI_Results.unity` is confirmed in all builds.

### 🟢 ModuleDetail Action Button — Start/Retake Logic

The flow `ModuleDetail → LearningContent → LocationCapture → AR Scene` is wired correctly. However, there is no "Continue" mode for InProgress modules — re-entering always restarts from slide 1.

---

## Data & Persistence

### 🟢 Certificate Random ID Duplication

**File:** `Assets/Scripts/UI/Pages/CertificatePageController.cs` (line 89):
```csharp
$"JH-{moduleId.ToUpper().Substring(0, 4)}-{Random.Range(100000,999999)}"
```
Generated IDs are not persisted back to Firestore immediately; on each view of the Certificate page, a new random ID may be generated if `ModuleProgress.certificateId` is empty. This produces non-deterministic certificate IDs.

### 🟢 PlayerPrefs vs Firestore Language Sync

The worker's language preference is stored both in `PlayerPrefs("SelectedLanguage")` and in Firestore (`WorkerData.language` field). These are not synchronized — a language change in Settings does not update Firestore.

---

## Technical Debt

### 🟢 Duplicate `QuizQuestion` Types

Two classes represent quiz questions:
1. `QuizQuestion` in `Assets/Scripts/Data/LocalDataModels.cs` (older, used by `AssessmentEngine.cs`)
2. `QuizQuestionData` in `Assets/Scripts/Data/QuizQuestionData.cs` (newer, used by `AppDataService` and `AssessmentPageController`)

The older `QuizQuestion` class and `AssessmentEngine.cs` should be consolidated or removed.

### 🟢 `CloudSyncManager.cs` — Empty File

See above. Should be deleted.

### 🟢 `ARPlacementManager` Pointer Event Conflict

Login and registration text fields call `PointerDownEvent.StopPropagation()` to prevent `ARPlacementManager` from consuming touch events when the AR scene is loaded additively alongside UI scenes. This is a workaround for a scene architecture issue.
