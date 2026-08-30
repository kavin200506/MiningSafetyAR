# LoginPage (`/login`)

## Purpose
Worker login form with PIN-based authentication (demo mode - no real auth).

## Route
`/login`

## File
`src/pages/LoginPage.jsx` (224 lines)

## Imports
- `useState` from React
- `useNavigate` from react-router-dom

## State
| Variable | Type | Initial | Description |
|---|---|---|---|
| `workerId` | string | `''` | Worker ID input value |
| `pin` | string | `''` | PIN input value |
| `showPin` | boolean | `false` | Toggle PIN visibility |

## UI Elements (Top to Bottom)

### 1. Full-Screen Container
- Flexbox column
- Background: `#121212`
- Padding: `0 24px`
- Height: `100vh`

### 2. Top Section (Branding)
- Padding top: 60px, padding bottom: 32px
- **Logo Box:**
  - 64x64px rounded square (`borderRadius: 16px`)
  - Background: `linear-gradient(135deg, #FF6D00, #E65100)`
  - Box shadow: `0 6px 24px rgba(255, 109, 0, 0.25)`
  - Contains: Shield emoji `🛡️` (36px)
- **Title:** "ARC" -- 28px, weight 800, white, letter spacing 4px
- **Subtitle:** "Industrial Safety Training" -- 13px, `#707070`

### 3. Form Section

#### Worker ID Input
- Label: "Worker ID" -- 12px, weight 600, `#B0B0B0`
- Input: text type, placeholder "e.g. JH10293"
- Height: 48px, background `#262626`, border `1px solid #333`
- Border radius: 10px, padding 0 14px
- Text: 15px, white

#### PIN Input Group
- Label: "PIN" -- same style as Worker ID
- Container: flex row with gap 8px
- PIN Input: password/text type (toggled), placeholder "Enter 4-digit PIN"
  - Same style as Worker ID input but with `flex: 1`
  - maxLength: 4
- Eye Toggle Button:
  - 48x48px, background `#262626`, border `1px solid #333`
  - Border radius: 10px
  - Shows: 👁 (eye) when hidden, 🔒 (lock) when visible
  - Cursor: pointer

#### LOGIN Button
- Full width, height 48px
- Background: `linear-gradient(135deg, #FF6D00, #E65100)`
- Text: "LOGIN" -- white, 15px, weight 700, letter spacing 1px
- Border: none, border radius 10px
- Margin top: 8px

#### Demo Mode Button
- Full width, height 44px
- Background: transparent
- Text: "Demo Mode (Skip Login)" -- `#FF6D00`, 13px, weight 600
- Border: `1px solid #FF6D00`, border radius 10px
- Margin top: 12px

#### Divider
- Flex row with gap 12px
- Two lines: flex 1, height 1px, `#333`
- Center text: "OR" -- 11px, `#555`, weight 600

#### Create New Account Button
- Full width, height 44px
- Background: `#262626`
- Text: "Create New Account" -- `#B0B0B0`, 13px, weight 600
- Border: `1px solid #333`, border radius 10px

### 4. Footer
- Padding bottom: 24px, centered
- Text: "Language: **English**" -- 12px, `#555`, "English" in `#FF6D00`

## Event Handlers
| Handler | Action |
|---|---|
| `handleLogin` | `navigate('/dashboard')` -- no validation |
| `handleDemo` | `navigate('/dashboard')` |
| `setShowPin` toggle | Toggles PIN visibility |
| Register button | `navigate('/register')` |

## Navigation
- LOGIN -> `/dashboard`
- Demo Mode -> `/dashboard`
- Create New Account -> `/register`

## Colors Used
| Element | Color |
|---|---|
| Background | `#121212` |
| Logo box | `#FF6D00` to `#E65100` gradient |
| Title | `#FFFFFF` |
| Subtitle | `#707070` |
| Labels | `#B0B0B0` |
| Inputs | bg `#262626`, border `#333`, text `#FFFFFF` |
| LOGIN button | `#FF6D00` to `#E65100` gradient |
| Demo button | transparent, border `#FF6D00`, text `#FF6D00` |
| Divider lines | `#333` |
| "OR" text | `#555` |
| Register button | bg `#262626`, border `#333`, text `#B0B0B0` |
| Footer | `#555`, "English" `#FF6D00` |

## Visual Layout
```
┌─────────────────────────┐
│                         │
│         🛡️              │  (orange gradient box)
│         ARC             │
│  Industrial Safety...   │
│                         │
│  Worker ID              │
│  ┌───────────────────┐  │
│  │ e.g. JH10293      │  │
│  └───────────────────┘  │
│                         │
│  PIN                    │
│  ┌──────────────┬────┐  │
│  │ Enter 4-digit│ 🔒 │  │
│  └──────────────┴────┘  │
│                         │
│  ┌───────────────────┐  │
│  │      LOGIN        │  │  (orange gradient)
│  └───────────────────┘  │
│                         │
│  ┌───────────────────┐  │
│  │ Demo Mode (Skip)  │  │  (orange outline)
│  └───────────────────┘  │
│                         │
│  ─────── OR ───────    │
│                         │
│  ┌───────────────────┐  │
│  │ Create New Account│  │  (dark)
│  └───────────────────┘  │
│                         │
│  Language: English      │
└─────────────────────────┘
```
