# Technical Implementation Spec — Fire & Explosion Response Scoring

Status: **Spec only — describes exactly how to build what `scoring.md` and `scoring-explained.md` describe. Nothing in this document has been implemented yet.**

Read order: [`scoring.md`](scoring.md) (why — the audit and the decisions), [`scoring-explained.md`](scoring-explained.md) (what the worker experiences), this document (how — every file, field, method, and formula needed to actually build it). `sweep.md` remains the separate, already-approved spec for the sweep-intensity mechanic itself; this document only *consumes* its output (`currentSweepIntensity`).

Every proposed value (penalty sizes, distances, durations, floors) is called out as **tunable** and collected in one table at the end (§13), the same convention `sweep.md` uses.

---

## 0. Summary of what changes, file by file

| File | Change |
|---|---|
| `Assets/Scripts/Data/LocalDataModels.cs` | No structural change — `StepMetric` and `TrainingResult` already have the fields needed. |
| `Assets/Scripts/Data/ModuleData.cs` | Add one new field to `CompetencyScores`: `timeManagement`. |
| `Assets/Scripts/Modules/FireSafetyModuleManager.cs` | The core rewrite — step list, mistake severity, evacuation step, per-step score overrides, competency computation, drill result packaging. |
| `Assets/Scripts/AR/FireExtinguisherGrabController.cs` | Expose a running average sweep intensity for the whole spray duration. |
| `Assets/Scripts/AR/ARProximitySafetyValidator.cs` | Pass mistake severity into `RegisterMistake`; no other logic change. |
| `Assets/Scripts/AR/ARWaypointNavigationPointer.cs` | No change — already generic; just gets called for the first time. |
| `Assets/Scripts/AR/ARGuidanceController.cs` | Re-index `contextualHints` to 4 entries; wire `SetCurrentStepIndex` (currently dead) so the hint button shows the right text. |
| `Assets/Scripts/UI/Pages/ARSimulationPageController.cs` | `RedirectToQuizAfterDelay` passes a full data dictionary instead of a bare module-id string; trim the mock 5-entry test list to 4. |
| `Assets/Scripts/UI/Pages/AssessmentPageController.cs` | Parse the new dictionary fields; compute the unified final score in one place; call the new single-save path. |
| `Assets/Scripts/Data/AppDataService.cs` | New expanded `SaveAttempt` overload carrying full result data; new `UpdateModuleCompetencyScoresFromDrill` method; remove the premature save from the drill side. |
| `Assets/Scripts/UI/Pages/ResultsPageController.cs`, `ModuleDetailPageController.cs` | Read `timeManagement` instead of `ppeSelection` for the "Time" bar. |
| New: `Assets/Scripts/Modules/ScoringConstants.cs` | One shared file for the pass threshold and other cross-file constants, so they can't drift again. |

---

## 1. New shared constants file

Four different pass thresholds exist today (`FireSafetyModuleManager.cs:197`, `BaseModuleManager.cs:76`, `AssessmentEngine.cs:13`, `AssessmentPageController.cs:215`, plus UI copy in `ResultsPageController.cs:99` claiming 60%). Rather than edit five call sites to the same magic number, add one static class every one of them references:

```csharp
namespace MiningSafetyAR.Modules
{
    public static class ScoringConstants
    {
        public const float PassThresholdPercentage = 70f;

        public const int GenericMistakePenalty = 25;
        public const int ProximityBreachPenalty = 50;

        public const float DrillWeight = 0.70f;
        public const float QuizWeight = 0.30f;
    }
}
```
Every file in §0 that currently hardcodes `70f`, `75`, `25`, or `50` switches to referencing this class. `AssessmentEngine.passThresholdPercentage` (an `[SerializeField] float`, currently defaulted to 70 in the Inspector) either gets removed in favor of the constant, or left as an Inspector override that defaults to reading `ScoringConstants.PassThresholdPercentage` — your call at implementation time, noted here so it isn't missed.

---

## 2. Data model: one new field, nothing else

`ModuleData.cs`, `CompetencyScores` class — add:
```csharp
public int timeManagement;
```

**Why a new field instead of reusing `ppeSelection` for "Time":** `ppeSelection` is not just a mislabeled leftover — it's the *real* competency tag used by the Gas Leak, Machinery, Electrical, and Heights modules' actual PPE quiz questions (confirmed in `Phase1Seeder.cs` — e.g. `competency="ppeSelection"` on real gas-mask/harness/glove questions). None of the Fire & Explosion quiz questions use that tag today, which is exactly why the "Time Taken" bar has been silently showing an always-empty number. If Fire & Explosion's new Time score were written into `ppeSelection` instead of a dedicated field, it would corrupt that field for every other module that legitimately uses it once this scoring rework is applied there too (`scoring.md` §8 Q6). A dedicated field avoids that collision entirely.

Everywhere `CompetencyScores` is read or written field-by-field needs the new field added alongside the existing five — specifically:
- `AppDataService.cs` — the Firestore write dictionary (`data["competencyScores"] = new Dictionary<...> { ... }`, ~line 323-329) and the Firestore read parsing (~line 236-241) both need a `timeManagement` entry added, same pattern as the existing five.
- `AppDataService.cs` line ~256-263 (the local/remote merge that does `Mathf.Max` per field) needs a sixth `Mathf.Max` line for `timeManagement`.
- `ResultsPageController.cs:136` and `ModuleDetailPageController.cs:136` — change `cs.ppeSelection` / `mod.competencyScores.ppeSelection` to `cs.timeManagement` / `mod.competencyScores.timeManagement`.

No other structural changes needed — `StepMetric` (`stepName`, `errorCount`, `durationSeconds`, `score`) and `TrainingResult` already carry everything else required.

---

## 3. `FireSafetyModuleManager.cs` — the core rewrite

### 3.1 Step list and indices

Replace:
```csharp
private static readonly string[] StepNames =
{
    "Sound Alarm", "Select Extinguisher", "Pull Pin", "Aim & Test Spray", "Squeeze & Sweep"
};
```
with:
```csharp
private const int PullPinStepIndex        = 0;
private const int AimSprayStepIndex       = 1;
private const int SqueezeSweepStepIndex   = 2;
private const int EvacuationStepIndex     = 3;

private static readonly string[] StepNames =
{
    "Pull Pin", "Aim & Test Spray", "Squeeze & Sweep", "Evacuate to Safe Distance"
};
```
`Awake()`'s `totalSteps = 5;` becomes `totalSteps = 4;`.

`GetStepInstruction(int stepIndex)` renumbers its 5 cases down to 4, dropping the old "Step 1"/"Step 2" text and shifting the rest:
```csharp
public override string GetStepInstruction(int stepIndex)
{
    switch (stepIndex)
    {
        case PullPinStepIndex:      return "Pull the safety pin on the extinguisher handle.";
        case AimSprayStepIndex:     return "Aim the nozzle at the base of the fire and test spray.";
        case SqueezeSweepStepIndex: return "Squeeze the handle and sweep side-to-side across the base.";
        case EvacuationStepIndex:   return "Fire's out! Move to a safe distance to complete evacuation.";
        default: return "Module Complete!";
    }
}
```
The old "Step 1: Sound the emergency alarm..." / "Step 2: Select the correct CO2/Dry Powder..." text moves out of the scored step machine entirely and becomes static mission-briefing copy (owned by `ARGuidanceController`'s briefing text or `ARSimulationPageController.RedirectToQuizAfterDelay`'s mission modal — a content/UI change, not a scoring one).

`step1Audio*`/`step2Audio*` serialized fields (currently all null anyway, §1.5 of `scoring.md`) either get deleted along with the removed steps, or kept and silently unused if you want to preserve the Inspector layout for now — flag this as a small decision at implementation time, doesn't affect scoring either way since the clips are unassigned.

### 3.2 Deleting the fake-step machinery

Delete entirely:
- `NotifyAlarmActivated()` (`:267-273`)
- `NotifyExtinguisherSelected(bool isValid)` (`:278-290`) — including its `RegisterMistake("Selected wrong extinguisher type...")` call, which was unreachable dead code (`scoring.md` §1.3).
- The burst-loop body of `HandlePinPulled()` — replaced per §3.4 below, since Pull Pin is now a standalone first step rather than something bundled with two others.

### 3.3 Mistake severity — replacing flat −25-for-everything

Today, every mistake costs the same 25 points regardless of type, and `ARProximitySafetyValidator.penaltyPoints = 50` is declared but never read (`scoring.md` §1.6). Fix:

```csharp
public enum MistakeSeverity
{
    Standard = ScoringConstants.GenericMistakePenalty,      // 25
    ProximityBreach = ScoringConstants.ProximityBreachPenalty // 50
}
```
*(Note: C# enums can't directly reference a `const` from another class as their underlying value in all cases depending on exact const typing — if that causes a compile issue, define the enum with plain literals `Standard = 25, ProximityBreach = 50` and have `ScoringConstants` reference the enum instead, so there's only one source of truth either direction. Flagging so this isn't a surprise at implementation time.)*

```csharp
private int[] stepErrorCounts;      // existing — count, for the "Errors" column display
private int[] stepPenaltyPoints;    // new — accumulated point loss, for the score formula
private int proximityBreachCount;   // new — whole-drill count, feeds Hazard Recognition (§3.6)

public override void RegisterMistake(string feedbackMessage) => RegisterMistake(feedbackMessage, MistakeSeverity.Standard);

public void RegisterMistake(string feedbackMessage, MistakeSeverity severity)
{
    if (!isModuleActive || failureEscalated) return;

    if (currentStepIndex < stepErrorCounts.Length)
    {
        stepErrorCounts[currentStepIndex]++;
        stepPenaltyPoints[currentStepIndex] += (int)severity;
    }
    if (severity == MistakeSeverity.ProximityBreach) proximityBreachCount++;

    base.RegisterMistake(feedbackMessage);
    OnMistakeMade?.Invoke(feedbackMessage);
}
```
`StartModule()` gains `stepPenaltyPoints = new int[totalSteps]; proximityBreachCount = 0;` alongside the existing array resets.

`ARProximitySafetyValidator.cs:73` and `:97` change from:
```csharp
Modules.FireSafetyModuleManager.Instance.RegisterMistake("Step back! Don't stand within 3.5 ft. of the fire.");
```
to:
```csharp
Modules.FireSafetyModuleManager.Instance.RegisterMistake("Step back! Don't stand within 3.5 ft. of the fire.", Modules.FireSafetyModuleManager.MistakeSeverity.ProximityBreach);
```
(needs `using MiningSafetyAR.Modules;` added to that file if not already present).

### 3.4 Real step scoring, including two special-cased formulas

Most steps still use the simple `100 − penaltyPoints` formula. Two don't: Squeeze & Sweep is scored by technique quality (§3.5), and Evacuation is scored by how promptly the safe point was reached (§3.7). Rather than branch on step name inside `RecordStepMetric`, use a per-step override slot that a step can fill in before completing itself:

```csharp
private int?[] stepScoreOverride; // set by a step's own completion logic when it isn't the generic formula
```
initialized alongside the other arrays in `StartModule()`: `stepScoreOverride = new int?[totalSteps];`

```csharp
private void RecordStepMetric(int stepIndex)
{
    if (stepIndex >= totalSteps) return;
    if (stepIndex < stepStartTimes.Length && stepStartTimes[stepIndex] > 0f)
    {
        float duration = Time.time - stepStartTimes[stepIndex];
        int errors = stepIndex < stepErrorCounts.Length ? stepErrorCounts[stepIndex] : 0;
        int penalty = stepIndex < stepPenaltyPoints.Length ? stepPenaltyPoints[stepIndex] : 0;

        int stepScore = (stepIndex < stepScoreOverride.Length && stepScoreOverride[stepIndex].HasValue)
            ? stepScoreOverride[stepIndex].Value
            : Mathf.Clamp(pointsPerStep - penalty, 0, pointsPerStep);

        stepMetrics.Add(new StepMetric
        {
            stepName = StepNames[stepIndex],
            errorCount = errors,
            durationSeconds = duration,
            score = stepScore
        });
    }
}
```

`HandlePinPulled()` becomes a plain single completion (no more burst-loop, since there's nothing left before it to bundle):
```csharp
private void HandlePinPulled()
{
    if (isModuleActive && currentStepIndex == PullPinStepIndex)
    {
        CompleteCurrentStep();
    }
}
```

`HandleSprayStarted()` keeps a narrow safety net (in case the spray-start event fires before the pin-pull event is processed, which the current burst-loop was guarding against) but scoped only to the two real steps it could legitimately need to catch up through — not the old blanket burst to index 3:
```csharp
private void HandleSprayStarted()
{
    if (!isModuleActive) return;
    if (currentStepIndex == PullPinStepIndex) CompleteCurrentStep();   // catch-up if pin-pull event hadn't landed yet
    if (isModuleActive && currentStepIndex == AimSprayStepIndex) CompleteCurrentStep();
}
```

### 3.5 Squeeze & Sweep — quality-based score

`FireExtinguisherGrabController` already computes `currentSweepIntensity` every frame while spraying (`sweep.md` §4.1). Add a running average for the whole spray duration:

```csharp
// FireExtinguisherGrabController.cs
private float sweepIntensityAccumulator = 0f;
private int sweepIntensitySampleCount = 0;
public float AverageSweepIntensity => sweepIntensitySampleCount > 0 ? sweepIntensityAccumulator / sweepIntensitySampleCount : 0f;
```
Accumulated once per frame inside `UpdateSweepIntensity()` (right after `currentSweepIntensity` is updated):
```csharp
sweepIntensityAccumulator += currentSweepIntensity;
sweepIntensitySampleCount++;
```
Reset (`sweepIntensityAccumulator = 0f; sweepIntensitySampleCount = 0;`) everywhere `currentSweepIntensity = 0f;` is already reset today (`StartSqueezing()`, and the depletion/UpdateFoamSpray reset points already identified in `sweep.md` §4.1) — so the average is scoped to a single squeeze-and-spray attempt, not the whole session.

In `FireSafetyModuleManager`, when the fire goes out (§3.6), before calling `CompleteCurrentStep()` for the Squeeze & Sweep step:
```csharp
float avgIntensity = AR.FireExtinguisherGrabController.Instance != null
    ? AR.FireExtinguisherGrabController.Instance.AverageSweepIntensity : 0f;
int qualityScore = Mathf.RoundToInt(pointsPerStep * avgIntensity);
int penalty = stepPenaltyPoints[SqueezeSweepStepIndex];
stepScoreOverride[SqueezeSweepStepIndex] = Mathf.Clamp(qualityScore - penalty, sweepQualityFloor, pointsPerStep);
```
`sweepQualityFloor` (tunable, §13) prevents a technically-completed-but-zero-technique attempt from scoring literally 0 — there's a difference between "put the fire out standing still" and "never put it out," and the floor keeps that distinction visible instead of flooring both at zero.

### 3.6 Fire-extinguished handler — starts Evacuation instead of finishing the module

Replace:
```csharp
private void HandleFireExtinguished()
{
    if (isModuleActive)
    {
        while (isModuleActive && currentStepIndex < totalSteps)
        {
            CompleteCurrentStep();
        }
    }
}
```
with:
```csharp
private void HandleFireExtinguished()
{
    if (!isModuleActive) return;

    // Safety net: complete anything not already marked done (shouldn't normally be needed).
    while (isModuleActive && currentStepIndex < SqueezeSweepStepIndex)
    {
        CompleteCurrentStep();
    }
    if (!isModuleActive || currentStepIndex != SqueezeSweepStepIndex) return;

    float avgIntensity = AR.FireExtinguisherGrabController.Instance != null
        ? AR.FireExtinguisherGrabController.Instance.AverageSweepIntensity : 0f;
    int qualityScore = Mathf.RoundToInt(pointsPerStep * avgIntensity);
    int penalty = stepPenaltyPoints[SqueezeSweepStepIndex];
    stepScoreOverride[SqueezeSweepStepIndex] = Mathf.Clamp(qualityScore - penalty, sweepQualityFloor, pointsPerStep);

    CompleteCurrentStep(); // records Squeeze & Sweep, advances into Evacuation via OnStepStart
}
```
`OnStepStart(int stepIndex)` gets one more case:
```csharp
case EvacuationStepIndex:
    BeginEvacuation();
    break;
```

### 3.7 Evacuation step — built entirely from existing pieces (per `scoring.md` §3.0/§4)

New fields:
```csharp
[Header("Evacuation")]
[SerializeField] private float evacuationSafeDistance = 5.0f;
[SerializeField] private float evacuationArrivalRadius = 1.0f;
[SerializeField] private float evacuationSustainDuration = 1.5f;
[SerializeField] private float evacuationTimeBudgetSeconds = 15f;
[SerializeField] private float evacuationLatePenaltyPerSecond = 4f;
[SerializeField] private int evacuationScoreFloor = 40;

private Vector3 evacuationTargetPosition;
private Transform evacuationTargetAnchor; // empty transform, not a new asset — see note below
private float evacuationStepStartTime;
private float evacuationSustainedSince = -1f;
private bool isEvacuationActive = false;
```

```csharp
private void BeginEvacuation()
{
    Vector3 firePos = AR.ARPlacementManager.Instance != null && AR.ARPlacementManager.Instance.FireHazardTransform != null
        ? AR.ARPlacementManager.Instance.FireHazardTransform.position
        : transform.position;

    Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
    Vector3 retreatDir = cam != null ? (cam.transform.position - firePos) : Vector3.forward;
    retreatDir.y = 0f;
    if (retreatDir.sqrMagnitude < 0.01f)
    {
        // Degenerate case: player standing essentially on top of the fire's position.
        retreatDir = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
    }
    retreatDir.Normalize();

    evacuationTargetPosition = firePos + retreatDir * evacuationSafeDistance;

    // A plain, non-rendering anchor at the computed point — not a new prefab or model, just a
    // Transform for ARWaypointNavigationPointer.SetTarget() to point at (it only needs a Transform).
    var anchorObj = new GameObject("EvacuationTargetAnchor");
    anchorObj.transform.position = evacuationTargetPosition;
    evacuationTargetAnchor = anchorObj.transform;

    if (AR.ARWaypointNavigationPointer.Instance != null)
    {
        AR.ARWaypointNavigationPointer.Instance.SetTarget(evacuationTargetAnchor);
        AR.ARWaypointNavigationPointer.Instance.Activate();
    }

    evacuationStepStartTime = Time.time;
    evacuationSustainedSince = -1f;
    isEvacuationActive = true;
}
```

`FireSafetyModuleManager` currently has no `Update()` method — add one, gated so it costs nothing outside the Evacuation step:
```csharp
private void Update()
{
    if (!isEvacuationActive) return;

    Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
    if (cam == null) return;

    Vector3 camPos = cam.transform.position;
    float dx = camPos.x - evacuationTargetPosition.x;
    float dz = camPos.z - evacuationTargetPosition.z;
    float distanceToTarget = Mathf.Sqrt(dx * dx + dz * dz);

    if (distanceToTarget <= evacuationArrivalRadius)
    {
        if (evacuationSustainedSince < 0f) evacuationSustainedSince = Time.time;
        if (Time.time - evacuationSustainedSince >= evacuationSustainDuration)
        {
            CompleteEvacuation();
        }
    }
    else
    {
        evacuationSustainedSince = -1f;
    }
}

private void CompleteEvacuation()
{
    isEvacuationActive = false;

    if (AR.ARWaypointNavigationPointer.Instance != null) AR.ARWaypointNavigationPointer.Instance.Deactivate();
    if (evacuationTargetAnchor != null) Destroy(evacuationTargetAnchor.gameObject);

    float elapsed = Time.time - evacuationStepStartTime;
    float lateSeconds = Mathf.Max(0f, elapsed - evacuationTimeBudgetSeconds);
    int latenessDeduction = Mathf.RoundToInt(lateSeconds * evacuationLatePenaltyPerSecond);
    int penalty = stepPenaltyPoints[EvacuationStepIndex];

    stepScoreOverride[EvacuationStepIndex] = Mathf.Clamp(pointsPerStep - penalty - latenessDeduction, evacuationScoreFloor, pointsPerStep);

    CompleteCurrentStep(); // this is the last step — triggers FinishModule()
}
```

*Design note on why this doesn't reuse `ARProximitySafetyValidator` as a component:* that class's public API (`EnableChecking(Transform fireTarget)`, breach-when-too-close semantics) is specifically shaped around the "stay away from the fire" check and fires a *breach* event below a threshold. Evacuation needs the opposite shape — *arrival* at a point, sustained — and keeping that check inline in `FireSafetyModuleManager` (which already owns every other step's completion logic) keeps all step-completion logic in one place rather than splitting it across a repurposed unrelated component. The *technique* (XZ distance math + hysteresis-style sustained check) is reused, per `scoring.md` §4; the class itself isn't.

### 3.8 `FinishModule()` — package real data instead of saving directly

Today `FinishModule()` (`:189-224`) computes the real score and immediately calls `AppDataService.SaveAttempt()` and `LocalScoreManager.SaveResult()` — which is exactly the save that gets overwritten a few seconds later by the quiz page's hardcoded-`80` version (`scoring.md` §1.2). Fix: keep the local save (that one's harmless — it's a local JSON backup, not the one causing the conflict), but stop calling `AppDataService.SaveAttempt()` from here. Instead, package everything the quiz page will need to finish the job in one place:

```csharp
protected override void FinishModule()
{
    isModuleActive = false;
    float timeTaken = Time.time - startTime;

    int drillScore = GetTotalScore();          // sum of the 4 real step scores
    int drillMaxScore = GetMaxPossibleScore();  // 4 × pointsPerStep
    float drillPercentage = drillMaxScore > 0 ? (float)drillScore / drillMaxScore * 100f : 0f;

    // Competency inputs derived from real gameplay — see §3.9 for the exact formulas.
    int hazardRecognitionPct = ComputeHazardRecognitionScore();
    int extinguisherUsePct   = ComputeExtinguisherUseScore();
    int timeManagementPct    = ComputeTimeScore(timeTaken);
    int evacuationPct        = stepMetrics.Count > EvacuationStepIndex ? stepMetrics[EvacuationStepIndex].score : 0;

    TrainingResult localResult = new TrainingResult
    {
        workerId = PlayerPrefs.GetString("WorkerID", "WORKER_001"),
        moduleName = moduleName,
        score = drillScore,
        maxScore = drillMaxScore,
        percentage = drillPercentage,
        passed = drillPercentage >= ScoringConstants.PassThresholdPercentage,
        mistakesCount = mistakesCount,
        completionTimeSeconds = timeTaken,
        stepMetrics = new List<StepMetric>(stepMetrics)
    };
    if (LocalScoreManager.Instance != null) LocalScoreManager.Instance.SaveResult(localResult); // local backup only — not the shared/Firestore save

    lastDrillResult = new DrillResultPayload
    {
        drillScorePercentage = drillPercentage,
        mistakesCount = mistakesCount,
        completionTimeSeconds = timeTaken,
        stepMetrics = new List<StepMetric>(stepMetrics),
        hazardRecognitionPct = hazardRecognitionPct,
        extinguisherUsePct = extinguisherUsePct,
        timeManagementPct = timeManagementPct,
        evacuationPct = evacuationPct
    };

    Debug.Log($"[FireSafetyModuleManager] Drill finished. Steps: {stepMetrics.Count}, Drill Score: {drillScore}/{drillMaxScore} ({drillPercentage:F1}%).");
    OnModuleCompletedWithMetrics?.Invoke(new List<StepMetric>(stepMetrics));
}

public DrillResultPayload LastDrillResult => lastDrillResult;
private DrillResultPayload lastDrillResult;
```

New small carrier type (put in `LocalDataModels.cs` alongside `StepMetric`, or a new file — either is fine, just needs to exist once):
```csharp
[Serializable]
public class DrillResultPayload
{
    public float drillScorePercentage;
    public int mistakesCount;
    public float completionTimeSeconds;
    public List<StepMetric> stepMetrics;
    public int hazardRecognitionPct;
    public int extinguisherUsePct;
    public int timeManagementPct;
    public int evacuationPct;
}
```

### 3.9 Competency formulas — exact, not just "inverse of breach count"

```csharp
private int ComputeHazardRecognitionScore()
{
    // Tunable: each proximity breach costs 20 points off a 100 base.
    return Mathf.Clamp(100 - proximityBreachCount * hazardRecognitionPenaltyPerBreach, 0, 100);
}

private int ComputeExtinguisherUseScore()
{
    // Average of the 3 hands-on technique steps (Pull Pin, Aim & Spray, Squeeze & Sweep).
    if (stepMetrics.Count < SqueezeSweepStepIndex + 1) return 0;
    int sum = stepMetrics[PullPinStepIndex].score + stepMetrics[AimSprayStepIndex].score + stepMetrics[SqueezeSweepStepIndex].score;
    return Mathf.RoundToInt(sum / 3f);
}

private int ComputeTimeScore(float actualSeconds)
{
    float overage = Mathf.Max(0f, actualSeconds - parTimeSeconds);
    return Mathf.Clamp(100 - Mathf.RoundToInt(overage * timeScorePointsLostPerSecondOver), 0, 100);
}
```
`hazardRecognitionPenaltyPerBreach`, `parTimeSeconds`, `timeScorePointsLostPerSecondOver` are new `[SerializeField]` tunables (§13).

---

## 4. `AssessmentPageController.cs` — receiving the real drill data

### 4.1 What the drill hands off

`ARSimulationPageController.RedirectToQuizAfterDelay()` currently does:
```csharp
NavigationManager.Instance?.NavigateTo("UI_Assessment", targetModule);
```
passing a bare string. Change to package a full dictionary from `FireSafetyModuleManager.Instance.LastDrillResult` (§3.8):
```csharp
var payload = FireSafetyModuleManager.Instance?.LastDrillResult;
var navParam = new Dictionary<string, object>
{
    { "moduleId", targetModule },
    { "simulationScore", payload != null ? Mathf.RoundToInt(payload.drillScorePercentage) : 0 },
    { "drillMistakesCount", payload?.mistakesCount ?? 0 },
    { "drillTimeSeconds", payload?.completionTimeSeconds ?? 0f },
    { "hazardRecognitionPct", payload?.hazardRecognitionPct ?? 0 },
    { "extinguisherUsePct", payload?.extinguisherUsePct ?? 0 },
    { "timeManagementPct", payload?.timeManagementPct ?? 0 },
    { "evacuationPct", payload?.evacuationPct ?? 0 }
};
NavigationManager.Instance?.NavigateTo("UI_Assessment", navParam);
```
Note `simulationScore` is the same key `AssessmentPageController.SetNavigationParameter` already parses (`:54`) — today nothing ever populates it in this flow, which is exactly why the hardcoded `80` default always wins. This is the one-line root cause fix underneath everything else in this section.

### 4.2 Parsing the rest

`SetNavigationParameter` (`:44-56` roughly) already does `if (dict.TryGetValue("simulationScore", out var sim)) simulationScore = ...`. Add the same pattern for each new key, into new private fields (`drillMistakesCount`, `drillTimeSeconds`, `hazardRecognitionPct`, `extinguisherUsePct`, `timeManagementPct`, `evacuationPct`), defaulted to 0 so direct-play testing of this page in isolation doesn't crash — it just produces a 0-drill-score result, same honesty principle as the rest of this rework (no fake fallback number, an honest zero instead).

### 4.3 `ShowResults()` — the one place the final score is computed and saved

Replace:
```csharp
int mcqScore = total > 0 ? (int)((float)correctCount / total * 100f) : 0;
int finalScore = (int)(simulationScore * 0.6f + mcqScore * 0.4f);
bool passed = finalScore >= 75;
if (AppDataService.Instance != null)
{
    AppDataService.Instance.SaveAttempt(moduleId, finalScore, passed);
    AppDataService.Instance.UpdateModuleCompetencyScores(moduleId, correctByCompetency, totalByCompetency);
}
```
with:
```csharp
int mcqScore = total > 0 ? (int)((float)correctCount / total * 100f) : 0;
int finalScore = Mathf.RoundToInt(simulationScore * ScoringConstants.DrillWeight + mcqScore * ScoringConstants.QuizWeight);
bool passed = finalScore >= ScoringConstants.PassThresholdPercentage;

if (AppDataService.Instance != null)
{
    AppDataService.Instance.SaveAttempt(
        moduleId, finalScore, passed,
        mistakesCount: drillMistakesCount,
        completionTimeSeconds: drillTimeSeconds,
        stepMetrics: null /* already saved locally by FireSafetyModuleManager; see §5.1 note */
    );

    // Gameplay-derived competencies are the primary source (per scoring.md §8 Q4 default:
    // quiz does NOT additionally nudge competencies — it only affects Final Score above).
    AppDataService.Instance.UpdateModuleCompetencyScoresFromDrill(
        moduleId, hazardRecognitionPct, extinguisherUsePct, timeManagementPct, evacuationPct);
}
```
This is now the **only** place `SaveAttempt` and any competency-updating method get called for a Fire & Explosion attempt — one call, one number, matching `scoring.md` §2 goal 1.

**Decided (2026-09-05):** quiz answers do NOT nudge competency scores — confirmed final. Competency bars stay purely gameplay-driven; quiz only affects the Final Score blend above. `emergencyResponse` was also confirmed to stay unsurfaced — it's moot for this module anyway, since none of Fire & Explosion's quiz questions are tagged `emergencyResponse` (only `hazardRecognition`, `extinguisherUse`, `evacuation` are used — see `Phase1Seeder.cs`), and the drill produces no signal for it either. The 70/30 drill/quiz weighting and 70% pass threshold (`ScoringConstants`) are also confirmed as final, no change.

---

## 5. `AppDataService.cs` — the write side

### 5.1 Expanded `SaveAttempt`

Current signature only takes `(moduleId, score, passed)` and hardcodes `mistakesCount = 0, completionTimeSeconds = 0` (`:699-725`). Add an overload (keep the old one for `BaseModuleManager`'s still-separate Gas Leak path, untouched per `scoring.md` §8 Q6, so nothing there breaks):
```csharp
public void SaveAttempt(string moduleId, int score, bool passed, int mistakesCount = 0, float completionTimeSeconds = 0f, List<StepMetric> stepMetrics = null)
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
    // ...rest unchanged (allAttempts.Add, SaveAttemptsLocally, Firestore save, UpdateLocalProgress)
}
```
Because the new parameters have defaults, every existing call site (`AssessmentEngine.cs:107`, `BaseModuleManager.cs:97`, and any other module manager) keeps compiling unchanged — only the Fire & Explosion call site in §4.3 passes the real values.

### 5.2 New `UpdateModuleCompetencyScoresFromDrill`

Sits alongside the existing quiz-driven `UpdateModuleCompetencyScores` (`:588-631`), same best-score-ratchet pattern (`Mathf.Max`), same Firestore-save-then-cache tail:
```csharp
public void UpdateModuleCompetencyScoresFromDrill(string moduleId, int hazardRecognitionPct, int extinguisherUsePct, int timeManagementPct, int evacuationPct)
{
    if (string.IsNullOrEmpty(moduleId)) return;
    var prog = GetModuleProgress(moduleId);
    if (prog == null) return;
    if (prog.competencyScores == null) prog.competencyScores = new CompetencyScores();

    prog.competencyScores.hazardRecognition = Mathf.Max(prog.competencyScores.hazardRecognition, hazardRecognitionPct);
    prog.competencyScores.extinguisherUse   = Mathf.Max(prog.competencyScores.extinguisherUse, extinguisherUsePct);
    prog.competencyScores.timeManagement    = Mathf.Max(prog.competencyScores.timeManagement, timeManagementPct);
    prog.competencyScores.evacuation        = Mathf.Max(prog.competencyScores.evacuation, evacuationPct);

    SaveModuleProgressToFirestore(CurrentWorker.firebaseUid, moduleId, prog);
    PlayerPrefs.SetString("ProgressMap_" + CurrentWorker.firebaseUid, ProgressMapToJson());
    PlayerPrefs.Save();
}
```

### 5.3 `emergencyResponse` vs. `quizScore` — decided: swap the 5th bar

**Decided (2026-09-05):** no `emergencyResponse` bar for Fire & Explosion — it has no real signal feeding it for this module (only `hazardRecognition`/`extinguisherUse`/`evacuation` are quiz-tagged here, see `Phase1Seeder.cs`), and computing a synthetic composite for it would just be another fake number. `emergencyResponse` itself is untouched and stays live for Gas Leak / Machinery Safety / Electrical Safety, whose own quiz questions are tagged with it — this decision is Fire & Explosion-scoped only.

Instead, added a new dedicated field, `CompetencyScores.quizScore` (`ModuleData.cs`), populated from the real end-of-module MCQ percentage (`mcqScore` in `AssessmentPageController.ShowResults()`) — not a synthetic composite, an actual measured value. `UpdateModuleCompetencyScoresFromDrill` takes it as a 5th parameter and ratchets it with `Mathf.Max` exactly like the other four. It is purely an additional display bar — it does not change the pass/fail formula, which already includes the quiz via the existing `DrillWeight`/`QuizWeight` blend in §3.6/§4.1.

---

## 6. UI-layer changes

### 6.1 `ARSimulationPageController.cs`

- `BuildScoreHistoryTable` (`:574-621`) needs **no logic change** — it already iterates whatever `List<StepMetric>` it's given generically. Since `FireSafetyModuleManager.GetStepMetrics()` now only ever contains the 4 real steps, the table is correct automatically once §3 lands.
- `TestShowScoreHistoryModalTable()` (`:905-917`, a `[ContextMenu]` test helper) — trim its 5-entry mock list down to 4 (drop "Sound Alarm" and "Select Extinguisher" rows) so the test menu matches reality.
- `OnRetryClicked` (`:728-742`) resets `currentScore = 100` as a placeholder display value before the drill restarts — harmless, unrelated to the scoring fix, no change needed.

### 6.2 `ARGuidanceController.cs`

`contextualHints` (5 entries, indexed 0-4 today) re-indexes to 4, matching the new step order:
```csharp
private string[] contextualHints = new string[]
{
    "Pull the safety pin on the extinguisher handle to unlock mechanism.",
    "Aim the nozzle at the base of the fire and test spray.",
    "Squeeze handle firmly and sweep nozzle side-to-side across base of flames.",
    "Move to the safe distance shown by the arrow to complete evacuation."
};
```
Separately (a pre-existing bug, not caused by this rework, but worth fixing while touching this file): `SetCurrentStepIndex(int)` is declared but **never called by anything** (confirmed via project-wide search), so `cachedStepIndex` is permanently `0` and the hint button always shows the Pull Pin hint no matter what step the player is actually on. Fix by calling it from `ARSimulationPageController.OnStepChanged` (`:780-787`):
```csharp
private void OnStepChanged(int step, string instruction)
{
    ShowTier1Info(instruction);
    ARGuidanceController.Instance?.SetCurrentStepIndex(step);
    if (FireSafetyModuleManager.Instance != null) UpdateScoreBadge(FireSafetyModuleManager.Instance.GetTotalScore());
}
```

### 6.3 `ResultsPageController.cs` / `ModuleDetailPageController.cs`

Both currently do `AddScoreBar("Time Taken", cs.ppeSelection)` / `AddScoreBar("Time Taken", mod.competencyScores.ppeSelection)`. Change both to `cs.timeManagement` / `mod.competencyScores.timeManagement` (§2). No other changes needed in either file — `AddScoreBar` and `ScoreBarHelper.Configure` are already generic over label + value.

---

## 7. What's deliberately *not* touched

- `GasLeakModuleManager.cs` and its `BaseModuleManager.FinishModule()` path — still calls the old, narrower `AppDataService.SaveAttempt(moduleName, (int)percentage, passed)` 3-arg form, which keeps compiling unchanged since the new parameters are optional (§5.1). Its own version of the "steps complete with no real check" problem (`OnIsolationValveClosed()`, etc.) is out of scope per `scoring.md` §8 Q6 unless you decide otherwise.
- `AssessmentEngine.cs` — the *separate* standalone-quiz-mode engine (not the one `AssessmentPageController` actually uses for the post-drill quiz — that page has its own inline question/answer handling). Only its `passThresholdPercentage` field is touched (§1), nothing else.
- Certificate issuance (`AppDataService.cs` ~`:740-767`) — reads `bestScore`, unaffected structurally; it just receives a trustworthy number once this lands instead of a possibly-fake one.
- The AR ground-placement height issue — separate, already-diagnosed, explicitly deferred.

---

## 8. Sequence of events, end to end, after this is implemented

1. Drill starts. `FireSafetyModuleManager.StartModule()` resets all per-step arrays, starts step 0 (Pull Pin).
2. Player pulls pin → `HandlePinPulled()` → step 0 completes for real, individually timed.
3. Player aims and test-sprays → `OnSprayStarted` → step 1 completes.
4. Player squeezes and sweeps. `FireExtinguisherGrabController` accumulates `AverageSweepIntensity` every frame. Proximity breaches during this window call `RegisterMistake(..., MistakeSeverity.ProximityBreach)`, costing −50 each and incrementing `proximityBreachCount`.
5. Fire reaches 0 HP → `GroundFireController.OnFireExtinguished` → `HandleFireExtinguished()` computes the quality-based Squeeze & Sweep score and completes step 2.
6. `OnStepStart(EvacuationStepIndex)` → `BeginEvacuation()` computes the safe point off the (still-valid, deactivated-not-destroyed) fire hazard transform, activates the existing `ARWaypointNavigationPointer`.
7. Player walks to the point, sustains for `evacuationSustainDuration` → `CompleteEvacuation()` computes the lateness-adjusted score, completes step 3 (the last step) → `FinishModule()` runs.
8. `FinishModule()` computes the real drill percentage, the four competency percentages, saves a **local-only** backup copy, and stores everything in `LastDrillResult` — does **not** call `AppDataService.SaveAttempt` yet.
9. `ARSimulationPageController.RedirectToQuizAfterDelay` reads `LastDrillResult`, packages it into a navigation dictionary, hands off to `UI_Assessment`.
10. `AssessmentPageController` parses the real drill numbers out of that dictionary (no more hardcoded `80`), runs the quiz as today, and in `ShowResults()` computes `finalScore = drillScore×0.70 + quizScore×0.30` **once**, calls `AppDataService.SaveAttempt` **once** with the full real data, and calls `UpdateModuleCompetencyScoresFromDrill` **once**.
11. Results screen renders one real Final Score, one real pass/fail state, and four competency bars each backed by an actual formula over actual gameplay data.

---

## 9. Manual verification checklist (once implemented)

- Score History modal during a drill shows exactly 4 rows (Pull Pin, Aim & Test Spray, Squeeze & Sweep, Evacuate to Safe Distance) — never Sound Alarm or Select Extinguisher.
- Standing near the fire during spraying visibly costs more (−50) than a generic mistake would.
- Spraying without any side-to-side motion still eventually puts the fire out (per `sweep.md`'s base rate) but the Squeeze & Sweep row shows a low score (down to `sweepQualityFloor`), not 100.
- After the fire goes out, the blue arrow actually appears and points somewhere sensible; walking to it and stopping completes the drill; walking through it without stopping does *not*.
- `totalAttempts` on the worker profile increases by exactly **1** per full drill+quiz playthrough, not 2.
- The Results screen's Final Score changes visibly between a clean run and a mistake-heavy run of the *same* quiz answers — proving the drill half is no longer a fixed constant.
- All four Competency bars show non-zero, differentiated values after a real drill, even before any quiz question tagged with a matching competency is ever answered.
- The same pass/fail cutoff (70%) is what every screen agrees on — no more "60%" copy anywhere.

---

## 10. Renumbered tunables (referenced throughout this document)

| Constant | Location | Proposed value | Purpose |
|---|---|---|---|
| `ScoringConstants.PassThresholdPercentage` | new `ScoringConstants.cs` | 70 | Single pass/fail cutoff, used everywhere |
| `ScoringConstants.GenericMistakePenalty` | new `ScoringConstants.cs` | 25 | Default mistake cost |
| `ScoringConstants.ProximityBreachPenalty` | new `ScoringConstants.cs` | 50 | Proximity-breach mistake cost (finally applies the existing-but-unused field) |
| `ScoringConstants.DrillWeight` / `QuizWeight` | new `ScoringConstants.cs` | 0.70 / 0.30 | Final score blend |
| `sweepQualityFloor` | `FireSafetyModuleManager` | 30 | Minimum Squeeze & Sweep score even at zero measured technique |
| `evacuationSafeDistance` | `FireSafetyModuleManager` | 5.0 m | Distance from fire the safe point is placed at |
| `evacuationArrivalRadius` | `FireSafetyModuleManager` | 1.0 m | How close to the safe point counts as "arrived" |
| `evacuationSustainDuration` | `FireSafetyModuleManager` | 1.5 s | Must remain at the point this long (prevents pass-through completion) |
| `evacuationTimeBudgetSeconds` | `FireSafetyModuleManager` | 15 s | Par time before lateness starts costing points |
| `evacuationLatePenaltyPerSecond` | `FireSafetyModuleManager` | 4 | Points lost per second over budget |
| `evacuationScoreFloor` | `FireSafetyModuleManager` | 40 | Minimum Evacuation score even if very late |
| `hazardRecognitionPenaltyPerBreach` | `FireSafetyModuleManager` | 20 | Points off Hazard Recognition per proximity breach across the whole drill |
| `parTimeSeconds` | `FireSafetyModuleManager` | 40 s | Target total drill time for the Time competency |
| `timeScorePointsLostPerSecondOver` | `FireSafetyModuleManager` | 2 | Points lost per second over the par time |

All of the above are plain `[SerializeField]` fields (or `const`s for the shared ones) — tunable live in the Inspector without touching code, same convention `sweep.md` §6 already established.
