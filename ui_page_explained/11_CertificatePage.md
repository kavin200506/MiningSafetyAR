# CertificatePage (`/certificate/:id`)

## Purpose
Digital safety certificate display with worker name, module, score, QR placeholder, and metadata.

## Route
`/certificate/:id`

## File
`src/pages/CertificatePage.jsx` (147 lines)

## Imports
- `useParams`, `useNavigate` from react-router-dom
- `getCertificateById` from data
- `Header` component

## URL Parameters
| Param | Description |
|---|---|
| `id` | Certificate ID (e.g., `JH-FIRE-001928`) |

## Computed Values
| Variable | Calculation |
|---|---|
| `cert` | `getCertificateById(id)` |

## Not Found State
If `!cert`: Header with back + "Certificate not found" centered text `#707070`

## UI Elements (Top to Bottom)

### 1. Container
- Height `100vh`, bg `#121212`, flex column

### 2. Header Component
- Props: `title="Certificate"`, `showBack`

### 3. Scroll Area
- Flex 1, overflowY auto, padding 0 16px

### 4. Certificate Card
- Border: `2px solid #FF6D0044`, border radius 16px
- Background: `linear-gradient(180deg, #1E1E1E, #181818)`

#### Certificate Header
- Background: `linear-gradient(135deg, #FF6D00, #E65100)`
- Padding: 24px, centered
- Shield emoji 🛡️ (32px)
- "SAFETY CERTIFICATE" -- 18px, weight 800, white, letter spacing 3px, margin top 8px
- "Government of Jharkhand" -- 11px, `#ffffffcc`, margin top 4px

#### Certificate Body (padding: 24px 20px)
- "This certifies that" -- 12px, `#707070`, centered
- Worker name "Ramesh Kumar" -- 22px, weight 800, white, centered
- "has successfully completed" -- 12px, `#707070`, centered
- Module title "Fire & Explosion Response" -- 15px, weight 700, `#FF6D00`, centered

#### Score Row
- Flex, centered, gap 32px, margin bottom 20px
- Two items, each centered:
  - "Score" label (10px, `#707070`) + "85%" value (20px, weight 800, white)
  - "Status" label + "PASSED" value (`#4CAF50`)

#### QR Code Section
- Centered, margin bottom 20px
- **QR box:** 100x100px, white background, border radius 8px, padding 8px
  - **Placeholder:** bg `#f0f0f0`, border radius 4px, full size
    - "QR Code" text (10px, `#555`)
    - Certificate ID text (8px, `#444`)
- "Scan to verify certificate" -- 10px, `#707070`, margin top 6px

#### Certificate Metadata
- Border top: `1px solid #333`, padding top 12px
- Rows: flex, space-between, padding 4px 0

| Label | Value |
|---|---|
| Certificate ID | JH-FIRE-001928 |
| Issued | 2026-08-26 |
| Expires | 2027-08-26 |
| Organization | Jharkhand Steel Works |

- Label: 11px, `#707070`
- Value: 11px, `#B0B0B0`, weight 600

### 5. Action Section
- Flex column, gap 8px, padding bottom 24px

#### Download Certificate Button
- Full width, height 46px
- Background: `#FF6D00`
- Text: "💾 Download Certificate" -- white, 14px, weight 600
- Border: none, border radius 10px
- **No onClick handler (no-op)**

#### Share Certificate Button
- Full width, height 44px
- Background: `#262626`
- Text: "📤 Share Certificate" -- `#B0B0B0`, 13px, weight 600
- Border: `1px solid #333`, border radius 10px
- **No onClick handler (no-op)**

#### Verify Certificate Button
- Full width, height 44px
- Background: transparent
- Text: "🔍 Verify Certificate" -- `#FF6D00`, 13px, weight 600
- Border: `1px solid #FF6D00`, border radius 10px
- onClick: `navigate('/verify')`

## Navigation
| Element | Route |
|---|---|
| Verify Certificate | `/verify` |
| Back button (Header) | previous page |

## Visual Layout
```
┌─────────────────────────┐
│ [←]     Certificate     │
│─────────────────────────│
│ ┌─────────────────────┐ │
│ │ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│ │  (orange gradient header)
│ │ ▓  🛡️              ▓│ │
│ │ ▓ SAFETY CERTIFICATE▓│ │
│ │ ▓ Govt. of Jharkh..▓│ │
│ │ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│ │
│ │                     │ │
│ │  This certifies that│ │
│ │   Ramesh Kumar      │ │  (large, bold)
│ │ has successfully... │ │
│ │ Fire & Explosion... │ │  (orange)
│ │                     │ │
│ │  Score    Status    │ │
│ │   85%     PASSED    │ │
│ │                     │ │
│ │    ┌──────────┐     │ │
│ │    │ QR Code  │     │ │  (white box)
│ │    │ JH-FIRE..│     │ │
│ │    └──────────┘     │ │
│ │  Scan to verify     │ │
│ │─────────────────────│ │
│ │ Cert ID  JH-FIRE..  │ │
│ │ Issued   2026-08-26 │ │
│ │ Expires  2027-08-26 │ │
│ │ Org      Jharkhand. │ │
│ └─────────────────────┘ │
│                         │
│ ┌───────────────────┐   │
│ │ Download Cert.    │   │  (orange)
│ └───────────────────┘   │
│ ┌───────────────────┐   │
│ │ Share Certificate │   │  (dark)
│ └───────────────────┘   │
│ ┌───────────────────┐   │
│ │ Verify Cert.      │   │  (orange outline)
│ └───────────────────┘   │
└─────────────────────────┘
```
