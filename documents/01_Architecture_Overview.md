# 01 — Architecture Overview

## System Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    UNITY 6 (URP)                         │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐  │
│  │UI_Splash │ │UI_Login  │ │UI_Reg   │ │UI_Dash   │  │
│  │Scene     │ │Scene     │ │Scene    │ │Scene     │  │
│  └──────────┘ └──────────┘ └─────────┘ └──────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐  │
│  │UI_Train  │ │UI_Detail │ │UI_Learn │ │UI_ARSim  │  │
│  │Catalogue │ │Module    │ │Content  │ │Scene     │  │
│  │Scene     │ │Scene     │ │Scene    │ └──────────┘  │
│  └──────────┘ └──────────┘ └─────────┘ ┌──────────┐  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ │UI_Quiz   │  │
│  │UI_Results│ │UI_Cert   │ │UI_Progress│ │Scene     │  │
│  │Scene     │ │Scene     │ │Scene    │ └──────────┘  │
│  └──────────┘ └──────────┘ └─────────┘ ┌──────────┐  │
│  ┌──────────┐                           │UI_Settings│  │
│  │UI_QRVerify│                          │Scene     │  │
│  │Scene     │                           └──────────┘  │
│  └──────────┘                                          │
│         │ (each page = separate scene)                  │
├─────────┼───────────────────────────────────────────────┤
│                     CORE SERVICES                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │NavigationMgr │  │FirebaseAuthService│ │LanguageManager│  │
│  │SceneLoader   │  │FirestoreService   │ │CloudSyncManager│  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
├─────────────────────────────────────────────────────────┤
│                     FIREBASE                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │Firebase Auth │  │Cloud Firestore│  │Firebase      │  │
│  │(Login/Register)│ │(Worker Data, │  │Storage       │  │
│  │              │  │Scores, Certs)│  │(Files)       │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
├─────────────────────────────────────────────────────────┤
│                     AR LAYER                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ARPlacement   │  │ARImageTrack  │  │ModuleManagers│  │
│  │Manager       │  │Manager       │  │Fire/Gas      │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
└─────────────────────────────────────────────────────────┘
```

## Technology Stack

| Layer | Technology | Version |
|---|---|---|
| Engine | Unity | 6000.3.23f1 LTS |
| Render Pipeline | URP | 17.3.0 |
| UI Framework | UI Toolkit | Built-in (Unity 6) |
| AR Framework | AR Foundation | 6.3.5 |
| Auth | Firebase Auth | 12.x |
| Database | Cloud Firestore | (via REST API or SDK) |
| Storage | Firebase Storage | (via REST API or SDK) |
| Localization | LanguageManager (custom) | EN/HI/SAT |
| Platform | Android | API 29+ (ARM64) |

## Theme: Light Mode

| Element | Color | Usage |
|---|---|---|
| Background | `#FFFFFF` | Page background |
| Card Background | `#F5F5F5` | Card surfaces |
| Input Background | `#F0F0F0` | Text fields |
| Accent | `#FF6D00` | Primary actions, highlights |
| Accent Light | `#FF9E40` | Hover states |
| Accent Dark | `#E65100` | Pressed states |
| Success | `#4CAF50` | Pass, completed |
| Danger | `#F44336` | Fail, errors |
| Warning | `#FFC107` | Caution |
| Text Primary | `#1A1A1A` | Main text |
| Text Secondary | `#666666` | Subtitles, meta |
| Text Muted | `#999999` | Disabled, hints |
| Border | `#E0E0E0` | Card borders, dividers |

## Folder Structure

```
Assets/
├── UI/                                    # All UI Toolkit assets
│   ├── PanelSettings/
│   │   └── DefaultPanelSettings.asset
│   ├── Styles/                            # USS stylesheets
│   │   ├── Theme.uss                      # Light theme CSS variables
│   │   ├── Reset.uss
│   │   ├── Components.uss
│   │   ├── BottomNav.uss
│   │   ├── Header.uss
│   │   ├── Forms.uss
│   │   ├── Tables.uss
│   │   └── Animations.uss
│   ├── Templates/                         # UXML templates
│   │   ├── Components/                    # 12 reusable components
│   │   └── Pages/                         # 14 page templates
│   ├── Sprites/                           # Icons and images
│   │   ├── Icons/
│   │   ├── Badges/
│   │   └── Logo/
│   └── Fonts/                             # Inter font family
│
├── Scenes/                                # One scene per page
│   ├── UI_Splash.unity
│   ├── UI_Login.unity
│   ├── UI_Register.unity
│   ├── UI_Dashboard.unity
│   ├── UI_TrainingCatalogue.unity
│   ├── UI_ModuleDetail.unity
│   ├── UI_LearningContent.unity
│   ├── UI_ARSimulation.unity
│   ├── UI_Assessment.unity
│   ├── UI_Results.unity
│   ├── UI_Certificate.unity
│   ├── UI_Progress.unity
│   ├── UI_Settings.unity
│   ├── UI_QRVerify.unity
│   └── AR Scene (existing, unchanged)
│
├── Scripts/
│   ├── UI/                                # All UI controllers
│   │   ├── Navigation/
│   │   ├── Core/
│   │   ├── Helpers/
│   │   └── Pages/
│   ├── Data/                              # Data models
│   │   ├── AppDataService.cs              # Firebase-connected service
│   │   ├── ModuleData.cs
│   │   └── ...
│   ├── Firebase/                          # Firebase integration
│   │   ├── FirebaseAuthManager.cs
│   │   └── FirestoreService.cs
│   ├── AR/                                # Existing (unchanged)
│   ├── Modules/                           # Existing (unchanged)
│   └── ...                                # Existing (unchanged)
│
├── Resources/
│   ├── Fonts/
│   └── PanelSettings/
│
└── Plugins/
    └── Firebase/                          # Firebase SDK
        ├── Firebase Auth/
        ├── Firebase Firestore/
        └── Firebase Storage/
```
