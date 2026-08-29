# ModuleDetailPage (`/training/:id`)

## Purpose
Single module detail view with description, objectives, scores, attempt history, and action buttons.

## Route
`/training/:id`

## File
`src/pages/ModuleDetailPage.jsx` (212 lines)

## Imports
- `useNavigate`, `useParams` from react-router-dom
- `getModuleById` from data
- `worker` from data
- `Header`, `ProgressBar`, `ScoreBar` components

## URL Parameters
| Param | Description |
|---|---|
| `id` | Module ID (e.g., `fire_safety`) |

## Computed Values
| Variable | Calculation |
|---|---|
| `mod` | `getModuleById(id)` -- module object or null |
| `moduleAttempts` | `worker.attempts.filter(a => a.module === id)` |
| `locked` | `mod.status === 'locked'` |

## Constants
```js
statusColors = {
  completed: '#4CAF50',
  in_progress: '#FF6D00',
  not_started: '#707070',
  locked: '#555'
}
statusLabels = {
  completed: 'Completed',
  in_progress: 'In Progress',
  not_started: 'Not Started',
  locked: 'Locked'
}
```

## UI Elements (Top to Bottom)

### 1. Not Found State (if `!mod`)
- Header with title "Module Not Found" + back button
- Centered text: "Module not found." -- `#707070`

### 2. Header Component
- Props: `title={mod.title}`, `showBack`

### 3. Scroll Area
- Height: `calc(100vh - 56px)`, overflowY auto, padding 0 16px

### 4. Hero Section
- Flex row, space-between, padding 12px 0 16px
- **Icon box:** 64x64px, border radius 16px, background `mod.color + '20'`
  - Module emoji (40px)
- **Status badge:**
  - Inline pill: background `statusColors[status] + '22'`, text in status color
  - Padding: 4px 10px, border radius 6px, font 11px, weight 600
  - Text: "Completed" / "In Progress" / "Not Started" / "Locked"

### 5. Description
- Module description text
- Font: 13px, `#B0B0B0`, line height 1.6, margin bottom 16px

### 6. Meta Row
- Flex row, gap 12px, margin bottom 16px
- 3 pills, each: flex row, gap 4px, bg `#1E1E1E`, padding 6px 10px, border radius 8px
  - **Duration:** ⏱️ emoji + "45 min" -- 11px, `#B0B0B0`
  - **Difficulty:** ⭐ emoji + "Medium"
  - **Domain:** 🎯 emoji + "Fire Safety"

### 7. Best Score Card (conditional: `!locked && mod.bestScore > 0`)
- Background: `#1E1E1E`, border radius 12px, padding 14px
- Border: `1px solid #333`, margin bottom 16px
- **Row:** "Personal Best" (13px, `#B0B0B0`) + "85%" (22px, weight 800, `#FF6D00`)
- **ProgressBar:** value={mod.bestScore}, height 8, color from status
- **Meta:** "2 attempts · Last: 2026-08-26" -- 11px, `#707070`

### 8. Learning Objectives Section
- Title: "Learning Objectives" -- 14px, weight 700, white, margin bottom 10px
- Numbered list (5 items):
  - Each: flex row, gap 8px
  - Number: `#FF6D00`, 12px, weight 700
  - Text: 13px, `#B0B0B0`, line height 1.5

### 9. Competency Scores Section (conditional: `!locked && competencyScores && bestScore > 0`)
- Title: "Competency Scores"
- ScoreBar components:
  - Hazard Recognition
  - Extinguisher Use OR PPE Selection (depending on module)
  - Evacuation
  - Emergency Response

### 10. Attempt History Table (conditional: `moduleAttempts.length > 0`)
- Title: "Attempt History"
- **Table header:** flex row, padding 8px 0, border bottom `1px solid #333`
  - Columns: # (flex 0.5), Date (flex 1), Score (flex 1), Status (flex 1)
  - Font: 10px, weight 600, `#707070`, uppercase
- **Table rows:**
  - Each: flex row, padding 8px 0, border bottom `1px solid #262626`
  - Attempt # : 12px, `#707070`
  - Date: 12px, `#B0B0B0`
  - Score: 12px, white, weight 600
  - Status: "Pass" (`#4CAF50`) or "Fail" (`#F44336`)

### 11. Action Section (padding 16px 0)

#### If NOT locked:
- **Start/Continue/Retake Button:**
  - Full width, height 48px
  - Background: `linear-gradient(135deg, #FF6D00, #E65100)`
  - Text logic:
    - `completed` -> "RETAKE TRAINING"
    - `in_progress` -> "CONTINUE TRAINING"
    - else -> "START TRAINING"
  - White, 14px, weight 700, letter spacing 1px
- **View Certificate Button (if completed + has certificateId):**
  - Full width, height 44px
  - Background: transparent
  - Text: "VIEW CERTIFICATE" -- `#FF6D00`, border `1px solid #FF6D00`

#### If locked:
- Centered, padding 24px
- Lock emoji 🔒 (24px)
- "Complete previous modules to unlock this training" -- `#707070`, 13px

### 12. Bottom Spacer
- Height: 80px

## Navigation
| Element | Route |
|---|---|
| Start/Continue/Retake button | `/training/${id}/learn` |
| View Certificate button | `/certificate/${mod.certificateId}` |

## Visual Layout
```
┌─────────────────────────┐
│ [←]  Fire & Explosion   │  (Header)
│─────────────────────────│
│                         │
│ 🔥              [Compl.]│  (hero: icon + badge)
│                         │
│ Fire safety training... │  (description)
│                         │
│ ⏱️ 45min ⭐ Medium 🎯..│  (meta pills)
│                         │
│ ┌─────────────────────┐ │
│ │ Personal Best  85%  │ │
│ │ ━━━━━━━━━━━━━━━━━━━ │ │
│ │ 2 attempts · Last.. │ │
│ └─────────────────────┘ │
│                         │
│ Learning Objectives     │
│ 1. Identify fire...     │
│ 2. Use P.A.S.S...       │
│ 3. Evacuate safely...   │
│ ...                     │
│                         │
│ Competency Scores       │
│ Hazard Recognition ━━━ │  (ScoreBar)
│ Extinguisher Use   ━━  │
│ Evacuation         ━━━ │
│ Emergency Response ━━  │
│                         │
│ Attempt History         │
│ # │ Date │ Score │ St. │
│ 1 │ 08-26│  85%  │ Pass│
│ 2 │ 08-25│  60%  │ Fail│
│                         │
│ ┌───────────────────┐   │
│ │ RETAKE TRAINING   │   │  (orange gradient)
│ └───────────────────┘   │
│ ┌───────────────────┐   │
│ │ VIEW CERTIFICATE  │   │  (orange outline)
│ └───────────────────┘   │
└─────────────────────────┘
```
