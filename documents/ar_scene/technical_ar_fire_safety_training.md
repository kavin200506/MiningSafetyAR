# Technical Implementation Architecture — AR Fire Safety Training Module

## Document Purpose
This document provides the complete, low-level technical specification and C# implementation guide for building the **AR Fire Safety Training Module** in Unity. It detailly covers class designs, Unity engine APIs, AR Foundation 6.x integration, UI Toolkit bindings, particle dynamics, vector mathematics, state machines, and cloud data persistence.

---

## 1. Technical Stack Overview

| Layer | Technology / Package | Technical Function |
| :--- | :--- | :--- |
| **Engine Core** | Unity 2022.3 LTS / Unity 6 | Core game loop, MonoBehaviours, physics raycasting, asset pipelines. |
| **AR Framework** | AR Foundation 6.x (`com.unity.xr.arfoundation`) | ARSession, ARPlaneManager, ARRaycastManager, ARAnchorManager, AROcclusionManager. |
| **Input System** | Unity New Input System (`com.unity.inputsystem`) | Touch pointer press binding `<Pointer>/press`, EnhancedTouchSupport. |
| **UI Framework** | Unity UI Toolkit (`com.unity.ui`) | UXML layouts, USS style sheets, PageControllers, dynamic DOM element creation. |
| **Graphics & VFX** | Universal Render Pipeline (URP) / Shaders | Particle System foam/fire rendering, URP decal reticles, post-processing vignette. |
| **Model Loader** | GLTFast (`com.atteneder.gltfast`) | Runtime async streaming & caching of 3D glTF/GLB equipment models. |
| **Localization** | Custom Trilingual Audio Engine | English, Hindi, and Santali localized voiceovers. |
| **Persistence** | PlayerPrefs + Firebase Firestore SDK | Local JSON fallback and async cloud Firestore attempt logging. |

---

## 2. Technical Subsystems & Implementation Architecture

---

### SUBSYSTEM 1: Dual-Plane AR Foundation Tracking & Surface Anchoring Engine

#### Primary Class: `ARPlacementManager.cs`
* **Dependencies**: `ARRaycastManager`, `ARPlaneManager`, `ARAnchorManager`, `InputAction`.

```
                    ┌──────────────────────────────────────────────┐
                    │            ARPlacementManager                │
                    └──────────────────────┬───────────────────────┘
                                           │
             ┌─────────────────────────────┴─────────────────────────────┐
             ▼                                                           ▼
┌───────────────────────────┐                               ┌───────────────────────────┐
│     Horizontal Floor      │                               │       Vertical Wall       │
│      Raycast Engine       │                               │      Raycast Engine       │
├───────────────────────────┤                               ├───────────────────────────┤
│ PlaneWithinPolygon        │                               │ PlaneWithinPolygon        │
│ Alignment: HorizontalUp   │                               │ Alignment: Vertical       │
│ Spawns: Trash Can Fire    │                               │ Spawns: Extinguisher Stand│
└───────────────────────────┘                               └───────────────────────────┘
```

#### Technical Implementation Details:
1. **Screen-Center Raycasting Loop**:
   - Performs a raycast from screen center `Vector2(Screen.width / 2f, Screen.height / 2f)` into the AR environment per frame.
   - Raycast hit filtering uses `TrackableType.PlaneWithinPolygon`.

2. **Plane Classification Filter**:
   - **Horizontal Floor Mode**: Validates `ARPlane.alignment == PlaneAlignment.HorizontalUp`. Snaps the floor reticle transform to `hit.pose.position` and `hit.pose.rotation`.
   - **Vertical Wall Mode**: Validates `ARPlane.alignment == PlaneAlignment.Vertical`. Snaps the wall reticle transform to wall normal vectors.

3. **Confirmation Lock & Anchoring Logic**:
   - When the user taps the **Orange Checkmark (`✓`)** UI button:
     ```csharp
     ARAnchor anchor = anchorManager.AttachAnchor(plane, hitPose);
     GameObject instance = Instantiate(prefab, hitPose.position, hitPose.rotation, anchor.transform);
     ```
   - Disables continuous plane boundary mesh rendering (`ARPlane.navMeshSurface` / `MeshRenderer.enabled = false`) once placement is locked to optimize mobile CPU/GPU performance.

4. **Surface Mesh Visualization Toggle (`🧱`)**:
   - Iterates through `planeManager.trackables` and toggles `lineRenderer` / `meshRenderer` components on/off dynamically.

---

### SUBSYSTEM 2: Physical Step Counter & 6-DOF AR Displacement Engine

#### Primary Class: `ARStepCounterTracker.cs`
* **Dependencies**: `Camera.main`, `ARPlacementManager`.

#### Technical Implementation Details:
1. **Camera Displacement Calculation**:
   - Tracks 6-DOF camera position displacement on the horizontal $XZ$-plane:
     ```csharp
     Vector3 currentCamPos = mainCamera.transform.position;
     Vector3 displacement = new Vector3(currentCamPos.x - lastCamPos.x, 0f, currentCamPos.z - lastCamPos.z);
     float distanceMoved = displacement.magnitude;
     ```

2. **Noise Filter & Step Threshold**:
   - Applies a low-pass threshold to eliminate camera tracking jitter when standing still:
     ```csharp
     if (distanceMoved >= minDisplacementThreshold && (Time.time - lastStepTime) >= MinTimeBetweenSteps)
     {
         accumulatedDistance += distanceMoved;
         if (accumulatedDistance >= averageStepLengthMeters) // ~0.65m per step
         {
             currentStepCount++;
             accumulatedDistance = 0f;
             lastStepTime = Time.time;
             OnStepCountUpdated?.Invoke(currentStepCount, targetSteps);
         }
     }
     lastCamPos = currentCamPos;
     ```

3. **Phase Transition Callback**:
   - When `currentStepCount >= targetSteps`, triggers state transition `StepTrackerState.ScanningForWall` and prompts the trainee to point camera at a wall surface.

---

### SUBSYSTEM 3: Proximity Safety Engine & Spatial Distance Validation

#### Primary Class: `ARProximitySafetyValidator.cs`
* **Dependencies**: `Transform` (Fire Target), `FireSafetyModuleManager`.

#### Technical Implementation Details:
1. **Real-Time Spatial Distance Check**:
   - Computes Euclidean distance between camera and fire target in `Update()`:
     ```csharp
     float distanceToFire = Vector3.Distance(mainCamera.transform.position, fireTargetTransform.position);
     ```

2. **Safety Boundary Evaluation ($3.5\text{ ft}$ / $1.06\text{ m}$)**:
   - Compares distance against minimum safety threshold `const float SafeDistanceThreshold = 1.0668f;`:
     ```csharp
     if (distanceToFire < SafeDistanceThreshold)
     {
         if (!isCurrentlyViolating)
         {
             isCurrentlyViolating = true;
             OnProximityBreached?.Invoke(distanceToFire);
             FireSafetyModuleManager.Instance.RegisterMistake("Step back! Don't stand within 3.5 ft. of the fire.");
         }
     }
     else
     {
         if (isCurrentlyViolating && distanceToFire >= SafeDistanceThreshold + HysteresisMargin)
         {
             isCurrentlyViolating = false;
             OnProximityResolved?.Invoke();
         }
     }
     ```
   - Includes a $0.15\text{ m}$ hysteresis margin to prevent flickering UI alerts when standing right on the boundary.

---

### SUBSYSTEM 4: AR Floor Waypoint Pointer & Navigation System

#### Primary Class: `ARWaypointNavigationPointer.cs`
* **Dependencies**: `Transform` (Target), `RectTransform` / `VisualElement` (Pointer UI).

#### Technical Implementation Details:
1. **Target Viewport Projection**:
   - Converts 3D target world position to 2D camera viewport coordinates:
     ```csharp
     Vector3 screenPos = mainCamera.WorldToViewportPoint(targetTransform.position);
     bool isBehind = screenPos.z < 0;
     ```

2. **Horizontal Direction Angle Math**:
   - Projects direction vector onto horizontal plane to determine rotation angle of the floor pointer arrow:
     ```csharp
     Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
     Vector3 targetDir = Vector3.ProjectOnPlane(targetTransform.position - mainCamera.transform.position, Vector3.up).normalized;
     float signedAngle = Vector3.SignedAngle(camForward, targetDir, Vector3.up);
     pointerElement.transform.rotation = Quaternion.Euler(0, 0, -signedAngle);
     ```

---

### SUBSYSTEM 5: First-Person Extinguisher Rigging & P.A.S.S. Suppression Engine

#### Primary Classes: `FireExtinguisherGrabController.cs` & `GroundFireController.cs`

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        FireExtinguisherGrabController                                  │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ - Rigid Position offset relative to Camera transform: (0.25, -0.30, 0.60)              │
│ - Pin Pull State Handler ──► Safety Pin Transform Detach Animation                     │
│ - Spray Nozzle Raycast ──► Physics.Raycast(nozzle.position, nozzle.forward)            │
│ - Foam Depletion Timer ──► currentFoamCapacity -= Time.deltaTime                       │
└────────────────────────────────────────┬───────────────────────────────────────────────┘
                                         │ Foam Spray Hit Detection
                                         ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                             GroundFireController                                       │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ - Listens to foam raycast hits on "FireHazard" collider layer                          │
│ - Calculates side-to-side sweeping coverage delta (XZ plane)                           │
│ - Dynamically scales down ParticleSystem emission rate & Light intensity               │
│ - Fires OnFireExtinguished event when scale reaches 0                                  │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Technical Implementation Details:
1. **First-Person Camera Attachment**:
   - When grabbed, smoothly interpolates extinguisher transform relative to camera viewport:
     ```csharp
     Vector3 targetPosition = mainCamera.transform.TransformPoint(cameraOffset);
     Quaternion targetRotation = mainCamera.transform.rotation * Quaternion.Euler(holdingRotationOffset);
     transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
     transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);
     ```

2. **Foam Spray Emission & Raycast Detection**:
   - Squeezing lever activates particle emission `foamParticleSystem.Play()`.
   - Fires forward raycast from nozzle tip:
     ```csharp
     if (Physics.Raycast(nozzleTip.position, nozzleTip.forward, out RaycastHit hit, maxSprayRange, fireLayerMask))
     {
         GroundFireController fire = hit.collider.GetComponentInParent<GroundFireController>();
         if (fire != null)
         {
             fire.ApplyFoamSuppression(hit.point, Time.deltaTime);
         }
     }
     ```

3. **Extinguisher Resource Depletion Logic**:
   - Tracks remaining capacity:
     ```csharp
     currentFoamCapacity -= Time.deltaTime;
     if (currentFoamCapacity <= 0f)
     {
         foamParticleSystem.Stop();
         OnExtinguisherDepleted?.Invoke();
         FireSafetyModuleManager.Instance.TriggerFailureEscalation("Extinguisher empty and fire is spreading!");
     }
     ```

4. **Dynamic Fire Suppression Math (`GroundFireController.cs`)**:
   - Reduces flame scale and particle emission rate linearly based on foam contact time and sweeping displacement:
     ```csharp
     currentFireHealth -= foamPower * deltaTime;
     float normalizedHealth = Mathf.Clamp01(currentFireHealth / maxFireHealth);
     
     var emission = fireParticleSystem.emission;
     emission.rateOverTime = initialEmissionRate * normalizedHealth;
     fireLight.intensity = initialLightIntensity * normalizedHealth;
     transform.localScale = Vector3.one * (0.2f + 0.8f * normalizedHealth);
     
     if (currentFireHealth <= 0f)
     {
         OnFireExtinguished?.Invoke();
     }
     ```

---

### SUBSYSTEM 6: Guidance & On-Demand Hint Engine

#### Primary Class: `ARGuidanceController.cs`
* **Dependencies**: `UIToolkit`, `FireSafetyModuleManager`.

#### Technical Implementation Details:
1. **Step-Based Hint Lookup**:
   - Map active step index to localized advice strings:
     ```csharp
     public string GetContextualHint(int currentStepIndex)
     {
         switch (currentStepIndex)
         {
             case 0: return "Sound the emergency alarm pull station located on the safety stand.";
             case 1: return "Select the Red CO2 Extinguisher suitable for electrical/trash fires.";
             case 2: return "Pull the safety pin on the extinguisher handle to unlock mechanism.";
             case 3: return "Aim the nozzle at the base of the fire and test spray.";
             case 4: return "Squeeze handle firmly and sweep nozzle side-to-side across base of flames.";
             default: return "Follow emergency evacuation arrows to safe assembly zone.";
         }
     }
     ```

2. **On-Demand Lightbulb Button (`💡`) Binding**:
   - Binds UI Toolkit click event `root.Q<Button>("btn-hint").clicked += ToggleHintModal;`.

---

### SUBSYSTEM 7: Serious-Game State Machine & 5-Step Drill Logic

#### Primary Classes: `BaseModuleManager.cs` & `FireSafetyModuleManager.cs`

```csharp
public struct StepMetric
{
    public string stepName;
    public int errorCount;
    public float durationSeconds;
    public int score;
}
```

```
[Module Start] ──► [Step 0: Sound Alarm] ──► [Step 1: Select Tool] ──► [Step 2: Pull Pin]
                                                                                │
                                                                                ▼
[Module Complete] ◄── [Step 4: Extinguish Fire] ◄── [Step 3: Aim & Test Spray]
```

#### Technical Implementation Details:
1. **State Machine Execution**:
   - Manages step transitions and records start times per step:
     ```csharp
     protected override void OnStepStart(int stepIndex)
     {
         stepStartTimes[stepIndex] = Time.time;
         stepErrorCounts[stepIndex] = 0;
         PlayStepAudio(stepAudioEN[stepIndex], stepAudioHI[stepIndex], stepAudioSAT[stepIndex]);
         OnStepChanged?.Invoke(stepIndex, GetStepInstruction(stepIndex));
     }
     ```

2. **Mistake Registration & Scoring Deductions**:
   - Records mistakes per step and calculates step score:
     ```csharp
     public override void RegisterMistake(string feedbackMessage)
     {
         stepErrorCounts[currentStepIndex]++;
         totalMistakesCount++;
         OnMistakeMade?.Invoke(feedbackMessage);
     }
     ```

3. **Failure Escalation Transition**:
   - Triggered on extinguisher depletion or max step mistakes:
     ```csharp
     public void TriggerFailureEscalation(string reason)
     {
         isModuleActive = false;
         OnFailureEscalated?.Invoke(reason);
     }
     ```

---

### SUBSYSTEM 8: UI Toolkit HUD, 3-Tier Alerts & Score History Modal Table

#### Primary Class: `ARSimulationPageController.cs`
* **UXML Layout**: `ARSimulationOverlay.uxml`
* **USS Stylesheet**: `ARSimulationOverlay.uss`

#### Technical Implementation Details:
1. **UI Toolkit DOM Bindings**:
   ```csharp
   protected override void BindUI()
   {
       btnExit = root.Q<Button>("btn-exit");
       btnMove = root.Q<Button>("btn-move");
       btnMesh = root.Q<Button>("btn-mesh");
       btnHint = root.Q<Button>("btn-hint");
       btnScoreDropdown = root.Q<Button>("btn-score-dropdown");
       
       scoreLabel = root.Q<Label>("score-value");
       timerLabel = root.Q<Label>("timer-value");
       instructionLabel = root.Q<Label>("instruction-text");
       
       bannerInfo = root.Q<VisualElement>("banner-info");
       bannerWarning = root.Q<VisualElement>("banner-warning");
       modalScoreHistory = root.Q<VisualElement>("modal-score-history");
       tableContainer = root.Q<VisualElement>("table-step-rows");
   }
   ```

2. **3-Tier Notification Banner Renderer**:
   - **Tier 1 (Info `ⓘ`)**: Updates `instructionLabel.text`.
   - **Tier 2 (Warning `❌`)**: Toggles `.visible` class on `bannerWarning` for proximity warnings.
   - **Tier 3 (Critical Alert `⚡`)**: Displays full-screen alert dialog with yellow/black diagonal hazard stripes CSS rules (`background-image: repeating-linear-gradient(...)`).

3. **Dynamic Score History Table Generator**:
   - Populates rows in UI Toolkit DOM:
     ```csharp
     private void BuildScoreHistoryTable(List<StepMetric> metrics)
     {
         tableContainer.Clear();
         foreach (var metric in metrics)
         {
             VisualElement row = new VisualElement();
             row.AddToClassList("table-row");
             
             Label nameLbl = new Label(metric.stepName);
             Label errLbl = new Label($"{metric.errorCount} ●");
             Label durLbl = new Label(FormatTime(metric.durationSeconds));
             Label scoreLbl = new Label($"{metric.score}/100");
             
             row.Add(nameLbl);
             row.Add(errLbl);
             row.Add(durLbl);
             row.Add(scoreLbl);
             tableContainer.Add(row);
         }
     }
     ```

---

### SUBSYSTEM 9: Data Persistence & Cloud Synchronization

#### Primary Classes: `LocalScoreManager.cs` & `AppDataService.cs`

#### Technical Implementation Details:
1. **Local PlayerPrefs Persistence (`LocalScoreManager.cs`)**:
   - Serializes `TrainingResult` object to JSON and appends to local history:
     ```csharp
     string json = JsonUtility.ToJson(result);
     PlayerPrefs.SetString($"Result_{result.moduleName}_{DateTime.Now.Ticks}", json);
     PlayerPrefs.Save();
     ```

2. **Asynchronous Firebase Firestore Sync (`AppDataService.cs`)**:
   - Syncs attempt metrics to Cloud Firestore asynchronously:
     ```csharp
     public async Task SaveAttemptToCloud(TrainingResult result)
     {
         Dictionary<string, object> attemptData = new Dictionary<string, object>
         {
             { "workerId", result.workerId },
             { "moduleName", result.moduleName },
             { "score", result.score },
             { "maxScore", result.maxScore },
             { "percentage", result.percentage },
             { "passed", result.passed },
             { "mistakesCount", result.mistakesCount },
             { "completionTimeSeconds", result.completionTimeSeconds },
             { "timestamp", FieldValue.ServerTimestamp }
         };
         
         DocumentReference docRef = firestore.Collection("users").Document(result.workerId).Collection("attempts").Document();
         await docRef.SetAsync(attemptData);
     }
     ```

---

## 3. Class Relationship Diagram

```
                              ┌────────────────────────┐
                              │  ARSimulationOverlay   │
                              │    (UI Toolkit HUD)    │
                              └───────────┬────────────┘
                                          │ Events / Bindings
                                          ▼
┌──────────────────────────┐    ┌────────────────────┐    ┌──────────────────────────┐
│   ARPlacementManager     │◄───┤FireSafetyModule    ├─►  │ GroundFireController     │
│  (Dual Plane Raycast)    │    │ Manager (Drill)    │    │ (Particle VFX Scaling)   │
└────────────┬─────────────┘    └─────────┬──────────┘    └──────────────────────────┘
             │                            │
             ▼                            ▼
┌──────────────────────────┐    ┌────────────────────┐    ┌──────────────────────────┐
│   ARStepCounterTracker   │    │ARProximitySafety   │    │FireExtinguisherGrab      │
│(6-DOF Camera Displacement)│   │ Validator (3.5ft)  │    │ Controller (1st-Person)  │
└──────────────────────────┘    └────────────────────┘    └──────────────────────────┘
                                          │
                                          ▼
                                ┌────────────────────┐
                                │ AppDataService     │
                                │ (Firestore Cloud)  │
                                └────────────────────┘
```
