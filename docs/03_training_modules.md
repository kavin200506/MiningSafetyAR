# 03 — Training Modules

## Module Architecture

All training modules share a common abstract base class and implement scenario-specific logic in subclasses.

### Base Class

**File:** `Assets/Scripts/Modules/BaseModuleManager.cs`

`BaseModuleManager : MonoBehaviour` — abstract class.

#### Key Fields

| Field | Type | Value |
|---|---|---|
| `moduleType` | `ModuleType` (enum) | Set in subclass `Awake()` |
| `moduleName` | `string` | Set in subclass `Awake()` |
| `totalSteps` | `int` | Set in subclass `Awake()` |
| `baseScorePerStep` | `int` | `25` (default) |
| `currentStepIndex` | `int` | 0-based step index |
| `mistakesCount` | `int` | Incremented by `RegisterMistake()` |
| `isModuleActive` | `bool` | Set `true` on `StartModule()` |

#### Events

| Event | Signature | Description |
|---|---|---|
| `OnStepChanged` | `Action<int, string>` | Fires when advancing to next step |
| `OnModuleCompleted` | `Action<TrainingResult>` | Fires when all steps are done |
| `OnMistakeMade` | `Action<string>` | Fires with feedback message |

#### Scoring Formula (in `FinishModule()`)

```
maxScore = totalSteps * baseScorePerStep       // e.g. 4 * 25 = 100
penalty  = mistakesCount * 5                   // -5 per mistake
finalScore = Clamp(maxScore - penalty, 0, max)
percentage = finalScore / maxScore * 100
passed = percentage >= 70f
```

Results are saved to both `LocalScoreManager` (JSON on disk) and `AppDataService` (→ Firestore).

---

## Module 1: Fire & Explosion Response

**File:** `Assets/Scripts/Modules/FireSafetyModuleManager.cs`

`FireSafetyModuleManager : BaseModuleManager`

```
moduleType = ModuleType.FireAndExplosion
moduleName = "Fire & Explosion Response"
totalSteps = 4
```

### Steps

| Step | Index | Description | Completion Method |
|---|---|---|---|
| 1 | 0 | Identify emergency exit | `OnEmergencyExitTapped(bool isCorrectExit)` |
| 2 | 1 | Select correct extinguisher type (CO2/Dry Powder) | `OnExtinguisherSelected(bool isValidExtinguisherType)` |
| 3 | 2 | Execute P.A.S.S. technique | `PerformPASSSubStep(string passStep)` |
| 4 | 3 | Follow evacuation arrows to safety zone | `OnSafetyZoneReached()` |

### P.A.S.S. Sub-Step Logic

`PerformPASSSubStep(string passStep)` enforces strict ordering:

```
PULL → AIM → SQUEEZE → SWEEP
```

- Attempting `AIM` without `PULL` → `RegisterMistake("Must pull the pin before aiming!")`
- Attempting `SQUEEZE` without `AIM` → `RegisterMistake("Must aim nozzle at the base of the fire...")`
- Attempting `SWEEP` without `SQUEEZE` → `RegisterMistake("Must squeeze handle before sweeping!")`
- All 4 steps complete → `CompleteCurrentStep()` advances to Step 4

### Wrong Extinguisher Selection

`OnExtinguisherSelected(false)` → `RegisterMistake("Selected wrong extinguisher type for electrical/chemical fire!")`

### Audio Clips

Each step has 3 audio clips (EN, HI, SAT):
- `step1AudioEN/HI/SAT` through `step4AudioEN/HI/SAT`
- Played via `LanguageManager.Instance.PlayVoiceover(en, hi, sat)`

### AR Instruction Text Per Step (GetStepInstruction)

```
Step 0: "Step 1: Identify the primary emergency exit door in the AR environment."
Step 1: "Step 2: Locate and select the correct CO2/Dry Powder Fire Extinguisher."
Step 2: "Step 3: Execute the P.A.S.S. technique: Pull Pin -> Aim at base -> Squeeze -> Sweep."
Step 3: "Step 4: Follow the AR evacuation arrows to reach the safe assembly zone."
```

---

## Module 2: Gas Leak & Confined Space Protocol

**File:** `Assets/Scripts/Modules/GasLeakModuleManager.cs`

`GasLeakModuleManager : BaseModuleManager`

```
moduleType = ModuleType.GasLeakAndConfinedSpace
moduleName = "Gas Leak & Confined Space Protocol"
totalSteps = 4
```

### Steps

| Step | Index | Description | Completion Method |
|---|---|---|---|
| 1 | 0 | Identify gas leak source using multi-gas detector | `OnGasLeakSourceIdentified(bool isCorrectSource)` |
| 2 | 1 | Equip SCBA from AR safety locker | `OnPPESelected(bool selectedSCBA)` |
| 3 | 2 | Signal standby buddy & establish radio contact | `OnBuddySignaled(bool signalSent)` |
| 4 | 3 | Close gas isolation valve & evacuate | `OnIsolationValveClosed()` |

### Mistake Messages

| Wrong Action | Feedback Message |
|---|---|
| Wrong leak source | `"Selected incorrect location! Monitor the gas detector reading for methane/CO buildup."` |
| Inadequate PPE (non-SCBA) | `"Standard dust mask is insufficient for toxic/oxygen-deficient confined space! Use SCBA."` |
| No buddy signal | `"Never enter a confined space without verifying communications with your standby buddy!"` |

### AR Instruction Text Per Step

```
Step 0: "Step 1: Use your multi-gas detector to pinpoint the toxic methane/CO gas leak source."
Step 1: "Step 2: Equip the Self-Contained Breathing Apparatus (SCBA) from the AR safety locker."
Step 2: "Step 3: Signal your standby buddy miner and establish radio contact before entering."
Step 3: "Step 4: Close the gas isolation valve and evacuate the confined workspace immediately."
```

---

## Module Data Structure

**File:** `Assets/Scripts/Data/ModuleData.cs`

```csharp
public class ModuleData {
    public string id;           // e.g. "fire_safety"
    public string title;        // e.g. "Fire & Explosion Response"
    public string iconEmoji;    // e.g. "🔥"
    public string domain;       // e.g. "Fire Safety"
    public string duration;     // e.g. "45 min"
    public string difficulty;   // "Medium" | "Hard"
    public ModuleStatus status; // NotStarted | InProgress | Completed | Locked
    public int progress;        // 0-100
    public int bestScore;       // 0-100
    public int attempts;
    public string lastAttempt;  // ISO date string
    public string certificateId;
    public string color;        // hex color for UI
    public string description;
    public string[] objectives;
    public CompetencyScores competencyScores;
}
```

**`ModuleProgress`** (per-worker Firestore data):
```csharp
public class ModuleProgress {
    public string moduleId;
    public ModuleStatus status;
    public int progress;
    public int bestScore;
    public int attempts;
    public string lastAttempt;
    public string certificateId;
    public CompetencyScores competencyScores;
}
```

**`CompetencyScores`**:
```csharp
public class CompetencyScores {
    public int hazardRecognition;
    public int extinguisherUse;
    public int ppeSelection;
    public int evacuation;
    public int emergencyResponse;
}
```

---

## Module Database (ScriptableObject)

**Asset:** `Assets/Data/ModuleDatabase.asset`  
**Script:** `Assets/Scripts/Data/ModuleDatabase.cs` (ScriptableObject)

5 modules seeded by `Phase1Seeder.cs`:

| ID | Title | Difficulty | Status (Demo) |
|---|---|---|---|
| `fire_safety` | Fire & Explosion Response | Medium | Completed (85%) |
| `gas_safety` | Gas Leak & Confined Space | Hard | Completed (72%) |
| `machinery_safety` | Machinery Safety | Medium | InProgress (45%) |
| `electrical_safety` | Electrical Safety | Medium | NotStarted |
| `heights_safety` | Working at Heights | Hard | Locked |

---

## Ground Fire VFX

**File:** `Assets/Scripts/Modules/GroundFireController.cs`

`GroundFireController` manages the `GroundFireParticles.prefab` (Vefects URP pack):

- `groundFireParticles[]` — array of `ParticleSystem` children (flames, embers, smoke).
- `lowSpecMode = true` — disables heat haze / distortion child objects on mobile.
- `IgniteFire()` / `ExtinguishFire()` — public methods called by simulation events.
- `ApplyLowSpecOptimizations()` — runs in `Awake()`, disables VFX systems named "HeatHaze", "Distortion", "Smoke" if `lowSpecMode = true`.

---

## AR Simulation Page Controller

**File:** `Assets/Scripts/UI/Pages/ARSimulationPageController.cs`

Drives the transparent HUD overlay over the AR scene:
- `score` starts at 100, decremented by mistake events.
- `timeLeft = 180f` (3-minute countdown timer).
- Timer color: green (>60s), yellow (>30s), red (≤30s).
- Loads `AR Plane Detection Placement` scene **additively** via `SceneManager.LoadSceneAsync(... LoadSceneMode.Additive)`.

---

## NOT IMPLEMENTED — Additional Modules (Flagged against Roadmap)

| Module | Status |
|---|---|
| Machinery Safety (machinery_safety) | Database entry exists; NO `MachineryModuleManager.cs` — AR simulation not built |
| Electrical Safety (electrical_safety) | Database entry exists; NO `ElectricalModuleManager.cs` — AR simulation not built |
| Working at Heights (heights_safety) | Database entry exists (Locked); NO module manager — AR simulation not built |
