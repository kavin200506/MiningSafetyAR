# ARC MineSafetyAr — AR-Based Vocational Training Simulator

[![Unity](https://img.shields.io/badge/Unity-6000.3.23f1%20LTS-black?logo=unity)](https://unity.com/)
[![AR Foundation](https://img.shields.io/badge/AR%20Foundation-6.3-blue)](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/index.html)
[![Firebase](https://img.shields.io/badge/Firebase-Auth%20%7C%20Firestore-orange?logo=firebase)](https://firebase.google.com/)
[![Target Platform](https://img.shields.io/badge/Platform-Android%20(API%2029%2B)-green?logo=android)](https://developer.android.com/)

> **Smart India Hackathon (SIH26041)**  
> **Problem Statement Owner:** Government of Jharkhand — Department of Higher & Technical Education  
> **Domain:** Industrial & Subterranean Mining Vocational Safety Training  

---

## 📌 Overview

**ARC MineSafetyAr** is an augmented reality (AR) industrial safety training platform and certification system built with **Unity 6 (6000.3.23f1 LTS)** and **UI Toolkit**. It trains industrial and mine workers on critical emergency protocols, hazard mitigation, equipment positioning, and emergency procedures through interactive AR simulations and quizzes.

### Core Features
- **AR Emergency Simulations:** Interactive surface placement (plane detection) and marker tracking (Fire Extinguisher & Exit Sign posters) implementing the P.A.S.S. technique, PPE selection, and evacuation protocols.
- **14-Page UI Toolkit Architecture:** Clean, high-contrast Light Theme interface (`#FFFFFF` background, `#FF6D00` industrial orange accent) built with Unity UI Toolkit (UXML + USS).
- **Firebase Auth & Firestore Integration:** Worker account creation, PIN login (`workerid@miningsafety.app`), Firestore profile syncing, attempt history tracking, and personal best score evaluation.
- **Digital QR Certification:** Generates verifiable digital qualification certificates embedded with HMAC QR code verification.
- **Trilingual Localization & Offline Caching:** Multi-lingual support (**English, Hindi, Santali**) with local JSON persistence for offline subterranean operation and auto-syncing when connected.

---

## 🚀 Getting Started & Setup Guide

Follow these steps to set up and run the project after cloning.

### 1. Prerequisites
Ensure you have the following installed:
* [Unity Hub](https://unity.com/download)
* **Unity 6000.3.23f1 LTS** with the following build modules installed:
  - **Android Build Support**
  - **OpenJDK**
  - **Android SDK & NDK Tools**
* Developer Mode + USB Debugging enabled on your physical Android test device (Android 10.0 / API 29+).

---

### 2. Clone the Repository
```bash
git clone https://github.com/kavin200506/MiningSafetyAR.git
cd MiningSafetyAR
git checkout hari
```

---

### 3. Firebase Configuration File (`google-services.json`)
For security best practices, credentials are excluded from source control. Obtain `google-services.json` from the team administrator (Firebase Project: `minesafetyar`, Package: `com.company.minear`) and place it in the project root `Assets/` directory:

```
MiningSafetyAR/
└── Assets/
    └── google-services.json   <-- Place file here
```

---

### 4. Open Project & Switch Platform to Android
1. Open **Unity Hub**, click **Add**, and select the cloned `MiningSafetyAR` project folder.
2. Select Unity Version **6000.3.23f1 LTS**.
3. Once Unity finishes opening, go to **File ➔ Build Profiles** (or *Build Settings*).
4. Select **Android** under Platforms and click **Switch Platform**.

---

### 5. Force Resolve Android Dependencies
Before running or building, run Unity's Play Services Resolver to download native Android Firebase packages:

1. In the top menu bar, click **Assets** ➔ **External Dependency Manager** ➔ **Android Resolver** ➔ **Force Resolve**.
2. Wait 30–60 seconds for the popup message **"Android Zip Resolution Succeeded"**.

---

## 📱 Navigation & App Flow

```
UI_Splash ──► UI_Login / UI_Register ──► UI_Dashboard
                                              │
         ┌───────────────────┬────────────────┼───────────────────┐
         ▼                   ▼                ▼                   ▼
UI_TrainingCatalogue   UI_Progress      UI_Settings       UI_ModuleDetail
                                                                  │
                                                                  ▼
                                                         UI_LearningContent
                                                                  │
                                                                  ▼
                                                         UI_ARSimulation
                                                                  │
                                                                  ▼
                                                         UI_Assessment
                                                                  │
                                                                  ▼
                                                          UI_Results
                                                                  │
                                                                  ▼
                                                        UI_Certificate
                                                                  │
                                                                  ▼
                                                          UI_QRVerify
```

---

## ❓ Troubleshooting & Common Issues

| Issue / Error | Cause | Solution |
|---|---|---|
| **`google-services.json missing`** | Credentials file missing in `Assets/`. | Download `google-services.json` and copy it to `Assets/google-services.json`. |
| **"Resolving Android Dependencies" progress bar freezes** | Network timeout or initial resolution block. | Close the dialog and run *Assets ➔ External Dependency Manager ➔ Android Resolver ➔ Force Resolve*. |
| **Typed input text invisible in Login / Register** | USS CSS selector conflict. | Fixed in `Forms.uss` by explicitly targeting `#unity-text-input`. |
| **Right-side Scrollbar visible on pages** | Default UI Toolkit scroller track. | Hidden visually in `Reset.uss` while preserving touch/drag scrollability. |
| **AR Narration Audio Clips missing** | Scene instances destroyed by singleton `AutoSpawn()`. | Assign narration clips on prefab at `Assets/Resources/Prefabs/ARNarrationController.prefab` or in `Assets/Resources/Audio/Narration/{lang}/{event}.wav`. Do not place instances in scene. |

---

## 📂 Project Directory Structure

```
MiningSafetyAR/
├── Assets/
│   ├── ImageTracking/             # AR Reference Image Libraries & Posters
│   ├── Plugins/                   # Android native plugins & Firebase SDKs
│   ├── Prefabs/                   # 3D models (Fire Extinguisher, Exit Sign, Fire Particles)
│   ├── Scenes/                    # 14 UI Toolkit scenes + AR simulation scenes
│   │   ├── UI_Splash.unity
│   │   ├── UI_Login.unity
│   │   ├── UI_Dashboard.unity
│   │   └── ...
│   ├── Scripts/
│   │   ├── AR/                    # ARPlacementManager & ARImageTrackingManager
│   │   ├── Data/                  # AppDataService, ModuleData, WorkerData
│   │   ├── Firebase/              # FirebaseAuthManager & FirestoreService
│   │   ├── Modules/               # FireSafety & GasLeak module controllers
│   │   └── UI/                    # NavigationManager & 14 Page Controllers
│   ├── UI/                        # UI Toolkit UXML templates & USS stylesheets
│   │   ├── Styles/                # Theme.uss (Light Theme), Reset.uss, Components.uss
│   │   └── Templates/             # Reusable UI component & page UXML templates
│   └── google-services.json       # Firebase Android Config (Add manually)
├── ProjectSettings/               # Unity Player & Build settings
└── README.md
```

---

## 👥 Team & Attribution

Developed for **Smart India Hackathon (SIH26041)** — Government of Jharkhand.
