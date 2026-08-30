# MiningSafetyAR — Comprehensive Technology Stack & Dependencies

> **Document Version**: 1.0.0  
> **Last Updated**: August 30, 2026  
> **Project Scope**: Next-Generation Mining Safety Training Application in Augmented Reality (AR) with Cloud Synchronization, Interactive PBR 3D Models, Physical Step Tracking, and UI Toolkit Interface.

---

## 1. Executive Summary & Target Platforms

| Metric / Specification | Value / Configuration |
| :--- | :--- |
| **Game Engine** | Unity 6000.3.23f1 LTS (Unity 6 Core Runtime) |
| **Render Pipeline** | Universal Render Pipeline (URP) 17.3.0 |
| **Target Mobile OS (Android)** | Android 16 (API Level 36), ARCore Supported Devices |
| **Target Mobile OS (iOS)** | iOS 17.0+, ARKit Supported Devices |
| **Desktop Editor Environments** | macOS (Apple Silicon / Intel), Windows 11 |
| **Graphics API** | Vulkan (Android Primary), OpenGLES 3.0 (Android Fallback), Metal (iOS / macOS) |

---

## 2. Core Unity Engine Packages & Frameworks

All package dependencies defined in [`Packages/manifest.json`](file:///Users/kavin/Development/MiningSafetyAR/Packages/manifest.json):

### 2.1 Rendering & Graphics
* **`com.unity.render-pipelines.universal` (v17.3.0)**
  * Universal Render Pipeline (URP) core framework.
  * URP Lit Shader (`Universal Render Pipeline/Lit`), Forward+ Lighting Loop, Depth & Normal Prepasses, Soft Shadows, Render Graph architecture.
* **Vefects Stylized Fire VFX Package** (`Assets/Vefects/`)
  * Custom Particle Systems for realistic mine fire hazards and extinguishing smoke clouds.
  * Shaders: [`SH_Vefects_Extra_Billboard_01.shader`](file:///Users/kavin/Development/MiningSafetyAR/Assets/Vefects/Free%20Fire%20VFX%20URP/_%20Extra/Resources/Shaders/SH_Vefects_Extra_Billboard_01.shader), [`SH_Vefects_Extra_Grid_01_URP.shader`](file:///Users/kavin/Development/MiningSafetyAR/Assets/Vefects/Free%20Fire%20VFX%20URP/_%20Extra/Resources/Shaders/SH_Vefects_Extra_Grid_01_URP.shader).

### 2.2 Augmented Reality & Spatial Computing
* **`com.unity.xr.arfoundation` (v6.3.5)**
  * Unity AR Foundation 6.x core framework (`UnityEngine.XR.ARFoundation`).
  * Manages `ARSession`, `XROrigin`, spatial coordinate systems, and device camera lifecycles.
* **`com.unity.xr.arcore` (v6.3.5)**
  * Google ARCore XR Plugin (`UnityEngine.XR.ARCore`).
  * Android spatial motion tracking, plane detection, depth perception, and ARCore camera feed passthrough.
* **`com.unity.xr.arkit` (v6.3.5)**
  * Apple ARKit XR Plugin (`UnityEngine.XR.ARKit`).
  * iOS 6-DOF motion tracking, horizontal/vertical plane anchoring, and Metal passthrough.
* **`com.unity.xr.arSubsystems` (v6.3.5)**
  * Low-level spatial API subsystems (`UnityEngine.XR.ARSubsystems`):
    * **`ARRaycastManager` & `XRRaycastHit`**: Real-time raycasting against physical floor and wall surfaces.
    * **`ARPlaneManager` & `ARPlane`**: Surface mesh detection and boundary tracking.
    * **`ARTrackedImageManager` & `XRReferenceImageLibrary`**: 2D Image Marker Tracking (`ExitSignMarker.jpg`, `FireExtinguisherMarker.jpg`).
    * **`ARAnchorManager` & `ARAnchor`**: Spatial locking of virtual objects to physical room coordinates.
    * **`AROcclusionManager`**: Real-world depth occlusion testing against virtual 3D models.
* **`com.unity.xr-content.xr-sim-environments` (v2.1.1)**
  * XR Foundation Simulation package for testing AR plane detection and image tracking directly inside Unity Editor without a physical mobile device.

### 2.3 3D Model Runtime Ingestion
* **`com.unity.cloud.gltfast` (v6.10.2)**
  * High-performance asynchronous glTF 2.0 and GLB binary loader (`GLTFast.GltfImportBase`).
  * Ingests embedded 3D models (`FireExtinguisherGLTF.bytes`) directly in memory at runtime without disk I/O latency.
* **`com.unity.cloud.draco` (v5.1.4)**
  * Draco 3D mesh compression library (`com.unity.cloud.draco`).
  * Decompresses high-density vertex attribute geometry and UV coordinate streams on worker threads.

### 2.4 Input System & Utilities
* **`com.unity.inputsystem` (v1.20.0)**
  * Unity New Input System (`UnityEngine.InputSystem`).
  * Configured with [`InputSystem_Actions.inputactions`](file:///Users/kavin/Development/MiningSafetyAR/Assets/InputSystem_Actions.inputactions).
  * Multi-touch press bindings (`<Pointer>/press`), EnhancedTouch API, and Editor keyboard shortcuts (`F` for Fire Hazard, `E` for Extinguisher, `C` for Camera reset).
* **`com.unity.ai.navigation` (v2.0.14)**
  * AI Navigation and NavMesh baking for worker AI pathfinding in hazard environments.
* **`com.unity.visualscripting` (v1.9.12)**
  * Visual scripting runtime graph support.
* **`com.unity.timeline` (v1.8.13)**
  * Sequence timeline animation for training tutorial sequences.
* **`com.ivanmurzak.unity.mcp` (v0.90.0)**
  * Model Context Protocol editor plugin for agentic IDE integration.

---

## 3. User Interface (UI) Architecture — Unity UI Toolkit

The user interface is built entirely with **Unity UI Toolkit** (`UnityEngine.UIElements`), replacing traditional UGUI for high performance, vector scaling, and clean CSS-like separation of style and structure.

### 3.1 UXML Document Templates (`Assets/UI/Templates/`)
* **[`SplashPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/SplashPage.uxml)**: Initial animated splash screen with logo fade-in.
* **[`LoginPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/LoginPage.uxml)**: Worker authentication page (Worker ID + PIN entry).
* **[`RegisterPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/RegisterPage.uxml)**: New worker onboarding & profile creation.
* **[`DashboardPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/DashboardPage.uxml)**: Main dashboard with overall progress, active modules, and quick actions.
* **[`TrainingCataloguePage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/TrainingCataloguePage.uxml)**: Catalogue of available safety training modules (Fire Safety, Gas Hazards, Machinery, Electrical, Heights).
* **[`ModuleDetailPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/ModuleDetailPage.uxml)**: Detailed module overview, learning objectives, and start buttons.
* **[`LearningContentPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/LearningContentPage.uxml)**: Interactive slide-by-slide theory content with P.A.S.S. technique tips.
* **[`ARSimulationPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/ARSimulationPage.uxml)**: Transparent HUD overlay for 3D AR simulation passthrough.
* **[`AssessmentPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/AssessmentPage.uxml)**: Multiple-choice quiz assessment with instant feedback.
* **[`ResultsPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/ResultsPage.uxml)**: Score summary, competency breakdown, and certificate unlock.
* **[`CertificatePage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/CertificatePage.uxml)**: Official Mining Safety Certificate view with QR verification code.
* **[`ProgressPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/ProgressPage.uxml)**: Historical attempt analytics and competency charts.
* **[`SettingsPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/SettingsPage.uxml)**: App settings, language selector, and cloud sync trigger.
* **[`QRVerifyPage.uxml`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Templates/Pages/QRVerifyPage.uxml)**: QR code validation scanner interface.

### 3.2 USS Design System (`Assets/UI/Styles/`)
* **[`Theme.uss`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Styles/Theme.uss)**: Design token variables:
  * **Primary Colors**: Safety Orange (`#FF6D00`), Emerald Green (`#2E7D32`), Mine Charcoal (`#1A1A1A`), Surface Dark (`#242424`).
  * **Typography**: Clean sans-serif hierarchy, responsive font sizing, high contrast accessibility ratios.
* **[`Reset.uss`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Styles/Reset.uss)**: Standardized box-sizing, margin, padding, and border resets.
* **[`Forms.uss`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Styles/Forms.uss)**: Input fields, text fields, drop-down menus, and action buttons.
* **[`Header.uss`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Styles/Header.uss)** & **[`BottomNav.uss`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Styles/BottomNav.uss)**: Top app bar and bottom tab bar navigation styling.
* **[`Components.uss`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Styles/Components.uss)**: Cards, score badges, status pills, and progress bars.
* **[`Animations.uss`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Styles/Animations.uss)**: CSS transitions, pulse animations, and slide-in keyframes.
* **[`Icons.uss`](file:///Users/kavin/Development/MiningSafetyAR/Assets/UI/Styles/Icons.uss)**: SVG icon class mappings (`slide-icon-fire`, `slide-icon-extinguisher`, etc.).

---

## 4. Backend Cloud Infrastructure & Services

### 4.1 Google Firebase Architecture
* **Firebase Project ID**: `minesafetyar`
* **Project Number**: `193064823382`
* **Storage Bucket**: `minesafetyar.firebasestorage.app`
* **Configuration Asset**: [`Assets/google-services.json`](file:///Users/kavin/Development/MiningSafetyAR/Assets/google-services.json)

### 4.2 Authentication Engine (`MiningSafetyAR.Firebase.FirebaseAuthManager`)
* **Dual-Mode Hybrid Architecture**:
  1. **Native C++ SDK Mode** ([`FirebaseAuthManager.cs`](file:///Users/kavin/Development/MiningSafetyAR/Assets/Scripts/Firebase/FirebaseAuthManager.cs)): Uses `FirebaseApp.CheckAndFixDependenciesAsync()` and `FirebaseAuth.DefaultInstance` when running on physical Android and iOS devices.
  2. **Cross-Platform REST API Fallback Engine**: Uses `UnityWebRequest` against Google Identity Toolkit (`https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword` & `accounts:signUp`) when running in macOS Unity Editor (100% immune to `DllNotFoundException`).
* **Authentication Pattern**: Worker ID + PIN mapped to `workerId@miningsafety.app` / `ARC{PIN}2026!`.

### 4.3 Cloud Firestore Database (`MiningSafetyAR.Firebase.FirestoreService`)
* **Zero-SDK REST API Engine**: All database operations execute over HTTP via `UnityWebRequest` (`https://firestore.googleapis.com/v1/projects/minesafetyar/databases/(default)/documents`).
* **Data Schema**:
  * **Worker Profiles**: `workers/{uid}`
  * **Module Progress**: `workers/{uid}/progress/{moduleId}`
  * **Quiz & Assessment Results**: `trainingResults/{resultId}`
  * **Phase 0 Test Documents**: `phase0_tests/{uid}`

### 4.4 Automated Manager Lifecycle (`MiningSafetyAR.Firebase.FirebaseBootstrap`)
* Uses Unity's `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` to automatically instantiate persistent manager GameObjects (`FirebaseAuthManager` + `FirestoreService` + `MainThreadDispatcher`) before any scene loads.

---

## 5. C# Scripts & Custom Runtime Systems

```
Assets/Scripts/
├── AR/
│   ├── ARPlacementManager.cs          # AR plane detection & floor tap placement manager
│   ├── ARStepCounterTracker.cs        # 6-DOF physical step displacement tracker
│   ├── GLTFastModelLoader.cs          # Async glTF byte loader & URP PBR material binder
│   ├── FireExtinguisherModelLoader.cs # High-level model loading wrapper
│   ├── ARImageTrackingManager.cs      # 2D marker detection (Exit Sign & Extinguisher)
│   ├── ARCompleteOverlay.cs           # AR complete overlay helper
│   └── AndroidCameraPermissionHelper.cs# Runtime Android permission request helper
├── Firebase/
│   ├── FirebaseAuthManager.cs         # Hybrid Native/REST Firebase Auth service
│   ├── FirestoreService.cs            # Zero-SDK REST Firestore client
│   ├── FirebaseBootstrap.cs           # Auto-spawning runtime initializer
│   └── FirebasePhase0Tester.cs        # Verification suite for cloud connectivity
├── Data/
│   ├── AppDataService.cs              # Central data orchestrator (cloud + cache)
│   ├── WorkerData.cs                  # Worker profile DTO
│   ├── ModuleData.cs                  # Training module schema
│   ├── ModuleDatabase.cs              # Static module definitions
│   ├── QuizQuestionData.cs            # Assessment question schema
│   ├── QuestionDatabase.cs            # Pre-populated quiz question repository
│   ├── CertificateData.cs             # Safety certificate DTO
│   ├── CertificateDatabase.cs         # Certificate records repository
│   ├── LocalScoreManager.cs           # Attempt scores & progress calculator
│   └── LocalDataModels.cs             # Internal serialization models
├── UI/
│   ├── Core/
│   │   └── PageController.cs          # Abstract base controller for UI Toolkit pages
│   ├── Navigation/
│   │   └── NavigationManager.cs       # Scene navigation stack & tab bar manager
│   ├── Helpers/
│   │   ├── IconLoader.cs              # Dynamic UI Toolkit sprite loader
│   │   └── ProgressBarHelper.cs       # Smooth fill progress bar utility
│   └── Pages/
│       ├── SplashPageController.cs
│       ├── LoginPageController.cs
│       ├── RegisterPageController.cs
│       ├── DashboardPageController.cs
│       ├── TrainingCataloguePageController.cs
│       ├── ModuleDetailPageController.cs
│       ├── LearningContentPageController.cs
│       ├── ARSimulationPageController.cs
│       ├── AssessmentPageController.cs
│       ├── ResultsPageController.cs
│       ├── CertificatePageController.cs
│       ├── ProgressPageController.cs
│       ├── SettingsPageController.cs
│       ├── QRVerifyPageController.cs
│       └── BottomNavController.cs
├── Localization/
│   └── LocalizationManager.cs         # Multi-language text provider (EN, HI, TE, TA, KN)
├── Helpers/
│   └── MainThreadDispatcher.cs        # Thread-safe main thread dispatch queue
└── Editor/
    ├── ARSceneBuilder.cs              # Editor build automation script
    ├── Phase1Seeder.cs                # Database seeder for Phase 1
    ├── Phase3SceneSeeder.cs           # Scene seeder for Phase 3
    ├── Phase4SceneSeeder.cs           # Scene seeder for Phase 4
    ├── Phase5SceneSeeder.cs           # Scene seeder for Phase 5
    └── Phase6SceneSeeder.cs           # Scene seeder for Phase 6
```

---

## 6. Physical Motion Tracking & 3D AR Algorithms

### 6.1 Physical Step Counter Engine ([`ARStepCounterTracker.cs`](file:///Users/kavin/Development/MiningSafetyAR/Assets/Scripts/AR/ARStepCounterTracker.cs))
* **Tracking Mechanism**: Tracks real-world user walking displacement using 6-DOF camera coordinates (`Camera.main.transform.position`).
* **Filtering & Thresholds**:
  * Applies horizontal distance tracking (`Vector3.Distance` projected on XZ plane).
  * Filters noise and micro-jitter with step length thresholds (0.45m – 0.65m per step).
* **Dynamic Model Spawning**:
  * When 5 to 15 physical steps are completed, triggers `LoadFireExtinguisherModelAsync()`.
  * Computes position **1.0m directly in front of the camera's forward direction** (`mainCam.transform.position + forward * 1.0f`) at ground level.

### 6.2 URP Material Repair & Pink Shader Prevention ([`GLTFastModelLoader.cs`](file:///Users/kavin/Development/MiningSafetyAR/Assets/Scripts/AR/GLTFastModelLoader.cs))
* **Automatic Shader Querying**: Finds valid `Universal Render Pipeline/Lit` shaders (`guid: 910d3836c6f5c2a4492fb89eda5b4b86`).
* **Shader Filtering**: Explicitly rejects `Hidden/InternalErrorShader` (pink fallback) and `Simulation/Room X-Ray` (invisible stencil occlusion shader).
* **PBR Material Assignment**: Programmatically instantiates fresh URP Lit materials and binds textures:
  * **Cylinder Tank Body**: Fire Engine Red (`#D32F2F`), Metallic 0.85, Smoothness 0.80.
  * **Plastic Lever & Nozzle**: Charcoal Black (`#212121`), Metallic 0.10, Smoothness 0.40.
  * **Metal Valve & Ring**: Chrome Silver (`#E0E0E0`), Metallic 0.95, Smoothness 0.90.
  * **Warning Label**: High-resolution operating instruction texture sticker.

---

## 7. Developer Tooling & Automation Scripts

### 7.1 Shell Scripts (Log Streaming & Diagnostics)
* **[`stream_extinguisher_logs.sh`](file:///Users/kavin/Development/MiningSafetyAR/stream_extinguisher_logs.sh)**
  * Shell script filtering live Android `adb logcat` output for `extinguisher|gltfast|submesh|pink|shader|fire`.
  * Automatically caps output at 1500 lines in [`extinguisher_logs.txt`](file:///Users/kavin/Development/MiningSafetyAR/extinguisher_logs.txt).
* **[`stream_device_logs.sh`](file:///Users/kavin/Development/MiningSafetyAR/stream_device_logs.sh)**
  * General logcat streaming utility for Unity console messages.

### 7.2 Python Cloud Tools
* **[`create_firestore_db.py`](file:///Users/kavin/Development/MiningSafetyAR/create_firestore_db.py)**
  * Python script for seeding default collections, indexes, and initial worker schemas in Google Cloud Firestore.

### 7.3 Version Control Configuration
* **[`.gitignore`](file:///Users/kavin/Development/MiningSafetyAR/.gitignore)**
  * Excludes Unity temporary build artifacts (`/[Ll]ibrary/`, `/[Tt]emp/`, `/[Oo]bj/`, `/[Bb]uilds/`), IDE settings (`.vs/`, `.idea/`), and streaming log files (`extinguisher_logs.txt`, `device_logs.txt`).

---

## 8. Complete Tech Stack Summary Table

| Category | Component / Library | Version / Details | Purpose |
| :--- | :--- | :--- | :--- |
| **Engine** | Unity LTS | 6000.3.23f1 | Core game engine & runtime |
| **Render Pipeline** | Universal Render Pipeline | 17.3.0 | URP Forward+ lighting & rendering |
| **AR Framework** | Unity AR Foundation | 6.3.5 | Cross-platform AR session management |
| **AR Android Provider** | Google ARCore XR Plugin | 6.3.5 | Android spatial tracking & plane detection |
| **AR iOS Provider** | Apple ARKit XR Plugin | 6.3.5 | iOS spatial tracking & plane detection |
| **AR Simulation** | XR Sim Environments | 2.1.1 | In-Editor AR plane & marker simulation |
| **3D Loader** | glTFast | 6.10.2 | Async glTF 2.0 / GLB runtime model parser |
| **Mesh Compression** | Draco Compression | 5.1.4 | High-efficiency 3D mesh decompression |
| **Input System** | Unity Input System | 1.20.0 | Touch, gesture, and hotkey input management |
| **UI Framework** | Unity UI Toolkit | Built-in | Vector flexbox UI layout & styling |
| **Cloud Auth** | Firebase Auth (Native + REST) | Hybrid | Worker login, registration & session tokens |
| **Cloud Database** | Google Cloud Firestore REST | REST v1 API | Cloud profile, progress & quiz persistence |
| **VFX System** | Vefects Fire VFX Pack | Custom URP | Mine fire hazards & extinguishing particle effects |
| **Build Automation** | ARSceneBuilder.cs | Custom C# Editor | Automated URP AR scene build pipeline |
| **Log Streaming** | `stream_extinguisher_logs.sh` | Bash / adb | Android device log filtering (1500 lines) |
