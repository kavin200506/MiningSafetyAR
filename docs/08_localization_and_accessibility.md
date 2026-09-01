# 08 — Localization & Accessibility

## Supported Languages

The app supports **three languages**:

| Language | Enum Value | Code |
|---|---|---|
| English | `Language.English` | 0 |
| Hindi | `Language.Hindi` | 1 |
| Santali | `Language.Santali` | 2 |

---

## Language Manager

**File:** `Assets/Scripts/Localization/LanguageManager.cs`

`LanguageManager : MonoBehaviour` — Singleton, `DontDestroyOnLoad`.

### Language Persistence

Language selection is stored in `PlayerPrefs`:
```csharp
private const string LANGUAGE_PREF_KEY = "SelectedLanguage";
PlayerPrefs.SetInt(LANGUAGE_PREF_KEY, (int)language);
```

On `Awake()`:
```csharp
int savedLang = PlayerPrefs.GetInt(LANGUAGE_PREF_KEY, (int)Language.English);
currentLanguage = (Language)savedLang;
```

### Language Change Event

```csharp
public event Action<Language> OnLanguageChanged;
```

All systems that display localized text should subscribe to this event to refresh displayed content.

### Key Methods

#### `SetLanguage(Language language)`
- Updates `currentLanguage`.
- Persists to `PlayerPrefs`.
- Fires `OnLanguageChanged`.

#### `PlayVoiceover(AudioClip englishClip, AudioClip hindiClip, AudioClip santaliClip)`
- Selects the appropriate clip based on `currentLanguage`.
- Falls back to `englishClip` if the language-specific clip is null.
- Stops any currently playing voiceover and plays the new clip.
- Requires `voiceoverAudioSource` to be assigned in the Inspector.

#### `GetLocalizedText(string textEN, string textHI, string textSAT)`
- Returns the appropriate string based on `currentLanguage`.
- Falls back to `textEN` if the localized string is empty/null.

---

## Language Enum (Data Models)

**File:** `Assets/Scripts/Data/LocalDataModels.cs`

```csharp
[Serializable]
public enum Language {
    English,
    Hindi,
    Santali
}
```

The `WorkerData` model stores `language` as a string field (e.g. `"English"`, `"Hindi"`, `"Santali"`).

---

## Localized Data in Question Database

**File:** `Assets/Scripts/Data/QuizQuestionData.cs`

```csharp
public class QuizQuestionData {
    public string textEN;       // English question text
    public string textHI;       // Hindi question text
    public string textSAT;      // Santali question text
    public string[] optionsEN;  // English answers
    public string[] optionsHI;  // Hindi answers
    public string[] optionsSAT; // Santali answers
}
```

**Current State:** The seeded questions (from `Phase1Seeder.cs`) only populate `textEN` and `optionsEN`. Hindi and Santali text fields are **empty strings** in the seeded data. The `AssessmentPageController` currently only displays `q.textEN` and `q.optionsEN` regardless of language.

---

## Localized Data in Training Modules

**File:** `Assets/Scripts/Data/LocalDataModels.cs` — `QuizQuestion` class (older):

```csharp
public class QuizQuestion {
    public string questionTextTextEN;
    public string questionTextTextHI;
    public string questionTextTextSAT;
    public string[] optionsEN;
    public string[] optionsHI;
    public string[] optionsSAT;
    public int correctOptionIndex;
}
```

**File:** `Assets/Scripts/Modules/FireSafetyModuleManager.cs` — audio clips per language:
- `step1AudioEN`, `step1AudioHI`, `step1AudioSAT` (×4 steps)
- Same pattern for `GasLeakModuleManager`

---

## Language Selection UI

**In Settings Page** (`SettingsPageController`):

Three buttons: `lang-en`, `lang-hi`, `lang-sat`.

On tap:
```csharp
PlayerPrefs.SetString("SelectedLanguage", lang); // Stores "English"/"Hindi"/"Santali"
```

> **Note:** Settings page stores language as a string in `PlayerPrefs` under key `"SelectedLanguage"`, while `LanguageManager` uses the integer `Language` enum under key `"SelectedLanguage"` (same key, different type). This is a **known inconsistency** — see `09_known_gaps_and_todos.md`.

Active language button highlighted orange; others grayed.

**In Registration Page** (`RegisterPageController`):
- Same 3 language selector buttons.
- Selected language is stored in `selectedLanguage` field and sent to Firestore as part of worker profile.

---

## Voiceover Audio Clips

**Status: NOT FULLY WIRED**

The `FireSafetyModuleManager` and `GasLeakModuleManager` define `AudioClip` Inspector fields for each step in each language:
```
step1AudioEN, step1AudioHI, step1AudioSAT
step2AudioEN, step2AudioHI, step2AudioSAT
step3AudioEN, step3AudioHI, step3AudioSAT
step4AudioEN, step4AudioHI, step4AudioSAT
```

However, **no audio clip assets have been assigned** to these fields in the actual scene prefabs/GameObjects — the serialized references are null. `PlayVoiceover` will silently not play if all clips are null.

---

## NOT IMPLEMENTED — Localization

| Feature | Status |
|---|---|
| Hindi UI text for all labels | NOT IMPLEMENTED — all label strings are hardcoded in English in the UXML files |
| Santali UI text | NOT IMPLEMENTED |
| Hindi/Santali MCQ questions | NOT IMPLEMENTED — quiz only shows English text |
| Voiceover audio files | NOT IMPLEMENTED — AudioClip references unassigned |
| Right-to-left text support | NOT APPLICABLE (Hindi/Santali are LTR) |
| Runtime language switching of UXML labels | NOT IMPLEMENTED — changing language does not refresh existing page text |

---

## GPS / Location

**File:** `Assets/Scripts/Data/TrainingLocationCapture.cs`

`TrainingLocationCapture : MonoBehaviour` — Singleton, `DontDestroyOnLoad`.

### Purpose

Captures a **single GPS coordinate** at the start of each training session for regional analytics. This is **not continuous tracking**.

### Consent Gate

```csharp
private const string CONSENT_PREF_KEY = "LocationConsentGranted";
public static bool HasUserConsented {
    get => PlayerPrefs.GetInt(CONSENT_PREF_KEY, 0) == 1;
    set { PlayerPrefs.SetInt(CONSENT_PREF_KEY, value ? 1 : 0); }
}
```

If `HasUserConsented == false`, location is not captured and `LocationDataPayload.hasLocation` remains `false`.

### Location Capture Flow

1. `CaptureLocation(onComplete, onSubStatusUpdate)` is called from `LocationCapturePageController`.
2. Checks Android permission via `Permission.HasUserAuthorizedPermission(Permission.FineLocation)`.
3. Enables `Input.location` service (Unity location service).
4. Polls `Input.location.status` for up to `TIMEOUT_SECONDS = 8.0f` with `POLL_INTERVAL_SECONDS = 0.5f`.
5. On success: creates `LocationDataPayload` with lat/lng/accuracy.
6. Optionally calls a **reverse geocoding** service to convert lat/lng to a human-readable mining sector name.
7. If timeout: creates `LocationDataPayload` with `hasLocation = false`.
8. Fires `onComplete(LocationDataPayload)`.

### Location Data Model

```csharp
public class LocationDataPayload {
    public double latitude;
    public double longitude;
    public float horizontalAccuracy;
    public string locationName;     // Reverse-geocoded or "Regional Mining Sector (lat, lng)"
    public bool hasLocation;
    public bool capturedOffline;
}
```

### Tagging Training Results

The `locationName`, `latitude`, `longitude`, `locationAccuracyMeters`, `hasLocation`, and `capturedOffline` fields of `TrainingResult` are populated from `LocationDataPayload` before the result is saved.
