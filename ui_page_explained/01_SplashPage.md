# SplashPage (`/splash`)

## Purpose
Loading/splash screen that auto-advances to login after a progress bar fills.

## Route
`/splash`

## File
`src/pages/SplashPage.jsx` (139 lines)

## Imports
- `useEffect`, `useState` from React
- `useNavigate` from react-router-dom

## State
| Variable | Type | Initial | Description |
|---|---|---|---|
| `progress` | number | `0` | Progress bar value (0-100) |

## UI Elements (Top to Bottom)

### 1. Full-Screen Container
- Flexbox column, centered
- Background: `linear-gradient(180deg, #121212 0%, #1A1A1A 100%)`
- Padding: `40px 24px`
- Height: `100vh`

### 2. Logo Box
- 80x80px rounded square (`borderRadius: 20px`)
- Background: `linear-gradient(135deg, #FF6D00, #E65100)` (orange gradient)
- Box shadow: `0 8px 32px rgba(255, 109, 0, 0.3)`
- Contains: Shield emoji `🛡️` (48px font size)

### 3. Title
- Text: **"ARC"**
- Font: 36px, weight 800, white
- Letter spacing: 6px
- Margin bottom: 4px

### 4. Subtitle
- Text: **"MineSafetyAr"**
- Font: 14px, weight 600, orange `#FF6D00`
- Letter spacing: 2px
- Margin bottom: 8px

### 5. Description
- Text: **"AR-Based Industrial Safety Training"**
- Font: 13px, grey `#707070`
- Margin bottom: 40px

### 6. Progress Bar
- Width: 200px
- Track: 3px height, `#333` background, rounded
- Fill: orange `#FF6D00`, rounded, `transition: width 0.1s linear`
- Dynamic width: `${progress}%`

### 7. Loading Text
- Text: **"Loading..."** while `progress < 100`, **"Ready"** when `progress >= 100`
- Font: 11px, color `#555`
- Margin top: 8px

### 8. Footer
- **"Powered by Government of Jharkhand"** -- 11px, `#555`
- **"Department of Higher & Technical Education"** -- 10px, `#444`
- **"PS: SIH26041"** -- 10px, `#FF6D00`, weight 600

## Behavior / Logic

### Progress Animation
```
useEffect runs interval every 50ms:
  - Increments progress by 4 each tick
  - When progress >= 100:
    - Clears interval
    - After 300ms delay, navigates to /login
  - Cleanup: clears interval on unmount
```

### Conditional Rendering
- `{progress < 100 ? 'Loading...' : 'Ready'}`

## Navigation
- After progress reaches 100%: `navigate('/login')`

## Colors Used
| Element | Color |
|---|---|
| Background | `#121212` to `#1A1A1A` gradient |
| Logo box | `#FF6D00` to `#E65100` gradient |
| Title | `#FFFFFF` |
| Subtitle | `#FF6D00` |
| Description | `#707070` |
| Progress track | `#333` |
| Progress fill | `#FF6D00` |
| Loading text | `#555` |
| Footer text | `#555`, `#444` |
| Footer ID | `#FF6D00` |

## Visual Layout
```
┌─────────────────────────┐
│                         │
│         🛡️              │  (orange gradient box)
│                         │
│         ARC             │  (large white text)
│     MineSafetyAr        │  (orange subtitle)
│  AR-Based Industrial... │  (grey description)
│                         │
│   ━━━━━━━━━━━━━━━━━     │  (thin orange progress bar)
│       Loading...        │  (grey text)
│                         │
│                         │
│  Powered by Govt...     │
│  Dept of Higher...      │
│     PS: SIH26041        │  (orange)
└─────────────────────────┘
```
