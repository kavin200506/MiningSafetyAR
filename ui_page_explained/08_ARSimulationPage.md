# ARSimulationPage (`/training/:id/ar`)

## Purpose
Simulated AR training experience with 8 auto-advancing phases. No real camera/AR - uses emojis and timers to simulate the experience.

## Route
`/training/:id/ar`

## File
`src/pages/ARSimulationPage.jsx` (228 lines)

## Imports
- `useState`, `useEffect` from React
- `useNavigate`, `useParams` from react-router-dom
- `getModuleById` from data

## URL Parameters
| Param | Description |
|---|---|
| `id` | Module ID |

## State
| Variable | Type | Initial | Description |
|---|---|---|---|
| `phase` | number | `0` | Current phase (0-7) |
| `score` | number | `50` | Current score |
| `timeLeft` | number | `120` | Countdown timer in seconds |
| `instruction` | string | `'Scanning environment...'` | Current instruction text |

## Phase Sequence (auto-advancing)

| Phase | Delay | Next Phase | Instruction | Score |
|---|---|---|---|---|
| 0 | 2000ms | 1 | "Move phone slowly across a flat surface..." | - |
| 1 | 3000ms | 2 | "Surface detected! Tap to place scenario" | - |
| 2 | 2000ms | 3 | "🔥 Fire detected! Locate the extinguisher!" | - |
| 3 | 3000ms | 4 | "Extinguisher located! Move to it and tap to pick up" | 50 |
| 4 | 2000ms | 5 | "Tap to OPEN CAP" | 60 |
| 5 | 2000ms | 6 | "HOLD to SPRAY (6 seconds)..." | 70 |
| 6 | 6000ms | 7 | "Fire extinguished! 🎉" | 80 |
| 7 | 2000ms | quiz | (auto-navigate to quiz) | - |

## Timer Logic
- Starts counting down from 120 at phase 3 (when fire is detected)
- Color coding:
  - `> 60s`: green `#4CAF50`
  - `> 30s`: yellow `#FFC107`
  - `< 30s`: red `#F44336`
- Format: `M:SS` (e.g., "1:45")

## UI Elements

### 1. Container
- Flex column, height `100vh`, background: `#000` (black for AR feel)

### 2. Custom Header
- Background: `#121212`, padding 12px 16px
- **Back button:** 36x36px, bg `#262626`, border `1px solid #333`, chevron SVG
- **Title:** "AR Simulation" -- flex 1, centered, 16px, weight 700, white
- **Module label:** module title -- 11px, `#FF6D00`, weight 600

### 3. Scene Area (flex: 1, position relative, overflow hidden)

#### Phases 0-1: Scan Area
- Full height, gradient `linear-gradient(180deg, #1a1a1a, #111)`
- Centered flex column
- **Scan line:** 80% width, 2px height, bg `#FF6D00`, opacity 0.6
  - Animation: `scan 2s ease-in-out infinite` (CSS keyframe)
- **Scan grid:** 3x2 grid of dots (8px circles, `#FF6D0055`)
- "AR Camera View" text -- `#707070`, 13px, centered

#### Phases 2+: AR Scene
- Full height, gradient `linear-gradient(180deg, #2a2a2a, #1a1a1a)`
- Centered, position relative

**Phase 3+: Fire Object**
- Position: absolute, top 30%, centered horizontally
- Fire emoji 🔥 (48px)
- **Hazard zone:** 120x120px circle, bg `#F4433622`, border `2px dashed #F4433666`

**Phase 4+: Extinguisher Object**
- Position: absolute, bottom 25%, right 20%
- Shield emoji 🛡️ (40px)
- **Label:** "TAP TO PICK UP" -- 10px, `#FF6D00`, weight 700
  - Bg `#FF6D0022`, padding 4px 8px, border radius 4px

**Phase 5+: Cap Label**
- Position: absolute, bottom 35%, centered
- "TAP TO OPEN CAP" -- 11px, `#FFC107`, weight 700
  - Bg `#FFC10722`, padding 6px 12px, border radius 6px

**Phase 6+: Spray Area**
- Position: absolute, bottom 20%, centered
- **Progress bar:** 160px width, 6px height, bg `#333`
  - Fill: bg `#4CAF50`, 100% width (simulated)
- "Spraying..." text -- `#B0B0B0`, 11px

**Phase 7+: Clear Area**
- Centered flex column
- Checkmark ✅ (48px)
- "Area Clear!" -- `#4CAF50`, 16px, weight 700

**Score/Timer Overlay (phases 3+)**
- Position: absolute, top 16px, left/right 16px
- Flex row, space-between
- **Score pill:** padding 6px 14px, border radius 8px, bg `#FF6D00`
  - "Score: 80" -- 13px, weight 700, white
- **Timer pill:** same style, bg dynamic color + '33'
  - Time display in dynamic color

### 4. Instruction Bar
- Background: `#1E1E1E`, border top `1px solid #333`
- Padding: 14px 20px
- Instruction text: white, 14px, weight 600, centered

### 5. Phase Indicator
- Background: `#121212`, padding 12px, padding bottom 24px
- 7 dots, each 8px circle, gap 6px, centered
- **Completed/current:** bg `#FF6D00`
- **Upcoming:** bg `#333`
- Transition: `background 0.3s`

## Event Handlers
| Handler | Action |
|---|---|
| Back button | `navigate(-1)` |

## Navigation
- After phase 7 completes: auto-navigates to `/training/${id}/quiz` after 2 seconds

## Visual Layout
```
┌─────────────────────────┐
│ [←]  AR Simulation  fire│  (header)
│─────────────────────────│
│ Score: 80    ⏱️ 1:45    │  (overlay pills)
│                         │
│    🔥                   │  (fire object)
│   /   \  (red dashed    │
│  /     \  hazard zone)  │
│                         │
│              🛡️         │  (extinguisher)
│          TAP TO PICK UP │
│                         │
│      TAP TO OPEN CAP    │  (cap label)
│                         │
│    ━━━━━━━━━━━━━━━━━    │  (spray progress)
│       Spraying...       │
│                         │
│    ✅ Area Clear!       │  (on completion)
│─────────────────────────│
│  Fire detected! Locate..│  (instruction bar)
│─────────────────────────│
│  ● ● ● ● ○ ○ ○         │  (phase dots)
└─────────────────────────┘
```
