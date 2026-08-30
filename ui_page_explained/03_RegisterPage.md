# RegisterPage (`/register`)

## Purpose
New worker registration form with 7 fields and language selection.

## Route
`/register`

## File
`src/pages/RegisterPage.jsx` (216 lines)

## Imports
- `useState` from React
- `useNavigate` from react-router-dom

## Constants
```js
sectors = ['Mining', 'Steel Manufacturing', 'Mica Processing', 'Construction', 'Other']
```

## State
| Variable | Type | Initial | Description |
|---|---|---|---|
| `form` | object | `{name:'', workerId:'', organization:'', sector:'', phone:'', pin:'', confirmPin:''}` | All form fields |

## UI Elements (Top to Bottom)

### 1. Container
- Height: `100vh`, background: `#121212`

### 2. Scroll Area
- Full height, `overflowY: auto`

### 3. Sticky Header
- Position: sticky, top: 0, z-index: 10
- Background: `#121212`
- Padding: 12px 16px
- **Back Button:** 36x36px, bg `#262626`, border `1px solid #333`, border radius 10px
  - Contains: chevron SVG (left arrow, white, 20x20px)
- **Title:** "Register" -- flex 1, centered, 18px, weight 700, white
- **Spacer:** 36px width (balances layout)

### 4. Form Section (padding: 8px 24px 40px)

#### Full Name Input
- Label: "Full Name" -- 12px, weight 600, `#B0B0B0`
- Input: text, placeholder "e.g. Ramesh Kumar"
- Height: 46px, bg `#262626`, border `1px solid #333`, border radius 10px
- Text: 14px, white

#### Worker ID Input
- Label: "Worker ID"
- Input: text, placeholder "e.g. JH10293"
- Same style as Full Name

#### Organization / Mine Name Input
- Label: "Organization / Mine Name"
- Input: text, placeholder "e.g. Jharkhand Steel Works"
- Same style

#### Sector Dropdown
- Label: "Sector"
- Select element with 6 options:
  - "Select sector" (empty value)
  - "Mining"
  - "Steel Manufacturing"
  - "Mica Processing"
  - "Construction"
  - "Other"
- Same input style

#### Phone Number Input
- Label: "Phone Number"
- Input: tel type, placeholder "10-digit mobile number"
- maxLength: 10
- Same style

#### PIN Row (Side-by-Side)
- Flex row with gap 12px
- **Create PIN:** password type, placeholder "4-digit PIN", maxLength 4
- **Confirm PIN:** password type, placeholder "Re-enter PIN", maxLength 4
- Both: flex 1, same input style

#### Preferred Language Toggle
- Label: "Preferred Language"
- Flex row with gap 8px
- 3 buttons: "English", "Hindi", "Santali"
  - Each: flex 1, height 38px, border radius 8px
  - Selected: bg `#FF6D00`, text white, border `#FF6D00`
  - Unselected: bg `#262626`, text `#B0B0B0`, border `#333`

#### REGISTER Button
- Full width, height 48px
- Background: `linear-gradient(135deg, #FF6D00, #E65100)`
- Text: "REGISTER" -- white, 15px, weight 700, letter spacing 1px
- Border: none, border radius 10px
- Margin top: 8px

#### Disclaimer Note
- Text: "Your data is stored locally on this device. No server required for demo."
- Font: 11px, `#555`, centered
- Margin top: 16px

## Event Handlers
| Handler | Action |
|---|---|
| `update(key, val)` | Generic form field updater: `setForm({...form, [key]: val})` |
| `handleRegister` | `navigate('/dashboard')` -- no validation |
| Back button | `navigate(-1)` |
| Language buttons | `update('language', lang)` |

## Navigation
- Back button: previous page (`navigate(-1)`)
- REGISTER: `/dashboard`

## Visual Layout
```
┌─────────────────────────┐
│ [←]      Register       │  (sticky header)
│─────────────────────────│
│  Full Name              │
│  ┌───────────────────┐  │
│  │ e.g. Ramesh Kumar │  │
│  └───────────────────┘  │
│  Worker ID              │
│  ┌───────────────────┐  │
│  │ e.g. JH10293      │  │
│  └───────────────────┘  │
│  Organization / Mine    │
│  ┌───────────────────┐  │
│  │ Jharkhand Steel.. │  │
│  └───────────────────┘  │
│  Sector                 │
│  ┌───────────────────┐  │
│  │ Select sector  ▼  │  │
│  └───────────────────┘  │
│  Phone Number           │
│  ┌───────────────────┐  │
│  │ 10-digit mobile   │  │
│  └───────────────────┘  │
│  Create PIN  Confirm PIN│
│  ┌──────────┬────────┐  │
│  │ 4-digit  │ Re-ent │  │
│  └──────────┴────────┘  │
│  Preferred Language     │
│  [English][Hindi][San.] │
│  ┌───────────────────┐  │
│  │     REGISTER      │  │  (orange gradient)
│  └───────────────────┘  │
│  Your data is stored... │
└─────────────────────────┘
```
