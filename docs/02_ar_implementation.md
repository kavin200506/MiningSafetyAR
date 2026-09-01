# 02 — AR Implementation

## Overview

The AR layer is built on **AR Foundation 6.3.5** (provider-agnostic) with **ARCore 6.3.5** for Android. Two distinct AR modalities are implemented: plane detection + object placement, and 2D image tracking.

---

## 1. AR Plane Detection & Object Placement

**File:** `Assets/Scripts/AR/ARPlacementManager.cs`

### Architecture

`ARPlacementManager` is a Singleton MonoBehaviour. It drives the main AR simulation scene (`AR Plane Detection Placement.unity`).

### Key Components Used

| Component | Role |
|---|---|
| `ARRaycastManager` (AR Foundation) | Casts rays against detected AR planes to find hit points |
| `ARPlaneManager` (AR Foundation) | Manages plane detection lifecycle |
| `ARSession` (AR Foundation) | Controls the AR session |
| `ARCameraBackground` | Passthrough camera rendering |
| `InputSystem.EnhancedTouch.Touch` | Touch-to-place interaction |

### Placement State Machine

The manager uses `enum PlacementState` with the following states:

```
Idle → WaitingForPlane → ReadyToPlace → PlacementLocked
```

- `PlacementLocked` is entered immediately after the user taps to place.
- A **3-second safety window** (`private float placementStartTime; private bool isPlacementLocked`) prevents accidental duplicate placements. After 3 seconds, `isPlacementLocked = false` and the user can re-place.

### Placement Indicator

A `PlacementIndicator.prefab` (reticle) is positioned on the detected plane surface in real-time every frame during `ReadyToPlace` state, driven by `ARRaycastManager.Raycast()` hits against `TrackableType.PlaneWithinPolygon`.

### Object Placement

When a touch event triggers placement:
1. An `ARRaycast` hit is computed at the screen-center or touch position.
2. The placed GameObject (fire hazard or equipment) is instantiated at `hitPose.position` with `hitPose.rotation`.
3. The `PlacementIndicator` is hidden.
4. The `ARStepCounterTracker` is notified that placement has occurred (fires `OnFirePlaced` event).

### Editor Simulation Hotkeys

For Unity Editor testing without a physical device:
- `F` key → simulate tap-to-place fire hazard
- `E` key → simulate extinguisher discovered
- `C` key → simulate module completion

### Camera Permission

`Assets/Scripts/AR/AndroidCameraPermissionHelper.cs` — MonoBehaviour that:
- Calls `Permission.RequestUserPermission(Permission.Camera)` on Android Awake
- Polls for up to `10.0f` seconds with `0.5f` interval
- Logs timeout error if camera is not authorized

---

## 2. AR Step Counter & Walking Tracker

**File:** `Assets/Scripts/AR/ARStepCounterTracker.cs`

### Purpose

Tracks the user's **physical walking steps** in real-world meters using **6-DOF camera tracking** (AR Foundation camera pose tracking). The user must walk 5–15 steps after placing a fire hazard to "discover" the fire extinguisher.

### State Machine

```
Idle → SearchingForExtinguisher → ScanningForWall → ExtinguisherDiscovered
```

### Key Implementation Details

- `lastCameraPosition` (Vector3) is updated each frame; delta distance is measured.
- Steps are counted when `deltaDistance > averageStepLengthMeters * threshold` AND `Time.time - lastStepTime > MinTimeBetweenSteps (0.35f)`.
- `averageStepLengthMeters = 0.65f` (~65cm per step).
- `targetSteps` is randomly selected from `[minRequiredSteps=5, maxRequiredSteps=15]`.
- After reaching `targetSteps`, a **wall scan** coroutine runs for 5 seconds using `ARRaycastManager` to detect vertical planes (`TrackableType.PlaneWithinPolygon` with normal pointing horizontal).
- If a wall is found: fire extinguisher is mounted on the wall.
- If no wall is found: fire extinguisher falls back to floor placement.

### Events Fired

| Event | Signature |
|---|---|
| `OnStepCountUpdated` | `Action<int, int>` (currentSteps, targetSteps) |
| `OnExtinguisherDiscovered` | `Action<Vector3>` (world position of discovered extinguisher) |

---

## 3. Fire Extinguisher Grab & Carry

**File:** `Assets/Scripts/AR/FireExtinguisherGrabController.cs`

### Purpose

Implements a first-person "Grab & Carry" mechanic allowing the user to tap the fire extinguisher and carry it toward the fire hazard.

### Grab State Machine

```
Unbound → Grabbing → Held → ArrivedAtFire
```

### Key Implementation Details

- `maxGrabDistance = 6.0f` meters from camera — objects farther than this cannot be grabbed.
- `maxRaycastDistance = 15.0f` meters for raycasting against grabbable layer.
- Uses `LayerMask` `grabbableLayerMask` and `heldItemLayerMask`.
- On grab: object is smoothly lerp'd to `heldItemSlot` transform (child of AR camera) over `grabDuration = 0.4f` seconds.
- `heldItemSlot` localPosition recommended: `(0.18f, -0.22f, 0.35f)`, localRotation: `Euler(10°, -15°, 0°)`.
- On arrival at fire (within `arrivalDistanceMeters = 1.2f`): triggers `FireSafetyModuleManager.PerformPASSSubStep()` sequence.
- Touch tap detection uses `InputSystem.EnhancedTouch`.

---

## 4. 3D Model Loading (glTFast)

**File:** `Assets/Scripts/AR/GLTFastModelLoader.cs`

### Purpose

Loads the fire extinguisher 3D model at runtime from `StreamingAssets` as a `.gltf` file using **glTFast 6.10.2** with Draco mesh decompression.

### Key Implementation Details

- `GetFireExtinguisherGLTFCandidateURIs()` returns an ordered list of candidate file paths:
  1. `StreamingAssets/Models/FireExtinguisher/FireExtinguisher.gltf`
  2. `StreamingAssets/FireExtinguisher.gltf`
  3. Editor fallback paths for development
- On Android: uses `UnityWebRequest` to read from `StreamingAssets` (required because Android packages assets in APK).
- Performs **URP Lit shader assignment** after load to fix pink/invisible mesh issues on Android.
- Applies **3D geometric volume analysis** to determine correct scale.
- Logs full diagnostics on load success/failure.

### Shader Fix (Android URP)

After glTFast loads the model, all materials are iterated and replaced with `Shader.Find("Universal Render Pipeline/Lit")` to prevent the X-Ray/invisible rendering issue that occurs when glTFast defaults to Standard shader not available in URP.

---

## 5. AR Image Tracking

**File:** `Assets/Scripts/AR/ARImageTrackingManager.cs`

### Purpose

Manages **2D marker-based tracking** where physical printouts of markers trigger 3D model overlays.

### Key Components Used

| Component | Role |
|---|---|
| `ARTrackedImageManager` | Detects reference images from `XRReferenceImageLibrary` |
| `ARTrackedImage` | Represents a currently tracked 2D image in the scene |

### Marker Library

`Assets/ImageTrackingResources/ReferenceImageLibrary.asset` contains the reference images:
- `FireExtinguisherMarker.jpg` — triggers `FireExtinguisherModel.prefab` placement
- `ExitSignMarker.jpg` — triggers `ExitSignModel.prefab` placement

### Tracking Event Handling

- `OnTrackedImagesChanged` event fires with `ARTrackedImagesChangedEventArgs`.
- For `added` images: instantiates the matching prefab at `trackedImage.transform.position/rotation`.
- For `updated` images: moves the existing prefab to track the marker.
- For `removed` images: destroys or hides the prefab.

### Test Scene

`Assets/Scenes/AR Image Tracking.unity` — standalone scene for testing image tracking independently from the main simulation flow.

---

## 6. AR Platform Settings

### Android Manifest Permissions

The following permissions are set via Unity's XR plugin framework (managed in `ProjectSettings/XRPlugin`):
- `android.permission.CAMERA` — required for ARCore
- `android.permission.ACCESS_FINE_LOCATION` — for GPS training analytics

### Gradle Dependencies (mainTemplate.gradle)

```groovy
implementation 'com.google.android.gms:play-services-base:18.10.1'
implementation 'com.google.firebase:firebase-analytics:23.2.0'
implementation 'com.google.firebase:firebase-auth:24.2.0'
implementation 'com.google.firebase:firebase-common:22.2.0'
implementation 'com.google.firebase:firebase-firestore:26.6.0'
```

### URP Render Pipeline

Two render pipeline assets are configured:
- `Mobile_RPAsset.asset` — mobile-optimized settings for Android
- `PC_RPAsset.asset` — higher-quality settings for Editor testing

Both use their respective `Mobile_Renderer.asset` / `PC_Renderer.asset` forward renderers.

---

## AR Scenes Summary

| Scene Name | Purpose |
|---|---|
| `AR Plane Detection Placement.unity` | Main AR simulation — plane detection, fire hazard, extinguisher |
| `AR Image Tracking.unity` | Standalone image tracking test (not in main flow) |
| `AR Occlusion.unity` | AR depth occlusion demo (not in main flow) |

---

## NOT IMPLEMENTED (flagged against roadmap)

- **AR Occlusion** for realistic depth hiding of virtual objects behind real surfaces — scene exists but occlusion shader is not wired to training modules.
- **ARKit support** — ARKit package 6.3.5 is installed but iOS build is not configured (no iOS deployment target, no ARKit-specific configuration in `ProjectSettings`).
- **Multi-user/multiplayer AR** — no implementation exists.
- **AR Anchors / Cloud Anchors** — `ARAnchorManager` is not used anywhere in code.
- **QR code scanning via camera** — the `UI_QRVerify` scene uses manual text input only; no camera-based QR reader is implemented.
