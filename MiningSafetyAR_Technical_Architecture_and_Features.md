# MiningSafetyAR — Comprehensive Technical Architecture & Feature Specification

## Overview
**MiningSafetyAR** is an augmented reality (AR) vocational training simulator built with **Unity 6000.3.23f1 LTS**, **AR Foundation 6.3**, and **Universal Render Pipeline (URP)** for mobile Android devices (API Level 29+ / Android 10 to Android 16). The project trains mine workers on critical emergency protocols, hazard mitigation, equipment positioning, and safety inspection.

---

## Technical Stack & Compatibility Matrix
- **Engine**: Unity 6000.3.23f1 LTS (64-bit)
- **Render Pipeline**: Universal Render Pipeline (URP) with custom renderer features
- **AR Framework**: AR Foundation 6.3.0 + ARCore XR Plugin 6.3.0
- **Input Architecture**: Unity New Input System (`UnityEngine.InputSystem`, `EnhancedTouch`, `InputAction("<Pointer>/press")`)
- **Target Platform**: Android (ARM64 / ARMv7, Vulkan & OpenGLES3)
- **Minimum Android API Level**: Android 10.0 (API Level 29)

---

## Core Systems & Implementation Details

### 1. AR Plane Detection & Placement System (`ARPlacementManager.cs`)
- **Event-Driven Pointer Binding**: Adopts Unity's official `arfoundation-samples` architecture utilizing `InputAction("touch", binding: "<Pointer>/press")` and `EnhancedTouchSupport.Enable()`. Completely eliminates legacy `Input.touchCount` to prevent Android player exceptions.
- **3-Tier Surface Hit-Testing**:
  - **Tier 1 (Surface Polygon)**: Performs raycasting against real physical surface boundaries using `TrackableType.AllTypes | TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Planes`.
  - **Tier 2 (Environment Depth Map)**: Leverages `AROcclusionManager` depth images if supported by hardware.
  - **Tier 3 (Instant Placement)**: Falls back to `raycastManager.AddRaycast(touchPosition, 1.5f)` for instant spatial locking.
- **Spatial Anchoring (`ARAnchor`)**: Every instantiated or repositioned object is bound to a physical `ARAnchor` component. Re-anchoring uses synchronous `DestroyImmediate(spawnedAnchor)` before attaching a new anchor.
- **3D Model Integration**: Spawns animated 3D assets such as **`DogPrefab_Parent.prefab`** (`Dog_Model.fbx` with `Dog_Texture.png` and URP materials).

### 2. AR Image Marker Tracking System (`ARImageTrackingManager.cs`)
- **Marker Image Subscriptions**: Listens directly to `arTrackedImageManager.trackablesChanged.AddListener()`.
- **Supported Markers**:
  - **`FireExtinguisherMarker`**: High-contrast safety station poster (`Assets/ImageTracking/FireExtinguisherMarker.jpg`).
  - **`ExitSignMarker`**: Emergency exit sign poster (`Assets/ImageTracking/ExitSignMarker.jpg`).
- **Tracking Lifecycle Handling**:
  - **`TrackingState.Tracking`**: Instantiates or repositions 3D safety equipment models (`FireExtinguisherModel.prefab`, `ExitSignModel.prefab`) with `0.35m` scale and vivid URP materials (Vivid Safety Red and Emerald Green).
  - **`TrackingState.Limited` / `TrackingState.None`**: Safely hides spawned GameObjects (`SetActive(false)`), allowing clean re-appearance when tracking resumes.
  - **`eventArgs.removed`**: Cleans up destroyed markers using `Destroy(spawnedObj)` and updates the tracking dictionary.

### 3. Automated 1-Click AR Scene Builder (`ARSceneBuilder.cs`)
- **Menu Item**: `Mining Safety AR` -> `Setup Core AR Scene`
- **Automated Operations**:
  - Cleans up duplicate root main cameras.
  - Configures `ARSession`, `XR Origin`, `ARCameraManager`, `ARCameraBackground`, `AROcclusionManager`, `ARRaycastManager`, `ARPlaneManager`, and `ARPlacementManager`.
  - **URP Renderer Features**: Automatically injects `ARBackgroundRendererFeature` and `ARCommandBufferSupportRendererFeature` into **all** URP renderer assets (`Mobile_Renderer`, `PC_Renderer`, `UniversalRendererData`).
  - **Active Input Handling**: Sets `activeInputHandler = 1` (**Input System Package (New)**) in `ProjectSettings.asset` for Android player build compatibility.
  - **Programmatic Reference Image Library**: Uses official `UnityEditor.XR.ARSubsystems.XRReferenceImageLibraryExtensions` (`library.Add()`, `library.SetTexture()`, `library.SetName()`, `library.SetSize()`, `library.SetSpecifySize()`) to auto-create and populate `Assets/ImageTracking/MiningSafetyImageLibrary.asset` with `0.2m` physical real-world sizing and `isReadable=true` texture importer flags.

### 4. Educational & Assessment Engine
- **`AssessmentEngine.cs`**: Evaluates trainee performance across hazard detection, equipment selection, and safety procedure execution.
- **`CertificateGenerator.cs`**: Generates safety qualification certificates upon module completion.
- **`LocalScoreManager.cs`**: Manages offline-first score caching and history using JSON serialization.
- **`LanguageManager.cs`**: Multi-lingual localization engine supporting English, Hindi, and Santali.
- **`CloudSyncManager.cs`**: Syncs offline trainee scores and assessment metrics to cloud endpoints when connectivity is restored.

---

## Repository Structure & Assets

```
MiningSafetyAR/
├── Assets/
│   ├── ImageTracking/
│   │   ├── FireExtinguisherMarker.jpg
│   │   ├── ExitSignMarker.jpg
│   │   └── MiningSafetyImageLibrary.asset
│   ├── ImageTrackingResources/
│   │   ├── TrackedImageJPG/
│   │   └── ReferenceImageLibrary.asset
│   ├── Prefabs/
│   │   ├── AR Default Plane.prefab
│   │   ├── Dog/ (DogPrefab_Parent.prefab, Dog_Model.fbx, Dog_Texture.png)
│   │   ├── FireExtinguisherModel.prefab
│   │   ├── ExitSignModel.prefab
│   │   └── SampleAREquipment.prefab
│   ├── Scenes/
│   │   ├── SampleScene.unity
│   │   ├── AR Plane Detection Placement.unity
│   │   ├── AR Image Tracking.unity
│   │   └── AR Occlusion.unity
│   ├── Scripts/
│   │   ├── AR/
│   │   │   ├── ARPlacementManager.cs
│   │   │   ├── ARImageTrackingManager.cs
│   │   │   └── AndroidCameraPermissionHelper.cs
│   │   ├── Assessment/AssessmentEngine.cs
│   │   ├── Certification/CertificateGenerator.cs
│   │   ├── Data/LocalDataModels.cs & LocalScoreManager.cs
│   │   ├── Editor/ARSceneBuilder.cs
│   │   ├── Localization/LanguageManager.cs
│   │   ├── Modules/ (BaseModuleManager.cs, FireSafetyModuleManager.cs, GasLeakModuleManager.cs)
│   │   ├── Sync/CloudSyncManager.cs
│   │   └── ARPlaceCube.cs
│   └── UnityXRContent/ (Simulation Environments & Reference Objects)
├── ProjectSettings/
├── Packages/
├── stream_device_logs.sh
└── MiningSafetyAR_AllCodeSummary.txt
```
