# SettingsPage (`/settings`)

## Purpose
User preferences, profile info, language selection, toggles, data/storage info, and logout.

## Route
`/settings`

## File
`src/pages/SettingsPage.jsx` (191 lines)

## Imports
- `useState` from React
- `useNavigate` from react-router-dom
- `Header` component

## State
| Variable | Type | Initial | Description |
|---|---|---|---|
| `language` | string | `'English'` | Selected language |
| `sound` | boolean | `true` | Sound effects toggle |
| `voice` | boolean | `false` | Voice guidance toggle |

## UI Elements (Top to Bottom)

### 1. Container
- Height `100vh`, bg `#121212`

### 2. Header Component
- Props: `title="Settings"`

### 3. Scroll Area
- Height: `calc(100vh - 56px)`, overflowY auto, padding 0 16px

### 4. Profile Card
- Flex row, gap 14px, bg `#1E1E1E`, border radius 14px, padding 16px
- Border: `1px solid #333`, margin bottom 20px
- **Avatar:** 52x52px circle (border radius 26px)
  - Bg: `#FF6D0022`, border: `2px solid #FF6D00`
  - 👷 emoji (24px)
- **Info:**
  - Name: "Ramesh Kumar" -- 16px, weight 700, white
  - Worker ID: "Worker ID: JH10293" -- 12px, `#FF6D00`, weight 600, margin top 2px
  - Organization: "Jharkhand Steel Works" -- 11px, `#707070`, margin top 2px

### 5. Language Section
- Section title: "LANGUAGE" -- 12px, weight 600, `#707070`, uppercase, letter spacing 0.5px
- **Language toggle row:** flex, gap 8px
- 3 buttons: "English", "Hindi", "Santali"
  - Each: flex 1, height 40px, border radius 8px, font 13px, weight 600
  - **Selected:** bg `#FF6D00`, text white, border `#FF6D00`
  - **Unselected:** bg `#262626`, text `#B0B0B0`, border `1px solid #333`

### 6. Preferences Section
- Section title: "PREFERENCES"

#### Sound Effects Toggle Row
- Flex row, space-between, bg `#1E1E1E`, border radius 10px, padding 14px
- Border: `1px solid #333`, margin bottom 6px
- **Left:** "Sound Effects" (13px, weight 600, white) + "Play sounds for actions and feedback" (11px, `#707070`)
- **Right:** Custom toggle button
  - 40x22px, border radius 11px, no border
  - **On:** bg `#FF6D00`
  - **Off:** bg `#333`
  - **Dot:** 18x18px white circle, position absolute, top 2px
    - On: `translateX(18px)`
    - Off: `translateX(2px)`
    - Transition: `transform 0.2s`

#### Voice Guidance Toggle Row
- Same layout as Sound Effects
- "Voice Guidance" + "Read instructions and questions aloud"
- Toggle follows `voice` state

### 7. Data & Storage Section
- Section title: "DATA & STORAGE"

#### Offline Content Row
- Same row layout (no toggle, just value)
- "Offline Content" + "Training modules stored locally"
- Value: "3 of 5" -- 12px, `#B0B0B0`, weight 600

#### Storage Used Row
- "Storage Used" + "Local data and cached assets"
- Value: "248 MB"

#### Sync Status Row
- "Sync Status" + "Last synced with server"
- Value: "Synced" -- `#4CAF50`, weight 600

### 8. About Section
- Section title: "ABOUT"

#### App Version Row
- "App Version"
- Value: "1.0.0 (Demo)"

#### PS Number Row
- "PS Number"
- Value: "SIH26041"

#### Organization Row
- "Organization"
- Value: "Govt. of Jharkhand"

### 9. LOG OUT Button
- Full width, height 46px
- Background: `#F4433622` (very light red)
- Text: "LOG OUT" -- `#F44336`, 14px, weight 700
- Border: `1px solid #F4433644`, border radius 10px
- Margin bottom 16px

### 10. Footer Text
- "ARC MineSafetyAr - Demo UI Reference"
- "Not for production use"
- Font: 10px, `#444`, centered, line height 1.5

### 11. Bottom Spacer
- Height: 80px

## Event Handlers
| Handler | Action |
|---|---|
| Language buttons | `setLanguage(lang)` |
| Sound toggle | `setSound(!sound)` |
| Voice toggle | `setVoice(!voice)` |
| LOG OUT | `navigate('/login')` |

## Navigation
- LOG OUT -> `/login`

## Visual Layout
```
┌─────────────────────────┐
│       Settings          │  (Header)
│─────────────────────────│
│ ┌─────────────────────┐ │
│ │ 👷 Ramesh Kumar     │ │
│ │    JH10293          │ │  (orange)
│ │    Jharkhand Steel  │ │
│ └─────────────────────┘ │
│                         │
│ LANGUAGE                │
│ [English][Hindi][San.]  │
│                         │
│ PREFERENCES             │
│ ┌─────────────────────┐ │
│ │ Sound Effects  [●]  │ │  (toggle ON)
│ │ Play sounds...      │ │
│ └─────────────────────┘ │
│ ┌─────────────────────┐ │
│ │ Voice Guidance [○]  │ │  (toggle OFF)
│ │ Read instructions.. │ │
│ └─────────────────────┘ │
│                         │
│ DATA & STORAGE          │
│ ┌─────────────────────┐ │
│ │ Offline Content 3/5 │ │
│ │ Storage Used  248MB │ │
│ │ Sync Status  Synced │ │  (green)
│ └─────────────────────┘ │
│                         │
│ ABOUT                   │
│ ┌─────────────────────┐ │
│ │ App Version  1.0.0  │ │
│ │ PS Number  SIH26041 │ │
│ │ Organization Govt.. │ │
│ └─────────────────────┘ │
│                         │
│ ┌───────────────────┐   │
│ │     LOG OUT       │   │  (red)
│ └───────────────────┘   │
│  ARC MineSafetyAr - ..  │
│  Not for production use │
│                         │
│   [Bottom Nav Bar]      │
└─────────────────────────┘
```
