# MiningSafetyAR - Complete UI Implementation Master Index

> **Project:** ARC MineSafetyAr — AR-Based Industrial Safety Training
> **Context:** Smart India Hackathon (SIH26041) — Government of Jharkhand
> **Engine:** Unity 6000.3.23f1 LTS
> **UI Framework:** UI Toolkit (UXML + USS)
> **Backend:** Firebase (Auth, Firestore, Storage)
> **Target:** Android (API 29+), ARM64

---

## Document Index

| # | Document | Description |
|---|---|---|
| 01 | [Architecture Overview](01_Architecture_Overview.md) | High-level system design, tech stack, folder structure |
| 02 | [Scene Organization](02_Scene_Organization.md) | One scene per page, loading strategy, navigation flow |
| 03 | [Theme & Styling System](03_Theme_Styling_System.md) | USS variables, **light theme**, typography, color tokens |
| 04 | [Reusable Components](04_Reusable_Components.md) | BottomNav, Header, ModuleCard, ProgressBar, ScoreBar, etc. |
| 05 | [Data Layer](05_Data_Layer.md) | Firebase Auth, Firestore models, AppDataService |
| 06 | [Navigation System](06_Navigation_System.md) | NavigationManager, page transitions, back stack |
| 07 | [Animation System](07_Animation_System.md) | USS transitions, C# coroutines, page animations |
| 08 | [Page Implementation - Auth Flow](08_Pages_Auth_Flow.md) | SplashPage, LoginPage (Firebase), RegisterPage (Firebase) |
| 09 | [Page Implementation - Main Flow](09_Pages_Main_Flow.md) | Dashboard, TrainingCatalogue, ModuleDetail, Progress, Settings |
| 10 | [Page Implementation - AR Flow](10_Pages_AR_Flow.md) | LearningContent, ARSimulation, Assessment |
| 11 | [Page Implementation - Results & Certs](11_Pages_Results_Certs.md) | Results, Certificate, QRVerify |
| 12 | [File Inventory](12_File_Inventory.md) | Complete list of all files to create |
| 13 | [Build Phases & Roadmap](13_Build_Phases_Roadmap.md) | Step-by-step implementation order |
| 14 | [Unity UIToolkit Reference](14_Unity_UIToolkit_Reference.md) | API reference, gotchas, workarounds |
| 15 | [Firebase Integration](15_Firebase_Integration.md) | Firebase setup, Auth, Firestore, Storage |

---

## Design Principles

1. **Light Theme** — White background (#FFFFFF), cards (#F5F5F5), orange accent (#FF6D00)
2. **Firebase-First** — Auth via Firebase Auth, data in Firestore, files in Storage
3. **One Scene Per Page** — Every page is its own scene for easy debugging
4. **React Native Parity** — Unity UI indistinguishable from the React reference
5. **Mobile-First** — Max width 430px, safe area handling, touch-friendly targets (44px min)
6. **Animation-Rich** — Smooth transitions, animated progress bars, press feedback

## Current Project State

### What Exists (Backend - Complete)
- AR placement system (plane detection, tap-to-place)
- Image tracking (fire extinguisher/exit sign markers)
- 2 training module managers (Fire Safety, Gas Leak)
- Assessment engine (MCQ quiz)
- Certificate generator (HMAC-signed)
- Local JSON persistence + Firestore cloud sync
- Trilingual localization (English/Hindi/Santali)

### What Needs Building (UI - Nothing Exists)
- All 14 UI pages (each in its own scene)
- Firebase Auth integration (login, register)
- Firestore data layer (worker profiles, scores, certificates)
- Navigation system
- Light theme styling
- Reusable components
- Animations

## Firebase Configuration Status

- [x] `google-services.json` — Present in `Assets/`
- [x] `minesafetyar-firebase-adminsdk-fbsvc-8714f6eb7f.json` — Present
- [ ] Firebase Unity SDK packages (Auth, Firestore, Storage) — **Need to import**
- [ ] Firebase project config verification
