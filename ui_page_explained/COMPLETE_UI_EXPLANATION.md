# ARC MineSafetyAr - Complete UI Explanation

> This document explains every page, component, data structure, navigation flow, and feature in the React demo UI. This is a reference for rebuilding the entire application in Unity.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Technology & Styling](#2-technology--styling)
3. [Navigation Flow](#3-navigation-flow)
4. [All Pages (14)](#4-all-pages-14)
5. [All Reusable Components (5)](#5-all-reusable-components-5)
6. [Data Layer (4 files)](#6-data-layer-4-files)
7. [UI Elements Catalog](#7-ui-elements-catalog)
8. [Features Summary](#8-features-summary)
9. [What is NOT Implemented (Demo Only)](#9-what-is-not-implemented-demo-only)

---

## 1. Project Overview

**App Name:** ARC MineSafetyAr
**Purpose:** AR-Based Industrial Safety Training platform for mine/industrial workers
**Context:** Smart India Hackathon (SIH26041) - Government of Jharkhand
**Target Users:** Industrial workers (mining, steel, construction)
**Domain:** Safety training with AR simulation, quizzes, and certificates

**Theme:** Dark mode only
- Background: `#121212`
- Cards: `#262626`
- Accent: Orange `#FF6D00`
- Success: Green `#4CAF50`
- Danger: Red `#F44336`
- Text: White `#FFFFFF`, Secondary `#B0B0B0`

**Layout:** Mobile-first, max-width 430px, centered like a phone frame on desktop

---

## 2. Technology & Styling

| Aspect | Detail |
|---|---|
| Framework | React (Vite, functional components) |
| Routing | react-router-dom v6 |
| State | Local useState only, no global state |
| Styling | Inline JS style objects (no CSS modules) |
| Data | Static JS files, no API calls |
| Icons | Inline SVGs + emoji characters |
| Font | Inter, system fonts |

### CSS Variables (global.css)

```
--bg-primary: #121212
--bg-secondary: #1E1E1E
--bg-card: #262626
--bg-input: #2A2A2A
--accent: #FF6D00
--accent-light: #FF9E40
--accent-dark: #E65100
--text-primary: #FFFFFF
--text-secondary: #B0B0B0
--text-muted: #707070
--success: #4CAF50
--danger: #F44336
--warning: #FFC107
--border: #333333
```

---

## 3. Navigation Flow

```
SplashPage (auto-advances ~1.5s)
    |
    v
LoginPage
    |-- Login / Demo Mode -----> Dashboard
    |-- Create New Account ----> RegisterPage --(register)--> Dashboard
    |
Dashboard
    |-- "See All" (training) --> TrainingCataloguePage
    |-- Module card click -----> ModuleDetailPage
    |-- "View All" (certs) ----> ProgressPage
    |
TrainingCataloguePage
    |-- ModuleCard click ------> ModuleDetailPage
    |-- Filter buttons (All, Completed, In Progress, Not Started)
    |
ModuleDetailPage
    |-- "START/CONTINUE/RETAKE TRAINING" --> LearningContentPage
    |-- "VIEW CERTIFICATE" --> CertificatePage
    |-- "Locked" message (no navigation)
    |
LearningContentPage (slide-based, Prev/Next)
    |-- "I'm Ready" (last slide) --> ARSimulationPage
    |
ARSimulationPage (auto-advancing 8 phases)
    |-- (after phase 7) --> AssessmentPage
    |
AssessmentPage (MCQ quiz)
    |-- "View Results" (last question) --> ResultsPage
    |
ResultsPage
    |-- "VIEW CERTIFICATE" (if passed) --> CertificatePage
    |-- "BACK TO MODULE" / "TRY AGAIN" --> ModuleDetailPage
    |-- "BACK TO DASHBOARD" --> Dashboard
    |
CertificatePage
    |-- "Verify Certificate" --> QRVerifyPage
    |-- "Download Certificate" (no-op)
    |-- "Share Certificate" (no-op)
    |
QRVerifyPage
    |-- Quick-fill buttons for known IDs
    |-- "VIEW CERTIFICATE" (if found) --> CertificatePage
```

### Bottom Navigation (4 tabs)

Visible on: Dashboard, TrainingCatalogue, ModuleDetail, Progress, Settings, Certificate, QRVerify, Results

Hidden on: Splash, Login, Register, LearningContent, ARSimulation, Assessment

| Tab | Route | Icon |
|---|---|---|
| Home | /dashboard | House SVG |
| Training | /training | Book SVG |
| Progress | /progress | Bar chart SVG |
| Settings | /settings | Gear SVG |

---

## 4. All Pages (14)

### 4.1 SplashPage (`/splash`)

**Purpose:** Loading screen with animated progress bar

**UI Elements:**
- Full-screen dark gradient background
- Shield emoji logo
- "ARC" title (large, bold)
- "MineSafetyAr" subtitle
- Animated orange gradient progress bar (fills 0-100%)
- "Loading..." text while filling, "Ready" when complete
- Footer text:
  - "Powered by Government of Jharkhand"
  - "Department of Higher & Technical Education"
  - "PS: SIH26041"

**Behavior:**
- Progress bar increments by 4 every 50ms
- After reaching 100%, auto-navigates to `/login` after 300ms

**State:** `progress` (number 0-100)

---

### 4.2 LoginPage (`/login`)

**Purpose:** Worker login form

**UI Elements:**
- Logo + "ARC" branding at top
- Worker ID text input (placeholder: "e.g. JH10293")
- PIN input with show/hide toggle (eye/lock emoji), maxLength 4
- "LOGIN" primary button (orange gradient)
- "Demo Mode (Skip Login)" outlined button
- "OR" divider
- "Create New Account" button
- Language indicator footer ("English")

**State:**
- `workerId` (string)
- `pin` (string)
- `showPin` (boolean)

**Behavior:**
- No validation. Any input navigates to `/dashboard`
- Demo Mode also navigates to `/dashboard`
- Create Account navigates to `/register`

---

### 4.3 RegisterPage (`/register`)

**Purpose:** New worker registration form

**UI Elements:**
- Custom header with back button
- Form fields:
  - Full Name (text input)
  - Worker ID (text input, placeholder: "e.g. JH10293")
  - Organization / Mine Name (text input)
  - Sector (dropdown: Mining, Steel Manufacturing, Mica Processing, Construction, Other)
  - Phone Number (tel input, maxLength 10)
  - Create PIN + Confirm PIN (side-by-side, password, maxLength 4)
  - Preferred Language (toggle buttons: English, Hindi, Santali)
- "REGISTER" button
- Footer: "Your data is stored locally on this device. No server required for demo."

**State:** `form` object with: name, workerId, organization, sector, phone, pin, confirmPin, language

**Behavior:**
- No validation. Register button navigates to `/dashboard`

---

### 4.4 DashboardPage (`/dashboard`)

**Purpose:** Main home screen with overview stats

**UI Elements:**
- Top bar: greeting "Hello, Ramesh" + motivational subtext + avatar circle (worker emoji)
- Overall Progress card:
  - Large "68%" display
  - ProgressBar component
  - 3 stats in a row: Completed (2), Certificates (2), Attempts (7)
- Training Modules section:
  - "See All" link to `/training`
  - First 3 modules as clickable cards with icon, title, duration/difficulty, status emoji
- Certificate Banner:
  - Trophy icon + "Your Certificates" + "2 certificates earned"
  - "View All" button navigates to `/progress`
- Bottom padding for safe scroll above BottomNav

**State:** None (all from static data)

---

### 4.5 TrainingCataloguePage (`/training`)

**Purpose:** Filterable list of all training modules

**UI Elements:**
- Header with title "Training Modules"
- Horizontal filter row with 4 pill buttons:
  - All
  - Completed
  - In Progress
  - Not Started
- Active filter highlighted in orange
- Module count display ("X modules")
- Filtered list of ModuleCard components
- Empty state: mailbox emoji + "No modules found"

**State:** `activeFilter` (string: 'all', 'completed', 'in_progress', 'not_started')

**Notes:**
- "locked" is NOT a filter option (locked modules only appear in "All")

---

### 4.6 ModuleDetailPage (`/training/:id`)

**Purpose:** Single module detail view

**UI Elements:**
- Header with back button and module title
- Hero section: large emoji icon + status badge
- Description paragraph
- Meta row: duration (stopwatch), difficulty (star), domain (target) -- each in a pill
- Best Score card (if not locked and bestScore > 0):
  - Progress bar
  - Attempt count
  - Last attempt date
- Learning Objectives section: numbered list
- Competency Scores section (if not locked and bestScore > 0):
  - ScoreBar for each competency (hazardRecognition, extinguisherUse/ppeSelection, evacuation, emergencyResponse)
- Attempt History table:
  - Columns: #, Date, Score, Status (Pass/Fail in green/red)
- Action section:
  - Unlocked: "START TRAINING" / "CONTINUE TRAINING" / "RETAKE TRAINING" button
  - Completed + has certificate: "VIEW CERTIFICATE" outlined button
  - Locked: lock icon + "Complete previous modules to unlock this training"

**State:** None (data from URL params + static data)

---

### 4.7 LearningContentPage (`/training/:id/learn`)

**Purpose:** Slide-based learning content before AR simulation

**UI Elements:**
- Custom header with back button, "Learn" title, slide counter ("1/3")
- Horizontal progress track showing slide completion
- Slide card:
  - Large emoji icon
  - Slide title
  - Bullet-point list of learning points
  - Tip box (lightbulb emoji + highlighted tip text in orange)
- Navigation bar:
  - "Previous" button (disabled on first slide)
  - "Next" button (orange) OR "I'm Ready" button (green gradient, on last slide)

**State:** `currentSlide` (number, starts at 0)

**Learning Content per Module:**

| Module | Slides | Topics |
|---|---|---|
| fire_safety | 3 | Fire Safety Basics, P.A.S.S. Technique, Evacuation Procedures |
| gas_safety | 3 | Gas Leak Hazards, PPE for Gas Hazards, Confined Space Protocol |
| machinery_safety | 3 | Lockout/Tagout, Machine Guarding, Safe Operation |
| electrical_safety | 2 | Electrical Hazards, Safe Work Practices |
| heights_safety | 2 | Fall Protection, Ladder & Scaffold Safety |

**Behavior:**
- "I'm Ready" on last slide navigates to AR simulation (`/training/:id/ar`)

---

### 4.8 ARSimulationPage (`/training/:id/ar`)

**Purpose:** Simulated AR training experience (no real camera/AR)

**UI Elements:**
- Score pill at top (starts at 50)
- Timer pill at top (starts from 120s when fire detected)
  - Green: >60s, Yellow: >30s, Red: <30s
- AR scene area with phase-specific content
- Phase indicator dots at bottom (7 dots, filled orange as phases advance)
- Instruction bar at bottom showing current instruction text

**8 Phases:**

| Phase | Instruction | Visual | Score |
|---|---|---|---|
| 0 | "Scanning environment..." | Scan line animation + grid dots | - |
| 1 | "Move phone slowly across a flat surface..." | Scan area | - |
| 2 | "Surface detected! Tap to place scenario" | Scan area | - |
| 3 | "Fire detected! Locate the extinguisher!" | Fire emoji + red dashed hazard zone circle | - |
| 4 | "Extinguisher located! Move to it and tap to pick up" | Extinguisher emoji + "TAP TO PICK UP" | 50 |
| 5 | "Tap to OPEN CAP" | "TAP TO OPEN CAP" label | 60 |
| 6 | "HOLD to SPRAY (6 seconds)..." | Spray progress bar | 70 |
| 7 | "Fire extinguished!" | Green checkmark + "Area Clear!" | 80 |

**State:**
- `phase` (0-7)
- `score` (starts 50)
- `timeLeft` (120 seconds)
- `instruction` (string)

**Behavior:**
- Auto-advancing phases with timers
- Timer starts counting down from 120s at phase 3
- After phase 7: auto-navigates to quiz after 2 seconds

---

### 4.9 AssessmentPage (`/training/:id/quiz`)

**Purpose:** Multiple-choice quiz interface

**UI Elements:**
- Header with back button, "Assessment" title, question counter ("1/5")
- Progress track showing question completion
- Question card:
  - "Question X" label in orange
  - Question text
  - 4 option buttons (A, B, C, D) with letter badges
  - Option color states:
    - Default: dark gray `#262626` with gray border
    - Selected (unanswered): orange tint
    - Correct (answered): green highlight
    - Incorrect selected (answered): red highlight
  - Feedback banner: green checkmark "Correct!" or red X "Incorrect"
- "Next Question" button (or "View Results" on last question)

**State:**
- `currentQ` (index)
- `selected` (option index or null)
- `answers` (array of answer objects)
- `answered` (boolean)

**Behavior:**
- On last question: calculates mcqScore = (correct/total)*100
- Navigates to results with state: mcqScore, correct, total, simulationScore (hardcoded 80), competencyScores

---

### 4.10 ResultsPage (`/training/:id/results`)

**Purpose:** Score breakdown and pass/fail result

**UI Elements:**
- Result header: trophy (passed) or red circle (failed), title, motivational subtitle
- Score card with 4 sections:
  - Simulation Score: XX/100
  - MCQ Score: XX/100
  - Questions Correct: X/Y
  - Final Score: large colored number
  - Pass/Fail badge
- Competency Breakdown section: ScoreBar components
- Attempt Info card: module name, attempt number, personal best
- Navigation buttons:
  - "VIEW CERTIFICATE" (green, only if passed AND has certificateId)
  - "BACK TO MODULE" / "TRY AGAIN" (orange)
  - "BACK TO DASHBOARD" (dark outlined)

**Score Calculation:**
- `finalScore` = simulationScore * 0.6 + mcqScore * 0.4
- `passed` = finalScore >= 60

**State:** None (data from location.state)

---

### 4.11 CertificatePage (`/certificate/:id`)

**Purpose:** Digital safety certificate display

**UI Elements:**
- Header with "Certificate" title and back button
- Certificate card with orange gradient header:
  - Shield emoji + "SAFETY CERTIFICATE" + "Government of Jharkhand"
- Certificate body:
  - "This certifies that" + worker name (large, bold)
  - "has successfully completed" + module title (orange)
  - Score display + "PASSED" status
  - QR code placeholder (white box with "QR Code" text and certificate ID)
  - "Scan to verify certificate" hint
  - Metadata: Certificate ID, Issued date, Expiry date, Organization
- Action buttons:
  - "Download Certificate" (orange, no-op)
  - "Share Certificate" (dark outlined, no-op)
  - "Verify Certificate" (orange outlined, navigates to `/verify`)

**State:** None (data from URL params + static data)

---

### 4.12 ProgressPage (`/progress`)

**Purpose:** Overall progress, competency scores, attempt history

**UI Elements:**
- Header with "Progress" title
- Overview card (orange gradient background):
  - Large "68%" overall progress
  - 3 stats: Completed (2), Total Attempts (7), Certificates (2)
- Competency Scores section: 4 ScoreBars
  - Hazard Recognition: 82
  - PPE Selection: 65
  - Evacuation: 78
  - Emergency Response: 71
- Module Progress section: all 5 modules as rows with icon, title, status info, status emoji
- Attempt History table:
  - Columns: #, Module, Date, Score, Status (Pass/Fail)
  - 5 rows of historical attempts

**State:** None (all from static data)

---

### 4.13 SettingsPage (`/settings`)

**Purpose:** Preferences, profile, logout

**UI Elements:**
- Header with "Settings" title
- Profile card: avatar circle, name, Worker ID (orange), organization
- Language section: 3 toggle buttons (English, Hindi, Santali)
- Preferences section:
  - Sound Effects toggle (custom animated toggle switch)
  - Voice Guidance toggle (custom animated toggle switch)
- Data & Storage section (read-only info):
  - Offline Content: "3 of 5"
  - Storage Used: "248 MB"
  - Sync Status: "Synced" (green)
- About section:
  - App Version: "1.0.0 (Demo)"
  - PS Number: "SIH26041"
  - Organization: "Govt. of Jharkhand"
- "LOG OUT" button (red outlined) -- navigates to `/login`
- Footer: "ARC MineSafetyAr - Demo UI Reference / Not for production use"

**State:**
- `language` (string)
- `sound` (boolean)
- `voice` (boolean)

---

### 4.14 QRVerifyPage (`/verify`)

**Purpose:** Certificate verification by ID lookup

**UI Elements:**
- Header with "Verify Certificate" title and back button
- Search section:
  - Magnifying glass emoji
  - "Certificate Verification" title + description
  - Text input (placeholder: "e.g. JH-FIRE-001928")
  - "VERIFY" button
  - Quick-fill buttons for 3 IDs:
    - JH-FIRE-001928
    - JH-GAS-002156
    - INVALID-000
- Loading state: animated spinning circle + "Checking certificate..." (1s delay)
- Result if found:
  - Green "CERTIFICATE VALID" badge
  - Details: Worker, Worker ID, Module, Score, Issued, Expires, Status
  - "VIEW CERTIFICATE" button
- Result if not found:
  - Red "INVALID CERTIFICATE" badge
  - "No certificate found with ID: ..."

**State:**
- `certId` (string)
- `result` (object or null)
- `searching` (boolean)

---

## 5. All Reusable Components (5)

### 5.1 BottomNav

- **Props:** None (reads location from router)
- **Features:** 4 tabs with inline SVG icons. Active tab highlighted in orange. Fixed to bottom, max-width 430px. Uses `env(safe-area-inset-bottom)`.
- **Visibility:** Hidden on splash/login/register and during learning/AR/quiz flows.

### 5.2 Header

- **Props:**
  - `title` (string) -- displayed centered
  - `showBack` (boolean, default false) -- shows back arrow
  - `rightAction` (ReactNode, default null) -- custom right-side content
- **Features:** Sticky header at top, dark background. Back button calls `navigate(-1)`.

### 5.3 ModuleCard

- **Props:**
  - `module` (object) -- icon, title, duration, difficulty, status, progress, bestScore
  - `onClick` (function)
- **Features:** Card with emoji icon, title, meta info, status badge (color-coded), ProgressBar, "Best: X%" score. Locked modules: reduced opacity, no click.

### 5.4 ProgressBar

- **Props:**
  - `value` (number, 0-100)
  - `height` (number, default 8)
  - `color` (string, default `#FF6D00`)
  - `showLabel` (boolean, default false)
- **Features:** Animated fill. Green at 100%. 0.6s ease transition.

### 5.5 ScoreBar

- **Props:**
  - `label` (string)
  - `value` (number)
  - `maxValue` (number, default 100)
  - `color` (string, default `#FF6D00`)
- **Features:** Labeled horizontal bar with percentage. Green >= 80%, Orange >= 60%, Red < 60%.

---

## 6. Data Layer (4 files)

### 6.1 worker.js

Single worker object:
- **id:** JH10293
- **name:** Ramesh Kumar
- **pin:** 1234
- **organization:** Jharkhand Steel Works
- **sector:** Steel Manufacturing
- **Competency Scores:** hazardRecognition: 82, ppeSelection: 65, evacuation: 78, emergencyResponse: 71
- **Attempts:** 5 historical attempts across 3 modules
- **overallProgress:** 68%
- **certificatesEarned:** 2

### 6.2 modules.js

5 training modules:

| ID | Title | Icon | Domain | Duration | Difficulty | Status | Progress | Best Score |
|---|---|---|---|---|---|---|---|---|
| fire_safety | Fire & Explosion Response | fire | Fire Safety | 45 min | Medium | completed | 100% | 85% |
| gas_safety | Gas Leak & Confined Space | biohazard | Chemical Safety | 50 min | Hard | completed | 100% | 72% |
| machinery_safety | Machinery Safety | gear | Equipment Safety | 40 min | Medium | in_progress | 45% | 60% |
| electrical_safety | Electrical Safety | zap | Electrical Safety | 35 min | Medium | not_started | 0% | 0% |
| heights_safety | Working at Heights | mountain | Fall Protection | 40 min | Hard | locked | 0% | 0% |

Each module has: description, 5 learning objectives, competencyScores object.

Exported function: `getModuleById(id)`

### 6.3 questions.js

22 total questions across 5 modules:

| Module | Questions | Topics |
|---|---|---|
| fire_safety | 5 | P.A.S.S., safe distance, fire triangle, nozzle aim, evacuation |
| gas_safety | 5 | Gas leak response, PPE, buddy system, odorless gases, pre-entry testing |
| machinery_safety | 4 | LOTO, pre-maintenance, machine guards, unguarded machines |
| electrical_safety | 4 | Common causes, electrical PPE, grounding, electrocution response |
| heights_safety | 4 | Fall protection height, harness inspection, anchor points, scaffold safety |

Each question: id, text, 4 options, correct index (0-3), competency category.

Exported function: `getQuestionsForModule(id)`

### 6.4 certificates.js

2 certificates:

| ID | Worker | Module | Score | Issued | Expires | Status |
|---|---|---|---|---|---|---|
| JH-FIRE-001928 | Ramesh Kumar | Fire & Explosion Response | 85% | 2026-08-26 | 2027-08-26 | valid |
| JH-GAS-002156 | Ramesh Kumar | Gas Leak & Confined Space | 72% | 2026-08-28 | 2027-08-28 | valid |

Exported functions: `getCertificateById(id)`, `getCertificatesByWorker(workerId)`

---

## 7. UI Elements Catalog

### Forms
1. Login form (Worker ID + PIN with show/hide)
2. Registration form (7 fields + language selector + sector dropdown)

### Tables
1. Module Detail attempt history (#, Date, Score, Status)
2. Progress attempt history (#, Module, Date, Score, Status)

### Cards (20 types)
1. ModuleCard (reusable)
2. Dashboard progress card
3. Dashboard module cards (first 3 modules)
4. Dashboard certificate banner
5. Module detail hero section
6. Module detail best score card
7. Learning content slide card
8. AR scene card
9. Assessment question card
10. Results score card
11. Results competency section
12. Results attempt info card
13. Certificate card
14. Progress overview card
15. Progress module rows
16. Progress history card
17. Settings profile card
18. Settings row cards
19. QR verify result card
20. QR loading card

### Charts/Bars
1. ProgressBar (reusable, animated fill)
2. ScoreBar (reusable, labeled percentage bar)

### Buttons (20+ types)
- Login, Demo Mode, Create Account
- Register
- Filter pills x4
- "See All" / "View All" links
- Start/Continue/Retake Training
- View Certificate
- Previous/Next/I'm Ready
- Next Question/View Results
- Back to Module/Try Again/Back to Dashboard
- Download/Share/Verify Certificate
- Quick-fill IDs x3
- Verify
- Language buttons x3
- Sound/Voice toggles
- Log Out

### Toggles
1. Show/hide PIN (LoginPage)
2. Sound Effects (SettingsPage) -- custom animated toggle
3. Voice Guidance (SettingsPage) -- custom animated toggle

### Modals
None. The app uses no modal/dialog components.

### Icons
- All inline SVGs (BottomNav, Header back button)
- Emoji characters throughout

### Animations
1. Splash progress bar filling
2. AR scan line animation
3. AR spinner animation
4. ProgressBar fill transition (0.6s ease)
5. ScoreBar fill transition (0.6s ease)
6. Learning content progress track (0.3s ease)
7. Settings toggle dot (0.2s)
8. AR auto-advancing phases with timers
9. Timer countdown

---

## 8. Features Summary

| Feature | Status |
|---|---|
| Splash/loading screen with auto-advance | Implemented |
| Worker login (PIN-based, demo) | Implemented (no auth) |
| Worker registration | Implemented (no persistence) |
| Demo mode (skip login) | Implemented |
| Dashboard with stats overview | Implemented |
| Training module catalogue with filters | Implemented |
| Module detail with objectives, scores, history | Implemented |
| Slide-based learning content | Implemented |
| Simulated AR training experience (8 phases) | Implemented (no real AR) |
| MCQ quiz/assessment | Implemented |
| Score calculation (60% simulation + 40% MCQ) | Implemented |
| Pass/fail threshold (60%) | Implemented |
| Competency breakdown scoring | Implemented |
| Digital safety certificate display | Implemented |
| QR code certificate verification | Implemented (text lookup) |
| Progress tracking overview | Implemented |
| Attempt history table | Implemented |
| Settings with language selection | Implemented (no persistence) |
| Sound/Voice toggle preferences | Implemented (no effect) |
| Offline content indicators | Display only |
| Storage usage display | Display only |
| Sync status display | Display only |
| Multi-language support (English, Hindi, Santali) | UI only |
| Bottom navigation with 4 tabs | Implemented |
| Responsive phone-frame layout (max 430px) | Implemented |
| Dark theme | Implemented |
| Module locking (sequential access) | Implemented |

---

