# 04 — Assessment & Scoring System

## Overview

The assessment system is split into two phases:
1. **AR Simulation Score** — derived from step completion and mistake count during the AR scenario.
2. **MCQ Quiz Score** — from the post-simulation multiple-choice question bank.

The two scores are **combined** into a single `finalScore` with a weighted formula.

---

## Assessment Engine

**File:** `Assets/Scripts/Assessment/AssessmentEngine.cs`

`AssessmentEngine : MonoBehaviour` — Singleton.

> **Note:** `AssessmentEngine` is an older implementation and some of its quiz question data may be superseded by the `QuestionDatabase` ScriptableObject loaded by `AppDataService`. Both coexist in the codebase.

---

## Assessment Page Controller

**File:** `Assets/Scripts/UI/Pages/AssessmentPageController.cs`

`AssessmentPageController : PageController` — controls `UI_Assessment.unity` scene.

### Navigation Parameters

| Parameter | Type | Description |
|---|---|---|
| `moduleId` | `string` | Module identifier (e.g. `"fire_safety"`) |
| `simulationScore` | `int` | Score passed from the AR simulation (default 80) |

### Quiz Flow

1. `OnPageEnter()` calls `AppDataService.Instance.GetQuestions(moduleId)` to fetch questions.
2. If no questions found, a single fallback question is used.
3. Questions are displayed one at a time with:
   - A letter-badged option button for each answer choice (A, B, C, D).
   - After selection, correct answer highlighted green, wrong answer highlighted red.
   - Feedback banner shows ✓ or ✗.
   - "Next" button advances; "Show Results" button appears on last question.
4. Per-competency correct counts are tracked in `correctByCompetency` and `totalByCompetency` dictionaries.

### Final Score Calculation

```
mcqScore  = (correctCount / total questions) * 100
finalScore = simulationScore * 0.6 + mcqScore * 0.4
passed = finalScore >= 60
```

The `simulationScore` (AR module performance) carries 60% weight; the quiz carries 40%.

### Result Saving

```csharp
AppDataService.Instance.SaveAttempt(moduleId, finalScore, passed);
AppDataService.Instance.UpdateModuleCompetencyScores(moduleId, correctByCompetency, totalByCompetency);
```

Then navigates to `UI_Results` if that scene exists in Build Settings, otherwise falls back to `UI_Dashboard`.

---

## Question Database

**Asset:** `Assets/Data/QuestionDatabase.asset`  
**File:** `Assets/Scripts/Data/QuestionDatabase.cs` (ScriptableObject)  
**Data model:** `Assets/Scripts/Data/QuizQuestionData.cs`

```csharp
public class QuizQuestionData {
    public string id;              // e.g. "fire_q1"
    public string moduleId;        // e.g. "fire_safety"
    public string textEN;          // Question text in English
    public string textHI;          // Question text in Hindi
    public string textSAT;         // Question text in Santali
    public string[] optionsEN;     // Answer options in English
    public string[] optionsHI;     // Answer options in Hindi
    public string[] optionsSAT;    // Answer options in Santali
    public int correctIndex;       // 0-based index of correct answer
    public string competency;      // e.g. "hazardRecognition", "extinguisherUse"
}
```

### Seeded Questions (from Phase1Seeder.cs)

**Fire Safety (5 questions):**

| ID | Question | Correct Answer | Competency |
|---|---|---|---|
| fire_q1 | What does P.A.S.S. stand for? | Pull, Aim, Squeeze, Sweep | extinguisherUse |
| fire_q2 | Safe distance from fire? | 2m | hazardRecognition |
| fire_q3 | Fire triangle components? | Heat, Fuel, Oxygen | hazardRecognition |
| fire_q4 | Aim extinguisher nozzle at? | Base of fire | extinguisherUse |
| fire_q5 | First step in evacuation? | Sound alarm | evacuation |

**Gas Safety (5 questions):**

| ID | Question | Correct Answer | Competency |
|---|---|---|---|
| gas_q1 | First action on gas leak? | Evacuate and alert | hazardRecognition |
| gas_q2 | Correct PPE for gas? | Respirator | ppeSelection |
| gas_q3 | Buddy system means? | Pair and monitor | emergencyResponse |
| gas_q4 | Odorless gas requires? | Detector test | hazardRecognition |
| gas_q5 | Pre-entry test checks? | Oxygen, toxic, flammable | hazardRecognition |

**Machinery Safety (4 questions):** LOTO procedure, machine guards.  
**Electrical Safety (4 questions):** Electrical hazards, grounding, PPE.  
**Working at Heights (4 questions):** Fall protection, harness, scaffold.

---

## Learning Content (Pre-Quiz Slides)

**File:** `Assets/Scripts/UI/Pages/LearningContentPageController.cs`

Before the MCQ quiz, users must complete a **slide-based learning module**:
- For `fire_safety`: ~4 slides covering fire triangle, P.A.S.S., extinguisher types, evacuation.
- For `gas_safety`: ~4 slides covering gas detection, SCBA, buddy system, isolation.
- Slides contain: emoji icon, title, bullet-point list, and a "Pro Tip" footer.
- Navigation: "Previous" / "Next" buttons, progress bar fills as slides advance.
- "I'm Ready" button on the final slide navigates to `UI_Assessment`.

---

## Results Page

**File:** `Assets/Scripts/UI/Pages/ResultsPageController.cs`

`ResultsPageController : PageController` — controls `UI_Results.unity` scene.

### Navigation Parameters (Dictionary)

| Key | Type | Description |
|---|---|---|
| `moduleId` | `string` | Module identifier |
| `mcqScore` | `int` | MCQ-only percentage |
| `correct` | `int` | Number of correct answers |
| `total` | `int` | Total questions |
| `simulationScore` | `int` | AR simulation score |
| `finalScore` | `int` | Combined weighted final score |
| `passed` | `bool` | Pass/fail result |

### Displayed Data

- `sim-score` label: shows `simulationScore`%
- `mcq-score` label: shows `mcqScore`%
- `correct-count` label: shows `correct/total`
- `final-score` label: shows `finalScore`%
- `result-badge` label: "PASSED" (green) or "FAILED" (red)
- `attempt-num` label: shows current attempt number
- `personal-best` label: shows best score across all attempts
- `competency-bars`: per-competency score bars (via `ScoreBarHelper`)

### Action Buttons

| Button | Action |
|---|---|
| `cert-btn` | Navigate to `UI_Certificate` (only shown if passed) |
| `retry-btn` | Navigate back to `UI_LearningContent` |
| `module-btn` | Navigate to `UI_ModuleDetail` |
| `dashboard-btn` | Navigate to `UI_Dashboard` |

---

## Score Persistence

### Local Storage

`Assets/Scripts/Data/LocalScoreManager.cs`

- Saves `TrainingResult` objects as JSON to `Application.persistentDataPath/mining_safety_results.json`.
- Wraps in `TrainingResultListWrapper` for `JsonUtility.ToJson/FromJson`.
- Tracks `synced: bool` flag per result.
- `GetUnsyncedResults()` — returns all results where `synced == false`.
- `MarkResultAsSynced(resultId)` — marks and re-persists.

### `TrainingResult` Data Model

```csharp
public class TrainingResult {
    public string resultId;          // Guid.NewGuid()
    public string workerId;
    public string moduleName;
    public int score;
    public int maxScore;
    public float percentage;
    public bool passed;
    public int mistakesCount;
    public float completionTimeSeconds;
    public string timestamp;         // UTC ISO 8601
    public bool synced;
    // Geo fields
    public double latitude;
    public double longitude;
    public float locationAccuracyMeters;
    public string locationName;
    public bool hasLocation;
    public bool capturedOffline;
}
```

---

## NOT IMPLEMENTED

- **Adaptive question bank** — questions are static; no ML-based question selection.
- **Question randomization** — questions are served in database order; no shuffle logic.
- **Detailed per-question explanations** — no explanation text shown after selecting an answer.
- **Time pressure for MCQ** — no per-question timer (only the 3-min simulation countdown).
