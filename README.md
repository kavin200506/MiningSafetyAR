# 🛡️ Hi-AR — Subterranean & Industrial AR Mining Safety Training & Compliance Platform

[![Unity](https://img.shields.io/badge/Unity-6000.3.23f1%20LTS-black?logo=unity)](https://unity.com/)
[![AR Foundation](https://img.shields.io/badge/AR%20Foundation-6.3-blue)](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/index.html)
[![Unity Sentis AI](https://img.shields.io/badge/Sentis%20AI-ONNX%20Inference-purple)](https://unity.com/products/sentis)
[![Target Platform](https://img.shields.io/badge/Platform-Android%20(API%2029%2B)-green?logo=android)](https://developer.android.com/)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](LICENSE)

> **Smart India Hackathon (SIH 2026)**  
> **Problem Statement Owner:** Government of Jharkhand — Department of Higher & Technical Education  
> **Project Name:** Hi-AR (Subterranean AR Vocational Safety Simulator)  
> **Branch:** `kavin` (Production Master Branch)

---

## 📌 Executive Summary

**Hi-AR** is an augmented reality (AR) subterranean mining safety training and audit compliance platform built using **Unity 6 LTS**, **Unity Sentis ONNX AI**, and **UI Toolkit**. 

Traditional industrial safety training relies on printed manuals or static videos that fail to build muscle memory under stress. Furthermore, paper-based sign-offs offer zero verification of who completed the training or where it occurred. 

**Hi-AR solves this through:**
1. **Immersive AR Hazard Simulations:** Hands-on 3D emergency response drills (Fire Suppression & Gas Leak/Confined Space Evacuation).
2. **Scenario-Based Hazard Training (Novelty):** Dynamic scenario variants randomize hazard locations and fire intensity so workers learn to assess situations rather than memorize scripts.
3. **AI On-Device Face Verification:** On-device neural network scanning via **Unity Sentis (BlazeFace + MobileFaceNet)** prevents proxy training.
4. **GPS + Timestamp Anti-Spoof Audit Logging:** Real-time location and timestamp logging guarantees that training took place on site.
5. **Multilingual Vernacular Support:** Voice-assisted guidance in **Hindi, Santali, and English** for low-literacy miners.
6. **Instant QR Certification & Verification:** Generates tamper-proof QR digital certificates with an on-device verifier scanner (`UI_QRVerify`).

---

## 📊 System Implementation & Development Status

To provide full technical transparency for SIH evaluations, our system architecture is categorized into three stages:

### 🟢 1. Fully Implemented (Production Ready & Testable in Demo)
* **Fire & Explosion Response Module:** AR surface plane detection, 3D fire extinguisher interaction using the **P.A.S.S. technique** (Pull, Aim, Squeeze, Sweep), spatial hazard searching, and safe-zone evacuation.
* **Gas Leak & Confined Space Response Module:** 3D underground mine environment with dark tunnels, heavy machinery, Multi-Gas Detector UI, dynamic H2S/Methane audio alarm scaling, gas mask PPE selection, and evacuation routing.
* **Telemetry & Assessment Engine:** Real-time scoring based on practical actions combined with a post-drill theory quiz.
* **Digital QR Certification & Verification:** Dynamic QR certificate generation with an in-app verification scanner (`UI_QRVerify`).
* **100% Offline Persistence:** Local storage engine (`AppDataService` JSON & PlayerPrefs) operating without internet.
* **Multilingual Audio Engine:** Full voice-guided instructions in **English, Hindi, and Santali**.

### 🟡 2. Working Prototype / Partial Implementation
* **AI On-Device Face Verification:** Live front-camera scanning pipeline powered by Unity Sentis ONNX models (`BlazeFace` detector + `MobileFaceNet` embedding model).
* **Local Cloud Sync Manager:** Queues offline training attempts and auto-syncs to cloud endpoints when network connectivity is restored.
* **Worker & Supervisor Dashboard:** UI Toolkit interfaces (`UI_Dashboard`, `UI_Progress`) tracking scores, certifications, and compliance logs.

### 🔵 3. Post-Selection Roadmap
* **Offline Peer-to-Peer (P2P) Mesh Relay:** Device-to-device wireless sync (Wi-Fi Direct/Bluetooth) for off-grid Vocational Training Centers (VTCs) with zero cellular coverage.
* **AI-Driven Adaptive Learning at Scale:** Dynamic difficulty scaling based on historical worker mistake telemetry tags.
* **LLM-Powered Post-Drill Safety Insights:** Generative AI safety feedback summaries after completing drills.
* **Enterprise Multi-Site Fleet Deployment:** Organization-wide management across multiple mining sites.

---

## 🌟 Key Technical Features

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                   HI-AR PLATFORM CORE                                  │
├──────────────────────────┬──────────────────────────┬──────────────────────────────────┤
│   AR HAZARD SIMULATION   │   AI IDENTITY & GPS LOG  │  MULTILINGUAL AUDIT CERTIFICATE  │
├──────────────────────────┼──────────────────────────┼──────────────────────────────────┤
│ • AR Surface Plane Check │ • On-Device Sentis ONNX  │ • Vernacular Audio (Hindi/Santali)│
│ • P.A.S.S. Extinguisher  │ • GPS + Timestamp Logger │ • Telemetry + Quiz Score Engine  │
│ • 3D Underground Mine    │ • Anti-Proxy Prevention  │ • Tamper-Proof QR Certificate    │
│ • Dynamic Gas Audio Alarm│ • Zero-Latency Scan UI   │ • On-Device QR Scanner Verifier  │
└──────────────────────────┴──────────────────────────┴──────────────────────────────────┘
```

---

## 📱 Navigation & App Architecture Flow

```
UI_Splash ──► UI_Login / UI_Register ──► UI_Dashboard
                                              │
         ┌───────────────────┬────────────────┼───────────────────┐
         ▼                   ▼                ▼                   ▼
UI_TrainingCatalogue   UI_Progress      UI_Settings       UI_ModuleDetail
                                                                   │
                                                                   ▼ (Face Scanner Overlay)
                                                          UI_LocationCapture (GPS Log)
                                                                   │
                                                                   ▼
                                                       AR Simulation Scene
                                            (ar_fire_safety / gas_module)
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

## 🚀 Getting Started & Setup Guide

### 1. Prerequisites
Ensure you have the following installed:
* [Unity Hub](https://unity.com/download)
* **Unity 6000.3.23f1 LTS** with the following build modules installed:
  - **Android Build Support**
  - **OpenJDK**
  - **Android SDK & NDK Tools**
* Developer Mode + USB Debugging enabled on your physical Android test device (Android 10.0 / API 29+).

### 2. Clone the Repository
```bash
git clone https://github.com/kavin200506/MiningSafetyAR.git
cd MiningSafetyAR
git checkout kavin
```

### 3. Open Project & Switch Platform to Android
1. Open **Unity Hub**, click **Add**, and select the cloned `MiningSafetyAR` project folder.
2. Select Unity Version **6000.3.23f1 LTS**.
3. Go to **File ➔ Build Profiles** (or *Build Settings*).
4. Select **Android** under Platforms and click **Switch Platform**.

### 4. Build & Run on Android Device
Connect your Android phone via USB and click **Build and Run** in Unity.

---

## 📂 Project Structure

```
MiningSafetyAR/
├── Assets/
│   ├── ImageTracking/             # AR Reference Image Libraries & Posters
│   ├── ONNX/                      # Sentis Models (BlazeFace & MobileFaceNet)
│   ├── Prefabs/                   # 3D Mine Models, Gas Detector & Extinguisher
│   ├── Resources/
│   │   ├── Data/                  # Module, Question & Certificate Databases
│   │   └── UI/Templates/Pages/    # FaceScannerOverlay.uxml & UI Templates
│   ├── Scenes/                    # 24 Production Scenes (UI & AR Modules)
│   │   ├── UI_Splash.unity
│   │   ├── UI_Login.unity
│   │   ├── UI_Dashboard.unity
│   │   ├── ar_fire_safety.unity
│   │   └── gas_module.unity
│   ├── Scripts/
│   │   ├── AR/                    # ARGuidanceController & Plane Placement
│   │   ├── Data/                  # AppDataService & FaceVerificationService
│   │   ├── Modules/               # MultiGasDetectorController & GasLeakModuleManager
│   │   └── UI/                    # NavigationManager & Page Controllers
│   └── UI/                        # USS Stylesheets (Theme, Reset, FaceScannerOverlay)
├── ProjectSettings/               # Unity Player & 24 Enabled Scene Build Settings
└── README.md
```

---

## 🏆 Team & Acknowledgments

Developed for **Smart India Hackathon (SIH 2026)** — **Government of Jharkhand (Department of Higher & Technical Education)**.
