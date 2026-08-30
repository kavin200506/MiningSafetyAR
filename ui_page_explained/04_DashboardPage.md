# DashboardPage (`/dashboard`)

## Purpose
Main home screen with worker stats overview, training module preview, and certificate banner.

## Route
`/dashboard`

## File
`src/pages/DashboardPage.jsx` (264 lines)

## Imports
- `useNavigate` from react-router-dom
- `worker` from `../data/worker`
- `modules` from `../data/modules`
- `ProgressBar` component

## Computed Values
| Variable | Calculation |
|---|---|
| `completedCount` | `modules.filter(m => m.status === 'completed').length` = 2 |

## UI Elements (Top to Bottom)

### 1. Container
- Height: `100vh`, background: `#121212`

### 2. Scroll Area
- Full height, overflowY auto, padding 16px

### 3. Top Bar (Greeting)
- Flex row, space-between, margin bottom 20px
- **Left:**
  - "Hello, **Ramesh**" -- 22px, weight 700, white (first name from worker.name)
  - "Stay safe, stay certified" -- 13px, `#707070`, margin top 2px
- **Right:** Avatar circle
  - 44x44px, border radius 22px (circle)
  - Background: `#262626`
  - Border: `2px solid #FF6D00`
  - Contains: 👷 emoji (20px)

### 4. Overall Progress Card
- Background: `#1E1E1E`, border radius 14px, padding 16px
- Border: `1px solid #333`, margin bottom 20px
- **Header row:**
  - "Overall Progress" -- 14px, weight 600, `#B0B0B0`
  - "68%" -- 18px, weight 800, `#FF6D00`
- **ProgressBar component:**
  - Value: 68, height: 10px
- **Stats row** (flex, space-around):
  - **Completed:** value "2" (20px, weight 700, white), label "Completed" (11px, `#707070`)
  - **Certificates:** value "2", label "Certificates"
  - **Attempts:** value "7", label "Attempts"

### 5. Training Modules Section
- **Section header:** flex, space-between, margin bottom 12px
  - "Training Modules" -- 16px, weight 700, white
  - "See All" button -- background none, no border, `#FF6D00`, 12px, weight 600
- **Module cards** (first 3 modules only):
  - Flex row, gap 12px, bg `#262626`, border radius 12px, padding 12px
  - Border: `1px solid #333`, margin bottom 8px, cursor pointer
  - **Icon box:** 40x40px, border radius 10px, background `mod.color + '20'` (20% opacity)
    - Contains: module emoji (22px)
  - **Info:** flex 1
    - Title: 13px, weight 600, white
    - Meta: "45 min · Medium" -- 11px, `#707070`, margin top 2px
  - **Status emoji** (16px):
    - completed: ✅
    - in_progress: ▶️
    - not_started: ○
    - locked: 🔒

### 6. Certificate Banner
- Background: `linear-gradient(135deg, #FF6D0022, #E6510022)` (orange tinted)
- Border: `1px solid #FF6D0044`, border radius 14px, padding 16px
- **Content:** flex row, gap 12px
  - Trophy emoji 🏆 (28px)
  - "Your Certificates" -- 14px, weight 700, white
  - "2 certificates earned" -- 12px, `#B0B0B0`
- **"View All" button:**
  - Full width, height 36px
  - Background: `#FF6D00`, white text, 12px, weight 600
  - Border: none, border radius 8px, margin top 12px

### 7. Bottom Spacer
- Height: 80px (for BottomNav clearance)

## Navigation
| Element | Route |
|---|---|
| "See All" | `/training` |
| Module cards | `/training/${mod.id}` |
| "View All" (certificates) | `/progress` |

## Visual Layout
```
┌─────────────────────────┐
│ Hello, Ramesh     👷    │  (avatar circle)
│ Stay safe, stay cert... │
│─────────────────────────│
│ ┌─────────────────────┐ │
│ │ Overall Progress 68%│ │
│ │ ━━━━━━━━━━━━━━━━━━━ │ │  (progress bar)
│ │  2     2      7     │ │
│ │ Completed Cert. Att.│ │
│ └─────────────────────┘ │
│                         │
│ Training Modules See All│
│ ┌─────────────────────┐ │
│ │🔥 Fire & Explosion  ✅│ │
│ │   45 min · Medium    │ │
│ └─────────────────────┘ │
│ ┌─────────────────────┐ │
│ │☣️ Gas Leak & Conf.. ✅│ │
│ │   50 min · Hard      │ │
│ └─────────────────────┘ │
│ ┌─────────────────────┐ │
│ │⚙️ Machinery Safety ▶️ │ │
│ │   40 min · Medium    │ │
│ └─────────────────────┘ │
│                         │
│ ┌─────────────────────┐ │
│ │🏆 Your Certificates  │ │
│ │ 2 certificates earn. │ │
│ │    [ View All ]      │ │
│ └─────────────────────┘ │
└─────────────────────────┘
```
