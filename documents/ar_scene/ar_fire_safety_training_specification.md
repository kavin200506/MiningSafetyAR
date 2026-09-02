# AR Fire Safety Training Module — Complete Technical & Feature Specification

## Document Overview
This document defines the complete technical architecture, user flow, interactive components, visual interfaces, and evaluation systems for the **AR Fire Safety Training Module** in the Mining Safety AR application.

This scene operates as a serious-game simulation platform designed to train underground and surface mine workers in emergency fire response, spatial hazard awareness, tool selection, and the official 4-step P.A.S.S. fire suppression technique.

---

## 1. Entry & Scene Transition Flow

```
[Dashboard / Catalogue]
         │
         ▼
[Module Detail Page]
         │
         ▼
[Learning Content Page] ──► Trainee finishes reading slides ──► Clicks "I'm Ready" Button
                                                                         │
                                                                         ▼
                                                     NavigationManager.Instance.NavigateTo(...)
                                                                         │
                                                                         ▼
                                                    ┌─────────────────────────────────────────┐
                                                    │ AR Scene: AR Plane Detection Placement  │
                                                    │ Additive HUD: UI_ARSimulation           │
                                                    └─────────────────────────────────────────┘
```

When the trainee completes the theoretical slides on the **Learning Content Page** and clicks **"I'm Ready"**:
1. `NavigationManager.Instance.NavigateTo("UI_ARSimulation", "fire_safety")` is executed.
2. `UI_ARSimulation` loads additively as a transparent UI overlay containing the persistent header toolbar, scoring HUD, timer, and instruction cards.
3. `AR Plane Detection Placement.unity` (Build index 7) initializes the camera background stream, ARSession, plane detection managers, particle systems, and audio controllers.

---

## 2. Architecture Overview: The 8 Core Subsystems

The scene is driven by 8 decoupled subsystems:

```
                               ┌─────────────────────────┐
                               │   AR TRAINING ENGINE    │
                               └────────────┬────────────┘
                                            │
        ┌───────────────────────────────────┼───────────────────────────────────┐
        │                                   │                                   │
        ▼                                   ▼                                   ▼
┌──────────────┐                    ┌──────────────┐                    ┌──────────────┐
│ AR WORLD     │                    │ TRAINING     │                    │ INTERACTION  │
│ SYSTEM       │                    │ ENGINE       │                    │ ENGINE       │
├──────────────┤                    ├──────────────┤                    ├──────────────┤
│ Plane Detect │                    │ Scenarios    │                    │ Grab Object  │
│ Floor / Wall │                    │ Missions     │                    │ Move / Hold  │
│ Anchors      │                    │ State Machine│                    │ Extinguisher │
└──────────────┘                    └──────────────┘                    └──────────────┘
        │                                   │                                   │
        ├───────────────────────────────────┼───────────────────────────────────┤
        │                                   │                                   │
        ▼                                   ▼                                   ▼
┌──────────────┐                    ┌──────────────┐                    ┌──────────────┐
│ SAFETY       │                    │ GUIDANCE     │                    │ ASSESSMENT   │
│ ENGINE       │                    │ SYSTEM       │                    │ ENGINE       │
├──────────────┤                    ├──────────────┤                    ├──────────────┤
│ 3.5ft Check  │                    │ Tutorial     │                    │ Step Errors  │
│ Danger Zones │                    │ Hints (💡)   │                    │ Durations    │
│ Red Warnings │                    │ Floor Arrow  │                    │ Score Table  │
└──────────────┘                    └──────────────┘                    └──────────────┘
        │                                   │                                   │
        └───────────────────────────────────┼───────────────────────────────────┘
                                            │
                                 ┌──────────┴──────────┐
                                 ▼                     ▼
                          ┌─────────────┐       ┌─────────────┐
                          │ FIRE SYSTEM │       │ UI SYSTEM   │
                          ├─────────────┤       ├─────────────┤
                          │ Flame VFX   │       │ Header Bar  │
                          │ Depletion   │       │ Toasts      │
                          │ Escalation  │       │ Score Modal │
                          └─────────────┘       └─────────────┘
```

---

## 3. Detailed Step-by-Step Experience Specification

---

### PHASE 1: Entry & Persistent Top Header Toolbar

Upon scene transition, the camera stream initializes and displays the persistent top navigation bar across the top of the device screen:

| Control Icon | Icon Graphic | Functionality |
| :--- | :---: | :--- |
| **Exit Button** | `X` | Exits the AR training session and returns to the Module Detail page. |
| **Move Target** | `↕↔` | Enables re-positioning mode to adjust placed AR objects on detected planes. |
| **Plane Mesh Grid** | `🧱` | Toggles visual plane detection grid mesh overlay on/off. |
| **Hint System** | `💡` | Triggers contextual on-demand tips and tutorial callouts. |
| **Stopwatch Timer** | `⏱ 00:00 \|\|` | Tracks elapsed drill time in real time. Tapping `\|\|` pauses the simulation. |
| **Dynamic Score Badge** | `100 ∨` | Shows active dynamic score. Tapping the dropdown `∨` expands the **Score History Modal**. |

---

### PHASE 2: Horizontal Floor Plane Detection & Fire Ignition

```
[Camera Stream Active] ──► [Detect Horizontal Floor] ──► [Align White Floor Reticle]
                                                                  │
                                                                  ▼
                                                      [Display Confirmation Card]
                                                      "Place target on plane..."
                                                                  │
                                                                  ▼
                                                      [Trainee Taps Orange Checkmark ✓]
                                                                  │
                                                                  ▼
                                                      [Trash Can Fire Hazard Spawns]
```

1. **Floor Detection**: The `ARPlaneManager` scans for horizontal floor surfaces (`PlaneAlignment.HorizontalUp`).
2. **Reticle Placement**: A white circular ring with a solid center dot projects onto the detected floor surface.
3. **Bottom Confirmation Card**: A floating card appears at the bottom of the screen:
   > `ⓘ` *"Place the target on a plane then tap the orange checkmark."*
4. **Action Confirmation**: The trainee points the camera at the floor spot and taps the **Orange Checkmark (`✓`)** button.
5. **Object Anchor**: The **3D Trash Can Fire Hazard** spawns and locks onto the selected floor spot via `ARAnchor`.

---

### PHASE 3: Onboarding Tutorial & Mission Briefing

```
[Fire Anchored] ──► [Screen Darkens with Vignette] ──► [Display Speech Bubble TIP 1/3]
                                                                │
                                                                ▼
                                                    [Trainee Closes or Skips]
                                                                │
                                                                ▼
                                                    [Display "YOUR MISSION" Modal]
                                                    "A fire just started in a trash can!"
                                                                │
                                                                ▼
                                                    [Trainee Taps "START"]
```

1. **Tutorial Vignette**: The camera view darkens with a vignetted background.
2. **Popover Callout (`TIP 1/3`)**: A speech bubble attaches to the top Lightbulb icon (`💡`):
   > *"Need help to progress? Just tap this light bulb icon then follow the blue arrow."*  
   > *(Contains a `CLOSE` button and a bottom `"SKIP TUTORIAL"` text button).*
3. **Mission Briefing Modal**: Once tutorial tips are acknowledged or skipped, the **`YOUR MISSION`** card appears:
   > `ⓘ YOUR MISSION`  
   > *"A fire just started in a trash can! What should you do?"*
4. **Start Drill**: Trainee taps **`START`** to engage the emergency clock.

---

### PHASE 4: Emergency Ignition & Proximity Safety Monitoring

```
[Start Tapped] ──► [Flames & Ember VFX Ignite] ──► [Timer Starts 00:01]
                                                          │
                                                          ▼
                                            [Proximity Check: Distance < 3.5 ft?]
                                            ├─────────────YES─────────────► [Red Alert Card ❌]
                                            │                               "Step back! < 3.5 ft"
                                            │                               Penalty: -50 pts
                                            ▼ NO
                                            [AR Floor Pointer Arrow Points to Equipment]
```

1. **Particle Ignition**: Burning trash can particle VFX ignites (flames, smoke, heat distortion, and rising embers).
2. **Stopwatch Activation**: Real-time timer starts (`00:01`). Score initializes at `100`.
3. **Bottom Instruction Prompt**: The HUD displays `ⓘ What should you do now?`.
4. **Proximity Safety Engine (Real-Time Distance Validation)**:
   - Calculates real-time 3D camera distance to the fire hazard:
     $$\text{Distance} = \sqrt{(x_{\text{cam}} - x_{\text{fire}})^2 + (z_{\text{cam}} - z_{\text{fire}})^2}$$
   - **Safety Boundary**: If the trainee moves closer than **$3.5\text{ ft}$ ($1.06\text{ m}$)** to the fire, an instant **Red Alert Banner** pops up:
     > `(X)` *"Step back! Don't stand within 3.5 ft. of the fire."*
   - Each proximity violation registers a mistake penalty (`-50 pts`).
5. **AR Floor Direction Arrow**: A blue directional node with an arrow pointer appears on the floor guiding the trainee's view toward safety equipment.

---

### PHASE 5: Physical Step Counter & Vertical Wall Detection

```
[Fire Active] ──► [Trainee Walks 5–15 Real Steps] ──► [Step Target Reached]
                                                              │
                                                              ▼
                                                   [Prompt: "Scan a Wall"]
                                                              │
                                                              ▼
                                                   [Detect Vertical Plane]
                                                              │
                                                              ▼
                                                   [Tap Wall ──► Mount Extinguisher]
```

1. **Step Tracking**: As the trainee moves in the room, `ARStepCounterTracker` measures 6-DOF horizontal camera displacement and calculates step count (target randomized between 5 and 15 steps).
2. **Wall Scan Prompt**: Upon completing the steps, the HUD prompts: *"Locate a wall surface to access safety equipment"*.
3. **Vertical Plane Detection**: The `ARPlaneManager` detects vertical wall planes (`PlaneAlignment.Vertical`).
4. **Wall Reticle Alignment**: The reticle snaps onto the vertical wall surface.
5. **Mount Equipment**: Trainee taps the wall reticle to mount the **3D Fire Extinguisher & Safety Equipment Station** directly onto the wall.

---

### PHASE 6: Interactive Safety Station & Tool Selection

The mounted station contains:
- **Red CO2/Dry Powder Extinguisher** (Correct choice for electrical/trash fires).
- **Green Safety Shower / Eyewash Station** (Incorrect choice).
- **Fire Hose Reel Cart** (Incorrect choice).
- **Wall Fire Alarm Pull Box** (Required for Step 1).

#### Action Evaluation:
- **Step 1 — Sound Alarm**: Trainee taps the Fire Alarm box $\rightarrow$ Alarm siren plays $\rightarrow$ *Step 1 Completed*.
- **Step 2 — Select Equipment**: Trainee taps the Red Extinguisher $\rightarrow$ Extinguisher detaches and binds to camera view $\rightarrow$ *Step 2 Completed*. Tapping the hose reel or shower registers a tool selection error penalty.

---

### PHASE 7: First-Person Extinguisher Rig & 5-Step P.A.S.S. Suppression

```
[Extinguisher Selected] ──► [Rigged to 1st-Person Camera View]
                                      │
                                      ▼
                        [Step 3: Pull Pin Gesture]
                                      │
                                      ▼
                        [Step 4: Aim & Test Spray]
                                      │
                                      ▼
                        [Step 5: Squeeze Handle & Sweep Base]
                                      │
                                      ▼
                        [White Foam Spray Particle VFX]
                                      │
                                      ▼
                        [Fire Intensity Shrinks dynamically]
```

1. **First-Person Rigging**: The 3D Red Extinguisher attaches to the bottom-center of the camera view, pointing forward. Printed texture labels clearly show P.A.S.S. instructions.
2. **Step 3 — Pull Pin**: Trainee taps the safety ring pin $\rightarrow$ Pin pulls out with metal sound effect $\rightarrow$ *Step 3 Completed*.
3. **Step 4 — Aim & Test Spray**: Trainee aims at the base of the fire and performs a short test spray $\rightarrow$ *Step 4 Completed*.
4. **Step 5 — Squeeze & Sweep**:
   - Trainee holds down the squeeze lever and sweeps the device nozzle side-to-side across the base of the flames.
   - White suppression foam particle VFX streams from the nozzle.
   - `GroundFireController` tracks sweeping coverage at the base of the flames and dynamically scales down flame particles and heat light intensity.
   - Score increases (`+150 pts`).

---

### PHASE 8: Resource Depletion & Failure Escalation Branches

```
                             [SUPPRESSION DRILL]
                                      │
                   ┌──────────────────┴──────────────────┐
                   ▼                                     ▼
        [Successful Suppression]              [Resource Depleted / Mistakes]
                   │                                     │
                   ▼                                     ▼
        [Fire Extinguished]                   [Foam Capacity reaches 0]
                   │                                     │
                   ▼                                     ▼
        [Trilingual Victory Audio]            [Screen Tints Dark Red]
                   │                                     │
                   ▼                                     ▼
        [Passed Result Saved]                 [Yellow/Black Hazard Alert Popup]
                                              "⚡ ALERT! Extinguisher empty, fire
                                               spreading! Call Fire Department."
                                                         │
                                                         ▼
                                              [Scenario PAUSED with Orange Play ▶ Button]
```

#### Branch A: Successful Suppression
- Fire scale reaches 0. Fire particle system stops.
- Victory audio plays in selected language (English / Hindi / Santali).
- Result flagged as `Passed = true`.

#### Branch B: Resource Depletion & Escalation Failure
- Extinguisher foam capacity is limited (e.g. 15 seconds of continuous spray).
- If trainee sprays inefficiently or hesitates, foam capacity drops to 0.
- Camera view tints **Red**.
- A high-visibility hazard alert dialog pops up:
  > `⚡ ALERT!`  
  > *"The extinguisher is empty and the fire is spreading! Now you'd better call the Fire Department."*
- Scenario shifts to **`PAUSED`** state with a darkened blur overlay and a large central orange Play button (`▶`) for retrying.

---

### PHASE 9: Granular Performance Assessment & Score History Table Modal

At any point during or after the drill, tapping the top score dropdown badge (`100 ∨`) opens the **Score History Modal** over the scene:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            ⏱ SCORE HISTORY | SCENARIO 1                      │
├───────────────────────────────────────────────────────┬───────────┬─────────┤
│ 5 STEPS                                               │ ERRORS    │ DURATION│ SCORE   │
├───────────────────────────────────────────────────────┼───────────┼─────────┼─────────┤
│ Sounding the fire alarm                               │ 1 ●       │ 00:08   │ 50/100  │
│ Selecting the correct extinguisher                    │ 2 ●       │ 00:04   │ 0/100   │
│ Pulling out the pin                                   │ 2 ●       │ 00:09   │ 0/100   │
│ Testing the extinguisher                              │ 0 ●       │ 00:05   │ 100/100 │
│ Putting out the fire                                  │ 3 ●       │ 00:28   │ -150/100│
├───────────────────────────────────────────────────────┴───────────┴─────────┴─────────┤
│ TOTAL DURATION: 00:54                                       TOTAL SCORE: 0/500 PTS    │
│                                      [ CLOSE ]                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### Metric Calculation Logic:
- **Max Score**: $5 \text{ steps} \times 100 \text{ pts} = 500 \text{ PTS}$.
- **Mistake Penalty**: $-25 \text{ to } -50 \text{ pts}$ per error (proximity breach, wrong equipment select, sequence violation).
- **Time Metric**: Recorded independently for each step and globally for the session.
- **Persistence**: Results automatically save to local storage (`LocalScoreManager`) and sync to Cloud Firestore via `AppDataService`.

---

## 4. Summary Component Mapping

| Subsystem Component | Unity Script Reference | Responsibilities |
| :--- | :--- | :--- |
| **Plane Detection & Spawning** | [`ARPlacementManager.cs`](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/AR/ARPlacementManager.cs) | Dual-plane detection (Floor horizontal for fire, Wall vertical for extinguisher), reticle positioning, orange checkmark lock. |
| **Physical Step Tracker** | [`ARStepCounterTracker.cs`](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/AR/ARStepCounterTracker.cs) | Camera displacement step tracking (5–15 steps) to activate wall scanning mode. |
| **Extinguisher Rig & Grab** | [`FireExtinguisherGrabController.cs`](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/AR/FireExtinguisherGrabController.cs) | First-person camera rigging, pin pull, squeeze, aim raycasting, foam capacity depletion. |
| **Fire VFX & Suppression** | [`GroundFireController.cs`](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Modules/GroundFireController.cs) | Flame particle intensity, heat light scaling, P.A.S.S. sweep detection, foam collision. |
| **Module Logic & P.A.S.S. Drill** | [`FireSafetyModuleManager.cs`](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Modules/FireSafetyModuleManager.cs) | 5-step state machine, trilingual voiceover, mistake registration, score table generation. |
| **HUD & Score Overlay** | [`ARSimulationPageController.cs`](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/UI/Pages/ARSimulationPageController.cs) | Persistent 6-icon top bar, 3-tier notification banners, Score History Modal table. |
| **Score Storage** | [`LocalScoreManager.cs`](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Data/LocalScoreManager.cs) & [`AppDataService.cs`](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Data/AppDataService.cs) | Local PlayerPrefs and Firebase Firestore session saving. |
