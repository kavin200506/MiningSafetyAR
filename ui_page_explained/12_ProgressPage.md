# ProgressPage (`/progress`)

## Purpose
Overall progress overview, competency scores, module progress list, and attempt history table.

## Route
`/progress`

## File
`src/pages/ProgressPage.jsx` (140 lines)

## Imports
- `worker` from data
- `modules` from data
- `certificates` from data
- `ScoreBar`, `Header` components

## Computed Values
| Variable | Calculation |
|---|---|
| `completedModules` | `modules.filter(m => m.status === 'completed')` = 2 modules |

## UI Elements (Top to Bottom)

### 1. Container
- Height `100vh`, bg `#121212`

### 2. Header Component
- Props: `title="Progress"`

### 3. Scroll Area
- Height: `calc(100vh - 56px)`, overflowY auto, padding 0 16px

### 4. Overview Card
- Background: `linear-gradient(135deg, #FF6D0022, #E6510022)` (orange tinted)
- Border: `1px solid #FF6D0044`, border radius 14px, padding 20px
- Margin bottom 16px, centered
- **Title:** "Overall Progress" -- 14px, weight 600, `#B0B0B0`
- **Big Score:**
  - "68" -- 48px, weight 800, `#FF6D00`
  - "%" -- 18px, weight 700, `#FF9E40`
- **Stats Row** (flex, space-around):
  - **Completed:** "2" (20px, weight 700, white) + "Completed" (11px, `#707070`)
  - **Total Attempts:** "7" + "Total Attempts"
  - **Certificates:** "2" + "Certificates"

### 5. Competency Scores Section
- Title: "Competency Scores" -- 14px, weight 700, white
- **Competency card:** bg `#1E1E1E`, border radius 12px, padding 14px, border `1px solid #333`
- 4 ScoreBar components:
  - Hazard Recognition: 82
  - PPE Selection: 65
  - Evacuation: 78
  - Emergency Response: 71

### 6. Module Progress Section
- Title: "Module Progress"
- 5 module rows, each:
  - Flex row, gap 10px, bg `#1E1E1E`, border radius 10px
  - Padding 10px 12px, margin bottom 6px, border `1px solid #333`
  - **Icon box:** 36x36px, bg `#262626`, border radius 8px, centered emoji (20px)
  - **Info:** flex 1
    - Name: 12px, weight 600, white
    - Meta: "Best: 85%" or "45% done" or "Not started" -- 10px, `#707070`
  - **Status badge:** emoji (14px)
    - completed: ✅
    - in_progress: ▶️
    - not_started: ○ (opacity 0.5)
    - locked: 🔒 (opacity 0.3)

### 7. Attempt History Section
- Title: "Attempt History"
- **History card:** bg `#1E1E1E`, border radius 12px, padding 12px, border `1px solid #333`
- **Table header:** flex row, padding 6px 0, border bottom `1px solid #333`
  - Columns: # (flex 0.5), Module (flex 2), Date (flex 1.2), Score (flex 0.8), Status (flex 0.8)
  - Font: 10px, weight 600, `#707070`, uppercase, letter spacing 0.5px
- **Table rows:** (5 rows from worker.attempts)
  - Each: flex row, padding 8px 0, border bottom `1px solid #26262622`
  - #: 12px, `#707070`
  - Module: truncated to 15 chars, 11px, `#B0B0B0`
  - Date: 11px, `#707070`
  - Score: 12px, white, weight 600
  - Status: "Pass" (`#4CAF50`) or "Fail" (`#F44336`)

### 8. Bottom Spacer
- Height: 80px

## Visual Layout
```
┌─────────────────────────┐
│       Progress          │  (Header)
│─────────────────────────│
│ ┌─────────────────────┐ │
│ │  Overall Progress   │ │
│ │       68%           │ │  (large orange number)
│ │                     │ │
│ │  2       7      2   │ │
│ │Completed Attempts Crt│ │
│ └─────────────────────┘ │
│                         │
│ Competency Scores       │
│ ┌─────────────────────┐ │
│ │ Hazard Recog.  ━━━  │ │
│ │ PPE Selection  ━━   │ │
│ │ Evacuation     ━━━  │ │
│ │ Emergency Resp ━━   │ │
│ └─────────────────────┘ │
│                         │
│ Module Progress         │
│ ┌─────────────────────┐ │
│ │🔥 Fire & Explos.  ✅│ │
│ │☣️ Gas Leak & Con.  ✅│ │
│ │⚙️ Machinery Safe. ▶️│ │
│ │⚡ Electrical Saf. ○ │ │
│ │🏔️ Working at He. 🔒│ │
│ └─────────────────────┘ │
│                         │
│ Attempt History         │
│ ┌─────────────────────┐ │
│ │# │Module │Date│Sc│St│ │
│ │1 │Fire & │08..│85│Pa│ │
│ │2 │Fire & │08..│60│Fa│ │
│ │3 │Gas Lea│08..│72│Pa│ │
│ │...                    │
│ └─────────────────────┘ │
│                         │
│   [Bottom Nav Bar]      │
└─────────────────────────┘
```
