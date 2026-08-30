# QRVerifyPage (`/verify`)

## Purpose
Certificate verification by ID lookup. Shows valid/invalid result with certificate details.

## Route
`/verify`

## File
`src/pages/QRVerifyPage.jsx` (181 lines)

## Imports
- `useState` from React
- `useNavigate` from react-router-dom
- `getCertificateById` from data
- `Header` component

## State
| Variable | Type | Initial | Description |
|---|---|---|---|
| `certId` | string | `''` | Certificate ID input |
| `result` | object/null | `null` | Verification result |
| `searching` | boolean | `false` | Loading state |

## Quick-Fill IDs
```js
['JH-FIRE-001928', 'JH-GAS-002156', 'INVALID-000']
```

## Verification Logic
```js
handleVerify():
  1. If certId empty, return
  2. Set searching = true
  3. After 1000ms timeout:
     - Look up certificate by ID
     - If found: result = {...cert, found: true}
     - If not found: result = {found: false}
     - Set searching = false
```

## UI Elements (Top to Bottom)

### 1. Container
- Height `100vh`, bg `#121212`, flex column

### 2. Header Component
- Props: `title="Verify Certificate"`, `showBack`

### 3. Scroll Area
- Flex 1, overflowY auto, padding 0 16px

### 4. Search Section (centered, padding 20px 0)
- **Search icon:** 🔍 (32px), margin bottom 12px
- **Title:** "Certificate Verification" -- 18px, weight 700, white, margin bottom 6px
- **Description:** "Enter a certificate ID to verify its authenticity" -- 12px, `#707070`, margin bottom 20px

#### Input Group
- Flex row, gap 8px, margin bottom 12px
- **Input:** flex 1, height 46px, bg `#262626`, border `1px solid #333`
  - Border radius 10px, padding 0 14px, text white, 14px
  - Placeholder: "e.g. JH-FIRE-001928"
- **Verify Button:** height 46px, padding 0 20px
  - Bg `#FF6D00`, white text, 13px, weight 700
  - Border: none, border radius 10px, white space nowrap
  - Text: "VERIFY" (or "Verifying..." when searching)
  - Disabled when searching

#### Quick-Fill Buttons
- Flex row, centered, gap 6px, flex wrap
- "Try:" label -- 10px, `#555`
- 3 buttons: each bg `#262626`, border `1px solid #333`, border radius 6px
  - Padding 4px 8px, font 10px, `#B0B0B0`, cursor pointer
  - onClick: sets certId and clears result

### 5. Loading State (when `searching`)
- Card: bg `#1E1E1E`, border radius 14px, border `1px solid #333`
- Padding 32px, centered flex column, gap 12px
- **Spinner:** 28x28px circle
  - Border: 3px solid `#333`, borderTopColor `#FF6D00`
  - Animation: `spin 0.8s linear infinite`
- "Checking certificate..." -- `#B0B0B0`, 13px

### 6. Result Card (when `result && !searching`)
- Background: `#1E1E1E`, border radius 14px
- Border: `1px solid #333`, overflow hidden, margin bottom 24px

#### If VALID (`result.found`):

**Valid Badge:**
- Flex row, centered, gap 8px, padding 16px
- Background: `#4CAF5011`, border bottom `1px solid #4CAF5033`
- ✅ emoji (24px) + "CERTIFICATE VALID" -- 16px, weight 800, `#4CAF50`

**Result Body (padding 16px):**
- 7 detail rows, each: flex, space-between, padding 8px 0, border bottom `1px solid #262626`

| Label | Value |
|---|---|
| Worker | Ramesh Kumar |
| Worker ID | JH10293 |
| Module | Fire & Explosion Response |
| Score | 85% (`#FF6D00`, weight 700) |
| Issued | 2026-08-26 |
| Expires | 2027-08-26 |
| Status | VALID (`#4CAF50`, weight 600) |

- Label: 12px, `#707070`
- Value: 12px, white, weight 600

**View Certificate Button:**
- Full width, height 44px
- Background: `#FF6D00`
- Text: "VIEW CERTIFICATE" -- white, 13px, weight 600
- Border: none, border radius `0 0 14px 14px` (bottom corners only)

#### If INVALID (`!result.found`):

**Invalid Badge:**
- Same layout as valid badge but red
- ❌ emoji (24px) + "INVALID CERTIFICATE" -- 16px, weight 800, `#F44336`
- Background: `#F4433611`, border bottom `1px solid #F4433633`

**Description:**
- "No certificate found with ID: {certId}"
- Padding 20px, centered, 13px, `#707070`

## Event Handlers
| Handler | Action |
|---|---|
| `handleVerify` | Looks up certificate after 1s delay |
| Quick-fill buttons | Sets certId, clears result |
| Input onChange | Sets certId, clears result |
| View Certificate | `navigate(/certificate/${result.id})` |

## Navigation
- View Certificate -> `/certificate/${result.id}`
- Back button (Header) -> previous page

## Visual Layout
```
┌─────────────────────────┐
│ [←] Verify Certificate  │
│─────────────────────────│
│         🔍              │
│ Certificate Verification│
│ Enter a certificate ID..│
│                         │
│ ┌──────────────┬──────┐ │
│ │ JH-FIRE-00192│VERIFY│ │
│ └──────────────┴──────┘ │
│ Try: [JH-FIRE..][JH-GAS..][INVALID..] │
│                         │
│ ┌─────────────────────┐ │
│ │   ⏳ (spinner)      │ │  (loading state)
│ │ Checking cert...    │ │
│ └─────────────────────┘ │
│                         │
│ ┌─────────────────────┐ │
│ │ ✅ CERTIFICATE VALID│ │  (green header)
│ │─────────────────────│ │
│ │ Worker    Ramesh Ku.│ │
│ │ Worker ID JH10293   │ │
│ │ Module    Fire & Ex.│ │
│ │ Score     85%       │ │  (orange)
│ │ Issued    2026-08-26│ │
│ │ Expires   2027-08-26│ │
│ │ Status    VALID     │ │  (green)
│ │                     │ │
│ │ VIEW CERTIFICATE    │ │  (orange, bottom rounded)
│ └─────────────────────┘ │
│                         │
│ OR (if invalid):        │
│ ┌─────────────────────┐ │
│ │ ❌ INVALID CERT..  │ │  (red header)
│ │ No certificate found│ │
│ └─────────────────────┘ │
└─────────────────────────┘
```
