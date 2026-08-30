# 02 — Scene Organization

## Architecture: One Scene Per Page

Every UI page lives in its own scene. This makes debugging trivial — open only the scene you're working on. No shared containers, no conditional logic within scenes.

## Scene List (14 UI Scenes + 1 AR Scene)

| # | Scene File | Page | Bottom Nav |
|---|---|---|---|
| 1 | `Assets/Scenes/UI_Splash.unity` | SplashPage | Hidden |
| 2 | `Assets/Scenes/UI_Login.unity` | LoginPage | Hidden |
| 3 | `Assets/Scenes/UI_Register.unity` | RegisterPage | Hidden |
| 4 | `Assets/Scenes/UI_Dashboard.unity` | DashboardPage | Visible |
| 5 | `Assets/Scenes/UI_TrainingCatalogue.unity` | TrainingCataloguePage | Visible |
| 6 | `Assets/Scenes/UI_ModuleDetail.unity` | ModuleDetailPage | Visible |
| 7 | `Assets/Scenes/UI_LearningContent.unity` | LearningContentPage | Hidden |
| 8 | `Assets/Scenes/UI_ARSimulation.unity` | ARSimulationPage | Hidden |
| 9 | `Assets/Scenes/UI_Assessment.unity` | AssessmentPage | Hidden |
| 10 | `Assets/Scenes/UI_Results.unity` | ResultsPage | Visible |
| 11 | `Assets/Scenes/UI_Certificate.unity` | CertificatePage | Visible |
| 12 | `Assets/Scenes/UI_Progress.unity` | ProgressPage | Visible |
| 13 | `Assets/Scenes/UI_Settings.unity` | SettingsPage | Visible |
| 14 | `Assets/Scenes/UI_QRVerify.unity` | QRVerifyPage | Visible |
| 15 | `Assets/Scenes/AR Plane Detection Placement.unity` | AR Scene | N/A |

## Scene Structure (Each Scene)

Every UI scene contains:
```
UI_PageName Scene:
├── EventSystem
├── UIDocument
│   ├── PanelSettings (assigned)
│   └── PageTemplate.uxml (assigned)
└── PageController (MonoBehaviour on a GameObject)
    └── e.g. LoginPageController.cs
```

## Scene Loading Strategy

### Initial Load
```
App Start
  └─ Load UI_Splash (Single mode — only scene)
       └─ Splash auto-advances
            └─ Load UI_Login (Single mode — replace)
                 └─ User logs in via Firebase Auth
                      └─ Load UI_Dashboard (Single mode — replace)
```

### Page Navigation
```
NavigateTo("training_catalogue")
  └─ Load UI_TrainingCatalogue (Single mode — replace current)

NavigateTo("module_detail", "fire_safety")
  └─ Load UI_ModuleDetail (Single mode — replace current)

NavigateTo("learning_content", "fire_safety")
  └─ Load UI_LearningContent (Single mode — replace current)
```

### Why Single Mode (Not Additive)?
- **Simpler** — No need to track multiple loaded scenes
- **Less memory** — Only one UI scene in memory at a time
- **Easier debugging** — Open any scene in editor, it's self-contained
- **Faster load** — UI scenes are tiny, instant load

### Exception: AR Flow
When entering AR simulation, load the real AR scene **additively** alongside the UI scene:
```
Load UI_ARSimulation (Single)
  └─ Load AR Plane Detection Placement (Additive)
       └─ AR scene renders in background behind UI overlay
  └─ On completion:
       └─ Unload AR Plane Detection Placement
       └─ Load UI_Results (Single)
```

## Navigation Flow

```
UI_Splash ──(auto)──> UI_Login ──(Firebase Auth)──> UI_Dashboard
                         │
                    UI_Register ──(Firebase Auth)──> UI_Dashboard

UI_Dashboard ──> UI_TrainingCatalogue ──> UI_ModuleDetail
                                             │
UI_Dashboard ──> UI_Progress                 ├──> UI_LearningContent
UI_Dashboard ──> UI_Settings                 │        │
                                             │   UI_ARSimulation
UI_ModuleDetail ──> UI_Certificate           │        │
UI_ModuleDetail ──> UI_Results               │   UI_Assessment
                     │                       │        │
                     ├──> UI_Certificate     │   UI_Results
                     └──> UI_QRVerify        │
                                             │
UI_Certificate ──> UI_QRVerify
```

## Back Navigation

| Current Page | Back Goes To |
|---|---|
| UI_Splash | None (auto-advance) |
| UI_Login | None (exit app) |
| UI_Register | UI_Login |
| UI_Dashboard | None (stay) |
| UI_TrainingCatalogue | UI_Dashboard |
| UI_ModuleDetail | UI_TrainingCatalogue |
| UI_LearningContent | UI_ModuleDetail |
| UI_ARSimulation | UI_LearningContent |
| UI_Assessment | UI_ModuleDetail |
| UI_Results | UI_ModuleDetail |
| UI_Certificate | Previous (Results or ModuleDetail) |
| UI_Progress | UI_Dashboard |
| UI_Settings | UI_Dashboard |
| UI_QRVerify | Previous (Certificate or Dashboard) |

## Bottom Nav Visibility

| Scene | Bottom Nav |
|---|---|
| UI_Splash | Hidden |
| UI_Login | Hidden |
| UI_Register | Hidden |
| UI_Dashboard | Visible (Home active) |
| UI_TrainingCatalogue | Visible (Training active) |
| UI_ModuleDetail | Visible (Training active) |
| UI_LearningContent | Hidden |
| UI_ARSimulation | Hidden |
| UI_Assessment | Hidden |
| UI_Results | Visible (none active) |
| UI_Certificate | Visible (none active) |
| UI_Progress | Visible (Progress active) |
| UI_Settings | Visible (Settings active) |
| UI_QRVerify | Visible (none active) |

## Build Settings

All 14 UI scenes must be added to Build Settings. The AR scene is already in the build.

```
Build Scenes:
1. UI_Splash (enabled)
2. UI_Login (enabled)
3. UI_Register (enabled)
4. UI_Dashboard (enabled)
5. UI_TrainingCatalogue (enabled)
6. UI_ModuleDetail (enabled)
7. UI_LearningContent (enabled)
8. UI_ARSimulation (enabled)
9. UI_Assessment (enabled)
10. UI_Results (enabled)
11. UI_Certificate (enabled)
12. UI_Progress (enabled)
13. UI_Settings (enabled)
14. UI_QRVerify (enabled)
15. AR Plane Detection Placement (enabled, existing)
```
