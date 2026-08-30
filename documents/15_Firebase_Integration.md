# 15 — Firebase Integration Guide

## Firebase Services Used

| Service | Purpose | Package |
|---|---|---|
| Firebase Auth | User login/register (email+password) | `Firebase.Auth` |
| Cloud Firestore | Worker profiles, training results, certificates | `Firebase.Firestore` |
| Firebase Storage | File storage (optional, future) | `Firebase.Storage` |

## Required Firebase SDK Packages

Import these `.unitypackage` files from [Firebase Unity SDK](https://firebase.google.com/download/unity):

1. **FirebaseAuth** — `FirebaseAuth unitypackage`
2. **FirebaseFirestore** — `FirebaseFirestore unitypackage`
3. **FirebaseStorage** — `FirebaseStorage unitypackage` (optional)

## Current Firebase Config Files

| File | Path | Status |
|---|---|---|
| `google-services.json` | `Assets/google-services.json` | Present |
| Firebase Admin SDK | `Assets/minesafetyar-firebase-adminsdk-fbsvc-8714f6eb7f.json` | Present |

## Firebase Project Details

| Setting | Value |
|---|---|
| Project ID | `minesafetyar` |
| Project Number | 193064823382 |
| Android Package | `com.company.minear` |
| Storage Bucket | `minesafetyar.firebasestorage.app` |

## Firebase Auth Setup

### Email Format
```
{workerId}@miningsafety.app
```
Examples:
- `JH10293@miningsafety.app`
- `JH10456@miningsafety.app`
- `demo@miningsafety.app`

### Password Format
```
ARC{PIN}2026!
```
Examples:
- PIN `1234` → Password `ARC12342026!`
- PIN `5678` → Password `ARC56782026!`

### Auth Flow
```
1. User enters Worker ID + PIN
2. Construct email: {workerId}@miningsafety.app
3. Construct password: ARC{PIN}2026!
4. Call Firebase Auth.SignInWithEmailAndPasswordAsync()
5. On success → Load worker profile from Firestore
6. On failure → Show error message
```

## Firestore Collections

### 1. workers
```
Path: workers/{firebaseUid}

Fields:
- id: string (worker ID, e.g. "JH10293")
- name: string
- organization: string
- sector: string
- phone: string
- language: string
- joinDate: string (ISO date)
- overallProgress: number (0-100)
- certificatesEarned: number
- totalAttempts: number
- competencyScores: map
  - hazardRecognition: number
  - extinguisherUse: number
  - ppeSelection: number
  - evacuation: number
  - emergencyResponse: number
```

### 2. trainingResults
```
Path: trainingResults/{resultId}

Fields:
- resultId: string (GUID)
- workerId: string
- moduleId: string
- score: number
- maxScore: number
- percentage: number
- passed: boolean
- mistakesCount: number
- completionTimeSeconds: number
- timestamp: string (ISO datetime)
- synced: boolean
```

### 3. certificates
```
Path: certificates/{certId}

Fields:
- id: string (e.g. "JH-FIRE-001928")
- workerId: string
- moduleId: string
- moduleTitle: string
- score: number
- issuedDate: string (ISO date)
- expiryDate: string (ISO date)
- organization: string
- status: string ("valid" | "expired")
```

## Firebase Package Installation Steps

### Step 1: Download Firebase SDK
```
1. Go to: https://firebase.google.com/download/unity
2. Download Firebase SDK zip
3. Extract to a temp folder
```

### Step 2: Import Packages in Unity
```
1. Window → Package Manager → + → Import package
2. Import these packages:
   - FirebaseAuth
   - FirebaseFirestore
   - FirebaseStorage (optional)
   - FirebaseCommon (auto-included)
```

### Step 3: Configure google-services.json
```
1. Assets/google-services.json should already exist
2. Verify it has correct package name: "package_name": "com.company.minear"
3. Verify project_id matches: "project_id": "minesafetyar"
```

### Step 4: Initialize Firebase
```csharp
// In a persistent MonoBehaviour (e.g., AppManagers)
using Firebase;

void Awake()
{
    FirebaseApp.CheckAndFixDependenciesAsync()
        .ContinueWith(task => {
            if (task.Result == DependencyStatus.Available)
            {
                Debug.Log("Firebase initialized successfully");
            }
        });
}
```

### Step 5: Test Connection
```csharp
// Quick test
void Start()
{
    Debug.Log($"Firebase App: {FirebaseApp.DefaultInstance.Name}");
    Debug.Log($"Auth current user: {FirebaseAuth.DefaultInstance.CurrentUser}");
}
```

## Firebase Security Rules (Firestore)

Recommended rules for development:

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    // Workers - users can read/write their own data
    match /workers/{uid} {
      allow read, write: if request.auth != null
                         && request.auth.uid == uid;
    }

    // Training results - users can read/write their own
    match /trainingResults/{resultId} {
      allow read, write: if request.auth != null;
    }

    // Certificates - anyone can read, only auth can write
    match /certificates/{certId} {
      allow read: if true;
      allow write: if request.auth != null;
    }
  }
}
```

## Fallback: If Firebase SDK Not Available

If Firebase SDK cannot be imported (e.g., compilation issues), fall back to REST API:

```csharp
// Using UnityWebRequest with Firestore REST API
// No SDK required, works without Firebase packages

string projectId = "minesafetyar";
string baseUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents";

// GET request
UnityWebRequest.Get($"{baseUrl}/workers/{uid}");

// PUT request
var request = new UnityWebRequest($"{baseUrl}/workers/{uid}", "PUT");
byte[] body = System.Text.Encoding.UTF8.GetBytes(jsonString);
request.uploadHandler = new UploadHandlerRaw(body);
request.downloadHandler = new DownloadHandlerBuffer();
```

## What We Need From You

| Item | Status | Notes |
|---|---|---|
| google-services.json | Present | Good |
| Firebase Admin SDK JSON | Present | Good |
| Firebase Auth package | **Need to import** | Download from Firebase |
| Firebase Firestore package | **Need to import** | Download from Firebase |
| Firebase Storage package | Optional | For future file uploads |
| Firebase project access | Verify | Console access for rules |
