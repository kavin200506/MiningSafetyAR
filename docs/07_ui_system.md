# 07 — UI System

## Architecture

The app uses Unity **UI Toolkit** (UIElements) for all UI. Each screen is a **Unity scene** containing a `UIDocument` component loaded from a `.uxml` template. All scenes use `LoadSceneMode.Single` navigation (one scene loaded at a time), except the AR simulation scene which is loaded **additively**.

There is no Unity uGUI (`Canvas`, `Image`, `Text`); all UI elements are `VisualElement`, `Label`, `Button`, `TextField`, `DropdownField` instances from UI Toolkit.

---

## Navigation System

**File:** `Assets/Scripts/UI/Navigation/NavigationManager.cs`

`NavigationManager : MonoBehaviour` — Singleton, `DontDestroyOnLoad`.

### Navigation Model

- `Stack<string> backStack` — stores scene names for back navigation.
- `NavigateTo(sceneName, param, pushToStack)` — loads scene async, then calls `PageController.SetNavigationParameter(param)` + `PageController.OnPageEnter()`.
- `GoBack()` — pops the back stack and navigates without pushing.
- `NavigateToRoot(sceneName)` — clears stack, navigates.
- `NavigateToTab(sceneName)` — clears stack (for bottom nav tab switches).

### Bottom Nav Visibility

Bottom navigation is **automatically shown or hidden** based on scene name:

```csharp
// Visible:
"UI_Dashboard", "UI_TrainingCatalogue", "UI_ModuleDetail",
"UI_Progress", "UI_Settings"

// Hidden: UI_Login, UI_Register, UI_Assessment, UI_Certificate,
// UI_QRVerify, UI_Results, UI_Splash, AR scenes
```

Tab index is set automatically:
```
UI_Dashboard        → tab 0 (Home)
UI_TrainingCatalogue → tab 1 (Training)
UI_ModuleDetail     → tab 1 (Training)
UI_Progress         → tab 2 (Progress)
UI_Settings         → tab 3 (Settings)
```

---

## Bootstrap System

**File:** `Assets/Scripts/UI/Core/UIBootstrap.cs`

Uses `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` to auto-spawn:
- `NavigationManager` GameObject
- `AppDataService` GameObject (with databases auto-assigned in Editor via reflection)

---

## Page Controller Base Class

**File:** `Assets/Scripts/UI/Core/PageController.cs` (inferred from usage patterns)

`PageController : MonoBehaviour`

All page controllers inherit from this base:

```csharp
protected VisualElement root;  // Bound to UIDocument.rootVisualElement
public bool HasEntered { get; private set; }

protected virtual void Awake() { /* binds UIDocument */ }
protected abstract void BindUI();                  // Query and register UI elements
public virtual void SetNavigationParameter(object param) { }
public virtual void OnPageEnter() { }             // Called after scene load
public virtual void OnPageExit() { }
public void MarkEntered() { HasEntered = true; }
```

---

## Scene-to-Controller Map

| Scene | Controller | UXML Template |
|---|---|---|
| `UI_Splash` | `SplashPageController` | `SplashPage.uxml` |
| `UI_Login` | `LoginPageController` | `LoginPage.uxml` |
| `UI_Register` | `RegisterPageController` | `RegisterPage.uxml` |
| `UI_Dashboard` | `DashboardPageController` | `DashboardPage.uxml` |
| `UI_TrainingCatalogue` | `TrainingCataloguePageController` | `TrainingCataloguePage.uxml` |
| `UI_ModuleDetail` | `ModuleDetailPageController` | `ModuleDetailPage.uxml` |
| `UI_LearningContent` | `LearningContentPageController` | `LearningContentPage.uxml` |
| `UI_LocationCapture` | `LocationCapturePageController` | `UI_LocationCapture.uxml` |
| `UI_ARSimulation` | `ARSimulationPageController` | `ARSimulationPage.uxml` |
| `UI_Assessment` | `AssessmentPageController` | `AssessmentPage.uxml` |
| `UI_Results` | `ResultsPageController` | `ResultsPage.uxml` |
| `UI_Certificate` | `CertificatePageController` | `CertificatePage.uxml` |
| `UI_QRVerify` | `QRVerifyPageController` | `QRVerifyPage.uxml` |
| `UI_Progress` | `ProgressPageController` | `ProgressPage.uxml` |
| `UI_Settings` | `SettingsPageController` | `SettingsPage.uxml` |

---

## Reusable UXML Components

All located in `Assets/UI/Templates/Components/`:

| Component UXML | Purpose |
|---|---|
| `ModuleCard.uxml` | Training module card with emoji, title, progress bar, status badge |
| `ScoreBar.uxml` | Horizontal competency score bar (label + fill bar + percentage) |
| `ProgressBar.uxml` | Generic progress bar element |
| `BottomNav.uxml` | 4-tab bottom navigation bar |
| `Header.uxml` | Page header with title and optional back button |
| `FeedbackBanner.uxml` | Correct/incorrect feedback banner |
| `MetaPill.uxml` | Metadata pill (duration, difficulty) |
| `OptionButton.uxml` | Quiz answer option button (letter badge + text) |
| `PhaseDot.uxml` | Step progress dot indicator |
| `StatusBadge.uxml` | Status badge (Completed, In Progress, etc.) |
| `TableRow.uxml` | History table row |
| `ToggleSwitch.uxml` | Settings toggle switch |

---

## USS Stylesheets

All in `Assets/UI/Styles/`:

| File | Contents |
|---|---|
| `Theme.uss` | CSS custom properties (colors, spacing, typography scale) |
| `Components.uss` | Button styles, card styles, badge styles, filter pills |
| `Forms.uss` | TextField, DropdownField, label styles |
| `BottomNav.uss` | Bottom nav tab styles |
| `Header.uss` | Page header styles |
| `Icons.uss` | Emoji/icon class definitions |
| `Tables.uss` | History table row styles |
| `Animations.uss` | CSS transition/animation definitions |
| `Reset.uss` | Unity UI Toolkit style normalization |

---

## Key UI Helper Classes

All in `Assets/Scripts/UI/Helpers/`:

| Class | Purpose |
|---|---|
| `ModuleCardFactory` | Creates `ModuleCard.uxml` instances from `ModuleData`; assigns click callback |
| `ProgressBarHelper` | `SetProgress(VisualElement track, float percent)` — sets fill bar width |
| `ScoreBarHelper` | Fills competency score bars with animated width |
| `ToggleSwitchController` | Wraps a `VisualElement` toggle, manages on/off state |
| `TableRowFactory` | Creates table rows for history lists |
| `IconLoader` | Loads emoji/icon assets based on module type |

---

## Settings Page

**File:** `Assets/Scripts/UI/Pages/SettingsPageController.cs`

Controls `UI_Settings.unity` scene.

### Settings Available

| Setting | Backing Store | Default |
|---|---|---|
| Language | `PlayerPrefs.GetInt("SelectedLanguage", 0)` (Language enum int) | English |
| Sound enabled | `PlayerPrefs.GetInt("SoundEnabled", 1)` | On |
| Voice-over enabled | `PlayerPrefs.GetInt("VoiceEnabled", 1)` | On |
| Location consent | `PlayerPrefs.GetInt("LocationConsentGranted", 0)` | Off |

Language buttons (`lang-en`, `lang-hi`, `lang-sat`) visually highlight the active language with an orange background.

Logout: calls `AppDataService.Instance.Logout()` then navigates to `UI_Login`.

---

## Login Page

**File:** `Assets/Scripts/UI/Pages/LoginPageController.cs`

Key implementation details:
- Worker ID + PIN → converted to `email/password` format for Firebase Auth.
- PIN field is masked (`isPasswordField = true`); toggle shows/hides via `pin-toggle` button.
- `ForceTextFieldColors()` — extensively force-applies dark text color to combat Unity UI Toolkit's tendency to render invisible white text on white background.
- `PointerDownEvent.StopPropagation()` — prevents `ARPlacementManager` from stealing touch input.
- `Demo Mode` button calls `FirebaseAuthManager.Instance.DemoLogin()`.

---

## Registration Page

**File:** `Assets/Scripts/UI/Pages/RegisterPageController.cs`

Fields collected:
- Name, Worker ID, Organization, Phone
- Sector (DropdownField: Coal, Iron, Limestone, Manganese, Mica)
- PIN + Confirm PIN
- Language (buttons: English, Hindi, Santali)

On submit: validates fields, matches PINs, then calls `FirebaseAuthManager.Instance.Register(...)`.

---

## Splash Page

**File:** `Assets/Scripts/UI/Pages/SplashPageController.cs`

- Animates a progress bar from 0% to 100% over ~1.25 seconds.
- Checks `FirebaseAuthManager.Instance.IsLoggedIn`.
- If logged in → navigate to `UI_Dashboard`.
- If not → navigate to `UI_Login`.

---

## Dashboard Page

**File:** `Assets/Scripts/UI/Pages/DashboardPageController.cs`

Displays:
- Greeting (`Hello, {firstName}`).
- Overall progress bar (% from `WorkerData.overallProgress`).
- Stats: completed modules, certificates earned, total attempts.
- Up to 3 module cards (latest).
- "See All" → `UI_TrainingCatalogue`.
- "View Certificates" → `UI_Progress`.

Subscribes to `AppDataService.OnWorkerLoaded` to refresh when async Firestore load completes.

---

## Training Catalogue Page

**File:** `Assets/Scripts/UI/Pages/TrainingCataloguePageController.cs`

- Lists all modules as `ModuleCard` instances from `ModuleDatabase`.
- Filter buttons: All, Completed, In Progress, Not Started.
- Active filter highlighted via `filter-pill--active` USS class (orange background).
- Module count label updates on filter change.
- Click → navigate to `UI_ModuleDetail` with `moduleId` parameter.

---

## Module Detail Page

**File:** `Assets/Scripts/UI/Pages/ModuleDetailPageController.cs`

Displays full module info:
- Title, emoji, domain, duration, difficulty, description.
- Best score (if attempts > 0): progress bar + score bars per competency.
- Objectives list.
- Attempt history table.
- Action button: "Start Training" (first attempt) or "Retake" (subsequent attempts).
- Certificate button (shown only when module is Completed).

"Start Training" flow: `UI_ModuleDetail` → `UI_LearningContent` → `UI_LocationCapture` → `AR Plane Detection Placement` → `UI_Assessment` → `UI_Results`.

---

## Progress Page

**File:** `Assets/Scripts/UI/Pages/ProgressPageController.cs`

Displays:
- Overall progress percentage + bar.
- Completed count, total attempts, certificates earned.
- Per-competency score bars.
- Per-module status rows.
- Training history table (date, module, score, pass/fail).

---

## Location Capture Page

**File:** `Assets/Scripts/UI/Pages/LocationCapturePageController.cs`

Controls `UI_LocationCapture.unity` scene:
- Shows a 5-second animated GPS acquisition sequence.
- Calls `TrainingLocationCapture.Instance.CaptureLocation(...)`.
- Displays reverse-geocoded address or fallback "Mining Safety Sector (Default)".
- After 5 seconds, automatically navigates to `AR Plane Detection Placement` scene.
