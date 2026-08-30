# Complete Navigation Flow

## User Journey

```
┌──────────────┐
│  SplashPage  │  (auto-advance after ~1.5s)
│  /splash     │
└──────┬───────┘
       │
       v
┌──────────────┐
│  LoginPage   │
│  /login      │
└──┬───┬───┬───┘
   │   │   │
   │   │   └─── "Create New Account" ──→ ┌──────────────┐
   │   │                                  │ RegisterPage │
   │   │                                  │ /register    │
   │   │                                  └──────┬───────┘
   │   │                                         │
   │   └───── "Login" / "Demo Mode" ────────────┘
   │
   v
┌──────────────────┐
│  DashboardPage   │
│  /dashboard      │
└──┬───────┬───┬───┘
   │       │   │
   │       │   └─── "View All" ──→ ┌──────────────┐
   │       │                       │ ProgressPage  │
   │       │                       │ /progress     │
   │       │                       └───────────────┘
   │       │
   │       └─── Module card click ──→ ┌───────────────────┐
   │                                  │ ModuleDetailPage   │
   │                                  │ /training/:id      │
   │                                  └──┬──────┬──────┬───┘
   │                                     │      │      │
   │        "See All" ──→ ┌─────────────────┐  │      │
   │                      │ TrainingCatalogue│  │      │
   │                      │ /training        │  │      │
   │                      └──────┬───────────┘  │      │
   │                             │              │      │
   │              ModuleCard click│              │      │
   │                             └──────────────┘      │
   │                                                   │
   │  "START/CONTINUE/RETAKE" ──→ ┌─────────────────────┐
   │                              │ LearningContentPage  │
   │                              │ /training/:id/learn  │
   │                              └──────────┬──────────┘
   │                                         │
   │                              "I'm Ready" (last slide)
   │                                         │
   │                                         v
   │                              ┌─────────────────────┐
   │                              │ ARSimulationPage     │
   │                              │ /training/:id/ar     │
   │                              └──────────┬──────────┘
   │                                         │
   │                              (after phase 7)
   │                                         │
   │                                         v
   │                              ┌─────────────────────┐
   │                              │ AssessmentPage       │
   │                              │ /training/:id/quiz   │
   │                              └──────────┬──────────┘
   │                                         │
   │                              "View Results" (last Q)
   │                                         │
   │                                         v
   │                              ┌─────────────────────┐
   │                              │ ResultsPage          │
   │                              │ /training/:id/results│
   │                              └──┬──────────────┬───┘
   │                                 │              │
   │  "VIEW CERTIFICATE" ───────────┘              │
   │  (if passed)                                   │
   │                                                │
   │  "BACK TO MODULE" / "TRY AGAIN" ──→ ModuleDetailPage
   │  "BACK TO DASHBOARD" ──→ DashboardPage
   │
   │  "VIEW CERTIFICATE" ──→ ┌───────────────────┐
   │                         │ CertificatePage    │
   │                         │ /certificate/:id   │
   │                         └───────┬───────────┘
   │                                 │
   │  "Verify Certificate" ──────────┘
   │                                 │
   │                                 v
   │                         ┌───────────────────┐
   │                         │ QRVerifyPage       │
   │                         │ /verify            │
   │                         └───────────────────┘
   │
   └─── BottomNav tabs ──→ /dashboard, /training, /progress, /settings
```

## Bottom Navigation Visibility

| Page | BottomNav Visible |
|---|---|
| SplashPage | Hidden |
| LoginPage | Hidden |
| RegisterPage | Hidden |
| DashboardPage | Visible |
| TrainingCataloguePage | Visible |
| ModuleDetailPage | Visible |
| LearningContentPage | Hidden |
| ARSimulationPage | Hidden |
| AssessmentPage | Hidden |
| ResultsPage | Visible |
| CertificatePage | Visible |
| ProgressPage | Visible |
| SettingsPage | Visible |
| QRVerifyPage | Visible |

## Route Summary

| Route | Page | Params |
|---|---|---|
| `/splash` | SplashPage | - |
| `/login` | LoginPage | - |
| `/register` | RegisterPage | - |
| `/dashboard` | DashboardPage | - |
| `/training` | TrainingCataloguePage | - |
| `/training/:id` | ModuleDetailPage | `id` |
| `/training/:id/learn` | LearningContentPage | `id` |
| `/training/:id/ar` | ARSimulationPage | `id` |
| `/training/:id/quiz` | AssessmentPage | `id` |
| `/training/:id/results` | ResultsPage | `id` |
| `/certificate/:id` | CertificatePage | `id` |
| `/progress` | ProgressPage | - |
| `/settings` | SettingsPage | - |
| `/verify` | QRVerifyPage | - |
| `*` | Redirect to `/splash` | - |
