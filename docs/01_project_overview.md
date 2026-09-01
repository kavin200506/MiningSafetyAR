# 01 — Project Overview

## Project Purpose

**MiningSafetyAR** is an Android-first Augmented Reality (AR) mobile application designed for safety training of miners in Jharkhand, India. It targets compliance with DGMS (Directorate General of Mines Safety) standards and aims to deliver immersive, on-device AR simulations of mining hazard scenarios — fire/explosion, gas leaks, machinery — with multilingual support for English, Hindi, and Santali.

## Problem Statement Alignment

The app addresses the high incidence of preventable mining accidents caused by inadequate safety training, particularly in rural Jharkhand where literacy rates and internet connectivity are limited. Key goals:

- Replace classroom-only training with interactive AR simulations that workers can practice on a smartphone.
- Issue verifiable digital certificates that mine supervisors and DGMS inspectors can QR-scan to confirm compliance.
- Track regional training analytics (GPS-tagged results) for mine safety officers.
- Work offline or in low-connectivity conditions with deferred cloud sync.

## Tech Stack Summary

| Layer | Technology |
|---|---|
| Engine | Unity 6 (6000.x) |
| AR Framework | AR Foundation 6.3.5 + ARCore 6.3.5 + ARKit 6.3.5 |
| Render Pipeline | Universal Render Pipeline (URP) 17.3.0 |
| UI System | Unity UI Toolkit (UIElements / UXML + USS) |
| Backend | Google Firebase — Firestore (REST API, no Unity SDK at runtime) |
| Authentication | Firebase Auth (Native C++ SDK with full REST API fallback) |
| 3D Model Loading | glTFast 6.10.2 + Draco 5.1.4 decompression |
| Input System | Unity Input System 1.20.0 (EnhancedTouch + Pointer) |
| Navigation | Custom SceneManager-based `NavigationManager` |
| Localization | Custom `LanguageManager` (in-code string arrays) |
| Local Storage | JSON files via `Application.persistentDataPath` + Unity `PlayerPrefs` |
| Target Platform | Android (ARCore) — APK deployed and tested |
| Build System | Gradle (Groovy DSL) via Unity Android build |

## Folder Structure

```
MiningSafetyAR/
├── Assets/
│   ├── Data/                          # ScriptableObject databases (ModuleDatabase, QuestionDatabase, CertificateDatabase)
│   ├── Firebase/                      # Firebase Unity SDK plugin (native Android .aar libs, EDM config)
│   ├── ImageTracking/                 # 2D marker image library (FireExtinguisherMarker, ExitSignMarker)
│   ├── ImageTrackingResources/        # Original high-res marker JPEGs + ReferenceImageLibrary.asset
│   ├── Models/FireExtinguisher/       # 3D model files folder (content served via StreamingAssets)
│   ├── Plugins/Android/               # mainTemplate.gradle, Firebase Android libs
│   ├── Plugins/NuGet/                 # ASP.NET SignalR + Roslyn DLLs (MCP/IDE tooling, NOT app logic)
│   ├── Prefabs/                       # AR planes, fire VFX, fire extinguisher 3D model, placement indicator
│   ├── Resources/                     # Runtime-loadable assets (GLTF bytes, textures, UI templates)
│   ├── Scenes/                        # 19 Unity scenes (1 AR + 18 UI)
│   ├── Scripts/AR/                    # AR Foundation controllers (placement, image tracking, GLTF loading)
│   ├── Scripts/Assessment/            # AssessmentEngine quiz engine
│   ├── Scripts/Certification/         # CertificateGenerator (HMAC-SHA256)
│   ├── Scripts/Data/                  # Data models, AppDataService, LocalScoreManager, GPS capture
│   ├── Scripts/Editor/                # Unity Editor menu scripts (scene seeders, DB creators)
│   ├── Scripts/Firebase/              # FirebaseAuthManager, FirestoreService, FirebaseBootstrap
│   ├── Scripts/Helpers/               # MainThreadDispatcher
│   ├── Scripts/Localization/          # LanguageManager (3-language selector)
│   ├── Scripts/Modules/               # BaseModuleManager, FireSafetyModuleManager, GasLeakModuleManager
│   ├── Scripts/Sync/                  # CloudSyncManager (empty shell — deprecated)
│   ├── Scripts/UI/                    # UI system: Navigation, Pages, Helpers, Core
│   ├── Settings/                      # URP render pipeline configs (Mobile + PC)
│   ├── StreamingAssets/FireExtinguisher/ # GLTF + PBR textures (runtime-loaded on Android)
│   ├── UI/Templates/Pages/            # 15 UXML page templates (one per scene)
│   ├── UI/Templates/Components/       # 12 reusable UXML components
│   ├── UI/Styles/                     # 9 USS stylesheets
│   ├── Vefects/                       # Third-party URP particle VFX pack (ground fire + smoke)
│   └── XR/                            # XR plugin loaders and subsystem settings
├── ContentPackages/                   # XR simulation environment tarballs
├── Packages/manifest.json             # Unity package manifest (25 packages)
├── ProjectSettings/                   # Unity player settings, XR settings, etc.
├── create_firestore_db.py             # Python admin script to provision Firestore DB
├── monitor_location_logs.sh           # adb logcat helper script for GPS logs
├── stream_device_logs.sh              # adb logcat helper for all device logs
├── stream_extinguisher_logs.sh        # adb logcat helper for fire extinguisher logs
├── new.apk                            # Built APK file (~96 MB)
├── AR App in 15 Minutes 2026.txt      # Developer notes
├── AR Mining Safety Project Roadmap.md
└── README.md
```

## Firebase Project

| Field | Value |
|---|---|
| Project ID | `minesafetyar` |
| App Package | `com.company.minear` |
| Firebase Project Number | `193064823382` |
| Firestore Region | `asia-south1` (Mumbai) |
| API Key | `AIzaSyBxhWZvIqgIMP1niRSc_H2iRMkdDLNdybI` (hardcoded — see 09_known_gaps_and_todos.md) |
