# LearningContentPage (`/training/:id/learn`)

## Purpose
Slide-based learning content shown before AR simulation. Each module has 2-3 slides with bullet points and tips.

## Route
`/training/:id/learn`

## File
`src/pages/LearningContentPage.jsx` (303 lines)

## Imports
- `useState` from React
- `useNavigate`, `useParams` from react-router-dom
- `getModuleById` from data

## Static Data: `learningContent`
All slides stored as a hardcoded object keyed by module ID.

### fire_safety (3 slides)
| Slide | Title | Icon | Points |
|---|---|---|---|
| 1 | Fire Safety Basics | 🔥 | 4 points about fire triangle, industrial hazards, exits |
| 2 | P.A.S.S. Technique | 🛡️ | 4 points: Pull, Aim, Squeeze, Sweep |
| 3 | Evacuation Procedures | 🚨 | 5 points: activate alarm, alert, evacuate, proceed, report |

### gas_safety (3 slides)
| Slide | Title | Icon | Points |
|---|---|---|---|
| 1 | Gas Leak Hazards | ☣️ | 4 points about common gases, odorless gases, accumulation |
| 2 | PPE for Gas Hazards | 🛡️ | 4 points: gas mask, goggles, suit, detector |
| 3 | Confined Space Protocol | ⛏️ | 5 points: buddy system, test atmosphere, communication, rescue, PRCS |

### machinery_safety (3 slides)
| Slide | Title | Icon | Points |
|---|---|---|---|
| 1 | Lockout/Tagout (LOTO) | 🔒 | 5 points: notify, shut down, isolate, lock/tag, verify |
| 2 | Machine Guarding | 🛡️ | 4 points: guards protect, never remove, report, point-of-operation |
| 3 | Safe Operation | ⚙️ | 4 points: startup/shutdown, keep clear, proper tools, wait for stop |

### electrical_safety (2 slides)
| Slide | Title | Icon | Points |
|---|---|---|---|
| 1 | Electrical Hazards | ⚡ | 4 points: 50V fatal, wet conditions, damaged insulation, treat as live |
| 2 | Safe Work Practices | 🛡️ | 4 points: de-energize, test before touch, insulated tools, safe distances |

### heights_safety (2 slides)
| Slide | Title | Icon | Points |
|---|---|---|---|
| 1 | Fall Protection | 🏔️ | 4 points: 4ft/6ft requirement, inspect harness, 5000lbs anchor, no damaged equipment |
| 2 | Ladder & Scaffold Safety | 🪜 | 4 points: 3-point contact, level ground, top rungs, guardrails |

### Tip Boxes (per slide)
Each slide has a tip at the bottom starting with 💡 (lightbulb emoji) highlighted in orange.

## State
| Variable | Type | Initial | Description |
|---|---|---|---|
| `currentSlide` | number | `0` | Current slide index |

## URL Parameters
| Param | Description |
|---|---|
| `id` | Module ID |

## Computed Values
| Variable | Calculation |
|---|---|
| `mod` | `getModuleById(id)` |
| `slides` | `learningContent[id] \|\| []` |
| `slide` | `slides[currentSlide]` |
| `isLast` | `currentSlide === slides.length - 1` |

## UI Elements (Top to Bottom)

### 1. Not Found State (if `!mod || slides.length === 0`)
- Custom header with back button + "Learning Content" title
- Centered: "No content available" -- `#707070`

### 2. Custom Header (not using Header component)
- Sticky header, bg `#121212`, padding 12px 16px
- **Back button:** 36x36px, bg `#262626`, border `1px solid #333`, chevron SVG
- **Title:** "Learn" -- flex 1, centered, 18px, weight 700, white
- **Slide counter:** "1/3" -- 13px, `#FF6D00`, weight 700

### 3. Progress Track
- Padding: 0 16px
- Track: width 100%, height 3px, bg `#333`, border radius 2px
- Fill: bg `#FF6D00`, border radius 2px, `transition: width 0.3s`
- Width: `${((currentSlide + 1) / slides.length) * 100}%`

### 4. Scroll Area
- Flex 1, overflowY auto, padding 20px 16px

### 5. Slide Card
- Background: `#1E1E1E`, border radius 14px, padding 20px
- Border: `1px solid #333`
- **Icon:** Centered, emoji (40px), margin bottom 16px
- **Title:** Centered, 18px, weight 700, white, margin bottom 16px
- **Bullet points:**
  - Each: flex row, gap 12px, margin bottom 12px
  - Bullet: "•" -- 14px, `#FF6D00`, flex shrink 0
  - Text: 14px, `#B0B0B0`, line height 1.6
- **Tip box:**
  - Background: `#FF6D0011` (very light orange)
  - Border: `1px solid #FF6D0033`
  - Border radius: 10px, padding 12px
  - Flex row, gap 8px, margin top 16px
  - 💡 emoji (14px) + tip text (13px, `#FF9E40`, line height 1.5)

### 6. Navigation Bar
- Background: `#1E1E1E`, border top `1px solid #333`
- Padding: 12px 16px, padding bottom 24px
- Flex row, gap 10px

#### Previous Button (if not first slide)
- Full width, height 44px
- Background: `#262626`
- Text: "Previous" -- `#B0B0B0`, 13px, weight 600
- Border: `1px solid #333`, border radius 10px
- Disabled style: opacity 0.5, color `#555`

#### Next Button (if not last slide)
- Full width, height 44px
- Background: `#FF6D00`
- Text: "Next" -- white, 13px, weight 600
- Border: none, border radius 10px

#### I'm Ready Button (if last slide)
- Full width, height 44px
- Background: `linear-gradient(135deg, #4CAF50, #388E3C)` (green gradient)
- Text: "I'm Ready" -- white, 13px, weight 700
- Border: none, border radius 10px

## Event Handlers
| Handler | Action |
|---|---|
| Previous | `setCurrentSlide(s => s - 1)` |
| Next | `setCurrentSlide(s => s + 1)` |
| I'm Ready | `navigate(/training/${id}/ar)` |
| Back button | `navigate(-1)` |

## Navigation
- "I'm Ready" (last slide) -> `/training/${id}/ar`
- Back button -> previous page

## Visual Layout
```
┌─────────────────────────┐
│ [←]     Learn      1/3  │  (header + counter)
│ ━━━━━━━━━━━━━━━━━━━━━━ │  (progress track)
│─────────────────────────│
│ ┌─────────────────────┐ │
│ │         🔥          │ │  (slide icon)
│ │                     │ │
│ │  Fire Safety Basics │ │  (title)
│ │                     │ │
│ │ • Fire requires...  │ │  (bullet points)
│ │ • Remove any one... │ │
│ │ • Common industri.. │ │
│ │ • Always identify.. │ │
│ │                     │ │
│ │ 💡 Remember: The..  │ │  (tip box, orange)
│ └─────────────────────┘ │
│                         │
│ ┌──────┐ ┌────────────┐ │
│ │Prev. │ │    Next    │ │  (or "I'm Ready" on last)
│ └──────┘ └────────────┘ │
└─────────────────────────┘
```
