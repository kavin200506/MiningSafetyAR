# AssessmentPage (`/training/:id/quiz`)

## Purpose
Multiple-choice quiz interface with 4-5 questions per module. Shows correct/incorrect feedback after each answer.

## Route
`/training/:id/quiz`

## File
`src/pages/AssessmentPage.jsx` (190 lines)

## Imports
- `useState` from React
- `useNavigate`, `useParams` from react-router-dom
- `getModuleById` from data
- `getQuestionsForModule` from data

## URL Parameters
| Param | Description |
|---|---|
| `id` | Module ID |

## State
| Variable | Type | Initial | Description |
|---|---|---|---|
| `currentQ` | number | `0` | Current question index |
| `selected` | number/null | `null` | Selected option index |
| `answers` | array | `[]` | Array of answer objects `{questionId, selected, correct}` |
| `answered` | boolean | `false` | Whether current question is answered |

## Computed Values
| Variable | Calculation |
|---|---|
| `mod` | `getModuleById(id)` |
| `allQuestions` | `getQuestionsForModule(id)` |
| `question` | `allQuestions[currentQ]` |
| `totalQ` | `allQuestions.length` |
| `isLast` | `currentQ === totalQ - 1` |

## Empty State
If `allQuestions.length === 0`: centered "No questions available for this module" -- `#707070`

## UI Elements (Top to Bottom)

### 1. Container
- Flex column, height `100vh`, background `#121212`

### 2. Custom Header
- Background: `#121212`, padding 12px 16px, z-index 10
- **Back button:** 36x36px, bg `#262626`, border `1px solid #333`, chevron SVG
- **Title:** "Assessment" -- flex 1, centered, 18px, weight 700, white
- **Counter:** "1/5" -- 13px, `#FF6D00`, weight 700

### 3. Progress Track
- Padding: 0 16px
- Track: 100% width, 3px height, bg `#333`, border radius 2px
- Fill: bg `#FF6D00`, border radius 2px, `transition: width 0.3s`
- Width: `${((currentQ + 1) / totalQ) * 100}%`

### 4. Scroll Area
- Flex 1, overflowY auto, padding 20px 16px

### 5. Question Card
- Background: `#1E1E1E`, border radius 14px, padding 20px
- Border: `1px solid #333`
- **Question label:** "Question 1" -- 11px, `#FF6D00`, weight 700, letter spacing 1px
- **Question text:** 16px, weight 700, white, line height 1.5, margin bottom 20px

### 6. Options List
- Flex column, gap 10px
- 4 option buttons (A, B, C, D):

#### Option Button States

**Default (unanswered, not selected):**
- Background: `#262626`
- Border: `1px solid #333`
- Text color: `#B0B0B0`

**Selected (unanswered):**
- Background: `#FF6D0022`
- Border: `1px solid #FF6D00`
- Text color: `#FF6D00`

**Correct (after answering):**
- Background: `#4CAF5022`
- Border: `1px solid #4CAF50`
- Text color: `#4CAF50`

**Incorrect selected (after answering):**
- Background: `#F4433622`
- Border: `1px solid #F44336`
- Text color: `#F44336`

#### Option Layout
- Flex row, gap 12px, padding 14px, border radius 10px
- **Letter badge:** 28x28px, bg `#33333388`, border radius 8px
  - Centered letter (A/B/C/D) -- 12px, weight 700, white
- **Option text:** 13px, line height 1.5

### 7. Feedback Banner (after answering)
- Flex row, gap 10px, margin top 16px
- Padding: 12px, border radius 10px, border `1px solid`
- **Correct:** bg `#4CAF5011`, border `#4CAF5044`
  - ✅ emoji (16px) + "Correct!" -- `#4CAF50`, 13px, weight 600
- **Incorrect:** bg `#F4433611`, border `#F4433644`
  - ❌ emoji (16px) + "Incorrect" -- `#F44336`, 13px, weight 600

### 8. Navigation Bar
- Padding: 12px 16px, padding bottom 24px
- **Next/View Results button (only after answering):**
  - Full width, height 48px
  - Background: `linear-gradient(135deg, #FF6D00, #E65100)`
  - Text: "Next Question" (or "View Results" on last question)
  - White, 14px, weight 700

## Event Handlers
| Handler | Action |
|---|---|
| `handleSelect(idx)` | If not answered: set selected, set answered true, add to answers array |
| `handleNext` | If last question: calculate score and navigate to results. Else: next question |
| Back button | `navigate(-1)` |

## Scoring Logic (on last question)
```js
correct = answers.filter(a => a.correct).length
mcqScore = Math.round((correct / totalQ) * 100)
navigate(`/training/${id}/results`, {
  state: {
    mcqScore,
    correct,
    total: totalQ,
    simulationScore: 80,  // hardcoded
    competencyScores: mod?.competencyScores || {}
  }
})
```

## Navigation
- "View Results" (last question) -> `/training/${id}/results` (with state)
- Back button -> previous page

## Visual Layout
```
┌─────────────────────────┐
│ [←]   Assessment    1/5 │
│ ━━━━━━━━━━━━━━━━━━━━━━ │  (progress)
│─────────────────────────│
│ ┌─────────────────────┐ │
│ │ Question 1          │ │  (orange label)
│ │                     │ │
│ │ What is the first   │ │  (question text)
│ │ step when you...    │ │
│ │                     │ │
│ │ ┌─────────────────┐ │ │
│ │ │A  Pull the pin  │ │ │  (option, default)
│ │ └─────────────────┘ │ │
│ │ ┌─────────────────┐ │ │
│ │ │B  Aim at flames │ │ │  (selected = orange)
│ │ └─────────────────┘ │ │
│ │ ┌─────────────────┐ │ │
│ │ │C  Squeeze handle│ │ │  (correct = green)
│ │ └─────────────────┘ │ │
│ │ ┌─────────────────┐ │ │
│ │ │D  Sweep side..  │ │ │  (incorrect = red)
│ │ └─────────────────┘ │ │
│ │                     │ │
│ │ ✅ Correct!         │ │  (feedback banner)
│ └─────────────────────┘ │
│                         │
│ ┌───────────────────┐   │
│ │  Next Question    │   │  (orange gradient)
│ └───────────────────┘   │
└─────────────────────────┘
```
