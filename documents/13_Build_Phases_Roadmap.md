# 13 — Build Phases & Implementation Roadmap

## Phase Overview

| Phase | Name | Depends On | Effort |
|---|---|---|---|
| 0 | Firebase Setup | Nothing | Low |
| 1 | Foundation | Phase 0 | Medium |
| 2 | Reusable Components | Phase 1 | Medium |
| 3 | Auth Flow (3 pages) | Phase 0+1+2 | Low |
| 4 | Main Flow (5 pages) | Phase 2 | High |
| 5 | AR Flow (3 pages) | Phase 2 | High |
| 6 | Results & Certs (3 pages) | Phase 2 | Medium |
| 7 | Polish & Animations | Phase 3-6 | Medium |

---

## Phase 0: Firebase Setup

**Goal:** Firebase SDK imported and working.

### Tasks
1. Download Firebase SDK from [firebase.google.com/download/unity](https://firebase.google.com/download/unity)
2. Import packages in Unity: `FirebaseAuth`, `FirebaseFirestore`
3. Verify `google-services.json` is in `Assets/` (already present)
4. Create `FirebaseAuthManager.cs` and `FirestoreService.cs`
5. Create a persistent `AppManagers` GameObject with Firebase managers
6. Test: `FirebaseApp.DefaultInstance` initializes without errors
7. Test: Can create a test user with `CreateUserWithEmailAndPasswordAsync`

### Verification
- Firebase initializes on app start
- Can register a test user
- Can login with test user
- Firestore write/read works

---

## Phase 1: Foundation

**Goal:** Core infrastructure — navigation, theming, data service.

### Tasks
1. Create folder structure (`Assets/UI/`, `Assets/Scripts/UI/`, etc.)
2. Write `Theme.uss` — **light theme** (white bg, orange accent)
3. Write `Reset.uss`
4. Create `PanelSettings.asset`
5. Write `PageController.cs` (base class)
6. Write `NavigationManager.cs` (scene-per-page navigation)
7. Write `BottomNavController.cs`
8. Write `HeaderController.cs`
9. Write `PageTransitionAnimator.cs`
10. Write `AppDataService.cs` — connected to Firebase
11. Write data models: `ModuleData.cs`, `WorkerData.cs`, `CertificateData.cs`
12. Create `ModuleDatabase.asset` with 5 modules
13. Create `QuestionDatabase.asset` with 22 questions
14. Create `CertificateDatabase.asset` with 2 certificates

### Verification
- All scripts compile
- Theme.uss loads correctly
- NavigationManager can load scenes
- AppDataService connects to Firebase

---

## Phase 2: Reusable Components

**Goal:** All shared UI components.

### Tasks
1. Write `Components.uss` — light theme button/card/badge styles
2. Write `BottomNav.uss` — white bg bottom nav
3. Write `Header.uss` — white bg header
4. Write `Forms.uss` — light inputs
5. Write `Tables.uss`
6. Write `Animations.uss`
7. Create all 12 component UXML templates
8. Write all helper C# files

### Verification
- Components render with light theme
- Toggle switch animates
- ModuleCard displays correctly

---

## Phase 3: Auth Flow (Firebase)

**Goal:** Splash → Login (Firebase) → Register (Firebase)

### Tasks
1. Create `UI_Splash.unity` scene
2. Write `SplashPage.uxml` + `SplashPageController.cs`
3. Create `UI_Login.unity` scene
4. Write `LoginPage.uxml` + `LoginPageController.cs` — **Firebase Auth**
5. Create `UI_Register.unity` scene
6. Write `RegisterPage.uxml` + `RegisterPageController.cs` — **Firebase Auth**
7. Add 3 scenes to Build Settings
8. Test: Splash auto-advances to Login
9. Test: Login with Firebase Auth (email = workerId@miningsafety.app)
10. Test: Register creates Firebase user + Firestore profile
11. Test: Demo Mode creates test user
12. Test: After login → navigates to Dashboard

### Verification
- Firebase Auth login works
- Firebase Auth register works
- Firestore worker profile created on register
- Demo mode works
- Error messages display on auth failure

---

## Phase 4: Main Flow (5 Pages)

**Goal:** Dashboard, Training, ModuleDetail, Progress, Settings

### Tasks
1. Create `UI_Dashboard.unity` — with BottomNav
2. Write `DashboardPage.uxml` + `DashboardPageController.cs`
3. Create `UI_TrainingCatalogue.unity` — with BottomNav
4. Write `TrainingCataloguePage.uxml` + controller
5. Create `UI_ModuleDetail.unity` — with BottomNav
6. Write `ModuleDetailPage.uxml` + controller
7. Create `UI_Progress.unity` — with BottomNav
8. Write `ProgressPage.uxml` + controller
9. Create `UI_Settings.unity` — with BottomNav
10. Write `SettingsPage.uxml` + controller
11. Test: Dashboard loads worker data from Firestore
12. Test: Training Catalogue filters work
13. Test: Module Detail shows correct data
14. Test: Settings logout calls Firebase signOut

### Verification
- Dashboard shows Firestore data
- All navigation works
- Bottom nav tabs work
- Settings logout returns to Login

---

## Phase 5: AR Flow (3 Pages)

**Goal:** Learning, AR Simulation, Quiz

### Tasks
1. Create `UI_LearningContent.unity`
2. Write `LearningContentPage.uxml` + controller
3. Create `UI_ARSimulation.unity`
4. Write `ARSimulationPage.uxml` + controller
5. Create `UI_Assessment.unity`
6. Write `AssessmentPage.uxml` + controller
7. Test: Learning slides navigate correctly
8. Test: AR simulation auto-advances phases
9. Test: Quiz works with questions from QuestionDatabase

### Verification
- Learning → AR → Quiz flow works
- Quiz saves result to Firestore via AppDataService
- Back navigation works on all pages

---

## Phase 6: Results & Certificates (3 Pages)

**Goal:** Results, Certificate, QR Verify

### Tasks
1. Create `UI_Results.unity`
2. Write `ResultsPage.uxml` + controller
3. Create `UI_Certificate.unity`
4. Write `CertificatePage.uxml` + controller
5. Create `UI_QRVerify.unity`
6. Write `QRVerifyPage.uxml` + controller
7. Test: Results show correct scores
8. Test: Certificate displays worker data
9. Test: QR Verify finds certificates

### Verification
- Results page shows pass/fail correctly
- Certificate generates with Firebase user data
- QR Verify works with Firestore lookup

---

## Phase 7: Polish & Animations

**Goal:** React Native-level polish.

### Tasks
1. Page transition animations
2. Progress bar fill animations
3. Card press feedback
4. Toggle switch smooth transitions
5. AR scan line animation
6. Timer countdown with color changes
7. Quiz option feedback animations
8. Test on Android device
9. Tune safe area for notched phones

### Verification
- All animations smooth at 60fps
- Safe area handled correctly
- Touch targets are 44px minimum
- Light theme consistent across all pages
