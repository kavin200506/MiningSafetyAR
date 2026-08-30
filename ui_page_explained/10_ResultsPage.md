# ResultsPage (`/training/:id/results`)

## Purpose
Score breakdown and pass/fail result after completing the assessment.

## Route
`/training/:id/results`

## File
`src/pages/ResultsPage.jsx` (148 lines)

## Imports
- `useLocation`, `useNavigate`, `useParams` from react-router-dom
- `getModuleById` from data
- `ScoreBar` component

## URL Parameters
| Param | Description |
|---|---|
| `id` | Module ID |

## State from Location
| Variable | Default | Description |
|---|---|---|
| `mcqScore` | 75 | MCQ percentage score |
| `correct` | 4 | Number of correct answers |
| `total` | 5 | Total questions |
| `simulationScore` | 80 | AR simulation score |
| `competencyScores` | `mod?.competencyScores \|\| {}` | Competency breakdown |

## Computed Values
| Variable | Calculation |
|---|---|
| `finalScore` | `Math.round(simulationScore * 0.6 + mcqScore * 0.4)` |
| `passed` | `finalScore >= 60` |

## Score Formula
```
finalScore = (simulationScore * 0.6) + (mcqScore * 0.4)
```
Example: `(80 * 0.6) + (75 * 0.4) = 48 + 30 = 78%`

## UI Elements (Top to Bottom)

### 1. Container
- Flex column, height `100vh`, background `#121212`

### 2. Scroll Area
- Flex 1, overflowY auto, padding 24px 16px

### 3. Result Header (centered)
- **Icon:** 🏆 (passed) or 🔴 (failed) -- 40px
- **Title:** "TRAINING COMPLETE" or "TRAINING INCOMPLETE" -- 20px, weight 800, white
- **Subtitle:** "Great job! You have passed the assessment." or "Keep practicing to improve your score." -- 13px, `#707070`

### 4. Score Card
- Background: `#1E1E1E`, border radius 14px, padding 20px
- Border: `1px solid #333`, margin bottom 16px

**Score Sections (separated by dividers):**

| Row | Label | Value |
|---|---|---|
| 1 | Simulation Score | `80/100` |
| 2 | MCQ Score | `75/100` |
| 3 | Questions Correct | `4/5` |

- Each row: flex, space-between, padding 8px 0
- Label: 13px, `#B0B0B0`
- Value: 14px, weight 700, white
- Dividers: height 1px, bg `#333`

**Final Score Section:**
- "FINAL SCORE" -- 14px, weight 700, white
- Score: 28px, weight 800, color green `#4CAF50` (passed) or red `#F44336` (failed)

**Pass/Fail Badge:**
- Centered, padding 8px, border radius 8px
- Text: "✅ PASSED" or "❌ FAILED" -- 14px, weight 700
- **Passed:** bg `#4CAF5022`, text `#4CAF50`
- **Failed:** bg `#F4433622`, text `#F44336`

### 5. Competency Breakdown Section
- Title: "Competency Breakdown" -- 14px, weight 700, white, margin bottom 12px
- ScoreBar components (conditional, only if defined):
  - Hazard Recognition
  - Extinguisher Use OR PPE Selection
  - Evacuation
  - Emergency Response

### 6. Attempt Info Card
- Background: `#1E1E1E`, border radius 12px, padding 14px
- Border: `1px solid #333`
- **Rows:**
  - Module: title text (white, weight 600)
  - Attempt: "#3" (white, weight 600)
  - Personal Best: "85%" (`#FF6D00`, weight 700)

### 7. Navigation Bar
- Padding: 12px 16px, padding bottom 24px
- Flex column, gap 8px

#### View Certificate Button (if passed + has certificateId)
- Full width, height 46px
- Background: `linear-gradient(135deg, #4CAF50, #388E3C)` (green gradient)
- Text: "VIEW CERTIFICATE" -- white, 14px, weight 700
- Border: none, border radius 10px

#### Back to Module / Try Again Button
- Full width, height 44px
- Background: `#FF6D00`
- Text: "BACK TO MODULE" (passed) or "TRY AGAIN" (failed)
- White, 13px, weight 600

#### Back to Dashboard Button
- Full width, height 44px
- Background: `#262626`
- Text: "BACK TO DASHBOARD" -- `#B0B0B0`, 13px, weight 600
- Border: `1px solid #333`, border radius 10px

## Navigation
| Element | Route |
|---|---|
| View Certificate | `/certificate/${mod.certificateId}` |
| Back to Module / Try Again | `/training/${id}` |
| Back to Dashboard | `/dashboard` |

## Visual Layout
```
┌─────────────────────────┐
│                         │
│         🏆              │  (or 🔴)
│   TRAINING COMPLETE     │
│  Great job! You have..  │
│                         │
│ ┌─────────────────────┐ │
│ │ Simulation Score    │ │
│ │              80/100 │ │
│ │─────────────────────│ │
│ │ MCQ Score           │ │
│ │              75/100 │ │
│ │─────────────────────│ │
│ │ Questions Correct   │ │
│ │                4/5  │ │
│ │─────────────────────│ │
│ │ FINAL SCORE         │ │
│ │              78%    │ │  (large, green)
│ │   ✅ PASSED         │ │  (green badge)
│ └─────────────────────┘ │
│                         │
│ Competency Breakdown    │
│ Hazard Recognition ━━━ │
│ Extinguisher Use   ━━  │
│ Evacuation         ━━━ │
│ Emergency Response ━━  │
│                         │
│ ┌─────────────────────┐ │
│ │ Module   Fire & ... │ │
│ │ Attempt  #3         │ │
│ │ Best     85%        │ │
│ └─────────────────────┘ │
│                         │
│ ┌───────────────────┐   │
│ │ VIEW CERTIFICATE  │   │  (green gradient)
│ └───────────────────┘   │
│ ┌───────────────────┐   │
│ │  BACK TO MODULE   │   │  (orange)
│ └───────────────────┘   │
│ ┌───────────────────┐   │
│ │ BACK TO DASHBOARD │   │  (dark)
│ └───────────────────┘   │
└─────────────────────────┘
```
