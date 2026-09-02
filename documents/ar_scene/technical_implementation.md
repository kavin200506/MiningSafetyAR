# Technical Implementation Plan — AR Fire Safety Training Module

## Document Purpose
This document serves as the step-by-step implementation guide for building the **AR Fire Safety Training Module** in Unity. It covers all file changes, new scripts, UXML/USS assets, data model additions, and scene wiring required to match the specification in `ar_fire_safety_training_specification.md`.

---

## 1. Constraints & Scope

| Rule | Detail |
|------|--------|
| **Working Scene** | `Assets/Scenes/ar_fire_safety.unity` — this is the only scene we modify |
| **Additive Scenes** | New scenes can be added if needed |
| **Protected Scenes** | NO modifications to UI_Splash, UI_Login, UI_Register, UI_Dashboard, or any other existing UI scene |
| **UI Assets** | New UXML/USS files created under `Assets/UI/` for the AR overlay |
| **Audio** | Code will reference AudioClip fields but no audio integration now (assign later in Inspector) |

---

## 2. File Inventory

### New Files to Create (5 files)

| # | File Path | Lines (est.) | Purpose |
|---|-----------|-------------|---------|
| 1 | `Assets/Scripts/AR/ARProximitySafetyValidator.cs` | ~120 | Real-time 3.5ft distance check with hysteresis |
| 2 | `Assets/Scripts/AR/ARWaypointNavigationPointer.cs` | ~100 | Floor arrow pointing to equipment/target |
| 3 | `Assets/Scripts/AR/ARGuidanceController.cs` | ~150 | Tutorial tips + on-demand hint system |
| 4 | `Assets/UI/Templates/Pages/ARSimulationOverlay.uxml` | ~200 | Full AR HUD overlay layout |
| 5 | `Assets/UI/Styles/ARSimulation.uss` | ~180 | AR overlay styles |

### Existing Files to Modify (6 files)

| # | File Path | Current Lines | Change Scope |
|---|-----------|--------------|--------------|
| 6 | `Assets/Scripts/Data/LocalDataModels.cs` | ~90 | Add StepMetric struct (~15 lines) |
| 7 | `Assets/Scripts/Modules/GroundFireController.cs` | 162 | Major — fire health system (~100 lines added) |
| 8 | `Assets/Scripts/AR/FireExtinguisherGrabController.cs` | 662 | Major — P.A.S.S. + foam depletion (~200 lines) |
| 9 | `Assets/Scripts/Modules/FireSafetyModuleManager.cs` | 141 | Major — 5-step state machine rewrite (~200 lines) |
| 10 | `Assets/Scripts/UI/Pages/ARSimulationPageController.cs` | 102 | Major — full HUD rewrite (~250 lines) |
| 11 | `Assets/Scripts/AR/ARPlacementManager.cs` | 742 | Minor — event hooks (~30 lines) |

### Total: 11 files, ~1250 lines of new/modified code

---

## 3. Implementation Order

| Step | Action | Files | Depends On |
|------|--------|-------|------------|
| 1 | Write this plan file | `technical_implementation.md` | — |
| 2 | Add StepMetric data model | `LocalDataModels.cs` | — |
| 3 | Enhance GroundFireController | `GroundFireController.cs` | Step 2 |
| 4 | Create ARProximitySafetyValidator | `ARProximitySafetyValidator.cs` | Step 3 |
| 5 | Create ARWaypointNavigationPointer | `ARWaypointNavigationPointer.cs` | — |
| 6 | Create ARGuidanceController | `ARGuidanceController.cs` | — |
| 7 | Rewrite FireExtinguisherGrabController | `FireExtinguisherGrabController.cs` | Steps 3, 4 |
| 8 | Rewrite FireSafetyModuleManager | `FireSafetyModuleManager.cs` | Steps 2, 3, 7 |
| 9 | Create ARSimulationOverlay.uxml | `ARSimulationOverlay.uxml` | — |
| 10 | Create ARSimulation.uss | `ARSimulation.uss` | — |
| 11 | Rewrite ARSimulationPageController | `ARSimulationPageController.cs` | Steps 4-9 |
| 12 | Modify ARPlacementManager (minor) | `ARPlacementManager.cs` | — |
| 13 | Scene wiring in Unity Editor | `ar_fire_safety.unity` | All above |

---

## 4. Detailed Script Specifications

---

### STEP 2: Add StepMetric to LocalDataModels.cs

**File:** `Assets/Scripts/Data/LocalDataModels.cs`

**What to add:** A new `StepMetric` struct after the existing `TrainingResult` class.

```csharp
[Serializable]
public struct StepMetric
{
    public string stepName;
    public int errorCount;
    public float durationSeconds;
    public int score;        // Per-step score out of 100
}
```

**Why:** Used by `FireSafetyModuleManager` to track per-step metrics and by `ARSimulationPageController` to render the Score History Modal table.

---

### STEP 3: Enhance GroundFireController.cs

**File:** `Assets/Scripts/Modules/GroundFireController.cs`

**Current state:** Simple ignite/extinguish with low-spec optimization. No health system.

**Changes:**

#### 3a. Add Fire Health Fields
```csharp
[Header("Fire Health System")]
[SerializeField] private float maxFireHealth = 100f;
[SerializeField] private float foamPower = 8f;  // damage per second of foam contact
private float currentFireHealth;
private Light fireLight;
private float initialLightIntensity;
private float initialEmissionRate;
```

#### 3b. Add ApplyFoamSuppression Method
Called by `FireExtinguisherGrabController` when foam raycast hits the fire:
```csharp
public void ApplyFoamSuppression(Vector3 hitPoint, float deltaTime)
{
    if (!isFireActive || currentFireHealth <= 0f) return;

    currentFireHealth -= foamPower * deltaTime;
    currentFireHealth = Mathf.Max(0f, currentFireHealth);

    float normalizedHealth = Mathf.Clamp01(currentFireHealth / maxFireHealth);

    // Scale particle emission rate
    foreach (var ps in groundFireParticles)
    {
        if (ps != null && ps.isPlaying)
        {
            var emission = ps.emission;
            emission.rateOverTime = initialEmissionRate * normalizedHealth;
        }
    }

    // Scale fire light intensity
    if (fireLight != null)
    {
        fireLight.intensity = initialLightIntensity * normalizedHealth;
    }

    // Scale fire visual size
    transform.localScale = Vector3.one * (0.2f + 0.8f * normalizedHealth);

    if (currentFireHealth <= 0f)
    {
        ExtinguishFireInstant();
        OnFireExtinguished?.Invoke();
    }
}
```

#### 3c. Add Events and Initialization
```csharp
public event Action OnFireExtinguished;

// In IgniteFire(), add:
currentFireHealth = maxFireHealth;
fireLight = GetComponentInChildren<Light>();
if (fireLight != null) initialLightIntensity = fireLight.intensity;
// Capture initial emission rate from first particle system
```

---

### STEP 4: Create ARProximitySafetyValidator.cs

**File:** `Assets/Scripts/AR/ARProximitySafetyValidator.cs`

**Dependencies:** `FireSafetyModuleManager`, `Camera.main`

**Class design:**
```csharp
namespace MiningSafetyAR.AR
{
    public class ARProximitySafetyValidator : MonoBehaviour
    {
        public static ARProximitySafetyValidator Instance { get; private set; }

        [Header("Safety Configuration")]
        [SerializeField] private float safeDistanceThreshold = 1.0668f; // 3.5ft in meters
        [SerializeField] private float hysteresisMargin = 0.15f;
        [SerializeField] private float penaltyPoints = 50f;

        private Transform fireTargetTransform;
        private bool isCurrentlyViolating = false;
        private bool isEnabled = false;

        public event Action<float> OnProximityBreached;
        public event Action OnProximityResolved;

        // Public methods:
        public void EnableChecking(Transform fireTarget);
        public void DisableChecking();

        // Update loop:
        // - Vector3.Distance(camera, fireTarget) on XZ plane
        // - Compare against safeDistanceThreshold
        // - Use hysteresisMargin to prevent flickering
        // - Call FireSafetyModuleManager.Instance.RegisterMistake() on breach
    }
}
```

**Algorithm:**
```
distance = Vector3.Distance(camera.position, fireTarget.position)
if distance < safeDistanceThreshold:
    if not currentlyViolating:
        currentlyViolating = true
        OnProximityBreached?.Invoke(distance)
        RegisterMistake("Step back! Don't stand within 3.5 ft. of the fire.")
else:
    if currentlyViolating AND distance >= safeDistanceThreshold + hysteresisMargin:
        currentlyViolating = false
        OnProximityResolved?.Invoke()
```

---

### STEP 5: Create ARWaypointNavigationPointer.cs

**File:** `Assets/Scripts/AR/ARWaypointNavigationPointer.cs`

**Class design:**
```csharp
namespace MiningSafetyAR.AR
{
    public class ARWaypointNavigationPointer : MonoBehaviour
    {
        public static ARWaypointNavigationPointer Instance { get; private set; }

        [Header("Pointer Configuration")]
        [SerializeField] private VisualElement pointerElement; // UI Toolkit arrow
        [SerializeField] private float showDistanceThreshold = 2.0f;
        [SerializeField] private float hideDistanceThreshold = 0.5f;

        private Transform targetTransform;
        private Camera mainCamera;
        private bool isActive = false;

        public void SetTarget(Transform target);
        public void Activate();
        public void Deactivate();

        // Update loop:
        // - Convert target to viewport coordinates
        // - Calculate horizontal direction angle
        // - Rotate pointer element
        // - Show/hide based on distance
    }
}
```

**Direction Arrow Math:**
```csharp
Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
Vector3 targetDir = Vector3.ProjectOnPlane(target.position - cam.transform.position, Vector3.up).normalized;
float signedAngle = Vector3.SignedAngle(camForward, targetDir, Vector3.up);
pointerElement.transform.rotation = Quaternion.Euler(0, 0, -signedAngle);
```

---

### STEP 6: Create ARGuidanceController.cs

**File:** `Assets/Scripts/AR/ARGuidanceController.cs`

**Class design:**
```csharp
namespace MiningSafetyAR.AR
{
    public class ARGuidanceController : MonoBehaviour
    {
        public static ARGuidanceController Instance { get; private set; }

        [Header("Tutorial UI References")]
        [SerializeField] private VisualElement vignetteOverlay;
        [SerializeField] private VisualElement tutorialCallout;  // TIP 1/3 bubble
        [SerializeField] private VisualElement missionModal;     // YOUR MISSION card
        [SerializeField] private Button btnCloseTip;
        [SerializeField] private Button btnSkipTutorial;
        [SerializeField] private Button btnStartMission;

        [Header("Hint Configuration")]
        private string[] contextualHints = new string[]
        {
            "Sound the emergency alarm pull station located on the safety stand.",
            "Select the Red CO2 Extinguisher suitable for electrical/trash fires.",
            "Pull the safety pin on the extinguisher handle to unlock mechanism.",
            "Aim the nozzle at the base of the fire and test spray.",
            "Squeeze handle firmly and sweep nozzle side-to-side across base of flames."
        };

        private int currentTipIndex = 0;
        private bool tutorialCompleted = false;

        public event Action OnTutorialCompleted;
        public event Action OnMissionStarted;

        // Public methods:
        public void StartTutorial();           // Show vignette + tip 1/3
        public string GetContextualHint(int stepIndex);  // Return hint for current step
        public void ShowHintModal();            // Toggle hint popup

        // Private:
        private void ShowNextTip();
        private void SkipTutorial();
        private void ShowMissionBriefing();
        private void StartDrill();
    }
}
```

**Tutorial Flow:**
1. `StartTutorial()` → darkens screen, shows TIP 1/3
2. Close/Skip → shows "YOUR MISSION" modal
3. Tap START → fires `OnMissionStarted`, hides all tutorial UI

---

### STEP 7: Rewrite FireExtinguisherGrabController.cs

**File:** `Assets/Scripts/AR/FireExtinguisherGrabController.cs`

**Current state:** Basic grab-and-carry with lerp, arrival detection at fire hazard.

**Major changes:**

#### 7a. P.A.S.S. Sub-Step State Machine
```csharp
public enum PassStepState
{
    None,
    PinPulled,
    NozzleAimed,
    HandleSqueezed,
    SweepComplete
}
private PassStepState currentPassState = PassStepState.None;
```

#### 7b. Foam Capacity System
```csharp
[Header("Foam Configuration")]
[SerializeField] private float maxFoamCapacity = 15f;  // seconds of continuous spray
private float currentFoamCapacity;
public event Action OnExtinguisherDepleted;
```

#### 7c. Foam Spray + Raycast
```csharp
// When squeezing:
if (Physics.Raycast(nozzleTip.position, nozzleTip.forward, out RaycastHit hit, maxSprayRange, fireLayerMask))
{
    GroundFireController fire = hit.collider.GetComponentInParent<GroundFireController>();
    if (fire != null)
    {
        fire.ApplyFoamSuppression(hit.point, Time.deltaTime);
    }
}
currentFoamCapacity -= Time.deltaTime;
```

#### 7d. Pin Pull Interaction
- Raycast from tap position to extinguisher pin collider
- On tap: detach pin transform, increment PassStepState

#### 7e. Squeeze & Sweep
- Detect touch-hold on squeeze lever
- Track horizontal nozzle displacement for sweep detection

#### 7f. Camera Offset
```csharp
private Vector3 cameraOffset = new Vector3(0.25f, -0.30f, 0.60f);
private Vector3 holdingRotationOffset = new Vector3(0f, 0f, 0f);
```

---

### STEP 8: Rewrite FireSafetyModuleManager.cs

**File:** `Assets/Scripts/Modules/FireSafetyModuleManager.cs`

**Current state:** 4-step module (exit, extinguisher, P.A.S.S., evacuation).

**Major rewrite to 5-step P.A.S.S. drill:**

#### 8a. 5-Step Configuration
```csharp
private void Awake()
{
    moduleType = ModuleType.FireAndExplosion;
    moduleName = "Fire & Explosion Response";
    totalSteps = 5;  // Changed from 4
    baseScorePerStep = 100;  // Each step worth 100 pts (max 500)
}
```

#### 8b. Step Definitions
| Step | Name | Trigger | Completion |
|------|------|---------|------------|
| 0 | Sound Alarm | Tap fire alarm box on safety station | Alarm siren plays |
| 1 | Select Extinguisher | Tap red CO2 extinguisher | Extinguisher binds to camera |
| 2 | Pull Pin | Tap safety pin | Pin detaches |
| 3 | Aim & Test Spray | Aim at base + short spray | Test spray confirmed |
| 4 | Squeeze & Sweep | Hold squeeze + sweep XZ | Fire health reaches 0 |

#### 8c. Per-Step Metrics
```csharp
private List<StepMetric> stepMetrics = new List<StepMetric>();
private float[] stepStartTimes;
private int[] stepErrorCounts;

// On each step start: record start time, reset error count
// On each step complete: calculate duration, score = 100 - (errors * 25)
```

#### 8d. Score Table Generation
```csharp
public List<StepMetric> GetStepMetrics() => stepMetrics;
public int GetTotalScore() => stepMetrics.Sum(m => m.score);
public float GetTotalDuration() => stepMetrics.Sum(m => m.durationSeconds);
```

#### 8e. Failure Escalation
```csharp
public void TriggerFailureEscalation(string reason)
{
    isModuleActive = false;
    OnFailureEscalated?.Invoke(reason);
}
```

#### 8f. Events
```csharp
public event Action<int, string> OnStepChanged;        // (stepIndex, instruction)
public event Action<string> OnMistakeMade;              // (feedbackMessage)
public event Action<string> OnFailureEscalated;         // (reason)
public event Action<List<StepMetric>> OnModuleCompletedWithMetrics;
```

---

### STEP 9: Create ARSimulationOverlay.uxml

**File:** `Assets/UI/Templates/Pages/ARSimulationOverlay.uxml`

**Layout structure:**

```xml
<UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="Styles/Theme.uss" />
    <Style src="Styles/Reset.uss" />
    <Style src="Styles/Components.uss" />
    <Style src="Styles/Animations.uss" />
    <Style src="Styles/Icons.uss" />
    <Style src="Styles/ARSimulation.uss" />

    <!-- FULL SCREEN OVERLAY (transparent background) -->
    <ui:VisualElement name="ar-overlay" style="width: 100%; height: 100%;">

        <!-- TOP TOOLBAR (6 icons) -->
        <ui:VisualElement name="ar-toolbar">
            <ui:Button name="btn-exit" text="X" class="ar-toolbar__btn" />
            <ui:Button name="btn-move" text="↕↔" class="ar-toolbar__btn" />
            <ui:Button name="btn-mesh" text="🧱" class="ar-toolbar__btn" />
            <ui:Button name="btn-hint" text="💡" class="ar-toolbar__btn" />
            <ui:VisualElement name="timer-pill" class="ar-pill">
                <ui:Label text="⏱" />
                <ui:Label name="timer-value" text="00:00" />
            </ui:VisualElement>
            <ui:Button name="btn-score-dropdown" class="ar-pill">
                <ui:Label name="score-value" text="100" />
                <ui:Label text="∨" />
            </ui:Button>
        </ui:VisualElement>

        <!-- BOTTOM INSTRUCTION BAR -->
        <ui:VisualElement name="instruction-bar">
            <ui:Label name="instruction-icon" text="ⓘ" />
            <ui:Label name="instruction-text" text="Scan floor to detect planes..." />
        </ui:VisualElement>

        <!-- TIER 2: PROXIMITY WARNING BANNER -->
        <ui:VisualElement name="banner-warning" class="banner banner-warning" style="display: none;">
            <ui:Label name="warning-icon" text="❌" />
            <ui:Label name="warning-text" text="" />
        </ui:VisualElement>

        <!-- TIER 3: CRITICAL HAZARD ALERT -->
        <ui:VisualElement name="banner-critical" class="banner banner-critical" style="display: none;">
            <ui:Label name="critical-icon" text="⚡" />
            <ui:Label name="critical-title" text="ALERT!" />
            <ui:Label name="critical-text" text="" />
            <ui:Button name="btn-retry" text="▶" class="retry-btn" />
        </ui:VisualElement>

        <!-- SCORE HISTORY MODAL -->
        <ui:VisualElement name="modal-score-history" class="modal-overlay" style="display: none;">
            <ui:VisualElement class="modal-card">
                <ui:Label name="modal-title" text="⏱ SCORE HISTORY" />
                <ui:VisualElement class="table-header">
                    <ui:Label text="STEP" />
                    <ui:Label text="ERRORS" />
                    <ui:Label text="DURATION" />
                    <ui:Label text="SCORE" />
                </ui:VisualElement>
                <ui:VisualElement name="table-step-rows" />
                <ui:VisualElement class="table-footer">
                    <ui:Label name="total-duration" text="TOTAL DURATION: 00:00" />
                    <ui:Label name="total-score" text="TOTAL SCORE: 0/500" />
                </ui:VisualElement>
                <ui:Button name="btn-close-modal" text="CLOSE" />
            </ui:VisualElement>
        </ui:VisualElement>

        <!-- TUTORIAL VIGNETTE -->
        <ui:VisualElement name="tutorial-vignette" class="vignette" style="display: none;" />

        <!-- TUTORIAL CALLOUT (TIP 1/3) -->
        <ui:VisualElement name="tutorial-callout" class="callout-bubble" style="display: none;">
            <ui:Label name="tip-label" text="TIP 1/3" />
            <ui:Label name="tip-text" text="" />
            <ui:Button name="btn-close-tip" text="CLOSE" />
            <ui:Button name="btn-skip-tutorial" text="SKIP TUTORIAL" />
        </ui:VisualElement>

        <!-- MISSION BRIEFING MODAL -->
        <ui:VisualElement name="mission-modal" class="modal-card" style="display: none;">
            <ui:Label name="mission-icon" text="ⓘ" />
            <ui:Label name="mission-title" text="YOUR MISSION" />
            <ui:Label name="mission-text" text="A fire just started in a trash can! What should you do?" />
            <ui:Button name="btn-start-mission" text="START" class="btn-start" />
        </ui:VisualElement>

    </ui:VisualElement>
</UXML>
```

---

### STEP 10: Create ARSimulation.uss

**File:** `Assets/UI/Styles/ARSimulation.uss`

```css
/* AR Simulation Overlay Styles */

/* Top Toolbar */
.ar-toolbar {
    position: absolute;
    top: 0; left: 0; right: 0;
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    padding: 8px 16px;
    background-color: rgba(0, 0, 0, 0.5);
}
.ar-toolbar__btn {
    width: 44px; height: 44px;
    border-radius: 22px;
    background-color: rgba(255, 255, 255, 0.2);
    color: white;
    font-size: 20px;
    justify-content: center; align-items: center;
}
.ar-pill {
    flex-direction: row;
    align-items: center;
    padding: 6px 14px;
    border-radius: 20px;
    background-color: rgba(0, 0, 0, 0.6);
    color: white;
}

/* Bottom Instruction Bar */
.instruction-bar {
    position: absolute;
    bottom: 0; left: 0; right: 0;
    flex-direction: row;
    align-items: center;
    padding: 12px 20px;
    background-color: rgba(0, 0, 0, 0.6);
    color: white;
}

/* Tier 2: Warning Banner */
.banner-warning {
    position: absolute;
    bottom: 60px; left: 16px; right: 16px;
    padding: 12px 16px;
    border-radius: 8px;
    background-color: rgba(255, 87, 34, 0.9);
    color: white;
    flex-direction: row;
    align-items: center;
}

/* Tier 3: Critical Alert */
.banner-critical {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    justify-content: center;
    align-items: center;
    background-color: rgba(0, 0, 0, 0.8);
}
.banner-critical .modal-card {
    width: 320px;
    padding: 24px;
    border-radius: 12px;
    /* Yellow/black hazard stripes */
    background-image: repeating-linear-gradient(
        45deg,
        #FFD600 0px, #FFD600 20px,
        #212121 20px, #212121 40px
    );
    background-color: #FFD600;
    align-items: center;
}
.retry-btn {
    width: 64px; height: 64px;
    border-radius: 32px;
    background-color: #FF6D00;
    color: white;
    font-size: 28px;
}

/* Score History Modal */
.modal-overlay {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    justify-content: center;
    align-items: center;
    background-color: rgba(0, 0, 0, 0.7);
}
.modal-card {
    width: 360px;
    padding: 20px;
    border-radius: 12px;
    background-color: #1E1E1E;
    color: white;
}
.table-header {
    flex-direction: row;
    border-bottom-width: 1px;
    border-bottom-color: #444;
    padding: 8px 0;
}
.table-header > Label { flex: 1; font-size: 11px; color: #888; }
.table-row {
    flex-direction: row;
    padding: 10px 0;
    border-bottom-width: 1px;
    border-bottom-color: #333;
}
.table-row > Label { flex: 1; font-size: 13px; }
.table-footer {
    flex-direction: row;
    justify-content: space-between;
    padding: 12px 0;
    border-top-width: 2px;
    border-top-color: #FF6D00;
}

/* Tutorial */
.vignette {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background-color: rgba(0, 0, 0, 0.6);
}
.callout-bubble {
    position: absolute;
    top: 80px; left: 50%;
    translate: -50% 0;
    width: 300px;
    padding: 16px;
    border-radius: 12px;
    background-color: #2196F3;
    color: white;
    align-items: center;
}
.btn-start {
    margin-top: 12px;
    padding: 12px 32px;
    border-radius: 8px;
    background-color: #FF6D00;
    color: white;
    font-size: 16px;
    font-weight: bold;
}
```

---

### STEP 11: Rewrite ARSimulationPageController.cs

**File:** `Assets/Scripts/UI/Pages/ARSimulationPageController.cs`

**Major rewrite.** The controller must:

1. **Load UXML:** Reference `ARSimulationOverlay.uxml` instead of the old `ARSimulationPage.uxml`
2. **Bind all UI elements** (6 toolbar buttons, banners, modal, tutorial elements)
3. **Subscribe to events** from FireSafetyModuleManager, ARProximitySafetyValidator, ARGuidanceController
4. **Handle 3-tier alerts:**
   - Tier 1: instructionText.text = instruction
   - Tier 2: bannerWarning display toggle + warning text
   - Tier 3: bannerCritical display toggle + retry button
5. **Score History Modal:** Build table rows from List<StepMetric>
6. **Timer:** Elapsed stopwatch (not countdown)
7. **Button handlers:** Exit→NavigateBack, Move→toggle reposition, Mesh→toggle planes, Hint→ARGuidanceController
8. **Dynamic score updates:** Subscribe to OnStepChanged, update score-value label
9. **Tutorial flow:** On page enter, start tutorial via ARGuidanceController

**Key method signatures:**
```csharp
protected override void BindUI();           // Bind all UXML elements
public override void OnPageEnter();         // Start tutorial, load AR scene
public override void OnPageExit();          // Cleanup, unload AR scene

private void OnStepChanged(int step, string instruction);
private void OnMistakeMade(string feedback);
private void OnProximityBreached(float distance);
private void OnProximityResolved();
private void OnModuleCompleted(List<StepMetric> metrics);
private void OnFailureEscalated(string reason);

private void ShowTier1Info(string message);
private void ShowTier2Warning(string message);
private void ShowTier3Critical(string title, string message);
private void HideAllBanners();

private void BuildScoreHistoryTable(List<StepMetric> metrics);
private void UpdateScoreBadge(int score);

private void OnExitClicked();
private void OnMoveClicked();
private void OnMeshClicked();
private void OnHintClicked();
private void OnScoreDropdownClicked();
private void OnRetryClicked();
```

---

### STEP 12: Modify ARPlacementManager.cs (Minor)

**File:** `Assets/Scripts/AR/ARPlacementManager.cs`

**Changes (~30 lines):**

#### 12a. Add Event for Placement Confirmation
```csharp
public event Action OnFireHazardConfirmed;  // After placement window locks
```

#### 12b. Fire Event on Lock
In `Update()`, when `isPlacementLocked` becomes true:
```csharp
if (hasFirstPlacementOccurred && !isPlacementLocked)
{
    if (elapsedTime >= placementWindowDuration)
    {
        isPlacementLocked = true;
        OnFireHazardConfirmed?.Invoke();  // NEW
    }
}
```

#### 12c. Add Property for Other Systems
```csharp
public bool IsFloorPlacementActive => spawnedObject != null && isPlacementLocked;
public Transform FireHazardTransform => spawnedObject?.transform;
```

---

## 5. UXML Element ID Reference

| Element ID | Type | Purpose |
|------------|------|---------|
| `btn-exit` | Button | Exit AR session |
| `btn-move` | Button | Toggle reposition mode |
| `btn-mesh` | Button | Toggle plane mesh grid |
| `btn-hint` | Button | Show contextual hint |
| `timer-value` | Label | Elapsed time display |
| `score-value` | Label | Current score |
| `btn-score-dropdown` | Button | Open score history modal |
| `instruction-text` | Label | Current instruction (Tier 1) |
| `banner-warning` | VisualElement | Proximity warning (Tier 2) |
| `warning-text` | Label | Warning message text |
| `banner-critical` | VisualElement | Hazard alert (Tier 3) |
| `critical-title` | Label | "ALERT!" title |
| `critical-text` | Label | Critical message |
| `btn-retry` | Button | Retry after failure |
| `modal-score-history` | VisualElement | Score history overlay |
| `table-step-rows` | VisualElement | Container for step rows |
| `total-duration` | Label | Total drill time |
| `total-score` | Label | Total score out of 500 |
| `btn-close-modal` | Button | Close score modal |
| `tutorial-vignette` | VisualElement | Dark overlay |
| `tutorial-callout` | VisualElement | TIP 1/3 bubble |
| `tip-text` | Label | Hint text |
| `btn-close-tip` | Button | Close current tip |
| `btn-skip-tutorial` | Button | Skip all tips |
| `mission-modal` | VisualElement | Mission briefing card |
| `mission-text` | Label | Mission description |
| `btn-start-mission` | Button | Start the drill |

---

## 6. USS Class Reference

| Class | Element | Purpose |
|-------|---------|---------|
| `.ar-toolbar` | Container | Top toolbar flex row |
| `.ar-toolbar__btn` | Button | Toolbar icon button |
| `.ar-pill` | Container | Timer/score pills |
| `.instruction-bar` | Container | Bottom instruction bar |
| `.banner-warning` | VisualElement | Orange proximity warning |
| `.banner-critical` | VisualElement | Full-screen hazard alert |
| `.retry-btn` | Button | Orange retry circle |
| `.modal-overlay` | VisualElement | Full-screen dimmed backdrop |
| `.modal-card` | VisualElement | Centered card container |
| `.table-header` | VisualElement | Score table header row |
| `.table-row` | VisualElement | Score table data row |
| `.table-footer` | VisualElement | Score table totals |
| `.vignette` | VisualElement | Tutorial dark overlay |
| `.callout-bubble` | VisualElement | Tutorial speech bubble |
| `.btn-start` | Button | Orange START button |

---

## 7. Event Flow Diagram

```
ARPlacementManager.OnObjectPlaced
    └─► ARStepCounterTracker.OnFireHazardIgnited
        └─► [Step counting begins]
            └─► ARStepCounterTracker.OnExtinguisherDiscovered
                └─► FireExtinguisherGrabController.SetupExtinguisherForGrabbing

FireExtinguisherGrabController.OnPinPulled
    └─► FireSafetyModuleManager.CompleteCurrentStep (step 2)

FireExtinguisherGrabController.OnSprayStarted
    └─► FireSafetyModuleManager (step 3 completion)

FireExtinguisherGrabController.OnExtinguisherDepleted
    └─► FireSafetyModuleManager.TriggerFailureEscalation

GroundFireController.OnFireExtinguished
    └─► FireSafetyModuleManager.CompleteCurrentStep (step 4)
        └─► FireSafetyModuleManager.OnModuleCompletedWithMetrics
            └─► ARSimulationPageController.OnModuleCompleted

ARProximitySafetyValidator.OnProximityBreached
    └─► FireSafetyModuleManager.RegisterMistake
    └─► ARSimulationPageController.ShowTier2Warning

ARGuidanceController.OnMissionStarted
    └─► FireSafetyModuleManager.StartModule

ARSimulationPageController.OnExitClicked
    └─► NavigationManager.NavigateBack
```

---

## 8. Scene Wiring Instructions (Step 13)

When all scripts are created/modified, the `ar_fire_safety.unity` scene needs:

### GameObjects to Configure:

| GameObject | Components to Add/Configure |
|------------|---------------------------|
| AR Session | ARSession, ARInputManager |
| AR Camera | Camera, ARCameraManager, ARCameraBackground, AROcclusionManager |
| AR Placement | ARPlacementManager, ARRaycastManager, ARPlaneManager, ARAnchorManager |
| AR Step Counter | ARStepCounterTracker (auto-attached by ARPlacementManager) |
| Proximity Validator | ARProximitySafetyValidator (NEW) |
| Waypoint Pointer | ARWaypointNavigationPointer (NEW) |
| Guidance Controller | ARGuidanceController (NEW) |
| Fire Hazard | GroundFireController (on prefab) |
| Extinguisher | FireExtinguisherGrabController (on prefab) |
| Safety Station | Station objects: Alarm Box, Red Extinguisher, Green Shower, Hose Cart |
| HUD Overlay | UIDocument + ARSimulationPageController (NEW UXML) |

### Layer Setup:
- Layer "FireHazard" — for fire collider (used by foam raycast)
- Layer "Grabbable" — for extinguisher pin/lever colliders
- Layer "HeldItem" — for held extinguisher

### Prefab Assignments on ARPlacementManager:
- `defaultPlacementPrefab` → Fire Hazard prefab (with GroundFireController)
- `wallExtinguisherPrefab` → Safety Station prefab (with interactive objects)
- `placementIndicator` → Reticle prefab

---

## 9. Testing Checklist

After implementation, verify:

- [ ] Floor plane detection shows reticle
- [ ] Tap checkmark spawns fire hazard
- [ ] 3-second placement window works
- [ ] Tutorial vignette + tips display correctly
- [ ] Mission briefing modal shows and START works
- [ ] Flames VFX ignite after START
- [ ] Timer counts up from 00:00
- [ ] Proximity check triggers red banner when < 3.5ft
- [ ] Step counter tracks physical walking
- [ ] Wall scan prompt appears after target steps
- [ ] Extinguisher spawns on wall or floor fallback
- [ ] Grab mechanic works (tap to pick up)
- [ ] P.A.S.S. steps register in correct order
- [ ] Foam spray VFX appears on squeeze
- [ ] Fire shrinks dynamically with foam
- [ ] Score updates in real-time
- [ ] Score history modal shows 5 rows with metrics
- [ ] Failure escalation shows on foam depletion
- [ ] Retry button resets drill
- [ ] Exit button navigates back
- [ ] Move button toggles reposition mode
- [ ] Grid button toggles plane mesh
- [ ] Hint button shows contextual tip

---

*Document generated for MiningSafetyAR — AR Fire Safety Training Module Implementation*
